using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TTSCompany.Components.Constants;
using TTSCompany.Components.Server.Components;

namespace TTSCompany.Components
{
    internal sealed class PiperTTSServer
    {
        private const string ReadyPrefix = "READY ON PORT ";

        private Process _process;
        private int _port;
        private readonly StringBuilder _stderrLog = new StringBuilder();

        internal bool IsRunning => _process != null && !_process.HasExited;
        internal int Port => _port;

        internal readonly VoiceModelMemoryManager _memoryManager;

        public PiperTTSServer()
        {
            _memoryManager = new VoiceModelMemoryManager(this);
        }

        internal async Task<bool> StartAsync(int startupTimeoutMs, CancellationToken cancellationToken)
        {
            if (IsRunning)
            {
                return true;
            }

            if (!File.Exists(TTSConstants.PIPER_EXECUTABLE_LOCATION))
            {
                LogConstants.PIPER_TTS_SERVER_EXE_NOT_FOUND.Log(nameof(PiperTTSServer), TTSConstants.PIPER_EXECUTABLE_LOCATION);
                return false;
            }

            if (!Directory.Exists(TTSConstants.TTS_DEFAULT_VOICE_MODELS_FOLDER_LOCATION))
            {
                LogConstants.PIPER_TTS_SERVER_VOICE_FOLDER_NOT_FOUND.Log(nameof(PiperTTSServer), TTSConstants.TTS_DEFAULT_VOICE_MODELS_FOLDER_LOCATION);
                return false;
            }
            _memoryManager.InitializeModelRegistry();

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = TTSConstants.PIPER_EXECUTABLE_LOCATION,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(TTSConstants.PIPER_EXECUTABLE_LOCATION)
            };

            Process process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            TaskCompletionSource<bool> exitTcs = new TaskCompletionSource<bool>();
            process.Exited += (_, __) => exitTcs.TrySetResult(true);

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                LogConstants.PIPER_TTS_SERVER_VOICE_FOLDER_NOT_FOUND.Log(nameof(PiperTTSServer), ex.Message);
                return false;
            }

            _ = DrainStreamAsync(process.StandardError, onLine =>
            {
                lock (_stderrLog)
                {
                    _stderrLog.AppendLine(onLine);
                    if (_stderrLog.Length > 8000)
                    {
                        _stderrLog.Remove(0, _stderrLog.Length - 8000);
                    }
                }
            });

            Task<string> readyLineTask = process.StandardOutput.ReadLineAsync();
            Task timeoutTask = Task.Delay(startupTimeoutMs, cancellationToken);

            Task winner = await Task.WhenAny(readyLineTask, exitTcs.Task, timeoutTask).ConfigureAwait(false);

            if (winner == exitTcs.Task)
            {
                string stderr;
                lock (_stderrLog) stderr = _stderrLog.ToString();
                LogConstants.PIPER_TTS_SERVER_STARTUP_ISSUE.Log(nameof(PiperTTSServer), SafeExitCode(process), stderr);
                return false;
            }

            if (winner != readyLineTask)
            {
                LogConstants.PIPER_TTS_SERVER_STARTUP_ISSUE.Log(nameof(PiperTTSServer), "Timed out", "Timed out waiting for server to report its port");
                TryKill(process);
                return false;
            }

            string line = await readyLineTask.ConfigureAwait(false);
            if (line == null || !line.StartsWith(ReadyPrefix, StringComparison.Ordinal) || !int.TryParse(line.Substring(ReadyPrefix.Length).Trim(), out _port))
            {
                string stderr;
                lock (_stderrLog) stderr = _stderrLog.ToString();
                LogConstants.PIPER_TTS_SERVER_STARTUP_ISSUE.Log(nameof(PiperTTSServer), $"Unexpected startup output at: {line}", stderr);
                TryKill(process);
                return false;
            }

            // prevent the pipe from filling up
            _ = DrainStreamAsync(process.StandardOutput, outLine => LogConstants.PIPER_TTS_SERVER_OUTPUT_DRAIN.Log(nameof(PiperTTSServer), outLine));

