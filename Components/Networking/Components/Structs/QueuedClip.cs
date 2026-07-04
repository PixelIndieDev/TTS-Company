using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal readonly struct QueuedClip
    {
        internal readonly AudioClip Clip;
        internal readonly float PauseAfter;

        internal QueuedClip(AudioClip clip, float pauseAfter)
        {
            Clip = clip;
            PauseAfter = pauseAfter;
        }
    }
}
