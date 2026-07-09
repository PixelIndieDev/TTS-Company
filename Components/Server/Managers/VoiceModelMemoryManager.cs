using BepInEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TTSCompany.Components.Constants;

namespace TTSCompany.Components.Server.Components
{
    internal sealed class VoiceModelMemoryManager
    {
        private readonly long _maxMemoryPoolBytes;
        private readonly long _fallbackModelSizeBytes = ConvertMBToLong(65); // 65Mb

        private readonly PiperTTSServer _piperServer;

        private readonly ConcurrentDictionary<string, string> _modelLocations = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _modelSizes = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _modelLastAccess = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HashSet<ulong>> _modelAssemblies = new ConcurrentDictionary<string, HashSet<ulong>>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, bool> _evictedModels = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private long _currentLoadedBytes;

        private readonly LinkedList<string> _LRU_list = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> _LRU_elements = new Dictionary<string, LinkedListNode<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly object _LRU_lock = new object();

        internal VoiceModelMemoryManager(PiperTTSServer piperServer)
        {
            _piperServer = piperServer ?? throw new ArgumentNullException(nameof(piperServer));
            _maxMemoryPoolBytes = ConvertMBToLong(DetermineOptimalPoolSizeMegabytes());
        }

        internal void InitializeModelRegistry()
        {
            string pluginPath = Paths.PluginPath;
            foreach (string voiceModelFolder in FindVoiceModelFolders())
            {
                string folderPath = Path.Combine(pluginPath, voiceModelFolder);
                foreach (FileInfo file in new DirectoryInfo(folderPath).EnumerateFiles("*.onnx", SearchOption.TopDirectoryOnly))
                {
                    string modelName = Path.GetFileNameWithoutExtension(file.Name);
                    _modelSizes[modelName] = file.Length;
                    _modelLocations.TryAdd(modelName, Path.Combine(folderPath, modelName + ".onnx"));
                    LogConstants.VOICE_MODEL_MEM_MANAGER_FOUND_VOICE_MODEL_WITH_SIZE.Log(nameof(VoiceModelMemoryManager), modelName, file.Length);
                }
            }
        }

        internal int GetAssemblyCountForModel(string modelName)
        {
            if (_modelAssemblies.TryGetValue(modelName, out HashSet<ulong> assemblies))
            {
                lock (assemblies)
                {
                    return assemblies.Count;
                }
            }
            return 0;
        }

        // a loaded voice model means that at least 1 assembly wants the voice model to be loaded
        // this does not mean that the voice model is actually loaded currently
        internal bool HasVoiceModelBeenLoaded(string modelName)
        {
            return GetAssemblyCountForModel(modelName) > 0;
        }
        // the voice model was forcefully unloaded
        internal bool WasVoiceModelEvicted(string modelName)
        {
            return _evictedModels.ContainsKey(modelName);
        }

        internal bool IsVoiceModelValid(string modelName)
        {
            return _modelSizes.ContainsKey(modelName);
        }

        internal void UpdateLastUse(string modelName)
        {
            _modelLastAccess[modelName] = DateTime.UtcNow;

            lock (_LRU_lock)
            {
                if (_LRU_elements.TryGetValue(modelName, out LinkedListNode<string> node))
                {
                    _LRU_list.Remove(node);
                }
                else
                {
                    node = new LinkedListNode<string>(modelName);
                }
                _LRU_list.AddLast(node);
                _LRU_elements[modelName] = node;
            }
        }

