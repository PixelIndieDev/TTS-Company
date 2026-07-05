using System;
using UnityEngine;

namespace TTSCompany.Components.Helpers
{
    internal static class ClampHelper
    {
        internal static float ClampAndRound(float value, float min, float max)
        {
            return (float)Math.Round(Mathf.Clamp(value, min, max), 2, MidpointRounding.AwayFromZero);
        }
    }
}
