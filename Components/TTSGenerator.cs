using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;
using Concentus.Structs;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Enums;
using TTS_Company.Components.Helpers;
using TTS_Company.Components.Server.Components;
using UnityEngine;

namespace TTS_Company.Components
{
    internal class TTSGenerator
    {
        private readonly PiperTTSServer _server = new PiperTTSServer();

        private readonly ConcurrentDictionary<string, BusyGeneration> _inFlightRequests = new ConcurrentDictionary<string, BusyGeneration>();
        internal int MaxConcurrentRequests
        {
            get => _maxConcurrent;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Must be at least 1.");
                }

                _maxConcurrent = value;

                var oldSemaphore = _semaphore;
                _semaphore = new SemaphoreSlim(value, value);

                // Don't dispose, let the garbage collector clear it after its tasks are done
                //oldSemaphore?.Dispose();
            }
        }

        private int _maxConcurrent;
        private SemaphoreSlim _semaphore;
        private bool _disposed;
        private bool _isAvailable;

        // cpu values
        private static readonly int CPU_totalCores = Environment.ProcessorCount;
        private static readonly int CPU_coresReservedForGame = CPU_totalCores / 10;
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

            if (!ZipHelper.CheckForPiperTTS())
            {
                LogConstants.TTS_GENERATOR_UNZIP_FAILED.Log(nameof(TTSGenerator), TTSConstants.PIPER_EXE_NAME);
                return false;
            }

            bool started = await _server.StartAsync(TTSConstants.PIPER_SERVER_STARTUP_TIMEOUT_MS, cancellationToken).ConfigureAwait(false);

            _isAvailable = started;
            return started;
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

            }

            _semaphore?.Dispose();
        }

        // voice loading must be called before calling GenerateTTSAsync()
        internal Task<(bool Success, string Error)> PreloadVoiceAsync(string voiceName, CancellationToken cancellationToken = default)
        {
            if (!_isAvailable || _disposed)
            {
                return Task.FromResult((false, TTSConstants.TTS_SERVER_UNAVAILABLE));
            }

            if (string.IsNullOrWhiteSpace(voiceName))
            {
                return Task.FromResult((false, TTSConstants.TTS_VOICE_MODEL_NAME_EMPTY));
            }

            return _server.LoadModelAsync(voiceName, cancellationToken);
        }

        internal Task<(bool Success, string Error)> UnloadVoiceAsync(string voiceName, CancellationToken cancellationToken = default)
        {
            if (!_isAvailable || _disposed)
            {
                return Task.FromResult((false, TTSConstants.TTS_SERVER_UNAVAILABLE));
            }

            if (string.IsNullOrWhiteSpace(voiceName))
            {
                return Task.FromResult((false, TTSConstants.TTS_VOICE_MODEL_NAME_EMPTY));
            }

            return _server.UnloadModelAsync(voiceName, cancellationToken);
        }

        internal bool isVoiceModelLoaded(string voiceModelName)
        {
            return _server.HasVoiceModelBeenLoaded(voiceModelName);
        }

        internal async Task<TTSResult> GenerateTTSAsync(string textToSpeak, PiperVoiceSettings settings, CancellationToken cancellationToken)
        {
            if (!_isAvailable || _disposed)
            {
                return new TTSResult { AudioClip = null, Success = false, Error = TTSConstants.TTS_SERVER_UNAVAILABLE };
            }

            cancellationToken.ThrowIfCancellationRequested();

            string hashCacheFileName = await Task.Run(() =>
            {
                if (!ValidateInputs(textToSpeak, settings))
                {
                    return string.Empty;
                }

                Directory.CreateDirectory(TTSConstants.TTS_VOICE_CACHE_SOUNDCLIPS_PATH);
                return HashHelper.GetHashTTSFileNameWithFileType(textToSpeak, settings);
            }, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(hashCacheFileName))
            {
                return new TTSResult { AudioClip = null, Success = false };
            }

            string fullCachePath = Path.Combine(TTSConstants.TTS_VOICE_CACHE_SOUNDCLIPS_PATH, hashCacheFileName);

            if (File.Exists(fullCachePath) && new FileInfo(fullCachePath).Length > 0)
            {
                LogConstants.TTS_GENERATOR_FOUND_CACHED_TTS.Log(nameof(TTSGenerator), hashCacheFileName);

                AudioClip clip = await LoadAudioClipFromDiskAsync(fullCachePath, hashCacheFileName).ConfigureAwait(false);
                return new TTSResult { AudioClip = clip, Success = clip != null };
            }

            BusyGeneration inFlight = _inFlightRequests.GetOrAdd(hashCacheFileName, _ => new BusyGeneration(ct => RunGenerationAsync(hashCacheFileName, fullCachePath, textToSpeak, settings, ct)));
            inFlight.AddBusy();

            try
            {
                var cancellationTcs = new TaskCompletionSource<TTSResult>();
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
            await _semaphore.WaitAsync();
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

                bool isServerAlive = await _server.PingAsync(sharedCancellationToken);
                if (!isServerAlive)
                {
                    return endResult;
                }

                LogConstants.TTS_GENERATOR_GENERATING_TTS.Log(nameof(TTSGenerator), textToSpeak, hashCacheFileName);
                TTSRawResult synthResult = await _server.SynthesizeAsync(textToSpeak, hashCacheFileName, settings, sharedCancellationToken).ConfigureAwait(false);
                if (!synthResult.IsSuccess)
                {
                    endResult.AudioClip = null;
                    endResult.Success = false;
                    LogConstants.CODE_GENERIC_FAIL.Log(nameof(TTSGenerator), "synthResult", synthResult.Error);
                    return endResult;
                }

                bool wasConvertedToOgg = await ConvertPcmToOggAsync(synthResult.Pcm, hashCacheFileName, fullCachePath, sharedCancellationToken);
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

                _semaphore.Release();
                _inFlightRequests.TryRemove(hashCacheFileName, out _);
            }
        }

        private static Task<bool> ConvertPcmToOggAsync(byte[] pcmData, string fileHashName, string oggOutputPath, CancellationToken cancellationToken)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSGenerator), nameof(ConvertPcmToOggAsync));

            return Task.Run(() =>
            {
                try
                {
                    short[] srcShorts = new short[pcmData.Length / 2];
                    Buffer.BlockCopy(pcmData, 0, srcShorts, 0, pcmData.Length);
                    short[] resampledShorts = Resample22050To24000(srcShorts);

                    cancellationToken.ThrowIfCancellationRequested();

                    using (FileStream fs = new FileStream(oggOutputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        IOpusEncoder encoder = OpusCodecFactory.CreateEncoder(OggConstants.OGG_SAMPLE_RATE, OggConstants.OGG_CHANNELS_AMOUNT, OpusApplication.OPUS_APPLICATION_VOIP);
                        encoder.Bitrate = OggConstants.OGG_SAMPLE_RATE;

                        OpusOggWriteStream oggStream = new OpusOggWriteStream(encoder, fs);
                        oggStream.WriteSamples(resampledShorts, 0, resampledShorts.Length);
                        oggStream.Finish();
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    TryDeleteCorruptedCache(oggOutputPath, fileHashName);
                    LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), nameof(ConvertPcmToOggAsync), ex);
                    return false;
                }
            });
        }

        // ------------------- Helpers -------------------
        private static short[] Resample22050To24000(short[] input)
        {
            double ratio = 22050.0 / 24000.0;
            int dstLength = (int)(input.Length * (24000.0 / 22050.0));
            short[] output = new short[dstLength];

            for (int i = 0; i < dstLength; i++)
            {
                double srcIndex = i * ratio;
                int indexLeft = (int)Math.Floor(srcIndex);
                int indexRight = Math.Min(indexLeft + 1, input.Length - 1);
                double t = srcIndex - indexLeft;
                output[i] = (short)((1 - t) * input[indexLeft] + t * input[indexRight]);
            }
            return output;
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

            var tcs = new TaskCompletionSource<AudioClip>();
            Plugin.instance.StartCoroutine(LoadCoroutine(absoluteFilePath, clipName, tcs));

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
            try
            {
                using (FileStream fs = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    IOpusDecoder decoder = OpusCodecFactory.CreateDecoder(OggConstants.OGG_SAMPLE_RATE, OggConstants.OGG_CHANNELS_AMOUNT);
                    OpusOggReadStream oggStream = new OpusOggReadStream(decoder, fs);

                    List<float> sampleList = new List<float>();

                    while (oggStream.HasNextPacket)
                    {
                        short[] packet = oggStream.DecodeNextPacket();
                        if (packet != null)
                        {
                            for (int i = 0; i < packet.Length; i++)
                            {
                                sampleList.Add(packet[i] / 32768f);
                            }
                        }
                    }
                    return sampleList.ToArray();
                }
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), nameof(DecodeOggOffThread), ex.Message);
                return null;
            }
        }

        private static bool ValidateInputs(string textToSpeak, PiperVoiceSettings settings)
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
            if (string.IsNullOrWhiteSpace(settings.ModelPath) || !File.Exists(settings.ModelPath))
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - settings.ModelPath", TTSConstants.TTS_VALI_MODEL_INVALID);
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
