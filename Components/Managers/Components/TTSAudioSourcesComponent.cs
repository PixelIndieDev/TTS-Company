using System.Collections.Concurrent;
using TTS_Company.Components.Constants;
using UnityEngine;

namespace TTS_Company.Components.Managers.Components
{
    internal sealed class TTSAudioSourcesComponent : MonoBehaviour
    {
        private readonly ConcurrentDictionary<ulong, AudioSource> audioSources = new ConcurrentDictionary<ulong, AudioSource>();

        internal bool AddAudioSource(ulong callingAssemblyHash)
        {
            if (!DoesAudioSourceExist(callingAssemblyHash))
            {
                AudioSource audioSource = gameObject.AddComponent<AudioSource>();
                return audioSources.TryAdd(callingAssemblyHash, audioSource);
            }
            return false;
        }

        internal bool DoesAudioSourceExist(ulong callingAssemblyHash)
        {
            return audioSources.ContainsKey(callingAssemblyHash);
        }

        internal bool GetAudioSource(ulong callingAssemblyHash, out AudioSource source) => audioSources.TryGetValue(callingAssemblyHash, out source);

        internal bool PlayAudioClip(ulong callingAssemblyHash, AudioClip newAudioClip)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSAudioSourcesComponent), nameof(PlayAudioClip));

            if (newAudioClip == null)
            {
                return false;
            }

            if (!GetAudioSource(callingAssemblyHash, out AudioSource audioSource))
            {
                return false;
            }

            audioSource.clip = newAudioClip;
            audioSource.Play();
            return true;
        }

        internal bool StopAudioClip(ulong callingAssemblyHash)
        {
            if (!GetAudioSource(callingAssemblyHash, out AudioSource audioSource))
            {
                return false;
            }

            audioSource.Stop();
            audioSource.clip = null;
            return true;
        }

        internal bool UpdateAudioSourceSettings(ulong callingAssemblyHash, TTSAudioSourceSettings audioSourceSettings)
        {
            if (!GetAudioSource(callingAssemblyHash, out AudioSource audioSource))
            {
                return false;
            }

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

            audioSource.outputAudioMixerGroup = audioSourceSettings._outputAudioMixerGroup;
            audioSource.mute = audioSourceSettings._mute;

            if (audioSourceSettings._customCurve != null)
            {
                (AudioSourceCurveType type, AnimationCurve curve) curveValue = audioSourceSettings._customCurve.Value;
                audioSource.SetCustomCurve(curveValue.type, curveValue.curve);
            }

            return true;
        }

        internal bool RemoveAudioSource(ulong callingAssemblyHash)
        {
            if (audioSources.TryRemove(callingAssemblyHash, out AudioSource source))
            {
                Destroy(source);
                return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            foreach (AudioSource source in audioSources.Values)
            {
                if (source != null)
                {
                    Destroy(source);
                }
            }
            audioSources.Clear();
        }
    }
}
