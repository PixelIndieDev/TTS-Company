using System.Collections;
using System.Collections.Generic;
using TTSCompany.Components.Managers;
using TTSCompany.Components.Networking;
using TTSCompany.Components.Networking.Components.Structs;
using Unity.Netcode;
using UnityEngine;

namespace TTSCompany.Components
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

                if (cache._audioQueue.TryDequeue(out QueuedClip queued))
                {
                    if (TTSAudioSourceManager.PlayAudioSource(cache._foundNetworkObject, cache._callingAssemblyHash, queued.Clip))
                    {
                        yield return new WaitForSeconds(queued.Clip.length);

                        bool wasFinalClip = cache._isLastBatch && cache._audioQueue.IsEmpty;
                        if (!wasFinalClip && queued.PauseAfter > 0f)
                        {
                            yield return new WaitForSeconds(queued.PauseAfter);
                        }
                    }
                    else
                    {
                        yield return null;
                    }
                }
                else if (cache._isLastBatch)
                {
                    break;
                }
                else
                {
                    yield return null;
                }
            }

            TTSCompanyBackend.WantedAudioClips.TryRemove(speakerHash, out SpeakTTSAudioClipCache removedCache);
            _activeCoroutines.Remove(speakerHash);
        }

        internal void CancelPlayback(CancelAudioTTS_NET data)
        {
            TTSCompanyNetworking.CancelClientTask(data._taskId);
            if (TTSCompanyBackend.WantedAudioClips.TryRemove(data._taskId, out SpeakTTSAudioClipCache cache))
            {
                while (cache._audioQueue.TryDequeue(out QueuedClip queued))
                {
                    if (queued.Clip != null)
                    {
                        Destroy(queued.Clip);
                    }
                }

                if (cache._foundNetworkObject != null)
                {
                    TTSAudioSourceManager.StopAudioSource(cache._foundNetworkObject, cache._callingAssemblyHash);

                    if (cache._foundNetworkObject.TryGetComponent(out NetworkObject netObj))
                    {
                        TTSCompanyBackend.SpeakingNetworkObjectIds.TryRemove(netObj.NetworkObjectId, out _);
                    }
                }
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
