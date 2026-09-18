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
    }
}
