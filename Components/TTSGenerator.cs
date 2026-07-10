using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TTSCompany.Components.Constants;
using TTSCompany.Components.Enums;
using TTSCompany.Components.Helpers;
using TTSCompany.Components.Server.Components;
using UnityEngine;

namespace TTSCompany.Components
{
    internal sealed class TTSGenerator
    {
        const float DecodeOggOffThreadMultiplier = 1f / 32768f;

        internal readonly PiperTTSServer _server = new PiperTTSServer();

        private readonly ConcurrentDictionary<string, BusyGeneration> _inFlightRequests = new ConcurrentDictionary<string, BusyGeneration>();
        internal int MaxConcurrentRequests
        {
            get => _maxConcurrent;
            set
            {
                if (value < 1)
                {
                    LogConstants.TTS_GENERATOR_ARGUMENT_OUT_OF_RANGE_EX.Log(nameof(TTSGenerator));
                }

                int clamped = Math.Max(1, value);
                _maxConcurrent = clamped;

                SemaphoreSlim oldSemaphore = _semaphore;
                _semaphore = new SemaphoreSlim(clamped, clamped);

                // Don't dispose, let the garbage collector clear it after its tasks are done
                //oldSemaphore?.Dispose();
            }
        }

        private int _maxConcurrent;
        private SemaphoreSlim _semaphore;
        private bool _disposed;
        private bool _isAvailable;

        private volatile bool _cacheDirectoryEnsured;

        // cpu values
        private static readonly int CPU_totalCores = Environment.ProcessorCount;
        private static readonly int CPU_coresReservedForGame = Mathf.Max(1, Mathf.CeilToInt(CPU_totalCores * 0.1f));
        private const int CPU_minimumMaxConcurrentRequests = 1;
        private static readonly int CPU_availableCoresForTTS = Math.Max(CPU_minimumMaxConcurrentRequests, CPU_totalCores - CPU_coresReservedForGame);

        internal TTSGenerator()
        {
            SetMaxConcurrentRequests(TTSGenPriority.Normal);
        }

        private void SetMaxConcurrentRequests(int maxConcurrentRequests)
        {
            MaxConcurrentRequests = maxConcurrentRequests;
            LogConstants.CODE_NEW_VALUE_SET.Log(nameof(TTSGenerator), "maxConcurrentRequests", MaxConcurrentRequests);
        }

        internal void SetMaxConcurrentRequests(TTSGenPriority priority)
        {
            switch (priority)
            {
                case TTSGenPriority.VeryLow:
                    SetMaxConcurrentRequests(Math.Max(CPU_minimumMaxConcurrentRequests, CPU_availableCoresForTTS / 6));
                    break;
                case TTSGenPriority.Low:
                    SetMaxConcurrentRequests(Math.Max(CPU_minimumMaxConcurrentRequests, CPU_availableCoresForTTS / 4));
                    break;
                default: // normal priority
                    SetMaxConcurrentRequests(Math.Max(CPU_minimumMaxConcurrentRequests, CPU_availableCoresForTTS / 2));
                    break;
                case TTSGenPriority.High:
                    SetMaxConcurrentRequests(Math.Max(CPU_minimumMaxConcurrentRequests, (int)(CPU_availableCoresForTTS * 0.75f)));
                    break;
                case TTSGenPriority.Max:
                    SetMaxConcurrentRequests(CPU_availableCoresForTTS);
                    break;
            }
        }

        internal async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isAvailable)
            {
                return true;
            }

            if (!FolderHelper.CheckForPiperTTS())
            {
                LogConstants.TTS_GENERATOR_UNZIP_FAILED.Log(nameof(TTSGenerator), TTSConstants.PIPER_EXE_NAME);
                return false;
            }

            if (!FolderHelper.CheckForDefaultVoiceModels())
            {
                LogConstants.TTS_GENERATOR_UNZIP_FAILED.Log(nameof(TTSGenerator), TTSConstants.TTS_VOICE_MODELS_FOLDER);
                return false;
            }

