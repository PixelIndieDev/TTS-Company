using NVorbis;
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
using UnityEngine;

namespace TTS_Company.Components
{
    internal class TTSGenerator
    {
        private readonly ConcurrentDictionary<string, Task<TTSResult>> _inFlightRequests = new ConcurrentDictionary<string, Task<TTSResult>>();
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
            if (!ZipHelper.CheckForPiperTTS())
            {
                LogConstants.TTS_GENERATOR_UNZIP_FAILED.Log(nameof(TTSGenerator), TTSConstants.PIPER_EXE_NAME);
                return;
            }
            if (!ZipHelper.CheckForFFmpeg())
            {
                LogConstants.TTS_GENERATOR_UNZIP_FAILED.Log(nameof(TTSGenerator), FFmpegConstants.FFMPEG_EXE_NAME);
                return;
            }

            _isAvailable = true;
        }

        private void SetMaxConcurrentRequests(int maxConcurrentRequests)
        {
            MaxConcurrentRequests = maxConcurrentRequests;
            LogConstants.CODE_NEW_VALUE_SET.Log(nameof(TTSGenerator), "maxConcurrentRequests", maxConcurrentRequests);
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

        internal async Task<TTSResult> GenerateTTSAsync(string textToSpeak, PiperVoiceSettings settings, CancellationToken cancellationToken)
        {
            if (!_isAvailable || _disposed)
            {
                return new TTSResult { AudioClip = null, Success = false };
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

                AudioClip clip = await LoadAudioClipFromDiskAsync(fullCachePath, hashCacheFileName);
                return new TTSResult { AudioClip = clip, Success = clip != null };
            }

            Task<TTSResult> generationTask = _inFlightRequests.GetOrAdd(hashCacheFileName, _ => RunGenerationAsync(hashCacheFileName, fullCachePath, textToSpeak, settings));

            try
            {
                var cancellationTcs = new TaskCompletionSource<TTSResult>();
                using (cancellationToken.Register(() => cancellationTcs.TrySetCanceled()))
                {
                    Task<TTSResult> winner = await Task.WhenAny(generationTask, cancellationTcs.Task);

                    if (winner == cancellationTcs.Task)
                    {
                        LogConstants.TTS_GENERATOR_TTS_CANCELLED.Log(nameof(TTSGenerator), textToSpeak, hashCacheFileName);
                        throw new OperationCanceledException(cancellationToken);
                    }

                    return await generationTask;
                }
            }
            finally
            {
                if (generationTask.IsCompleted)
                {
                    _inFlightRequests.TryRemove(hashCacheFileName, out _);
                }
            }
        }

        private async Task<TTSResult> RunGenerationAsync(string hashCacheFileName, string fullCachePath, string textToSpeak, PiperVoiceSettings settings)
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

                LogConstants.TTS_GENERATOR_GENERATING_TTS.Log(nameof(TTSGenerator), textToSpeak, hashCacheFileName);
                TTSResult result = await RunPiper(hashCacheFileName, fullCachePath, textToSpeak, settings, CancellationToken.None);

                if (result.Success)
                {
                    result.AudioClip = await LoadAudioClipFromDiskAsync(fullCachePath, hashCacheFileName);
                    result.Success = result.AudioClip != null;
                    if (!result.Success)
                    {
                        result.Error = TTSConstants.TTS_GENERATION_TO_AUDIO_CLIP_NO_SUCCESS;
                    };
                }

                return result;
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

        private static Task<TTSResult> RunPiper(string fileHashName, string oggOutputPath, string textToSpeak, PiperVoiceSettings settings, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var result = new TTSResult();

                string piperArgs = BuildArguments(settings);
                LogConstants.TTS_GENERATOR_RUN_PIPER_ARGUMENTS.Log(nameof(TTSGenerator), "piperTTS", piperArgs);
                string ffmpegArgs = $"-f s16le -ar {FFmpegConstants.FFMPEG_OGG_SAMPLE_RATE} -ac {FFmpegConstants.FFMPEG_OGG_CHANNELS_AMOUNT} -i pipe:0 -c:a libvorbis -q:a {FFmpegConstants.FFMPEG_OGG_QUALITY_LEVEL} -y \"{oggOutputPath}\"";
                LogConstants.TTS_GENERATOR_RUN_PIPER_ARGUMENTS.Log(nameof(TTSGenerator), "FFmpeg", ffmpegArgs);

                try
                {
                    using (var piperProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = TTSConstants.PIPER_EXECUTABLE_LOCATION,
                            Arguments = piperArgs,
                            UseShellExecute = false,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            WorkingDirectory = Path.GetDirectoryName(TTSConstants.PIPER_EXECUTABLE_LOCATION)
                        }
                    })
                    using (var ffmpegProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = FFmpegConstants.FFMPEG_EXE_FILE_LOCATION,
                            Arguments = ffmpegArgs,
                            UseShellExecute = false,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = false,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    })
                    {
                        piperProcess.StartInfo.EnvironmentVariables["OMP_NUM_THREADS"] = "1";
                        piperProcess.StartInfo.EnvironmentVariables["OMP_WAIT_POLICY"] = "PASSIVE";

                        piperProcess.Start();
                        ffmpegProcess.Start();

                        // Drain stderr concurrently — critical to prevent buffer deadlock
                        Task<string> piperErrorTask = piperProcess.StandardError.ReadToEndAsync();
                        Task<string> ffmpegErrorTask = ffmpegProcess.StandardError.ReadToEndAsync();

                        // Write input to piper and close its stdin
                        await piperProcess.StandardInput.WriteLineAsync(textToSpeak);
                        piperProcess.StandardInput.Close();

                        // Pipe piper stdout → ffmpeg stdin, then close ffmpeg stdin
                        Task pipeTask = PipeWithCancellationAsync(piperProcess.StandardOutput.BaseStream, ffmpegProcess.StandardInput.BaseStream, ffmpegProcess, cancellationToken);

                        // Poll piper for exit, watching for cancellation
                        await WaitForExitAsync(piperProcess, cancellationToken, ffmpegProcess);

                        // Wait for pipe to fully flush
                        await pipeTask;

                        // Poll ffmpeg for exit
                        await WaitForExitAsync(ffmpegProcess, cancellationToken);

                        // Await stderr drains (they should be done by now since processes exited)
                        string piperStderr = await piperErrorTask;
                        string ffmpegStderr = await ffmpegErrorTask;

                        int piperExitCode = piperProcess.ExitCode;
                        int ffmpegExitCode = ffmpegProcess.ExitCode;

                        if (piperExitCode != 0)
                        {
                            result.Error = $"Piper exited with code {piperExitCode}. stderr: {piperStderr}";
                            TryDeleteCorruptedCache(oggOutputPath, fileHashName);
                            return result;
                        }

                        if (ffmpegExitCode != 0)
                        {
                            result.Error = $"FFmpeg exited with code {ffmpegExitCode}. stderr: {ffmpegStderr}";
                            TryDeleteCorruptedCache(oggOutputPath, fileHashName);
                            return result;
                        }

                        if (!File.Exists(oggOutputPath) || new FileInfo(oggOutputPath).Length == 0)
                        {
                            result.Error = TTSConstants.TTS_GENERATION_OGG_FILE_CREATION_NO_SUCCESS;
                            TryDeleteCorruptedCache(oggOutputPath, fileHashName);
                            return result;
                        }
                        result.Success = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    result.Error = TTSConstants.TTS_GENERATION_CANCELLED;
                    TryDeleteCorruptedCache(oggOutputPath, fileHashName);
                    throw;
                }
                catch (Exception ex)
                {
                    TryDeleteCorruptedCache(oggOutputPath, fileHashName);
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        // ------------------- Helpers -------------------

        private static string BuildArguments(PiperVoiceSettings s)
        {
            var args = new System.Text.StringBuilder();
            args.Append($"--model \"{s.ModelName}\"");
            args.Append($" --length_scale {(1.0f / s.SpeechRate).ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
            args.Append($" --noise_scale {s.NoiseScale.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
            args.Append($" --noise_w {s.NoiseScaleW.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
            args.Append($" --sentence_silence {s.SentenceSilence.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
            args.Append(" --output-raw");
            return args.ToString();
        }

        private static void KillProcesses(Process p1, Process p2)
        {
            try 
            {
                if (!p1.HasExited)
                {
                    p1.Kill();
                }
            } 
            catch 
            {
                LogConstants.TTS_GENERATOR_PROCESS_FAILED_TO_STOP.Log(nameof(TTSGenerator), 1);
            }

            try
            {
                if (!p2.HasExited)
                {
                    p2.Kill();
                }
            }
            catch
            {
                LogConstants.TTS_GENERATOR_PROCESS_FAILED_TO_STOP.Log(nameof(TTSGenerator), 2);
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

        private static async Task PipeWithCancellationAsync(Stream source, Stream destination, Process destinationProcess, CancellationToken ct)
        {
            try
            {
                await source.CopyToAsync(destination, 81920, ct);
            }
            catch (OperationCanceledException)
            {
                LogConstants.CODE_GENERIC_CANCELLED.Log(nameof(TTSGenerator), "PipeWithCancellationAsync");
            }
            catch (Exception e)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "PipeWithCancellationAsync", e.Message);
            }
            finally
            {
                try
                {
                    destination.Close();
                }
                catch
                {
                    LogConstants.CODE_GENERIC_CATCH.Log(nameof(TTSGenerator), "PipeWithCancellationAsync");
                }
            }
        }

        private static async Task WaitForExitAsync(Process process, CancellationToken ct, Process companionToKillIfDead = null)
        {
            while (!process.HasExited)
            {
                if (ct.IsCancellationRequested)
                {
                    if (companionToKillIfDead != null)
                    {
                        KillProcesses(process, companionToKillIfDead);
                    }
                    else
                    {
                        try 
                        {
                            if (!process.HasExited) 
                            { 
                                process.Kill();
                            }
                        }
                        catch 
                        {
                            LogConstants.CODE_GENERIC_CATCH.Log(nameof(TTSGenerator), "WaitForExitAsync");
                        }
                    }

                    ct.ThrowIfCancellationRequested();
                }

                if (companionToKillIfDead != null && companionToKillIfDead.HasExited)
                {
                    KillProcesses(process, companionToKillIfDead);
                    LogConstants.TTS_GENERATOR_FFMPEG_EXITED_PREMATURE.Log(nameof(TTSGenerator));
                    return;
                }

                await Task.Delay(50, ct);
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
            Task<float[]> decodeTask = Task.Run(() => DecodeOggVorbisOffThread(absoluteFilePath));

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

            AudioClip clip = AudioClip.Create(clipName, decodeTask.Result.Length, FFmpegConstants.FFMPEG_OGG_CHANNELS_AMOUNT, FFmpegConstants.FFMPEG_OGG_SAMPLE_RATE, false);
            clip.SetData(decodeTask.Result, 0);
            tcs.SetResult(clip);
        }

        private static float[] DecodeOggVorbisOffThread(string absoluteFilePath)
        {
            try
            {
                using (var reader = new VorbisReader(absoluteFilePath))
                {
                    int channels = reader.Channels;
                    int frequency = reader.SampleRate;

                    var sampleList = new List<float>((int)(reader.TotalSamples > 0 ? reader.TotalSamples * channels : 22050 * 5)); // rough preallocation guess
                    float[] buffer = new float[4096];

                    int read;
                    while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < read; i++)
                        {
                            sampleList.Add(buffer[i]);
                        }
                    }
                    return sampleList.ToArray();
                }
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), nameof(DecodeOggVorbisOffThread), ex.Message);
                return null;
            }
        }

        private static bool ValidateInputs(string textToSpeak, PiperVoiceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - textToSpeak", "TTS text cannot be empty");
                return false;
            }
            if (settings == null)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - settings", "settings cannot be NULL");
                return false;
            }
            if (string.IsNullOrWhiteSpace(settings.ModelName))
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - settings.ModelName", "ModelPath must be set in settings");
                return false;
            }
            if (string.IsNullOrWhiteSpace(settings.ModelName) || !File.Exists(settings.ModelName))
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - settings.ModelName", "Piper model not found or valid");
                return false;
            }
            if (settings.SpeechRate <= 0f)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSGenerator), "ValidateInputs - settings.SpeechRate", "SpeechRate must be > 0");
                return false;
            }
            // checks passed
            return true;
        }

        internal void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _semaphore?.Dispose();
        }
    }
}
