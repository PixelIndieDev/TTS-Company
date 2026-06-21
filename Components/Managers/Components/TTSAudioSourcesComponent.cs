using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace TTS_Company.Components.Managers.Components
{
    internal sealed class TTSAudioSourcesComponent : MonoBehaviour
    {
        private readonly ConcurrentDictionary<string, AudioSource> audioSources = new ConcurrentDictionary<string, AudioSource>(StringComparer.Ordinal);

        internal bool AddAudioSource(string audioSourceIdentifier)
        {
            if (!DoesAudioSourceExist(audioSourceIdentifier))
            {
                AudioSource audioSource = gameObject.AddComponent<AudioSource>();
                return audioSources.TryAdd(audioSourceIdentifier, audioSource);
            }
            return false;
        }

        internal bool DoesAudioSourceExist(string audioSourceIdentifier)
        {
            return audioSources.ContainsKey(audioSourceIdentifier);
        }

        internal bool GetAudioSource(string audioSourceIdentifier, out AudioSource source) => audioSources.TryGetValue(audioSourceIdentifier, out source);

        internal bool PlayAudioClip(string audioSourceIdentifier, AudioClip newAudioClip)
        {
            if (newAudioClip == null)
            {
                return false;
            }

            GetAudioSource(audioSourceIdentifier, out AudioSource audioSource);
            if (audioSource == null)
            {
                return false;
            }

            audioSource.clip = newAudioClip;

            audioSource.Play();
            return true;
        }

        internal bool UpdateAudioSourceSettings(string audioSourceIdentifier, TTSAudioSourceSettings audioSourceSettings = default)
        {
            GetAudioSource(audioSourceIdentifier, out AudioSource audioSource);
            if (audioSource == null) return false;

            audioSource.spatialize = false;
            audioSource.spatializePostEffects = false;

            audioSource.bypassEffects = audioSourceSettings._bypassEffects;
            audioSource.bypassListenerEffects = audioSourceSettings._bypassListenerEffects;
            audioSource.bypassReverbZones = audioSourceSettings._bypassReverbZones;

            audioSource.playOnAwake = false; // call play() manually, so not play on awake

            audioSource.loop = false;
            audioSource.priority = audioSourceSettings._priority;
            audioSource.volume = audioSourceSettings._volume;

            audioSource.spatialBlend = audioSourceSettings._spatialBlend;
            audioSource.reverbZoneMix = audioSourceSettings._reverbZoneMix;

            audioSource.dopplerLevel = audioSourceSettings._dopplerLevel;
            audioSource.minDistance = audioSourceSettings._minDistance;
            audioSource.maxDistance = audioSourceSettings._maxDistance;
            audioSource.rolloffMode = audioSourceSettings._rolloffMode;

            return true;
        }

        internal bool RemoveAudioSource(string audioSourceIdentifier)
        {
            if (audioSources.TryRemove(audioSourceIdentifier, out var source))
            {
                Destroy(source);
                return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            audioSources.Clear();
        }
    }
}
