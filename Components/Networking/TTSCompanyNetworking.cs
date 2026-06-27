using LethalNetworkAPI;
using LethalNetworkAPI.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        private const string messageId_SpeakTTS_Clients = ModInfo.modGUID + message_PREFIX + "SpeakTTS_Clients";
        private const string messageId_SpeakTTS_Server = ModInfo.modGUID + message_PREFIX + "SpeakTTS_Server";
        private const string messageId_SentenceProgress = ModInfo.modGUID + message_PREFIX + "SentenceProgress";

        private const string messageId_PlaySpeakTTS = ModInfo.modGUID + message_PREFIX + "PlaySpeakTTS";
        private const string messageId_CancelSpeakTTS = ModInfo.modGUID + message_PREFIX + "CancelSpeakTTS";

        // networking messages
        private static LNetworkMessage<SpawnTTSAudioSource_NET> TTS_networkMessage_SpawnTTSAudioSource_Server;
        private static LNetworkMessage<SpawnTTSAudioSource_NET> TTS_networkMessage_SpawnTTSAudioSource_Clients;

        private static LNetworkMessage<TTSSpeakTTS_PLUS_NET> TTS_networkMessage_SpeakTTS_Clients;
        private static LNetworkMessage<TTSSpeakTTS_NET> TTS_networkMessage_SpeakTTS_Server;
        private static LNetworkMessage<SentenceProgressData_NET> TTS_networkMessage_SentenceProgress;

        private static LNetworkMessage<PlayAudioTTS_NET> TTS_networkMessage_PlaySpeakTTS;
        private static LNetworkMessage<CancelAudioTTS_NET> TTS_networkMessage_CancelSpeakTTS;

        // host/server bookkeeping
        private static readonly ConcurrentDictionary<ulong, TTSTask_Speak> ActiveTasks_Speak = new ConcurrentDictionary<ulong, TTSTask_Speak>();
        private static ulong _nextSessionId_Speak = 0;

        // client bookkeeping for cached audioclips
        private static readonly ConcurrentDictionary<ulong, ClientTaskState> ClientTasks = new ConcurrentDictionary<ulong, ClientTaskState>();

        internal static void Initialize()
        {
            // initialize networking messages
            TTS_networkMessage_SpawnTTSAudioSource_Server = LNetworkMessage<SpawnTTSAudioSource_NET>.Connect(messageId_SpawnTTSAudioSource_Server, onServerReceived: SpawnTTSAudioSource);
            TTS_networkMessage_SpawnTTSAudioSource_Clients = LNetworkMessage<SpawnTTSAudioSource_NET>.Connect(messageId_SpawnTTSAudioSource_Clients, onClientReceived: TTSAudioSourceManager.AddPermanentTTSAudioSource);

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

            TTS_networkMessage_SpawnTTSAudioSource_Clients.SendClients(data);
        }

        // server only
        private static void UpdateActiveTask(SentenceProgressData_NET data, ulong recievedFromPlayer)
        {
            if (!LNetworkUtils.IsHostOrServer)
            {
                return;
            }

            if (ActiveTasks_Speak.TryGetValue(data._sessionId, out TTSTask_Speak session))
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
        private static void CheckForFinishedTask(TTSTask_Speak task)
        {
            foreach (KeyValuePair<ulong, bool> client in task._snapshotClientIds)
            {
                if (!LNetworkUtils.AllConnectedClients.Contains(client.Key))
                {
                    continue;
                }

                if (task._completionList.TryGetValue(client.Key, out bool[] completionArray))
                {
                    if (task._lastStartSpeakingIndex < completionArray.Length)
                    {
                        if (!completionArray[task._lastStartSpeakingIndex])
                        {
                            return; // someone is still generating, do nothing
                        }
                    }
                }
            }

            // no one was generating
            // now check if it should start playing
            if (task._textsWaited >= task._startSpeakingAtAmountOfFinishedTasks)
            {
                TTS_networkMessage_PlaySpeakTTS.SendClients(new PlayAudioTTS_NET(task._taskId, task._lastStartSpeakingIndex, task._textsWaited));
                task._lastStartSpeakingIndex += task._textsWaited - task._lastStartSpeakingIndex;
            }
            task._textsWaited += 1;
        }

        // server only
        private static void StartActiveTask(TTSSpeakTTS_NET data, ulong recievedFromPlayer)
        {
            if (!LNetworkUtils.IsHostOrServer)
            {
                return;
            }

            _nextSessionId_Speak += 1;

            TTSTask_Speak session = new TTSTask_Speak(LNetworkUtils.AllConnectedClients, data._textsToSpeak.Length)
            {
                _taskId = _nextSessionId_Speak,
                _speakingObject = data._networkObjectRefOfSpeaker,
                _textsToSpeak = data._textsToSpeak,
                _callingAssemblyHash = data._callingAssemblyHash,
                _voiceSettings = data._voiceSettings,
                _textsWaited = 0,
                _cancelled = false,
                _cts = new CancellationTokenSource()
            };

            // if a session already targets the same NetworkObject+callingAssembly, cancel it
            CancelAnyExistingSessionFor(data._networkObjectRefOfSpeaker, data._callingAssemblyHash, "Superseded by new session");
            ActiveTasks_Speak[_nextSessionId_Speak] = session;

            TimeSpan timeout = TTSTimeoutHelper.GetTTSTimeout(data._textsToSpeak, data._voiceSettings);
            session._cts.Token.Register(() =>
            {
                HostCancelSession(_nextSessionId_Speak, "Timed out");
            });
            session._cts.CancelAfter(timeout);

            TTS_networkMessage_SpeakTTS_Clients.SendClients(new TTSSpeakTTS_PLUS_NET(data, _nextSessionId_Speak));
        }

        // server only, called by StartActiveTask() & UpdateActiveTask()
        private static void CancelAnyExistingSessionFor(NetworkObjectReference target, ulong callingAssemblyHash, string reason)
        {
            TTSTask_Speak existing = ActiveTasks_Speak.Values.FirstOrDefault(s => !s._cancelled && s._callingAssemblyHash == callingAssemblyHash && s._speakingObject.NetworkObjectId == target.NetworkObjectId);

            if (existing != null)
            {
                HostCancelSession(existing._taskId, reason);
            }
        }

        // server only, called by CancelAnyExistingSessionFor()
        private static void HostCancelSession(ulong sessionId, string reason)
        {
            if (!ActiveTasks_Speak.TryGetValue(sessionId, out TTSTask_Speak session) || session._cancelled)
            {
                return;
            }

            session._cancelled = true;
            ActiveTasks_Speak.TryRemove(sessionId, out _);
            session._cts?.Dispose();

            LogConstants.TTS_COMPANY_NETWORKING_TASK_CANCELLED.Log(nameof(TTSCompanyNetworking), sessionId, reason);
            TTS_networkMessage_CancelSpeakTTS.SendClients(new CancelAudioTTS_NET(sessionId, reason));
        }

        // all clients
        private static void PlayTTS(PlayAudioTTS_NET playData)
        {
            if (ClientTasks.TryGetValue(playData._taskId, out ClientTaskState taskValue))
            {
                AudioClip[] clips = new AudioClip[(playData._endIndex - playData._startIndex) + 1];
                for (int i = playData._startIndex; i <= playData._endIndex; i++)
                {
                    clips[i] = taskValue._generatedClips[i];
                }
                TTSCompanyBackend.PlaySpeakTTSAtNetworkObject_OnClient(playData._taskId, taskValue._networkObjectReference, taskValue._callingAssemblyHash, clips);
            }
        }

        // -------------------- requests --------------------
        internal static void Request_Server_SpawnTTSSource(SpawnTTSAudioSource_NET data)
        {
            TTS_networkMessage_SpawnTTSAudioSource_Server.SendServer(data);
        }

        internal static void Request_Server_SpeakTTS(TTSSpeakTTS_NET data)
        {
            TTS_networkMessage_SpeakTTS_Server.SendServer(data);
        }

        internal static void Request_Server_UpdateSentenceProgress(SentenceProgressData_NET data)
        {
            TTS_networkMessage_SentenceProgress.SendServer(data);
        }

        // -------------------- client calls --------------------
        internal static void CreateClientTask(ulong taskId, NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash, string[] textsToSpeak, CancellationTokenSource cts)
        {
            if (ClientTasks.TryRemove(taskId, out ClientTaskState oldState))
            {
                oldState._cts?.Cancel();
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
    }
}
