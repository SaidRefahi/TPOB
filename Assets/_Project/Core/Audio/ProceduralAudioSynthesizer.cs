using System;
using Game.Core.Enums;
using UnityEngine;

namespace Game.Core.Audio
{
    public static class ProceduralAudioSynthesizer
    {
        private const int SampleRate = 44100;
        private static readonly AudioClip[] CachedClips = new AudioClip[32];
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;

            Array cues = Enum.GetValues(typeof(AudioCue));
            foreach (AudioCue cue in cues)
            {
                int index = (int)cue;
                if (index >= 0 && index < CachedClips.Length)
                {
                    CachedClips[index] = GenerateClipForCue(cue);
                }
            }

            _initialized = true;
        }

        public static AudioClip GetClip(AudioCue cue)
        {
            if (!_initialized)
            {
                Initialize();
            }

            int index = (int)cue;
            if (index >= 0 && index < CachedClips.Length)
            {
                return CachedClips[index];
            }

            return null;
        }

        private static AudioClip GenerateClipForCue(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.ImpactLight:
                    return GenerateDecayingSine("Sfx_ImpactLight", 320f, 0.12f, 15f);

                case AudioCue.ImpactHeavy:
                    return GeneratePitchDrop("Sfx_ImpactHeavy", 180f, 45f, 0.3f, 8f);

                case AudioCue.ImpactMetal:
                    return GenerateMetallicClang("Sfx_ImpactMetal", 520f, 0.25f);

                case AudioCue.Kick:
                    return GeneratePitchDrop("Sfx_Kick", 240f, 50f, 0.18f, 12f);

                case AudioCue.Footstep:
                    return GenerateNoiseBurst("Sfx_Footstep", 0.06f, 25f);

                case AudioCue.Jump:
                    return GenerateChirp("Sfx_Jump", 180f, 480f, 0.16f);

                case AudioCue.HardLanding:
                    return GeneratePitchDrop("Sfx_HardLanding", 160f, 35f, 0.35f, 6f);

                case AudioCue.Dock:
                    return GenerateTwoTone("Sfx_Dock", 440f, 880f, 0.22f);

                case AudioCue.Undock:
                    return GenerateTwoTone("Sfx_Undock", 880f, 440f, 0.22f);

                case AudioCue.MagnetHum:
                    return GenerateSine("Sfx_MagnetHum", 90f, 0.4f);

                case AudioCue.MagnetPull:
                    return GenerateChirp("Sfx_MagnetPull", 220f, 660f, 0.28f);

                case AudioCue.Throw:
                    return GenerateNoiseSweep("Sfx_Throw", 0.18f);

                case AudioCue.Grab:
                    return GenerateChirp("Sfx_Grab", 400f, 800f, 0.08f);

                case AudioCue.Release:
                    return GenerateChirp("Sfx_Release", 800f, 400f, 0.08f);

                case AudioCue.ButtonPress:
                    return GenerateDecayingSine("Sfx_ButtonPress", 1100f, 0.08f, 25f);

                case AudioCue.LeverToggle:
                    return GenerateTwoTone("Sfx_LeverToggle", 300f, 600f, 0.18f);

                case AudioCue.DoorSlide:
                    return GenerateNoiseBurst("Sfx_DoorSlide", 0.45f, 4f);

                case AudioCue.PlayerDeath:
                    return GeneratePitchDrop("Sfx_PlayerDeath", 550f, 90f, 0.45f, 4f);

                case AudioCue.PlayerRespawn:
                    return GenerateArpeggio("Sfx_PlayerRespawn", new[] { 330f, 440f, 550f, 660f }, 0.35f);

                case AudioCue.PlayerTumble:
                    return GenerateSpringBoing("Sfx_PlayerTumble", 220f, 0.28f);

                case AudioCue.RoomClear:
                    return GenerateArpeggio("Sfx_RoomClear", new[] { 261.63f, 329.63f, 392.00f, 523.25f }, 0.65f);

                default:
                    return GenerateDecayingSine("Sfx_Default", 440f, 0.1f, 10f);
            }
        }

        private static AudioClip GenerateDecayingSine(string name, float freq, float duration, float decayRate)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-decayRate * t);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GeneratePitchDrop(string name, float startFreq, float endFreq, float duration, float decayRate)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t);
                phase += 2f * Mathf.PI * currentFreq / SampleRate;
                float envelope = Mathf.Exp(-decayRate * ((float)i / SampleRate));
                data[i] = Mathf.Sin(phase) * envelope;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateChirp(string name, float startFreq, float endFreq, float duration)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t);
                phase += 2f * Mathf.PI * currentFreq / SampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t);
                data[i] = Mathf.Sin(phase) * envelope;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateMetallicClang(string name, float baseFreq, float duration)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-14f * t);
                float wave = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.5f +
                             Mathf.Sin(2f * Mathf.PI * (baseFreq * 1.48f) * t) * 0.3f +
                             Mathf.Sin(2f * Mathf.PI * (baseFreq * 2.14f) * t) * 0.2f;
                data[i] = wave * envelope;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateNoiseBurst(string name, float duration, float decayRate)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            var rng = new System.Random(1337);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float envelope = Mathf.Exp(-decayRate * t);
                data[i] = noise * envelope;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateNoiseSweep(string name, float duration)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            var rng = new System.Random(42);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float envelope = Mathf.Sin(Mathf.PI * t);
                data[i] = noise * envelope * 0.7f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateTwoTone(string name, float freq1, float freq2, float duration)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            int half = samples / 2;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float freq = i < half ? freq1 : freq2;
                float envelope = Mathf.Sin(Mathf.PI * ((float)i / samples));
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateSine(string name, float freq, float duration)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateArpeggio(string name, float[] freqs, float duration)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            int noteCount = freqs.Length;
            int samplesPerNote = samples / noteCount;

            for (int i = 0; i < samples; i++)
            {
                int noteIdx = Mathf.Min(i / samplesPerNote, noteCount - 1);
                float freq = freqs[noteIdx];
                float t = (float)i / SampleRate;
                float noteT = (float)(i % samplesPerNote) / samplesPerNote;
                float envelope = Mathf.Sin(Mathf.PI * noteT);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateSpringBoing(string name, float baseFreq, float duration)
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float wobble = Mathf.Sin(2f * Mathf.PI * 18f * t) * 80f;
                float currentFreq = baseFreq + wobble;
                float envelope = Mathf.Exp(-8f * t);
                data[i] = Mathf.Sin(2f * Mathf.PI * currentFreq * t) * envelope;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
