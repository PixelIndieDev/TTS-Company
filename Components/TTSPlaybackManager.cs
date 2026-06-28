using System.Collections;
using System.Collections.Generic;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Managers;
using TTS_Company.Components.Networking.Components.Structs;
using UnityEngine;

namespace TTS_Company.Components
{
    internal sealed class TTSPlaybackManager : MonoBehaviour
    {
        private readonly Dictionary<ulong, Coroutine> _activeCoroutines = new Dictionary<ulong, Coroutine>();

        private void Update()
        {
            foreach (ulong speakerHash in TTSCompanyBackend.WantedAudioClips.Keys)
            {
                if (!_activeCoroutines.ContainsKey(speakerHash))
                {
                    Coroutine routine = StartCoroutine(ProcessAudioQueue(speakerHash));
                    _activeCoroutines.Add(speakerHash, routine);
                }
            }
        }

        private IEnumerator ProcessAudioQueue(ulong speakerHash)
        {
            while (TTSCompanyBackend.WantedAudioClips.TryGetValue(speakerHash, out SpeakTTSAudioClipCache cache))
            {
                if (cache._foundNetworkObject == null)
                {
                    break;
                }

                if (cache._audioQueue.TryDequeue(out AudioClip nextClip))
                {
                    if (TTSAudioSourceManager.PlayAudioSource(cache._foundNetworkObject, cache._callingAssemblyHash, nextClip))
                    {
                        yield return new WaitForSeconds(nextClip.length);
                    }
                    else
                    {
                        yield return null;
                    }
                }
                else
                {
                    yield return null;
                }
            }

            TTSCompanyBackend.WantedAudioClips.TryRemove(speakerHash, out _);
            _activeCoroutines.Remove(speakerHash);
        }

        internal void CancelPlayback(CancelAudioTTS_NET data)
        {
            LogConstants.logSource.LogWarning($"CancelPlayback for {data._taskId}");
            if (TTSCompanyBackend.WantedAudioClips.TryRemove(data._taskId, out SpeakTTSAudioClipCache cache))
            {
                while (cache._audioQueue.TryDequeue(out _)) { }
            }

            if (_activeCoroutines.TryGetValue(data._taskId, out Coroutine runningCoroutine))
            {
                if (runningCoroutine != null)
                {
                    StopCoroutine(runningCoroutine);
                }
                _activeCoroutines.Remove(data._taskId);
            }
        }
    }
}
