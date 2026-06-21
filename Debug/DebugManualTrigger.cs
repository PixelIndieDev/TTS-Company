using BepInEx;
using System.IO;
using TTS_Company.Components;
using TTS_Company.Components.Constants;
using Unity.Netcode;
using UnityEngine;
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
            "Warning! Retreat immediately!",
            "FUCK!"
        };

        private static readonly string[] voiceModels = new[]
        {
            "en_US-hfc_female-medium",
            "en_US-norman-medium",
            "en_GB-alba-medium",
            "en_US-hfc_male-medium",
            "nl_NL-pim-medium"
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
                string[] customStrings = new[]
                {
                    "Warning, ",
                    "ENTITYNAME",
                    " detected near your position."
                };

                string[] customStrings2 = new[]
                {
                    "WATCH OUT, ",
                    "ENTITYNAME",
                    " BEHIND YOU!"
                };

                string[][] allCustomStrings = new[] { customStrings, customStrings2 };

                string[] enemyNames = new[]
                {
                    "Barber",
                    "Bracken",
                    "Bunker Spider",
                    "Coil-Head",
                    "Hoarding Bug",
                    "Hygrodere",
                    "Jester",
                    "Nutcracker",
                    "Jerma985"
                };

                string para = "Welcome to your first day at The Company. Please remember your quota. Collect all scrap, avoid the coil-heads, and do not make eye contact with the entities. Failure to comply will result in disciplinary action.";

                if (Plugin.instance != null)
                {
                    if (localPlayer.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
                    {
                        NetworkObjectReference reference = new NetworkObjectReference(networkObject);

                        for (int i = 0; i < 8; i++)
                        {
                            //TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference, TTSConstants.DEBUG_AUDIOSOURCE_NAME + i);
                        }

                        for (int i = 0; i < 1; i++)
                        {
                            PiperVoiceSettings voice = new PiperVoiceSettings();
                            //voice.ModelName = GetRandomVoice();

                            int randomIndex = Random.Range(0, allCustomStrings.Length);
                            string[] chosenArray = allCustomStrings[0];

                            randomIndex = Random.Range(0, customStrings.Length);
                            chosenArray[1] = enemyNames[randomIndex];

                            TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, TTSConstants.DEBUG_AUDIOSOURCE_NAME + i, RandomVoiceLines[0], voice);
                        }
                    }
                }
            }
        }

        private static string GetRandomVoice()
        {
            int randomIndex = Random.Range(0, voiceModels.Length);
            return voiceModels[randomIndex];
        }
    }
}
