using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalConfig;
using LethalConfig.ConfigItems;
using TTS_Company.Components;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Enums;
using TTS_Company.Debug;
using TTS_Company.Debug.Inputs;
using TTS_Company.Patches;
using UnityEngine;

namespace TTS_Company
{
    [BepInPlugin(ModInfo.modGUID, ModInfo.modName, ModInfo.modVersion)]
    [BepInDependency("com.rune580.LethalCompanyInputUtils")]
    [BepInDependency("ainavt.lc.lethalconfig")]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(ModInfo.modGUID);
        internal static Plugin instance;

        internal static TTSGenerator _tts;

        internal static TTSCompanyDebugInputs inputActionsInstance;
        internal static ConfigEntry<TTSGenPriority> configEntryPriority;

        internal static ManualLogSource logSource;

        private bool _isShuttingDown = false;
        private bool _shutdownComplete = false;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            Application.wantsToQuit += OnWantsToQuit;

            logSource = BepInEx.Logging.Logger.CreateLogSource(ModInfo.modGUID);

            inputActionsInstance = new TTSCompanyDebugInputs();
            inputActionsInstance.PixelIndieDev_DoTestTTS.performed += DebugManualTrigger.triggerDEBUGTTS;

            _tts = new TTSGenerator();

            configEntryPriority = Config.Bind("General", "TTS generation priority", TTSGenPriority.Normal, "Adjusts the CPU priority for generating TTS. Higher priority may increase the TTS generation speed at the cost of performance.");
            configEntryPriority.SettingChanged += (sender, args) =>
            {
                _tts.SetMaxConcurrentRequests(configEntryPriority.Value);
            };
            var configEntryPriorityValue = new EnumDropDownConfigItem<TTSGenPriority>(configEntryPriority, requiresRestart: false);
            LethalConfigManager.AddConfigItem(configEntryPriorityValue);

            _tts.SetMaxConcurrentRequests(configEntryPriority.Value);
            OnAwake();

            harmony.PatchAll(typeof(NetworkPatch));

            LogConstants.PLUGIN_LOADED.Log(nameof(Plugin), ModInfo.modName, ModInfo.modVersion);
        }

        async void OnAwake()
        {
            bool canBeLoaded = await _tts.InitializeAsync();
            if (!canBeLoaded)
            {
                LogConstants.PLUGIN_TTS_COULD_NOT_BE_INITIALIZED.Log(nameof(Plugin), "new TTSGenerator()");
            }

            TTSCompanyAPI.PreloadTTSVoiceModelInMemory("en_US-hfc_female-medium");
            TTSCompanyAPI.PreloadTTSVoiceModelInMemory("en_US-norman-medium");
            TTSCompanyAPI.PreloadTTSVoiceModelInMemory("en_GB-alba-medium");
            TTSCompanyAPI.PreloadTTSVoiceModelInMemory("en_US-hfc_male-medium");
            TTSCompanyAPI.PreloadTTSVoiceModelInMemory("nl_NL-pim-medium");
        }

        private bool OnWantsToQuit()
        {
            LogConstants.PLUGIN_ON_QUIT.Log(nameof(Plugin), ModInfo.modName, ModInfo.modVersion);

            if (_shutdownComplete)
            {
                return true;
            }

            if (_isShuttingDown)
            {
                return false;
            }

            _isShuttingDown = true;
            ExecuteAsyncShutdown();

            return false;
        }

        private async void ExecuteAsyncShutdown()
        {
            try
            {
                if (_tts != null)
                {
                    LogConstants.CODE_TRIGGERED.Log(nameof(Plugin), nameof(ExecuteAsyncShutdown));
                    await _tts.ShutdownAsync();
                }
            }
            catch (System.Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(Plugin), nameof(ExecuteAsyncShutdown), ex.Message);
            }
            finally
            {
                _shutdownComplete = true;

                if (_tts != null)
                {
                    _tts.Dispose();
                }

                Application.Quit();
            }
        }

        void OnDisable()
        {
            if (!_isShuttingDown && _tts != null)
            {
                _tts.Dispose();
            }
        }

        void OnDestroy()
        {
            if (!_isShuttingDown && _tts != null)
            {
                _tts.Dispose();
            }
        }
    }
}
