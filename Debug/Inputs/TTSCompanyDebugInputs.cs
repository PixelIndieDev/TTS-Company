using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using UnityEngine.InputSystem;

namespace TTS_Company.Debug.Inputs
{
    public class TTSCompanyDebugInputs : LcInputActions
    {
        [InputAction(KeyboardControl.K, Name = "Do the test TTS")]
        public InputAction PixelIndieDev_DoTestTTS { get; set; }
    }
}
