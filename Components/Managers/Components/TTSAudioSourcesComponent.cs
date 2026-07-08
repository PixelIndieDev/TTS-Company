using System.Collections;
using System.Collections.Concurrent;
using TTSCompany.Components.Constants;
using UnityEngine;

namespace TTSCompany.Components.Managers.Components
{
    internal sealed class TTSAudioSourcesComponent : MonoBehaviour
    {
        private readonly ConcurrentDictionary<ulong, AudioSource> audioSources = new ConcurrentDictionary<ulong, AudioSource>();
        private readonly ConcurrentDictionary<ulong, Coroutine> noiseLoopCoroutines = new ConcurrentDictionary<ulong, Coroutine>();

        const float RmsScaler = 2.9f;
        const float LoudnessScaler = 2.0f;

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

        internal bool PlayAudioClip(ulong callingAssemblyHash, AudioClip newAudioClip, float noiseRangeMultiplier)
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

            AudioClip previousClip = audioSource.clip;
            audioSource.clip = newAudioClip;
            audioSource.Play();


            StopNoiseLoop(callingAssemblyHash);
            Coroutine loop = StartCoroutine(EmitNoise(callingAssemblyHash, audioSource, noiseRangeMultiplier));
            noiseLoopCoroutines[callingAssemblyHash] = loop;

            if (previousClip != null && previousClip != newAudioClip)
            {
                Destroy(previousClip);
            }
            return true;
        }

        private IEnumerator EmitNoise(ulong callingAssemblyHash, AudioSource audioSource, float noiseRangeMultiplier)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSAudioSourcesComponent), nameof(EmitNoise));

            if (noiseRangeMultiplier <= 0.0f)
            {
                yield break;
            }

            LogConstants.CODE_TRIGGERED.Log(nameof(TTSAudioSourcesComponent), nameof(EmitNoise) + " after");

            float[] TTSAudioBuffer = new float[512];
            float maxRmsSnapshot = 0f;
            byte loopsMade = 0;

            WaitForSeconds wait = new WaitForSeconds(0.1f);
            while (audioSource != null && audioSource.isPlaying)
            {
                // recalculate rms
                audioSource.GetOutputData(TTSAudioBuffer, 0);

                float sum = 0f;
                for (int i = 0; i < TTSAudioBuffer.Length; i++)
                {
                    sum += TTSAudioBuffer[i] * TTSAudioBuffer[i];
                }
                float rms = Mathf.Sqrt(sum / TTSAudioBuffer.Length) * RmsScaler;
                if (rms > maxRmsSnapshot)
                {
                    maxRmsSnapshot = rms;
                }

                if (loopsMade >= 2)
                {
                    loopsMade = 0;

                    float normalizedRms = Mathf.Clamp01(maxRmsSnapshot * audioSource.volume * RmsScaler);
                    if (normalizedRms > 0.01f && RoundManager.Instance != null)
                    {
                        float calculatedRange = Mathf.Lerp(audioSource.minDistance, audioSource.maxDistance, normalizedRms);
                        float calculatedLoudness = Mathf.Clamp(normalizedRms * LoudnessScaler, 0.6f, 0.9f);
                        bool noiseIsInsideClosedShip = false;

                        LogConstants.TTS_AUDIO_SOURCE_COMPONENT_NOISE_LEVEL.Log(nameof(TTSAudioSourcesComponent), calculatedRange, calculatedLoudness);
                        RoundManager.Instance.PlayAudibleNoise(transform.position, calculatedRange, calculatedLoudness, 0, noiseIsInsideClosedShip, 75);
                    }
                    maxRmsSnapshot = 0f;
                }
                else
                {
                    loopsMade += 1;
                }
                yield return wait;
            }
            noiseLoopCoroutines.TryRemove(callingAssemblyHash, out _);
        }

        private void StopNoiseLoop(ulong callingAssemblyHash)
        {
            if (noiseLoopCoroutines.TryRemove(callingAssemblyHash, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
            }
        }

        internal bool StopAudioClip(ulong callingAssemblyHash)
        {
            if (!GetAudioSource(callingAssemblyHash, out AudioSource audioSource))
            {
                return false;
            }

            StopNoiseLoop(callingAssemblyHash);
            audioSource.Stop();
            if (audioSource.clip != null)
            {
                Destroy(audioSource.clip);
            }
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

            audioSource.bypassEffects = audioSourceSettings.BypassEffects;
            audioSource.bypassListenerEffects = audioSourceSettings.BypassListenerEffects;
            audioSource.bypassReverbZones = audioSourceSettings.BypassReverbZones;

            audioSource.playOnAwake = false; // call play() manually, so not play on awake

            audioSource.loop = false;
            audioSource.priority = audioSourceSettings.Priority;
            audioSource.volume = audioSourceSettings.Volume;

            audioSource.spatialBlend = audioSourceSettings.SpatialBlend;
            audioSource.reverbZoneMix = audioSourceSettings.ReverbZoneMix;

            audioSource.dopplerLevel = audioSourceSettings.DopplerLevel;
            audioSource.minDistance = audioSourceSettings.MinDistance;
            audioSource.maxDistance = audioSourceSettings.MaxDistance;
            audioSource.rolloffMode = audioSourceSettings.RolloffMode;

            audioSource.outputAudioMixerGroup = audioSourceSettings.OutputAudioMixerGroup;

            if (audioSourceSettings.CustomCurve != null)
            {
                (AudioSourceCurveType type, AnimationCurve curve) curveValue = audioSourceSettings.CustomCurve.Value;
                audioSource.SetCustomCurve(curveValue.type, curveValue.curve);
            }

            return true;
        }

        internal bool RemoveAudioSource(ulong callingAssemblyHash)
        {
            StopNoiseLoop(callingAssemblyHash);
            if (audioSources.TryRemove(callingAssemblyHash, out AudioSource source))
            {
                Destroy(source);
                return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            foreach (Coroutine loop in noiseLoopCoroutines.Values)
            {
                if (loop != null)
                {
                    StopCoroutine(loop);
                }
            }
            noiseLoopCoroutines.Clear();

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
