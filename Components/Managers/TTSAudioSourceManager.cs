using System.Runtime.CompilerServices;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Managers.Components;
using TTS_Company.Components.Networking.Components.Structs;
using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Managers
{
    internal static class TTSAudioSourceManager
    {
        private static readonly ConditionalWeakTable<GameObject, TTSAudioSourcesComponent> GameObjectWithTTSAudioSourcesComponent = new ConditionalWeakTable<GameObject, TTSAudioSourcesComponent>();

        private static bool DoesGameObjectHaveTTSAudioSourcesComponent(GameObject networkObject, out TTSAudioSourcesComponent audioSourceContainingGameObject)
        {
            if (networkObject == null || !networkObject)
            {
                audioSourceContainingGameObject = null;
                return false;
            }

            return GameObjectWithTTSAudioSourcesComponent.TryGetValue(networkObject, out audioSourceContainingGameObject);
        }

        // returns true if succesfull
        internal static void AddPermanentTTSAudioSource(SpawnTTSAudioSource_NET data)
        {
            if (!data._networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
            {
                LogConstants.API_NETWORK_OBJECT_NOT_FOUND.Log(nameof(TTSCompanyAPI), data._networkObjectRefOfSpeaker);
                return;
            }

            TTSAudioSourceManager.AddPermanentTTSAudioSource(networkObject.gameObject, data._callingAssemblyHash, data._audioSourceSettings);
        }

        internal static bool AddPermanentTTSAudioSource(GameObject networkObject, ulong callingAssemblyHash, TTSAudioSourceSettings audioSourceSettings)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSAudioSourceManager), nameof(AddPermanentTTSAudioSource));

            if (networkObject == null)
            {
                LogConstants.CODE_INPUT_VARIABLES_INVALID.Log(nameof(TTSAudioSourceManager), nameof(AddPermanentTTSAudioSource), 1);
                return false;
            }

            if (!DoesGameObjectHaveTTSAudioSourcesComponent(networkObject, out TTSAudioSourcesComponent audioGO))
            {
                audioGO = networkObject.AddComponent<TTSAudioSourcesComponent>();
                GameObjectWithTTSAudioSourcesComponent.Add(networkObject, audioGO);
                AddAudioSource(audioGO, callingAssemblyHash, audioSourceSettings);
                return true;
            }
            else
            {
                if (!audioGO.DoesAudioSourceExist(callingAssemblyHash))
                {
                    AddAudioSource(audioGO, callingAssemblyHash, audioSourceSettings);
                    return true;
                }
            }
            return false;
        }

        internal static void RemovePermanentTTSAudioSource(DespawnTTSAudioSource_NET data)
        {
            if (!data._networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
            {
                LogConstants.API_NETWORK_OBJECT_NOT_FOUND.Log(nameof(TTSCompanyAPI), data._networkObjectRefOfSpeaker);
                return;
            }

            TTSAudioSourceManager.RemovePermanentTTSAudioSource(networkObject.gameObject, data._callingAssemblyHash);
        }

        internal static bool RemovePermanentTTSAudioSource(GameObject networkObject, ulong callingAssemblyHash)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSAudioSourceManager), nameof(RemovePermanentTTSAudioSource));

            if (networkObject == null)
            {
                LogConstants.CODE_INPUT_VARIABLES_INVALID.Log(nameof(TTSAudioSourceManager), nameof(RemovePermanentTTSAudioSource), 1);
                return false;
            }

            if (DoesGameObjectHaveTTSAudioSourcesComponent(networkObject, out TTSAudioSourcesComponent audioGO))
            {
                return audioGO.RemoveAudioSource(callingAssemblyHash);
            }
            return false;
        }

        internal static bool PlayAudioSource(GameObject networkObject, ulong callingAssemblyHash, AudioClip audioClipToPlay)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSAudioSourceManager), nameof(PlayAudioSource));

            if (networkObject == null || audioClipToPlay == null)
            {
                LogConstants.CODE_INPUT_VARIABLES_INVALID.Log(nameof(TTSAudioSourceManager), nameof(PlayAudioSource), 1);
                return false;
            }

            if (!DoesGameObjectHaveTTSAudioSourcesComponent(networkObject, out TTSAudioSourcesComponent audioSourceContainingGameObject))
            {
                LogConstants.TTS_AUDIO_SOURCE_MANAGER_FAIL_PLAYING_NO_AUDIO_SOURCE.Log(nameof(TTSAudioSourceManager), nameof(PlayAudioSource), networkObject.name);
                return false;
            }
            return audioSourceContainingGameObject.PlayAudioClip(callingAssemblyHash, audioClipToPlay);
        }

        internal static bool StopAudioSource(GameObject networkObject, ulong callingAssemblyHash)
        {
            if (!DoesGameObjectHaveTTSAudioSourcesComponent(networkObject, out TTSAudioSourcesComponent audioSourceContainingGameObject))
            {
                return false;
            }
            return audioSourceContainingGameObject.StopAudioClip(callingAssemblyHash);
        }

        // only call when audioSourceName doesn't already exist
        private static void AddAudioSource(TTSAudioSourcesComponent audioSourceContainingGameObject, ulong callingAssemblyHash, TTSAudioSourceSettings audioSourceSettings)
        {
            if (audioSourceContainingGameObject.AddAudioSource(callingAssemblyHash))
            {
                LogConstants.TTS_AUDIO_SOURCE_MANAGER_AUDIO_SOURCE_ADDED.Log(nameof(TTSAudioSourceManager), callingAssemblyHash);
                audioSourceContainingGameObject.UpdateAudioSourceSettings(callingAssemblyHash, audioSourceSettings);
            }
        }
    }
}
