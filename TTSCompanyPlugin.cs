using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LethalConfig;
using LethalConfig.ConfigItems;
using System.IO;
using TTSCompany.Components;
using TTSCompany.Components.Constants;
using TTSCompany.Components.Enums;
using TTSCompany.Components.Networking;
using TTSCompany.Debug;
using TTSCompany.Debug.Inputs;
using TTSCompany.Patches;
using UnityEngine;

namespace TTSCompany
{
    [BepInPlugin(ModInfo.modGUID, ModInfo.modName, ModInfo.modVersion)]
    [BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ainavt.lc.lethalconfig")]
    [BepInDependency("LethalNetworkAPI")]
    internal sealed class TTSCompanyPlugin : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(ModInfo.modGUID);
        internal static TTSCompanyPlugin instance { get; private set; }

        internal static bool IsInputUtilsPresent { get; private set; }

        internal static TTSGenerator _tts;
        internal static TTSPlaybackManager _ttsPlaybackManagerObject { get; private set; }

        internal static TTSCompanyDebugInputs inputActionsInstance;

        internal static ConfigEntry<TTSGenPriority> configEntryPriority;
        internal static ConfigEntry<TimeoutBufferScaling> configEntryTimeoutBuffer;
        internal static ConfigEntry<bool> configEntryClearCacheOnExit;
        internal static ConfigEntry<bool> configEntryClearCacheManually;

        private bool _isDeletingCache = false;

        private bool _isShuttingDown = false;
        private bool _shutdownComplete = false;

        void Awake()
        {
            this.gameObject.hideFlags = HideFlags.HideAndDontSave; // otherwise 'TTSCompanyPlugin.instance' becomes NULL

            if (instance == null)
            {
                instance = this;
            }

            Application.wantsToQuit += OnWantsToQuit;

            IsInputUtilsPresent = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.LethalCompanyInputUtils");

            if (IsInputUtilsPresent)
            {
                inputActionsInstance = new TTSCompanyDebugInputs();
                inputActionsInstance.PixelIndieDev_TestTTS_01.performed += DebugManualTrigger.TriggerTestTTS01;
                inputActionsInstance.PixelIndieDev_TestTTS_02.performed += DebugManualTrigger.TriggerTestTTS02;
                inputActionsInstance.PixelIndieDev_TestTTS_03.performed += DebugManualTrigger.TriggerTestTTS03;
            }

            _tts = new TTSGenerator();

            // create config entries
            configEntryPriority = Config.Bind("TTS Generation", "TTS generation priority", TTSGenPriority.Normal, "Adjusts the CPU priority for generating TTS. Higher priority may increase the TTS generation speed at the cost of performance.");
            configEntryPriority.SettingChanged += (sender, args) =>
            {
                _tts.SetMaxConcurrentRequests(configEntryPriority.Value);
            };
            EnumDropDownConfigItem<TTSGenPriority> configEntryPriorityValue = new EnumDropDownConfigItem<TTSGenPriority>(configEntryPriority, requiresRestart: false);
            LethalConfigManager.AddConfigItem(configEntryPriorityValue);

            configEntryTimeoutBuffer = Config.Bind("TTS Generation", "TTS timeout scaling", TimeoutBufferScaling.Normal, "Controls how long the mod waits for a voice line to generate. Increase this if your TTS audio keeps getting cut off, or decrease it if you want the mod to give up faster when experiencing delays. \n\nControlled by the host.");
            configEntryTimeoutBuffer.SettingChanged += (sender, args) =>
            {
                TTSConstants.UpdateTimeoutBuffers();
            };
            EnumDropDownConfigItem<TimeoutBufferScaling> configEntryTimeoutBufferValue = new EnumDropDownConfigItem<TimeoutBufferScaling>(configEntryTimeoutBuffer, requiresRestart: false);
            LethalConfigManager.AddConfigItem(configEntryTimeoutBufferValue);

            configEntryClearCacheOnExit = Config.Bind("Cache", "Clear TTS cache on exit", false, "When enabled, automatically deletes saved TTS cache when you close Lethal Company. Disabling this saves disk space but requires files to be regenerate next session.");
            BoolCheckBoxConfigItem configEntryClearOnExitValue = new BoolCheckBoxConfigItem(configEntryClearCacheOnExit, requiresRestart: false);
            LethalConfigManager.AddConfigItem(configEntryClearOnExitValue);

            configEntryClearCacheManually = Config.Bind("Cache", "Clear TTS cache now", false, "Check this checkbox to delete all saved local TTS cache. \n\nBest done in the main menu.");
            configEntryClearCacheManually.SettingChanged += (sender, args) =>
            {
                if (!_isDeletingCache)
                {
                    ClearTTSCache();
                }
                configEntryClearCacheManually.Value = false;
            };
            BoolCheckBoxConfigItem configEntryClearmanuallyValue = new BoolCheckBoxConfigItem(configEntryClearCacheManually, requiresRestart: false);
            LethalConfigManager.AddConfigItem(configEntryClearmanuallyValue);

            // other stuff
            _tts.SetMaxConcurrentRequests(configEntryPriority.Value);
            TTSConstants.UpdateTimeoutBuffers();

            GameObject go = new GameObject("TTSPlaybackManager");
            _ttsPlaybackManagerObject = go.AddComponent<TTSPlaybackManager>();
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);

            harmony.PatchAll(typeof(StartOfRoundPatch));
            harmony.PatchAll(typeof(GameNetworkManagerPatch));

            TTSCompanyNetworking.Initialize();

            OnAwake();

            LogConstants.PLUGIN_LOADED.Log(nameof(TTSCompanyPlugin), ModInfo.modName, ModInfo.modVersion);
        }

