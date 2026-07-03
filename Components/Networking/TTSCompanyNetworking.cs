using LethalNetworkAPI;
using LethalNetworkAPI.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Helpers;
using TTS_Company.Components.Managers;
using TTS_Company.Components.Networking.Components;
using TTS_Company.Components.Networking.Components.Structs;
using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Networking
{
    internal static class TTSCompanyNetworking
    {
        // networking id's
        private const string message_PREFIX = ".msg_";

        private const string messageId_SpawnTTSAudioSource_Server = ModInfo.modGUID + message_PREFIX + "SpawnTTSAudioSource_Server";
        private const string messageId_SpawnTTSAudioSource_Clients = ModInfo.modGUID + message_PREFIX + "SpawnTTSAudioSource_Clients";

        private const string messageId_DespawnTTSAudioSource_Server = ModInfo.modGUID + message_PREFIX + "DespawnTTSAudioSource_Server";
        private const string messageId_DespawnTTSAudioSource_Clients = ModInfo.modGUID + message_PREFIX + "DespawnTTSAudioSource_Clients";

        private const string messageId_SpeakTTS_Clients = ModInfo.modGUID + message_PREFIX + "SpeakTTS_Clients";
        private const string messageId_SpeakTTS_Server = ModInfo.modGUID + message_PREFIX + "SpeakTTS_Server";
        private const string messageId_SentenceProgress = ModInfo.modGUID + message_PREFIX + "SentenceProgress";

        private const string messageId_PlaySpeakTTS = ModInfo.modGUID + message_PREFIX + "PlaySpeakTTS";
        private const string messageId_CancelSpeakTTS = ModInfo.modGUID + message_PREFIX + "CancelSpeakTTS";

        // networking messages
        private static LNetworkMessage<SpawnTTSAudioSource_NET> TTS_networkMessage_SpawnTTSAudioSource_Server;
        private static LNetworkMessage<SpawnTTSAudioSource_NET> TTS_networkMessage_SpawnTTSAudioSource_Clients;

        private static LNetworkMessage<DespawnTTSAudioSource_NET> TTS_networkMessage_DespawnTTSAudioSource_Server;
        private static LNetworkMessage<DespawnTTSAudioSource_NET> TTS_networkMessage_DespawnTTSAudioSource_Clients;

        private static LNetworkMessage<TTSSpeakTTS_PLUS_NET> TTS_networkMessage_SpeakTTS_Clients;
        private static LNetworkMessage<TTSSpeakTTS_NET> TTS_networkMessage_SpeakTTS_Server;
        private static LNetworkMessage<SentenceProgressData_NET> TTS_networkMessage_SentenceProgress;

        private static LNetworkMessage<PlayAudioTTS_NET> TTS_networkMessage_PlaySpeakTTS;
        private static LNetworkMessage<CancelAudioTTS_NET> TTS_networkMessage_CancelSpeakTTS;

        // host/server bookkeeping
        private static readonly ConcurrentDictionary<ulong, TTSTask> ActiveTasks_Server = new ConcurrentDictionary<ulong, TTSTask>();
        private static ulong _nextSessionId_Speak = 0;

        // client bookkeeping for cached audioclips
        private static readonly ConcurrentDictionary<ulong, ClientTaskState> ClientTasks = new ConcurrentDictionary<ulong, ClientTaskState>();

        internal static void Initialize()
        {
            // initialize networking messages
            TTS_networkMessage_SpawnTTSAudioSource_Server = LNetworkMessage<SpawnTTSAudioSource_NET>.Connect(messageId_SpawnTTSAudioSource_Server, onServerReceived: SpawnTTSAudioSource);
            TTS_networkMessage_SpawnTTSAudioSource_Clients = LNetworkMessage<SpawnTTSAudioSource_NET>.Connect(messageId_SpawnTTSAudioSource_Clients, onClientReceived: TTSAudioSourceManager.AddPermanentTTSAudioSource);

            TTS_networkMessage_DespawnTTSAudioSource_Server = LNetworkMessage<DespawnTTSAudioSource_NET>.Connect(messageId_DespawnTTSAudioSource_Server, onServerReceived: DespawnTTSAudioSource);
            TTS_networkMessage_DespawnTTSAudioSource_Clients = LNetworkMessage<DespawnTTSAudioSource_NET>.Connect(messageId_DespawnTTSAudioSource_Clients, onClientReceived: TTSAudioSourceManager.RemovePermanentTTSAudioSource);

            TTS_networkMessage_SpeakTTS_Clients = LNetworkMessage<TTSSpeakTTS_PLUS_NET>.Connect(messageId_SpeakTTS_Clients, onClientReceived: TTSCompanyBackend.SpeakTTSAtNetworkObject_OnClient);
            TTS_networkMessage_SpeakTTS_Server = LNetworkMessage<TTSSpeakTTS_NET>.Connect(messageId_SpeakTTS_Server, onServerReceived: StartActiveTask);
            TTS_networkMessage_SentenceProgress = LNetworkMessage<SentenceProgressData_NET>.Connect(messageId_SentenceProgress, onServerReceived: UpdateActiveTask);

            TTS_networkMessage_PlaySpeakTTS = LNetworkMessage<PlayAudioTTS_NET>.Connect(messageId_PlaySpeakTTS, onClientReceived: PlayTTS);
            TTS_networkMessage_CancelSpeakTTS = LNetworkMessage<CancelAudioTTS_NET>.Connect(messageId_CancelSpeakTTS, onClientReceived: TTSCompanyPlugin._ttsPlaybackManagerObject.CancelPlayback);
        }

        // server only
        private static void SpawnTTSAudioSource(SpawnTTSAudioSource_NET data, ulong recievedFromPlayer)
        {
            if (!LNetworkUtils.IsHostOrServer)
            {
                return;
            }

            try
            {
                TTS_networkMessage_SpawnTTSAudioSource_Clients.SendClients(data);
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSCompanyNetworking), nameof(SpawnTTSAudioSource), ex);
            }
        }

        private static void DespawnTTSAudioSource(DespawnTTSAudioSource_NET data, ulong recievedFromPlayer)
        {
            if (!LNetworkUtils.IsHostOrServer)
            {
                return;
            }

            try
            {
                TTS_networkMessage_DespawnTTSAudioSource_Clients.SendClients(data);
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSCompanyNetworking), nameof(DespawnTTSAudioSource), ex);
            }
        }

        // server only
        private static void UpdateActiveTask(SentenceProgressData_NET data, ulong recievedFromPlayer)
        {
            if (!LNetworkUtils.IsHostOrServer)
            {
                return;
            }

            LogConstants.TTS_COMPANY_NETWORKING_UPDATE_TASK.Log(nameof(TTSCompanyNetworking), recievedFromPlayer, data._sessionId);
            if (ActiveTasks_Server.TryGetValue(data._sessionId, out TTSTask session))
            {
                if (!data._success)
                {
                    CancelAnyExistingSessionFor(session._speakingObject, session._callingAssemblyHash, "client failed generation");
                    return;
                }

                if (session._completionList.TryGetValue(recievedFromPlayer, out bool[] result))
                {
                    result[data._textIndex] = data._success;
                    CheckForFinishedTask(session);
                }
            }
        }

        // server only, called by UpdateActiveTask()
        private static void CheckForFinishedTask(TTSTask task)
        {
            int minCompleted = int.MaxValue;

            foreach (KeyValuePair<ulong, bool> client in task._snapshotClientIds)
            {
                if (!LNetworkUtils.AllConnectedClients.Contains(client.Key))
                {
                    continue;
                }

                if (task._completionList.TryGetValue(client.Key, out bool[] completionArray))
                {
                    int completedForClient = 0;
                    while (completedForClient < completionArray.Length && completionArray[completedForClient])
                    {
                        completedForClient++;
                    }

                    if (completedForClient < minCompleted)
                    {
                        minCompleted = completedForClient;
                    }
                }
            }

            if (minCompleted == int.MaxValue || minCompleted <= task._lastStartSpeakingIndex)
            {
                return; // no new TTS that is ready yet
            }

            int newlyReady = minCompleted - task._lastStartSpeakingIndex;
            bool isFinalRelease = minCompleted >= task._textsToSpeak.Length;

            if (newlyReady < task._startSpeakingAtAmountOfFinishedTasks && !isFinalRelease)
            {
                return;
            }

            int startIndex = task._lastStartSpeakingIndex;
            int endIndex = minCompleted - 1;

            try
            {
                TTS_networkMessage_PlaySpeakTTS.SendClients(new PlayAudioTTS_NET(task._taskId, startIndex, endIndex, isFinalRelease));
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSCompanyNetworking), "TTS_networkMessage_PlaySpeakTTS.SendClients", ex);
            }

            task._lastStartSpeakingIndex = minCompleted;

            if (isFinalRelease)
            {
                task._cts?.Dispose();
                task._cts = null;
                StartServerPlaybackCleanupTimeout(task);
            }
        }

        private static void StartServerPlaybackCleanupTimeout(TTSTask task)
        {
            if (!LNetworkUtils.IsHostOrServer)
            {
                return;
            }

            TimeSpan playbackTimeout = TTSTimeoutHelper.GetPlaybackTimeout(task._textsToSpeak, task._voiceSettings);
            Task.Delay(playbackTimeout).ContinueWith(_ =>
            {
                if (ActiveTasks_Server.TryRemove(task._taskId, out TTSTask _))
                {
                    LogConstants.TTS_COMPANY_NETWORKING_PLAYBACK_CLEANUP.Log(nameof(TTSCompanyNetworking), task._taskId);
                }
            });
        }

        // server only
        private static void StartActiveTask(TTSSpeakTTS_NET data, ulong recievedFromPlayer)
        {
            if (!LNetworkUtils.IsHostOrServer)
            {
                return;
            }

            // if a session already targets the same NetworkObject+callingAssembly, cancel it
            CancelAnyExistingSessionFor(data._networkObjectRefOfSpeaker, data._callingAssemblyHash, "Superseded by new session");

            _nextSessionId_Speak += 1;
            ulong currentSessionId = _nextSessionId_Speak;

            TTSTask session = new TTSTask(LNetworkUtils.AllConnectedClients, data._textsToSpeak.Length)
            {
                _taskId = currentSessionId,
                _speakingObject = data._networkObjectRefOfSpeaker,
                _textsToSpeak = data._textsToSpeak,
                _callingAssemblyHash = data._callingAssemblyHash,
                _voiceSettings = data._voiceSettings,
                _textsWaited = 0,
                _cancelled = false,
                _cts = new CancellationTokenSource()
            };

            TimeSpan timeout = TTSTimeoutHelper.GetGenerationTimeout(data._textsToSpeak, data._voiceSettings);
            session._cts.Token.Register(() =>
            {
                HostCancelSession(currentSessionId, "Timed out");
            });
            session._cts.CancelAfter(timeout);

            ActiveTasks_Server.TryAdd(currentSessionId, session);

            TTS_networkMessage_SpeakTTS_Clients.SendClients(new TTSSpeakTTS_PLUS_NET(data, currentSessionId));
        }

        // server only, called by StartActiveTask() & UpdateActiveTask()
        private static void CancelAnyExistingSessionFor(NetworkObjectReference target, ulong callingAssemblyHash, string reason)
        {
            TTSTask existing = ActiveTasks_Server.Values.FirstOrDefault(s => !s._cancelled && s._callingAssemblyHash == callingAssemblyHash && s._speakingObject.NetworkObjectId == target.NetworkObjectId);

            if (existing != null)
            {
                HostCancelSession(existing._taskId, reason);
            }
        }

        // server only, called by CancelAnyExistingSessionFor()
        private static void HostCancelSession(ulong sessionId, string reason)
        {
            if (!ActiveTasks_Server.TryGetValue(sessionId, out TTSTask session) || session._cancelled)
            {
                return;
            }

            session._cancelled = true;
            ActiveTasks_Server.TryRemove(sessionId, out _);
            session._cts?.Dispose();

            LogConstants.TTS_COMPANY_NETWORKING_TASK_CANCELLED.Log(nameof(TTSCompanyNetworking), sessionId, reason);

            try
            {
                TTS_networkMessage_CancelSpeakTTS.SendClients(new CancelAudioTTS_NET(sessionId, reason));
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSCompanyNetworking), nameof(HostCancelSession), ex);
            }
        }

        // all clients
        private static void PlayTTS(PlayAudioTTS_NET playData)
        {
            if (!ClientTasks.TryGetValue(playData._taskId, out ClientTaskState taskValue))
            {
                return;
            }

            AudioClip[] clips = new AudioClip[(playData._endIndex - playData._startIndex) + 1];
            float totalPlaybackDuration = 0f;

            for (int i = playData._startIndex; i <= playData._endIndex; i++)
            {
                AudioClip clip = taskValue._generatedClips[i];
                clips[i - playData._startIndex] = clip;
                if (clip != null)
                {
                    totalPlaybackDuration += clip.length;
                }
                taskValue._generatedClips[i] = null;
            }

            TTSCompanyBackend.PlaySpeakTTSAtNetworkObject_OnClient(playData._taskId, taskValue._networkObjectReference, taskValue._callingAssemblyHash, clips, playData._isLastBatch);

            if (playData._endIndex >= taskValue._generatedClips.Length - 1)
            {
                ClientTasks.TryRemove(playData._taskId, out _);
            }
        }

        // -------------------- requests --------------------
        internal static void Request_Server_SpawnTTSSource(SpawnTTSAudioSource_NET data)
        {
            try
            {
                TTS_networkMessage_SpawnTTSAudioSource_Server.SendServer(data);
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSCompanyNetworking), nameof(Request_Server_SpawnTTSSource), ex);
            }
        }

        internal static void Request_Server_DespawnTTSSource(DespawnTTSAudioSource_NET data)
        {
            try
            {
                TTS_networkMessage_DespawnTTSAudioSource_Server.SendServer(data);
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSCompanyNetworking), nameof(Request_Server_DespawnTTSSource), ex);
            }
        }

        internal static void Request_Server_SpeakTTS(TTSSpeakTTS_NET data)
        {
            try
            {
                TTS_networkMessage_SpeakTTS_Server.SendServer(data);
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSCompanyNetworking), nameof(Request_Server_SpeakTTS), ex);
            }
        }

        internal static void Request_Server_UpdateSentenceProgress(SentenceProgressData_NET data)
        {
            try
            {
                TTS_networkMessage_SentenceProgress.SendServer(data);
            }
            catch (Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSCompanyNetworking), nameof(Request_Server_UpdateSentenceProgress), ex);
            }
        }

        // -------------------- client calls --------------------
        internal static void CreateClientTask(ulong taskId, NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash, string[] textsToSpeak, CancellationTokenSource cts)
        {
            if (ClientTasks.TryRemove(taskId, out ClientTaskState oldState))
            {
                CtsHelper.SafeCancel(oldState._cts);
                oldState._cts?.Dispose();
            }

            ClientTaskState task = new ClientTaskState
            {
                _sentences = textsToSpeak,
                _generatedClips = new AudioClip[textsToSpeak.Length],
                _callingAssemblyHash = callingAssemblyHash,
                _cts = cts,
                _networkObjectReference = networkObjectRefOfSpeaker
            };

            ClientTasks.TryAdd(taskId, task);
        }

        internal static void UpdateClientTask(ulong taskId, int textIndex, AudioClip audioClip)
        {
            if (ClientTasks.TryGetValue(taskId, out ClientTaskState taskValue))
            {
                if (taskValue._generatedClips[textIndex] == null)
                {
                    taskValue._generatedClips[textIndex] = audioClip;
                }
            }
        }

        internal static void CancelClientTask(ulong taskId)
        {
            if (ClientTasks.TryRemove(taskId, out ClientTaskState oldState))
            {
                CtsHelper.SafeCancel(oldState._cts);
                oldState._cts?.Dispose();

                if (oldState._generatedClips != null)
                {
                    Array.Clear(oldState._generatedClips, 0, oldState._generatedClips.Length);
                }
            }
        }
    }
}
