using System.Collections.Concurrent;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal sealed class SpeakTTSAudioClipCache
    {
        internal GameObject _foundNetworkObject;
        internal ulong _callingAssemblyHash;

        internal ConcurrentQueue<QueuedClip> _audioQueue = new ConcurrentQueue<QueuedClip>();
        private readonly ConcurrentDictionary<AudioClip, bool> _knownClips = new ConcurrentDictionary<AudioClip, bool>(); // ignore the bool

        internal bool _isLastBatch;
        internal void MarkLastBatch() => _isLastBatch = true;

        internal SpeakTTSAudioClipCache(GameObject foundNetworkObject, ulong callingAssemblyHash, AudioClip[] audioClips, float[] pauseDurations)
        {
            _foundNetworkObject = foundNetworkObject;
            _callingAssemblyHash = callingAssemblyHash;

            AddAudioClips(audioClips, pauseDurations);
        }

        internal void AddAudioClips(AudioClip[] audioClips, float[] pauseDurations)
        {
            for (int i = 0; i < audioClips.Length; i++)
            {
                AudioClip clip = audioClips[i];
                if (clip == null)
                {
                    continue;
                }

                if (_knownClips.TryAdd(clip, false))
                {
                    float pause = (pauseDurations != null && i < pauseDurations.Length) ? pauseDurations[i] : 0f;
                    _audioQueue.Enqueue(new QueuedClip(clip, pause));
                }
            }
        }
    }
}
