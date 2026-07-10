using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using UnityEngine.InputSystem;

namespace TTSCompany.Debug.Inputs
{
    public class TTSCompanyDebugInputs : LcInputActions
    {
        [InputAction(KeyboardControl.None, Name = "Test TTS (random strings)")]
        public InputAction PixelIndieDev_TestTTS_01 { get; set; }

        [InputAction(KeyboardControl.None, Name = "Test TTS (random enemy warning, volume 0.5)")]
        public InputAction PixelIndieDev_TestTTS_02 { get; set; }

        [InputAction(KeyboardControl.None, Name = "Test TTS (multiple strings, noiseRangeMultiplier 1.5)")]
        public InputAction PixelIndieDev_TestTTS_03 { get; set; }
    }
}
