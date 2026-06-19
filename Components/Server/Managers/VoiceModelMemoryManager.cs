using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TTS_Company.Components.Constants;

namespace TTS_Company.Components.Server.Components
{
    internal sealed class VoiceModelMemoryManager
    {
        private readonly long _maxMemoryPoolBytes;
        private readonly long _fallbackModelSizeBytes = ConvertMBToLong(65); // 65Mb

        private readonly PiperTTSServer _piperServer;

        private readonly ConcurrentDictionary<string, long> _modelSizes = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _modelLastAccess = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HashSet<Assembly>> _modelAssemblies = new ConcurrentDictionary<string, HashSet<Assembly>>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, bool> _evictedModels = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public VoiceModelMemoryManager(PiperTTSServer piperServer)
        {
            _piperServer = piperServer ?? throw new ArgumentNullException(nameof(piperServer));
            _maxMemoryPoolBytes = ConvertMBToLong(DetermineOptimalPoolSizeMegabytes());
        }

        public void InitializeModelRegistry()
        {
            var dirInfo = new DirectoryInfo(TTSConstants.TTS_VOICE_MODELS_FOLDER_LOCATION);
            foreach (var file in dirInfo.EnumerateFiles("*.onnx", SearchOption.TopDirectoryOnly))
            {
                string modelName = Path.GetFileNameWithoutExtension(file.Name);
                _modelSizes[modelName] = file.Length;

                LogConstants.VOICE_MODEL_MEM_MANAGER_FOUND_VOICE_MODEL_WITH_SIZE.Log(nameof(VoiceModelMemoryManager), modelName, file.Length);
            }
        }

        internal int GetAssemblyCountForModel(string modelName)
        {
            if (_modelAssemblies.TryGetValue(modelName, out var assemblies))
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
        }

        internal async Task<(bool Success, string Error)> ReloadModelAsync(string modelName, CancellationToken cancellationToken)
        {
            UpdateLastUse(modelName);

            await EnforceDynamicMemoryLimitsAsync(modelName, cancellationToken).ConfigureAwait(false);

            string json = "{\"command\":\"load_model\",\"model\":\"" + JSONHelper.Escape(modelName) + "\",\"use_cuda\":" + "false" + "}\n";
            var response = await _piperServer.SendSimpleCommandAsync(json, cancellationToken).ConfigureAwait(false);
            var result = _piperServer.ToResult(response);
            if (result.Success)
            {
                _evictedModels.TryRemove(modelName, out _);
                LogConstants.PIPER_TTS_LOADED_VOICE_MODEL.Log(nameof(PiperTTSServer), modelName, "CPU2");
            }

            return result;
        }

        internal async Task<(bool Success, string Error)> LoadModelAsync(string modelName, CancellationToken cancellationToken)
        {
            Assembly callingAssembly = new StackFrame(1, false).GetMethod()?.DeclaringType?.Assembly;
            if (callingAssembly == null)
            {
                return (false, TTSConstants.TTS_MEM_MANAGER_UNKNOWN_ASSEMBLY);
            }

            UpdateLastUse(modelName);
            var assemblies = _modelAssemblies.GetOrAdd(modelName, _ => new HashSet<Assembly>());

            bool needsServerLoad;
            lock (assemblies)
            {
                if (_evictedModels.ContainsKey(modelName))
                {
                    needsServerLoad = true;
                }
                else
                {
                    if (assemblies.Contains(callingAssembly))
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
                    assemblies.Add(callingAssembly);
                }
                return (true, string.Empty);
            }

            await EnforceDynamicMemoryLimitsAsync(modelName, cancellationToken);

            string json = "{\"command\":\"load_model\",\"model\":\"" + JSONHelper.Escape(modelName) + "\",\"use_cuda\":" + "false" + "}\n"; // no CUDA support in the server exe
            var response = await _piperServer.SendSimpleCommandAsync(json, cancellationToken).ConfigureAwait(false);
            var result = _piperServer.ToResult(response);

            if (result.Success)
            {
                _evictedModels.TryRemove(modelName, out _);
                lock (assemblies)
                {
                    assemblies.Add(callingAssembly);
                }

                LogConstants.PIPER_TTS_LOADED_VOICE_MODEL.Log(nameof(PiperTTSServer), modelName, "CPU");
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
            }

            return result;
        }

        internal async Task<(bool Success, string Error)> UnloadModelAsync(string modelName, CancellationToken cancellationToken)
        {
            Assembly callingAssembly = new StackFrame(1, false).GetMethod()?.DeclaringType?.Assembly;
            if (callingAssembly == null)
            {
                return (false, TTSConstants.TTS_MEM_MANAGER_UNKNOWN_ASSEMBLY);
            }

            if (!_modelAssemblies.TryGetValue(modelName, out var assemblies))
            {
                return (true, string.Empty);
            }

            lock (assemblies)
            {
                if (!assemblies.Contains(callingAssembly))
                {
                    return (true, string.Empty);
                }

                if (assemblies.Count != 1)
                {
                    assemblies.Remove(callingAssembly);
                    return (true, string.Empty);
                }
            }

            if (_evictedModels.TryRemove(modelName, out _))
            {
                lock (assemblies)
                {
                    assemblies.Remove(callingAssembly);
                    if (assemblies.Count == 0)
                    {
                        _modelAssemblies.TryRemove(modelName, out _);
                        _modelLastAccess.TryRemove(modelName, out _);
                    }
                }
                return (true, string.Empty);
            }

            string json = "{\"command\":\"unload_model\",\"model\":\"" + JSONHelper.Escape(modelName) + "\"}\n";
            var response = await _piperServer.SendSimpleCommandAsync(json, cancellationToken).ConfigureAwait(false);
            var result = _piperServer.ToResult(response);

            if (result.Success)
            {
                lock (assemblies)
                {
                    assemblies.Remove(callingAssembly);
                    if (assemblies.Count == 0)
                    {
                        _modelAssemblies.TryRemove(modelName, out _);
                        _modelLastAccess.TryRemove(modelName, out _);
                    }
                }
            }

            return result;
        }

        private async Task EnforceDynamicMemoryLimitsAsync(string targetModelName, CancellationToken cancellationToken)
        {
            long targetModelSize = _modelSizes.TryGetValue(targetModelName, out long size) ? size : _fallbackModelSizeBytes;

            while (true)
            {
                long currentLoadedBytes = _modelAssemblies.Keys.Where(model => !_evictedModels.ContainsKey(model)).Sum(model => _modelSizes.TryGetValue(model, out long s) ? s : _fallbackModelSizeBytes);

                if (currentLoadedBytes + targetModelSize <= _maxMemoryPoolBytes)
                {
                    break;
                }

                var oldestModel = _modelLastAccess.Where(kvp => _modelAssemblies.ContainsKey(kvp.Key) && !_evictedModels.ContainsKey(kvp.Key) && !kvp.Key.Equals(targetModelName, StringComparison.OrdinalIgnoreCase)).OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key).FirstOrDefault();

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
            _evictedModels.TryAdd(modelName, true);

            string json = "{\"command\":\"unload_model\",\"model\":\"" + JSONHelper.Escape(modelName) + "\"}\n";
            await _piperServer.SendSimpleCommandAsync(json, cancellationToken).ConfigureAwait(false);
        }

        private static long ConvertMBToLong(int valueInMb)
        {
            return valueInMb * 1024L * 1024L;
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
            var memStatus = new MEMORYSTATUSEX();
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
