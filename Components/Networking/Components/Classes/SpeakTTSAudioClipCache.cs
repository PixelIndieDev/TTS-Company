using System.Collections.Concurrent;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal sealed class SpeakTTSAudioClipCache
    {
        internal GameObject _foundNetworkObject;
        internal ulong _callingAssemblyHash;

        internal ConcurrentQueue<AudioClip> _audioQueue = new ConcurrentQueue<AudioClip>();
        private readonly ConcurrentDictionary<AudioClip, bool> _knownClips = new ConcurrentDictionary<AudioClip, bool>(); // ignore the bool

        internal bool _isLastBatch;
        internal void MarkLastBatch() => _isLastBatch = true;

        internal SpeakTTSAudioClipCache(GameObject foundNetworkObject, ulong callingAssemblyHash, AudioClip[] audioClips)
        {
            _foundNetworkObject = foundNetworkObject;
            _callingAssemblyHash = callingAssemblyHash;

            AddAudioClips(audioClips);
        }

        internal void AddAudioClips(AudioClip[] audioClips)
        {
            foreach (AudioClip clip in audioClips)
            {
                if (clip == null)
                {
                    continue;
                }

                if (_knownClips.TryAdd(clip, false))
                {
                    _audioQueue.Enqueue(clip);
                }
            }
        }
    }
}
