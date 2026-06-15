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

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

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

            harmony.PatchAll(typeof(NetworkPatch));

            LogConstants.PLUGIN_LOADED.Log(nameof(Plugin), ModInfo.modName, ModInfo.modVersion);
        }

        void Destroy()
        {
            _tts.Dispose();
        }
    }
}
