using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using UnityEngine.InputSystem;

namespace TTSCompany.Debug.Inputs
{
    public class TTSCompanyDebugInputs : LcInputActions
    {
        [InputAction(KeyboardControl.None, Name = "Test TTS (random strings)")]
        public InputAction PixelIndieDev_TestTTS_01 { get; set; }

        [InputAction(KeyboardControl.None, Name = "Test TTS (random enemy)")]
        public InputAction PixelIndieDev_TestTTS_02 { get; set; }
    }
}
