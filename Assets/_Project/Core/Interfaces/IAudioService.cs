using Game.Core.Enums;
using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IAudioService
    {
        void PlaySfx(AudioCue cue, Vector3 position, float volume = 1f, float pitch = 1f);
        void PlayLocalSfx(AudioCue cue, float volume = 1f, float pitch = 1f);
        void SetMasterVolume(float volume);
        void SetSfxVolume(float volume);
        void SetMusicVolume(float volume);
        void PlayMusic(MusicTrack track, float fadeDuration = 1.2f);
        void StopMusic(float fadeDuration = 1.2f);
        void SetMusicDucked(bool ducked, float fadeDuration = 0.3f);
    }
}
