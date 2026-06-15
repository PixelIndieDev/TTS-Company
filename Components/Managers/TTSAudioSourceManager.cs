using BepInEx;
using System.Runtime.CompilerServices;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Helpers;
using TTS_Company.Components.Managers.Components;
using UnityEngine;

namespace TTS_Company.Components.Managers
{
    internal static class TTSAudioSourceManager
    {
        private static readonly ConditionalWeakTable<GameObject, TTSAudioSourcesComponent> GameObjectWithTTSAudioSourcesComponent = new ConditionalWeakTable<GameObject, TTSAudioSourcesComponent>();

        private static bool DoesGameObjectHaveTTSAudioSourcesComponent(GameObject networkObject, out TTSAudioSourcesComponent audioSourceContainingGameObject)
        {
            if (networkObject == null)
            {
                audioSourceContainingGameObject = null;
                return false;
            }

            return GameObjectWithTTSAudioSourcesComponent.TryGetValue(networkObject, out audioSourceContainingGameObject);
        }

        // returns true if succesfull
        internal static bool AddPermanentTTSAudioSource(GameObject networkObject, string audioSourceName, TTSAudioSourceSettings audioSourceSettings)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSAudioSourceManager), nameof(AddPermanentTTSAudioSource));

            if (networkObject == null || audioSourceName.IsNullOrWhiteSpace())
            {
                LogConstants.CODE_INPUT_VARIABLES_INVALID.Log(nameof(TTSAudioSourceManager), 1);
                return false;
            }

            if (!DoesGameObjectHaveTTSAudioSourcesComponent(networkObject, out TTSAudioSourcesComponent audioGO))
            {
                audioGO = networkObject.AddComponent<TTSAudioSourcesComponent>();

                audioGO.transform.SetParent(networkObject.transform, false);

                // add audiosource containing gameobject to cache
                GameObjectWithTTSAudioSourcesComponent.Add(networkObject, audioGO);

                // The GameObject will always have no audiosources when here
                AddAudioSource(audioGO, audioSourceName, audioSourceSettings);
            }
            else
            {
                if (!audioGO.DoesAudioSourceExist(audioSourceName))
                {
                    AddAudioSource(audioGO, audioSourceName, audioSourceSettings);
                    return true;
                }
            }
            return false;
        }

        internal static bool PlayAudioSource(GameObject networkObject, string audioSourceName, AudioClip audioClipToPlay)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSAudioSourceManager), nameof(PlayAudioSource));

            if (networkObject == null || audioSourceName.IsNullOrWhiteSpace() || audioClipToPlay == null)
            {
                LogConstants.CODE_INPUT_VARIABLES_INVALID.Log(nameof(TTSAudioSourceManager), 1);
                return false;
            }

            if (!DoesGameObjectHaveTTSAudioSourcesComponent(networkObject, out TTSAudioSourcesComponent audioSourceContainingGameObject))
            {
                return false;
            }

            audioSourceContainingGameObject.PlayAudioClip(audioSourceName, audioClipToPlay);
            return true;
        }

        // only call when audioSourceName doesn't already exist
        private static void AddAudioSource(TTSAudioSourcesComponent audioSourceContainingGameObject, string audioSourceName, TTSAudioSourceSettings audioSourceSettings)
        {
            if (audioSourceContainingGameObject.AddAudioSource(audioSourceName))
            {
                audioSourceContainingGameObject.UpdateAudioSourceSettings(audioSourceName, audioSourceSettings);
            }
        }
    }
}
