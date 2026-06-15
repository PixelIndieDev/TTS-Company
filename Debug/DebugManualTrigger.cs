using BepInEx;
using System.IO;
using TTS_Company.Components;
using TTS_Company.Components.Constants;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace TTS_Company.Debug
{
    internal class DebugManualTrigger
    {
        private static readonly string[] RandomVoiceLines = new[]
        {
            "This is a testing text-to-speech voice line.",
            "System online. All networks are fully functional.",
            "Warning, localized anomaly detected near your position.",
            "Please do not touch the operational machinery.",
            "The quick brown fox jumps over the lazy dog.",
            "I guess...this voice lines works.",
            "Testing complete.",
            "This is a voice line test.",
            "Warning! Retreat immediately!"
        };

        public static void triggerDEBUGTTS(InputAction.CallbackContext obj)
        {
            if (!obj.performed)
            {
                return;
            }

            LogConstants.CODE_TRIGGERED.Log(nameof(DebugManualTrigger), nameof(triggerDEBUGTTS));

            if (StartOfRound.Instance == null || StartOfRound.Instance.localPlayerController == null)
            {
                LogConstants.CODE_INPUT_VARIABLES_INVALID.Log(nameof(DebugManualTrigger), nameof(triggerDEBUGTTS), 1);
                return;
            }

            var localPlayer = StartOfRound.Instance.localPlayerController;

            if (localPlayer != null)
            {
                int randomIndex = UnityEngine.Random.Range(0, RandomVoiceLines.Length);

                if (Plugin.instance != null)
                {
                    if (localPlayer.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
                    {
                        NetworkObjectReference reference = new NetworkObjectReference(networkObject);

                        TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference, TTSConstants.DEBUG_AUDIOSOURCE_NAME);

                        TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference, "DEBUG_KEY_2");
                        TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference, "DEBUG_KEY_3");
                        TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference, "DEBUG_KEY_4");
                        TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference, "DEBUG_KEY_5");
                        TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference, "DEBUG_KEY_6");
                        TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference, "DEBUG_KEY_7");
                        TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference, "DEBUG_KEY_8");

                        //RandomVoiceLines[randomIndex]
                        TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, TTSConstants.DEBUG_AUDIOSOURCE_NAME, RandomVoiceLines[0]); // RandomVoiceLines[randomIndex]

                        TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, "DEBUG_KEY_2", RandomVoiceLines[1]);
                        TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, "DEBUG_KEY_3", RandomVoiceLines[2]);
                        TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, "DEBUG_KEY_4", RandomVoiceLines[3]);
                        TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, "DEBUG_KEY_5", RandomVoiceLines[4]);
                        TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, "DEBUG_KEY_6", RandomVoiceLines[5]);
                        TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, "DEBUG_KEY_7", RandomVoiceLines[6]);
                        TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, "DEBUG_KEY_8", RandomVoiceLines[7]);
                    }
                }
            }
        }
    }
}