        async void OnAwake()
        {
            bool canBeLoaded = await _tts.InitializeAsync();
            if (!canBeLoaded)
            {
                LogConstants.PLUGIN_TTS_COULD_NOT_BE_INITIALIZED.Log(nameof(TTSCompanyPlugin), "new TTSGenerator()");
            }

            // load default voices
            await TTSCompanyAPI.PreloadTTSVoiceModelInMemory("en_US-hfc_female-medium");
            await TTSCompanyAPI.PreloadTTSVoiceModelInMemory("en_US-hfc_male-medium");
            await TTSCompanyAPI.PreloadTTSVoiceModelInMemory("en_US-ryan-medium");
            await TTSCompanyAPI.PreloadTTSVoiceModelInMemory("en_US-sam-medium");
        }

        private void ClearTTSCache()
        {
            _isDeletingCache = true;

            if (Directory.Exists(TTSConstants.TTS_VOICE_CACHE_SOUNDCLIPS_PATH))
            {
                try
                {
                    Directory.Delete(TTSConstants.TTS_VOICE_CACHE_SOUNDCLIPS_PATH, true);
                }
                catch (IOException)
                {
                    LogConstants.CODE_GENERIC_CATCH.Log(nameof(TTSCompanyPlugin), nameof(ClearTTSCache));
                }
            }

            _isDeletingCache = false;
        }

        private bool OnWantsToQuit()
        {
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
            LogConstants.PLUGIN_ON_QUIT.Log(nameof(TTSCompanyPlugin), ModInfo.modName, ModInfo.modVersion);

            if (configEntryClearCacheOnExit.Value)
            {
                ClearTTSCache();
            }

            try
            {
                if (_tts != null)
                {
                    LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyPlugin), nameof(ExecuteAsyncShutdown));
                    await _tts.ShutdownAsync();
                }
            }
            catch (System.Exception ex)
            {
                LogConstants.CODE_GENERIC_EXCEPTION.Log(nameof(TTSCompanyPlugin), nameof(ExecuteAsyncShutdown), ex.Message);
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
