using GameNetcodeStuff;
using TTS_Company.Components;
using TTS_Company.Components.Constants;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TTS_Company.Debug
{
    internal static class DebugManualTrigger
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

        public async static void triggerDEBUGTTS(InputAction.CallbackContext obj)
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

            PlayerControllerB localPlayer = StartOfRound.Instance.localPlayerController;

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

                if (TTSCompanyPlugin.instance == null)
                {
                    return;
                }

                if (localPlayer.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
                {
                    NetworkObjectReference reference = new NetworkObjectReference(networkObject);
                    TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(reference);

                    for (int i = 0; i < 1; i++)
                    {
                        PiperVoiceSettings voice = new PiperVoiceSettings();
                        voice.ModelName = TTSCompanyUtils.GetRandomLoadedTTSVoice();

                        int randomIndex = Random.Range(0, allCustomStrings.Length);
                        string[] chosenArray = allCustomStrings[randomIndex];

                        randomIndex = Random.Range(0, enemyNames.Length);
                        chosenArray[1] = enemyNames[randomIndex];

                        TTSCompanyAPI.SpeakTTSAtNetworkObject(reference, chosenArray, voiceSettings: voice);
                    }
                }
            }
        }
    }
}
