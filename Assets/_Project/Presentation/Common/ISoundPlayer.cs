using UnityEngine;

namespace LiarsBar.Presentation.Common
{
    public interface ISoundPlayer
    {
        void Play(AudioClip clip);
    }

    public sealed class UiSoundPlayer : ISoundPlayer
    {
        private readonly AudioSource _source;
        public UiSoundPlayer(AudioSource source) => _source = source;
        public void Play(AudioClip clip)
        {
            if (_source != null && clip != null) _source.PlayOneShot(clip);
        }
    }
}