        private string FindOldestEvictableModel(string excludeModelName)
        {
            lock (_LRU_lock)
            {
                for (LinkedListNode<string> current = _LRU_list.First; current != null; current = current.Next)
                {
                    string candidate = current.Value;
                    if (_modelAssemblies.ContainsKey(candidate)
                        && !_evictedModels.ContainsKey(candidate)
                        && !candidate.Equals(excludeModelName, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
                return null;
            }
        }

        internal string GetRandomFoundTTSVoiceName()
        {
            int totalCount = _modelLocations.Count;
            if (totalCount == 0)
            {
                return null;
            }

            // Pick a target index
            int targetIndex = UnityEngine.Random.Range(0, totalCount);
            int currentIndex = 0;

            foreach (KeyValuePair<string, string> kvp in _modelLocations)
            {
                if (currentIndex == targetIndex)
                {
                    return kvp.Key;
                }
                currentIndex++;
            }
            return string.Empty;
        }

        internal string GetRandomLoadedTTSVoiceName()
        {
            int totalCount = _modelAssemblies.Count;
            if (totalCount == 0)
            {
                return null;
            }

            // Pick a target index
            int targetIndex = UnityEngine.Random.Range(0, totalCount);
            int currentIndex = 0;

            // Iterate through the dictionary without calling '.Keys'
            foreach (KeyValuePair<string, HashSet<ulong>> kvp in _modelAssemblies)
            {
                if (currentIndex == targetIndex)
                {
                    return kvp.Key;
                }
                currentIndex++;
            }
            return string.Empty;
        }

        private string GetLoadModelString(string modelName, string voiceModelLocation)
        {
            return "{\"command\":\"load_model\",\"model\":\"" + JSONHelper.Escape(modelName) + "\",\"model_path\":\"" + JSONHelper.Escape(voiceModelLocation.TrimEnd('\\', '/')).Replace("\\", "\\\\") + "\",\"use_cuda\":false}\n";
        }

        internal async Task<(bool Success, string Error)> ReloadModelAsync(string modelName, CancellationToken cancellationToken)
        {
            if (!_modelLocations.TryGetValue(modelName, out string voiceModelLocation))
            {
                return (false, TTSConstants.TTS_MEM_MANAGER_UNKNOWN_MODEL_LOCATION);
            }

            UpdateLastUse(modelName);

            await EnforceDynamicMemoryLimitsAsync(modelName, cancellationToken).ConfigureAwait(false);

            Dictionary<string, object> response = await _piperServer.SendSimpleCommandAsync(GetLoadModelString(modelName, voiceModelLocation), cancellationToken).ConfigureAwait(false);
            (bool Success, string Error) result = _piperServer.ToResult(response);
            if (result.Success)
            {
                _evictedModels.TryRemove(modelName, out _);
                long modelSize = _modelSizes.TryGetValue(modelName, out long s) ? s : _fallbackModelSizeBytes;
                Interlocked.Add(ref _currentLoadedBytes, modelSize);
                LogConstants.PIPER_TTS_RELOADED_VOICE_MODEL.Log(nameof(VoiceModelMemoryManager), modelName);
            }

            return result;
        }

        internal async Task<(bool Success, string Error)> LoadModelAsync(string modelName, ulong callingAssemblyHash, CancellationToken cancellationToken)
        {
            if (!_modelLocations.TryGetValue(modelName, out string voiceModelLocation))
            {
                return (false, TTSConstants.TTS_MEM_MANAGER_UNKNOWN_MODEL_LOCATION);
            }

            UpdateLastUse(modelName);
            HashSet<ulong> assemblies = _modelAssemblies.GetOrAdd(modelName, _ => new HashSet<ulong>());

            bool needsServerLoad;
            lock (assemblies)
            {
                if (_evictedModels.ContainsKey(modelName))
                {
                    needsServerLoad = true;
                }
                else
                {
                    if (assemblies.Contains(callingAssemblyHash))
                    {
                        return (true, string.Empty);
                    }
                    needsServerLoad = (assemblies.Count == 0);
                }
            }

            if (!needsServerLoad)
            {
                lock (assemblies)
                {
                    assemblies.Add(callingAssemblyHash);
                }
                return (true, string.Empty);
            }

            await EnforceDynamicMemoryLimitsAsync(modelName, cancellationToken);

            Dictionary<string, object> response = await _piperServer.SendSimpleCommandAsync(GetLoadModelString(modelName, voiceModelLocation), cancellationToken).ConfigureAwait(false);
            (bool Success, string Error) result = _piperServer.ToResult(response);

            if (result.Success)
            {
                _evictedModels.TryRemove(modelName, out _);
                long modelSize = _modelSizes.TryGetValue(modelName, out long s) ? s : _fallbackModelSizeBytes;
                Interlocked.Add(ref _currentLoadedBytes, modelSize);

                lock (assemblies)
                {
                    assemblies.Add(callingAssemblyHash);
                }

                LogConstants.PIPER_TTS_LOADED_VOICE_MODEL.Log(nameof(VoiceModelMemoryManager), modelName);
            }
            else
            {
                lock (assemblies)
                {
                    if (assemblies.Count == 0)
                    {
                        _modelAssemblies.TryRemove(modelName, out _);
                    }
                }

                LogConstants.PIPER_TTS_FAILED_LOADING_VOICE_MODEL.Log(nameof(VoiceModelMemoryManager), modelName);
            }

            return result;
        }

        internal async Task<(bool Success, string Error)> UnloadModelAsync(string modelName, ulong callingAssemblyHash, CancellationToken cancellationToken)
        {
            if (!_modelAssemblies.TryGetValue(modelName, out HashSet<ulong> assemblies))
            {
                return (true, string.Empty);
            }

            lock (assemblies)
            {
                if (!assemblies.Contains(callingAssemblyHash))
                {
                    return (true, string.Empty);
                }

                if (assemblies.Count != 1)
                {
                    assemblies.Remove(callingAssemblyHash);
                    return (true, string.Empty);
                }
            }

            if (_evictedModels.TryRemove(modelName, out _))
            {
                lock (assemblies)
                {
                    assemblies.Remove(callingAssemblyHash);
                    if (assemblies.Count == 0)
                    {
                        _modelAssemblies.TryRemove(modelName, out _);
                        _modelLastAccess.TryRemove(modelName, out _);
                    }
                }
                return (true, string.Empty);
            }

            string json = "{\"command\":\"unload_model\",\"model\":\"" + JSONHelper.Escape(modelName) + "\"}\n";
            Dictionary<string, object> response = await _piperServer.SendSimpleCommandAsync(json, cancellationToken).ConfigureAwait(false);
            (bool Success, string Error) result = _piperServer.ToResult(response);

            if (result.Success)
            {
                long modelSize = _modelSizes.TryGetValue(modelName, out long s) ? s : _fallbackModelSizeBytes;
                Interlocked.Add(ref _currentLoadedBytes, -modelSize);

                lock (assemblies)
                {
                    assemblies.Remove(callingAssemblyHash);
                    if (assemblies.Count == 0)
                    {
                        _modelAssemblies.TryRemove(modelName, out _);
                        _modelLastAccess.TryRemove(modelName, out _);
                    }
                }
                LogConstants.PIPER_TTS_UNLOADED_VOICE_MODEL.Log(nameof(VoiceModelMemoryManager), modelName);
            } else
            {
                LogConstants.PIPER_TTS_FAILED_UNLOADING_VOICE_MODEL.Log(nameof(VoiceModelMemoryManager), modelName);
            }

            return result;
        }

        private async Task EnforceDynamicMemoryLimitsAsync(string targetModelName, CancellationToken cancellationToken)
        {
            long targetModelSize = _modelSizes.TryGetValue(targetModelName, out long size) ? size : _fallbackModelSizeBytes;

            while (true)
            {
                long currentLoadedBytes = Interlocked.Read(ref _currentLoadedBytes);

                if (currentLoadedBytes + targetModelSize <= _maxMemoryPoolBytes)
                {
                    break;
                }

                string oldestModel = FindOldestEvictableModel(targetModelName);

                if (string.IsNullOrEmpty(oldestModel))
                {
                    LogConstants.VOICE_MODEL_MEM_MANAGER_NO_MODEL_TO_EVICT.Log(nameof(VoiceModelMemoryManager), targetModelName);
                    break;
                }

                LogConstants.VOICE_MODEL_MEM_MANAGER_POOL_LIMIT_REACHED.Log(nameof(VoiceModelMemoryManager), oldestModel);
                await ForceUnloadModelAsync(oldestModel, cancellationToken);
            }
        }

        private async Task ForceUnloadModelAsync(string modelName, CancellationToken cancellationToken)
        {
            string json = "{\"command\":\"unload_model\",\"model\":\"" + JSONHelper.Escape(modelName) + "\"}\n";

            try
            {
                Dictionary<string, object> response = await _piperServer.SendSimpleCommandAsync(json, cancellationToken).ConfigureAwait(false);
                (bool Success, string Error) result = _piperServer.ToResult(response);

                if (result.Success)
                {
                    _evictedModels.TryAdd(modelName, true);
                    long modelSize = _modelSizes.TryGetValue(modelName, out long s) ? s : _fallbackModelSizeBytes;
                    Interlocked.Add(ref _currentLoadedBytes, -modelSize);
                }
                else
                {
                    LogConstants.VOICE_MODEL_MEM_MANAGER_NO_MODEL_TO_EVICT.Log(nameof(VoiceModelMemoryManager), modelName);
                }
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(VoiceModelMemoryManager), nameof(ForceUnloadModelAsync), ex.Message);
            }
        }

        private static long ConvertMBToLong(int valueInMb)
        {
            return valueInMb * 1024L * 1024L;
        }

        private static List<string> FindVoiceModelFolders()
        {
            string pluginPath = Paths.PluginPath;
            List<string> results = new List<string>();

            foreach (string subDir in Directory.EnumerateDirectories(pluginPath))
            {
                string candidate = Path.Combine(subDir, TTSConstants.TTS_VOICE_MODELS_FOLDER);
                if (Directory.Exists(candidate))
                {
                    results.Add(candidate.Substring(pluginPath.Length).TrimStart(Path.DirectorySeparatorChar));
                }
            }
            return results;
        }

        #region Windows Native Memory Detection
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private static int DetermineOptimalPoolSizeMegabytes()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

            if (GlobalMemoryStatusEx(ref memStatus))
            {
                double totalGigabytes = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);

                if (totalGigabytes <= 2.5)
                {
                    return 512;
                }
                if (totalGigabytes <= 4.5)
                {
                    return 1536;
                }
                if (totalGigabytes <= 8.5)
                {
                    return 3072;
                }
            }
            return 4096;
        }
        #endregion
    }
}