            bool started = await _server.StartAsync(TTSConstants.PIPER_SERVER_STARTUP_TIMEOUT_MS, cancellationToken).ConfigureAwait(false);

            if (started)
            {
                EnsureCacheDirectoryExists();
            }

            _isAvailable = started;
            return started;
        }

        private void EnsureCacheDirectoryExists()
        {
            if (_cacheDirectoryEnsured)
            {
                return;
            }

            Directory.CreateDirectory(TTSConstants.TTS_VOICE_CACHE_SOUNDCLIPS_PATH);
            _cacheDirectoryEnsured = true;
        }

        internal async Task ShutdownAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isAvailable = false;

            await _server.ShutdownAsync().ConfigureAwait(false);
            _semaphore?.Dispose();
        }

        internal void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                ShutdownAsync().GetAwaiter().GetResult();
            }
            catch
            {
                LogConstants.CODE_GENERIC_CATCH.Log(nameof(TTSGenerator), nameof(Dispose));
            }
            _semaphore?.Dispose();
        }

        // voice loading must be called before calling GenerateTTSAsync()
        internal Task<(bool Success, string Error)> PreloadVoiceAsync(string voiceName, ulong callingAssemblyHash, CancellationToken cancellationToken = default)
        {
            if (!_isAvailable || _disposed)
            {
                return Task.FromResult((false, TTSConstants.TTS_SERVER_UNAVAILABLE));
            }

            if (string.IsNullOrWhiteSpace(voiceName))
            {
                return Task.FromResult((false, TTSConstants.TTS_VOICE_MODEL_NAME_EMPTY));
            }

            return _server.LoadModelAsync(voiceName, callingAssemblyHash, cancellationToken);
        }

        internal Task<(bool Success, string Error)> UnloadVoiceAsync(string voiceName, ulong callingAssemblyHash, CancellationToken cancellationToken = default)
        {
            if (!_isAvailable || _disposed)
            {
                return Task.FromResult((false, TTSConstants.TTS_SERVER_UNAVAILABLE));
            }

            if (string.IsNullOrWhiteSpace(voiceName))
            {
                return Task.FromResult((false, TTSConstants.TTS_VOICE_MODEL_NAME_EMPTY));
            }
            return _server.UnloadModelAsync(voiceName, callingAssemblyHash, cancellationToken);
        }

        internal bool isVoiceModelLoaded(string voiceModelName)
        {
            return _server.HasVoiceModelBeenLoaded(voiceModelName);
        }

        internal async Task<TTSResult> GenerateTTSAsync(string textToSpeak, PiperVoiceSettings settings, CancellationToken cancellationToken)
        {
            if (!_isAvailable || _disposed)
            {
                return new TTSResult { AudioClip = null, Success = false };
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!ValidateInputs(textToSpeak, settings))
            {
                return new TTSResult { AudioClip = null, Success = false };
            }

            EnsureCacheDirectoryExists();
            string hashCacheFileName = HashHelper.GetHashTTSFileNameWithFileType(textToSpeak, settings);

            if (string.IsNullOrWhiteSpace(hashCacheFileName))
            {
                return new TTSResult { AudioClip = null, Success = false };
            }

            string fullCachePath = Path.Combine(TTSConstants.TTS_VOICE_CACHE_SOUNDCLIPS_PATH, hashCacheFileName);
            if (File.Exists(fullCachePath) && new FileInfo(fullCachePath).Length > 0)
            {
                LogConstants.TTS_GENERATOR_FOUND_CACHED_TTS.Log(nameof(TTSGenerator), hashCacheFileName);

                AudioClip clip = await LoadAudioClipFromDiskAsync(fullCachePath, hashCacheFileName).ConfigureAwait(false);
                if (clip != null)
                {
                    return new TTSResult { AudioClip = clip, Success = true };
                }
            }

            BusyGeneration inFlight;
            while (true)
            {
                inFlight = _inFlightRequests.GetOrAdd(hashCacheFileName, _ => new BusyGeneration(ct => RunGenerationAsync(hashCacheFileName, fullCachePath, textToSpeak, settings, ct)));
                if (inFlight.TryAddBusy())
                {
                    break;
                }
                _inFlightRequests.TryRemove(hashCacheFileName, out _);
            }

            try
            {
                TaskCompletionSource<TTSResult> cancellationTcs = new TaskCompletionSource<TTSResult>();
                using (cancellationToken.Register(() => cancellationTcs.TrySetCanceled(cancellationToken)))
                {
                    Task<TTSResult> winner = await Task.WhenAny(inFlight.Task, cancellationTcs.Task).ConfigureAwait(false);

                    if (winner == cancellationTcs.Task)
                    {
                        LogConstants.TTS_GENERATOR_TTS_CANCELLED.Log(nameof(TTSGenerator), textToSpeak, hashCacheFileName);
                        await cancellationTcs.Task.ConfigureAwait(false);
                    }
                    return await inFlight.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                inFlight.RemoveBusy();

                if (inFlight.Task.IsCompleted)
                {
                    _inFlightRequests.TryRemove(hashCacheFileName, out _);
                }
            }
        }

        private async Task<TTSResult> RunGenerationAsync(string hashCacheFileName, string fullCachePath, string textToSpeak, PiperVoiceSettings settings, CancellationToken sharedCancellationToken)
        {
            SemaphoreSlim semaphore = _semaphore;
            await semaphore.WaitAsync(sharedCancellationToken).ConfigureAwait(false);
            try
            {
                // late cache hit
                if (File.Exists(fullCachePath) && new FileInfo(fullCachePath).Length > 0)
                {
                    AudioClip lateClip = await LoadAudioClipFromDiskAsync(fullCachePath, hashCacheFileName);
                    return new TTSResult
                    {
                        AudioClip = lateClip,
                        Success = lateClip != null
                    };
                }

                TTSResult endResult = new TTSResult();
                LogConstants.TTS_GENERATOR_GENERATING_TTS.Log(nameof(TTSGenerator), textToSpeak, hashCacheFileName);
                TTSRawResult synthResult = await _server.SynthesizeAsync(textToSpeak, hashCacheFileName, settings, sharedCancellationToken).ConfigureAwait(false);
                if (!synthResult.IsSuccess)
                {
                    endResult.AudioClip = null;
                    endResult.Success = false;
                    LogConstants.CODE_GENERIC_FAIL.Log(nameof(TTSGenerator), "synthResult", synthResult.Error);
                    return endResult;
                }

                bool wasConvertedToOgg = await ConvertPcmToOggAsync(synthResult.Pcm, synthResult.SampleRate, hashCacheFileName, fullCachePath, sharedCancellationToken);
                if (wasConvertedToOgg)
                {
                    endResult.AudioClip = await LoadAudioClipFromDiskAsync(fullCachePath, hashCacheFileName);
                    endResult.Success = endResult.AudioClip != null;
                }

                return endResult;
            }
            finally
            {
                if (File.Exists(fullCachePath) && new FileInfo(fullCachePath).Length == 0)
                {
                    TryDeleteCorruptedCache(fullCachePath, hashCacheFileName);
                }

                try
                {
                    semaphore.Release();
                }
                catch (Exception ex)
                {
                    LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), nameof(RunGenerationAsync), ex);
                }

                _inFlightRequests.TryRemove(hashCacheFileName, out _);
            }
        }

        private static Task<bool> ConvertPcmToOggAsync(byte[] pcmData, int sourceSampleRate, string fileHashName, string oggOutputPath, CancellationToken cancellationToken)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSGenerator), nameof(ConvertPcmToOggAsync));

            return Task.Run(() =>
            {
                string tempPath = oggOutputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

                short[] srcShorts = null;
                short[] rentedResampleBuffer = null;

                try
                {
                    int srcCount = pcmData.Length / 2;
                    srcShorts = ArrayPool<short>.Shared.Rent(srcCount);
                    Buffer.BlockCopy(pcmData, 0, srcShorts, 0, pcmData.Length);

                    short[] resampledShorts;
                    int resampledCount;

                    if (sourceSampleRate == OggConstants.OGG_SAMPLE_RATE || srcCount == 0)
                    {
                        resampledShorts = srcShorts;
                        resampledCount = srcCount;
                    }
                    else
                    {
                        resampledCount = (int)(srcCount * ((double)OggConstants.OGG_SAMPLE_RATE / sourceSampleRate));
                        rentedResampleBuffer = ArrayPool<short>.Shared.Rent(resampledCount);
                        Resample(srcShorts, srcCount, sourceSampleRate, OggConstants.OGG_SAMPLE_RATE, rentedResampleBuffer, resampledCount);
                        resampledShorts = rentedResampleBuffer;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        IOpusEncoder encoder = OpusCodecFactory.CreateEncoder(OggConstants.OGG_SAMPLE_RATE, OggConstants.OGG_CHANNELS_AMOUNT, OpusApplication.OPUS_APPLICATION_VOIP);
                        encoder.Bitrate = OggConstants.OGG_BITRATE;
                        encoder.UseVBR = true;

                        OpusOggWriteStream oggStream = new OpusOggWriteStream(encoder, fs);
                        oggStream.WriteSamples(resampledShorts, 0, resampledCount);
                        oggStream.Finish();
                    }

                    if (File.Exists(oggOutputPath) && new FileInfo(oggOutputPath).Length > 0)
                    {
                        TryDeleteTempFile(tempPath);
                        return true;
                    }

                    AtomicReplace(tempPath, oggOutputPath);
                    return true;
                }
                catch (Exception ex)
                {
                    TryDeleteTempFile(tempPath);
                    LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), nameof(ConvertPcmToOggAsync), ex);
                    return false;
                }
                finally
                {
                    if (srcShorts != null)
                    {
                        ArrayPool<short>.Shared.Return(srcShorts);
                    }
                    if (rentedResampleBuffer != null)
                    {
                        ArrayPool<short>.Shared.Return(rentedResampleBuffer);
                    }
                }
            });
        }

        // ------------------- Helpers -------------------
        private static void AtomicReplace(string tempPath, string destPath)
        {
            try
            {
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                File.Move(tempPath, destPath);
            }
            catch (IOException)
            {
                TryDeleteTempFile(tempPath);
            }
        }

        private static void TryDeleteTempFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), nameof(TryDeleteTempFile), ex);
            }
        }

        private static void Resample(short[] input, int inputCount, int sourceRate, int targetRate, short[] output, int outputCount)
        {
            double ratio = (double)sourceRate / targetRate;
            for (int i = 0; i < outputCount; i++)
            {
                double srcIndex = i * ratio;
                int indexLeft = (int)Math.Floor(srcIndex);
                int indexRight = Math.Min(indexLeft + 1, inputCount - 1);
                double t = srcIndex - indexLeft;
                output[i] = (short)((1 - t) * input[indexLeft] + t * input[indexRight]);
            }
        }

        private static void TryDeleteCorruptedCache(string fullCachePath, string hashCacheFileName)
        {
            try
            {
                if (File.Exists(fullCachePath) && new FileInfo(fullCachePath).Length == 0)
                {
                    LogConstants.TTS_GENERATOR_DELETE_0KB_CACHE.Log(nameof(TTSGenerator), hashCacheFileName);
                    File.Delete(fullCachePath);
                }
            }
            catch (Exception ex)
            {
                LogConstants.TTS_GENERATOR_FAILED_TO_DELETE_0KB_CACHE.Log(nameof(TTSGenerator), hashCacheFileName, ex.Message);
            }
        }

        private static Task<AudioClip> LoadAudioClipFromDiskAsync(string absoluteFilePath, string clipName)
        {
            if (!File.Exists(absoluteFilePath))
            {
                LogConstants.TTS_GENERATOR_NO_CACHED_AUDIO_FOUND.Log(nameof(TTSGenerator), clipName, absoluteFilePath);
                return Task.FromResult<AudioClip>(null);
            }

            TaskCompletionSource<AudioClip> tcs = new TaskCompletionSource<AudioClip>();
            TTSCompanyPlugin.instance.StartCoroutine(LoadCoroutine(absoluteFilePath, clipName, tcs));
            return tcs.Task;
        }

        private static IEnumerator LoadCoroutine(string absoluteFilePath, string clipName, TaskCompletionSource<AudioClip> tcs)
        {
            Task<float[]> decodeTask = Task.Run(() => DecodeOggOffThread(absoluteFilePath));

            yield return new WaitUntil(() => decodeTask.IsCompleted);

            if (decodeTask.IsFaulted)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "LoadCoroutine", decodeTask.Exception?.GetBaseException().Message);
                tcs.SetResult(null);
                yield break;
            }

            if (decodeTask.Result == null || decodeTask.Result.Length == 0)
            {
                tcs.SetResult(null);
                yield break;
            }

            AudioClip clip = AudioClip.Create(clipName, decodeTask.Result.Length, OggConstants.OGG_CHANNELS_AMOUNT, OggConstants.OGG_SAMPLE_RATE, false);
            clip.SetData(decodeTask.Result, 0);
            tcs.SetResult(clip);
        }

        private static float[] DecodeOggOffThread(string absoluteFilePath)
        {
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using (FileStream fs = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        IOpusDecoder decoder = OpusCodecFactory.CreateDecoder(OggConstants.OGG_SAMPLE_RATE, OggConstants.OGG_CHANNELS_AMOUNT);
                        OpusOggReadStream oggStream = new OpusOggReadStream(decoder, fs);

                        List<short[]> packets = new List<short[]>();
                        int totalSamples = 0;
                        while (oggStream.HasNextPacket)
                        {
                            short[] packet = oggStream.DecodeNextPacket();
                            if (packet != null)
                            {
                                packets.Add(packet);
                                totalSamples += packet.Length;
                            }
                        }
                        float[] samples = new float[totalSamples];
                        int offset = 0;
                        foreach (short[] packet in packets)
                        {
                            for (int i = 0; i < packet.Length; i++)
                            {
                                samples[offset + i] = packet[i] * DecodeOggOffThreadMultiplier;
                            }
                            offset += packet.Length;
                        }
                        return samples;
                    }
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(50 * attempt);
                }
                catch (Exception ex)
                {
                    LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), nameof(DecodeOggOffThread), ex.Message);
                    return null;
                }
            }
            return null;
        }

        private bool ValidateInputs(string textToSpeak, PiperVoiceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - textToSpeak", TTSConstants.TTS_VALI_TEXT_TO_SPEAK);
                return false;
            }
            if (settings == null)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - settings", TTSConstants.TTS_VALI_SETTINGS);
                return false;
            }
            if (string.IsNullOrWhiteSpace(settings.ModelName))
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - settings.ModelName", TTSConstants.TTS_VALI_MODEL_NAME);
                return false;
            }
            if (!_server.IsVoiceModelValid(settings.ModelName))
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - IsVoiceModelValid(settings.ModelName)", TTSConstants.TTS_VALI_MODEL_INVALID);
                return false;
            }
            if (settings.SpeechRate <= 0f)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - settings.SpeechRate", TTSConstants.TTS_VALI_SPEECH_RATE);
                return false;
            }
            // checks passed
            return true;
        }
    }
}