            _process = process;
            LogConstants.PIPER_TTS_SERVER_SUCCESS_STARTUP.Log(nameof(PiperTTSServer), _port, process.Id);
            return true;
        }

        internal async Task ShutdownAsync(int timeoutMs = 3000)
        {
            Process process = _process;
            if (process == null)
            {
                return;
            }

            if (!process.HasExited)
            {
                try
                {
                    await SendSimpleCommandAsync("{\"command\":\"shutdown\"}\n", CancellationToken.None, 1000).ConfigureAwait(false);
                }
                catch
                {
                    LogConstants.CODE_GENERIC_CATCH.Log(nameof(PiperTTSServer), "SendSimpleCommandAsync");
                }
            }

            try
            {
                if (!process.HasExited)
                {
                    TaskCompletionSource<bool> exitTcs = new TaskCompletionSource<bool>();
                    process.EnableRaisingEvents = true;
                    process.Exited += (_, __) => exitTcs.TrySetResult(true);
                    if (process.HasExited)
                    {
                        exitTcs.TrySetResult(true);
                    }

                    Task winner = await Task.WhenAny(exitTcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
                    if (winner != exitTcs.Task)
                    {
                        TryKill(process);
                    }
                }
            }
            catch
            {
                TryKill(process);
            }
            finally
            {
                _process = null;
                LogConstants.PIPER_TTS_SERVER_STOPPED.Log(nameof(PiperTTSServer));
            }
        }

        internal bool HasVoiceModelBeenLoaded(string modelName)
        {
            return _memoryManager.HasVoiceModelBeenLoaded(modelName);
        }

        internal bool IsVoiceModelValid(string modelName)
        {
            return _memoryManager.IsVoiceModelValid(modelName);
        }

        internal async Task<(bool Success, string Error)> LoadModelAsync(string modelName, ulong callingAssemblyHash, CancellationToken cancellationToken)
        {
            return await _memoryManager.LoadModelAsync(modelName, callingAssemblyHash, cancellationToken);
        }

        internal async Task<(bool Success, string Error)> UnloadModelAsync(string modelName, ulong callingAssemblyHash, CancellationToken cancellationToken)
        {
            return await _memoryManager.UnloadModelAsync(modelName, callingAssemblyHash, cancellationToken);
        }

        internal async Task<Dictionary<string, object>> SendSimpleCommandAsync(string requestJsonLine, CancellationToken cancellationToken, int timeoutMs = 30000)
        {
            using (TcpClient client = new TcpClient())
            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(timeoutMs);

                using (cts.Token.Register(() =>
                {
                    try
                    {
                        client.Close();
                    }
                    catch
                    {
                        LogConstants.CODE_GENERIC_CATCH.Log(nameof(PiperTTSServer), "SendSimpleCommandAsync");
                    }
                }))
                {
                    await ConnectAsync(client, _port, cts.Token).ConfigureAwait(false);
                    client.NoDelay = true;
                    NetworkStream stream = client.GetStream();

                    byte[] bytes = Encoding.UTF8.GetBytes(requestJsonLine);
                    await stream.WriteAsync(bytes, 0, bytes.Length, cts.Token).ConfigureAwait(false);

                    (string line, _) = await ReadLineWithLeftoverAsync(stream, cts.Token).ConfigureAwait(false);
                    return JSONHelper.ParseFlatObject(line);
                }
            }
        }

        internal (bool Success, string Error) ToResult(Dictionary<string, object> response)
        {
            bool ok = response.TryGetValue("status", out object status) && (status as string) == "ok";
            string error = ok ? null : (response.TryGetValue("message", out object msg) ? msg as string : "unknown error");
            return (ok, error);
        }

        // check if the server is still alive
        internal async Task<bool> PingAsync(CancellationToken cancellationToken)
        {
            try
            {
                Dictionary<string, object> response = await SendSimpleCommandAsync("{\"command\":\"ping\"}\n", cancellationToken).ConfigureAwait(false);
                return response.TryGetValue("alive", out object alive) && alive is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        internal async Task<TTSRawResult> SynthesizeAsync(string text, string hash, PiperVoiceSettings options, CancellationToken cancellationToken)
        {
            if (!_memoryManager.HasVoiceModelBeenLoaded(options.ModelName))
            {
                LogConstants.PIPER_TTS_VOICE_MODEL_NOT_LOADED.Log(nameof(PiperTTSServer), options.ModelName);
                return TTSRawResult.Cancelled();
            }

            if (_memoryManager.WasVoiceModelEvicted(options.ModelName))
            {
                (bool Success, string Error) result = await _memoryManager.ReloadModelAsync(options.ModelName, cancellationToken);
                if (!result.Success)
                {
                    return TTSRawResult.Cancelled();
                }
            }
            else
            {
                _memoryManager.UpdateLastUse(options.ModelName);
            }


            cancellationToken.ThrowIfCancellationRequested();

            int port = _port;
            TcpClient client = null;

            try
            {
                client = new TcpClient();

                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        client.Close();
                    }
                    catch (Exception ex)
                    {
                        LogConstants.CODE_GENERIC_CATCH.Log(nameof(PiperTTSServer), nameof(SynthesizeAsync), ex);
                    }
                    _ = SendCancelAsync(hash, port);
                }))
                {
                    await ConnectAsync(client, port, cancellationToken).ConfigureAwait(false);
                    client.NoDelay = true;
                    NetworkStream stream = client.GetStream();

                    string requestJson = BuildSynthesizeRequest(options.ModelName, text, hash, options);
                    byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken).ConfigureAwait(false);

                    (string line, byte[] leftover) = await ReadLineWithLeftoverAsync(stream, cancellationToken).ConfigureAwait(false);
                    Dictionary<string, object> response = JSONHelper.ParseFlatObject(line);

                    string status = response.TryGetValue("status", out object statusVal) ? statusVal as string : null;

                    switch (status)
                    {
                        case "ok":
                            int sampleRate = response.TryGetValue("sample_rate", out object srVal) && srVal != null ? Convert.ToInt32(srVal, CultureInfo.InvariantCulture) : 22050;
                            using (MemoryStream ms = new MemoryStream())
                            {
                                if (leftover.Length > 0)
                                {
                                    ms.Write(leftover, 0, leftover.Length);
                                }

                                await stream.CopyToAsync(ms, 81920, cancellationToken).ConfigureAwait(false);
                                return TTSRawResult.Ok(ms.ToArray(), sampleRate);
                            }
                        case "cancelled":
                            return TTSRawResult.Cancelled();
                        case "error":
                            string message = response.TryGetValue("message", out object msgVal) ? msgVal as string : "unknown error";
                            return TTSRawResult.Failure(message);
                        default:
                            return TTSRawResult.Failure($"unexpected response status: '{status}'");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return TTSRawResult.Cancelled();
            }
            catch (ObjectDisposedException)
            {
                return cancellationToken.IsCancellationRequested ? TTSRawResult.Cancelled() : TTSRawResult.Failure("connection closed unexpectedly");
            }
            catch (IOException ex)
            {
                return cancellationToken.IsCancellationRequested ? TTSRawResult.Cancelled() : TTSRawResult.Failure(ex.Message);
            }
            catch (SocketException ex)
            {
                return TTSRawResult.Failure($"socket error: {ex.Message}");
            }
            finally
            {
                client?.Close();
            }
        }

        private static string BuildSynthesizeRequest(string modelPath, string text, string hash, PiperVoiceSettings options)
        {
            StringBuilder sb = new StringBuilder(text.Length + 128);
            sb.Append("{\"command\":\"synthesize\"");
            sb.Append(",\"model\":\"").Append(JSONHelper.Escape(modelPath)).Append('"');
            sb.Append(",\"text\":\"").Append(JSONHelper.Escape(text)).Append('"');
            sb.Append(",\"hash\":\"").Append(JSONHelper.Escape(hash)).Append('"');

            sb.Append(",\"length_scale\":").Append((1.0f / options.SpeechRate).ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(",\"noise_scale\":").Append(options.NoiseScale.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(",\"noise_w\":").Append(options.NoiseScaleW.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append("}\n");
            return sb.ToString();
        }

        private async Task SendCancelAsync(string hash, int port)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return;
            }

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    Task connectTask = client.ConnectAsync(IPAddress.Loopback, port);
                    if (await Task.WhenAny(connectTask, Task.Delay(1000)).ConfigureAwait(false) != connectTask)
                    {
                        return;
                    }

                    await connectTask.ConfigureAwait(false);
                    client.NoDelay = true;

                    string json = "{\"command\":\"cancel\",\"hash\":\"" + JSONHelper.Escape(hash) + "\"}\n";
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    NetworkStream stream = client.GetStream();
                    await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                }
            }
            catch
            {
                LogConstants.CODE_GENERIC_CATCH.Log(nameof(PiperTTSServer), "SendCancelAsync");
            }
        }

        private static async Task ConnectAsync(TcpClient client, int port, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() =>
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    LogConstants.CODE_GENERIC_CATCH.Log(nameof(PiperTTSServer), "ConnectAsync");
                }
            }))
            {
                await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            }
        }

        private static async Task<(string Line, byte[] Leftover)> ReadLineWithLeftoverAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    int newlineIndex = Array.IndexOf(buffer, (byte)'\n', 0, read);
                    if (newlineIndex >= 0)
                    {
                        ms.Write(buffer, 0, newlineIndex);

                        int leftoverLength = read - newlineIndex - 1;
                        byte[] leftover = Array.Empty<byte>();
                        if (leftoverLength > 0)
                        {
                            leftover = new byte[leftoverLength];
                            Array.Copy(buffer, newlineIndex + 1, leftover, 0, leftoverLength);
                        }
                        return (Encoding.UTF8.GetString(ms.ToArray()), leftover);
                    }
                    ms.Write(buffer, 0, read);
                }
                return (Encoding.UTF8.GetString(ms.ToArray()), Array.Empty<byte>());
            }
        }

        private static async Task DrainStreamAsync(StreamReader reader, Action<string> onLine)
        {
            try
            {
                while (true)
                {
                    string line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                    {
                        break;
                    }

                    onLine?.Invoke(line);
                }
            }
            catch
            {
                LogConstants.CODE_GENERIC_CATCH.Log(nameof(PiperTTSServer), "DrainStreamAsync");
            }
        }

        private static void TryKill(Process process)
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
                LogConstants.CODE_GENERIC_CATCH.Log(nameof(PiperTTSServer), "TryKill");
            }
        }

        private static int SafeExitCode(Process process)
        {
            try
            {
                return process.HasExited ? process.ExitCode : -1;
            }
            catch
            {
                LogConstants.CODE_GENERIC_CATCH.Log(nameof(PiperTTSServer), "SafeExitCode");
                return -1;
            }
        }
    }
}