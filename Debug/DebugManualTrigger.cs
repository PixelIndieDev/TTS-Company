using GameNetcodeStuff;
using TTSCompany.Components;
using TTSCompany.Components.Constants;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TTSCompany.Debug
{
    internal static class DebugManualTrigger
    {
        private static readonly string[] randomVoiceLines = new[]
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

        private static readonly string[] enemyNames = new[]
        {
            "Barber",
            "Bracken",
            "Bunker Spider",
            "Coil-Head",
            "Hoarding Bug",
            "Hygrodere",
            "Jester",
            "Nutcracker"
        };

        private static readonly string[] multipleLines = new[]
        {
            "This is a testing text-to-speech voice line.",
            "This is a voice line test.",
            "Testing complete."
        };

        private static PlayerControllerB speakingPlayer = null;

        private static void GetSpeakingPlayer()
        {
            if (speakingPlayer != null)
            {
                return;
            }

            if (StartOfRound.Instance == null || StartOfRound.Instance.localPlayerController == null)
            {
                LogConstants.CODE_INPUT_VARIABLES_INVALID.Log(nameof(DebugManualTrigger), nameof(GetSpeakingPlayer), 1);
                return;
            }

            speakingPlayer = StartOfRound.Instance.localPlayerController;
            if (speakingPlayer == null)
            {
                return;
            }

            TTSCompanyAPI.AddTTSAudioSourceOnNetworkObject(speakingPlayer.gameObject);
        }

        internal async static void TriggerTestTTS01(InputAction.CallbackContext obj)
        {
            if (!obj.performed)
            {
                return;
            }

            LogConstants.CODE_TRIGGERED.Log(nameof(DebugManualTrigger), nameof(TriggerTestTTS01));

            GetSpeakingPlayer();
            if (speakingPlayer == null)
            {
                LogConstants.CODE_TRIGGERED.Log(nameof(DebugManualTrigger), "speakingPlayer == null");
                return;
            }

            PiperVoiceSettings voice = new PiperVoiceSettings();
            voice.ModelName = TTSCompanyUtils.GetRandomLoadedTTSVoicename();

            int randomIndex = Random.Range(0, randomVoiceLines.Length);

            TTSAudioSourceSettings audioS = new TTSAudioSourceSettings();
            audioS.Volume = 1.0f;

            TTSCompanyAPI.UpdateTTSAudioSourceSettingsOnNetworkObject(speakingPlayer.gameObject, audioS);

            TTSCompanyAPI.SpeakTTSAtNetworkObject(speakingPlayer.gameObject, randomVoiceLines[randomIndex], voiceSettings: voice, noiseRangeMultiplier: 1.0f);
        }

        internal async static void TriggerTestTTS02(InputAction.CallbackContext obj)
        {
            if (!obj.performed)
            {
                return;
            }

            LogConstants.CODE_TRIGGERED.Log(nameof(DebugManualTrigger), nameof(TriggerTestTTS02));

            GetSpeakingPlayer();
            if (speakingPlayer == null)
            {
                LogConstants.CODE_TRIGGERED.Log(nameof(DebugManualTrigger), "speakingPlayer == null");
                return;
            }

            string[] reactionEnemy01 = new[]
            {
                "Warning, ",
                "ENTITYNAME",
                " detected near your position."
            };

            string[] reactionEnemy02 = new[]
            {
                "WATCH OUT, ",
                "ENTITYNAME",
                " BEHIND YOU!"
            };

            string[][] reactionEnemyList = new[]
            {
                reactionEnemy01,
                reactionEnemy02
            };

            PiperVoiceSettings voice = new PiperVoiceSettings();
            voice.ModelName = TTSCompanyUtils.GetRandomLoadedTTSVoicename();

            int randomIndex = Random.Range(0, reactionEnemy01.Length);
            string[] chosenArray = reactionEnemyList[randomIndex];

            randomIndex = Random.Range(0, enemyNames.Length);
            chosenArray[1] = enemyNames[randomIndex];

            TTSAudioSourceSettings audioS = new TTSAudioSourceSettings();
            audioS.Volume = 0.25f;

            TTSCompanyAPI.UpdateTTSAudioSourceSettingsOnNetworkObject(speakingPlayer.gameObject, audioS);
            TTSCompanyAPI.SpeakTTSAtNetworkObject(speakingPlayer.gameObject, chosenArray, voiceSettings: voice, noiseRangeMultiplier: 1.0f);
        }

        internal async static void TriggerTestTTS03(InputAction.CallbackContext obj)
        {
            if (!obj.performed)
            {
                return;
            }

            LogConstants.CODE_TRIGGERED.Log(nameof(DebugManualTrigger), nameof(TriggerTestTTS03));

            GetSpeakingPlayer();
            if (speakingPlayer == null)
            {
                LogConstants.CODE_TRIGGERED.Log(nameof(DebugManualTrigger), "speakingPlayer == null");
                return;
            }

            TTSCompanyAPI.SpeakTTSAtNetworkObject(speakingPlayer.gameObject, multipleLines, noiseRangeMultiplier: 1.0f);
            //TTSCompanyAPI.SpeakTTSAtNetworkObject(speakingPlayer.gameObject, "This is one big line, that never seems to end, or does it, or does it just go on and on and on and on, where will this sentence end, is there even a end, that the question");
        }
    }
}
