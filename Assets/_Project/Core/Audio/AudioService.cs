using System;
using System.Collections.Generic;
using Game.Core.Enums;
using Game.Core.Interfaces;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        [Header("Configuración de Audio")]
        [Range(0f, 1f)]
        [SerializeField] private float _masterVolume = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float _sfxVolume = 1f;

        [SerializeField] private int _defaultPoolCapacity = 16;
        [SerializeField] private int _maxPoolSize = 32;

        [Header("Overrides de Clips")]
        [SerializeField] private AudioClip[] _clipOverrides = new AudioClip[32];

        private ObjectPool<AudioSource> _sourcePool;
        private readonly List<ActiveSourceTracker> _activeSources = new(32);

        private struct ActiveSourceTracker
        {
            public AudioSource Source;
            public float ReleaseTime;
        }

        private void Awake()
        {
            ProceduralAudioSynthesizer.Initialize();

            _sourcePool = new ObjectPool<AudioSource>(
                createFunc: CreateAudioSourceInstance,
                actionOnGet: OnTakeFromPool,
                actionOnRelease: OnReturnedToPool,
                actionOnDestroy: OnDestroyPoolObject,
                collectionCheck: false,
                defaultCapacity: _defaultPoolCapacity,
                maxSize: _maxPoolSize
            );
        }

        private AudioSource CreateAudioSourceInstance()
        {
            GameObject go = new GameObject("AudioSource_Pooled");
            go.transform.SetParent(transform);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 3f;
            source.maxDistance = 45f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        private void OnTakeFromPool(AudioSource source)
        {
            source.gameObject.SetActive(true);
        }

        private void OnReturnedToPool(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(false);
        }

        private void OnDestroyPoolObject(AudioSource source)
        {
            if (source != null)
            {
                Destroy(source.gameObject);
            }
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = _activeSources.Count - 1; i >= 0; i--)
            {
                if (now >= _activeSources[i].ReleaseTime)
                {
                    _sourcePool.Release(_activeSources[i].Source);
                    _activeSources.RemoveAt(i);
                }
            }
        }

        public void PlaySfx(AudioCue cue, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            AudioClip clip = GetClipForCue(cue);
            if (clip == null) return;

            AudioSource source = _sourcePool.Get();
            source.transform.position = position;
            source.spatialBlend = 1f;
            source.volume = volume * _sfxVolume * _masterVolume;
            source.pitch = pitch * UnityEngine.Random.Range(0.93f, 1.07f);
            source.clip = clip;
            source.Play();

            _activeSources.Add(new ActiveSourceTracker
            {
                Source = source,
                ReleaseTime = Time.time + (clip.length / Mathf.Max(0.1f, source.pitch)) + 0.05f
            });
        }

        public void PlayLocalSfx(AudioCue cue, float volume = 1f, float pitch = 1f)
        {
            AudioClip clip = GetClipForCue(cue);
            if (clip == null) return;

            AudioSource source = _sourcePool.Get();
            source.transform.position = transform.position;
            source.spatialBlend = 0f;
            source.volume = volume * _sfxVolume * _masterVolume;
            source.pitch = pitch;
            source.clip = clip;
            source.Play();

            _activeSources.Add(new ActiveSourceTracker
            {
                Source = source,
                ReleaseTime = Time.time + (clip.length / Mathf.Max(0.1f, source.pitch)) + 0.05f
            });
        }

        public void SetMasterVolume(float volume) => _masterVolume = Mathf.Clamp01(volume);
        public void SetSfxVolume(float volume) => _sfxVolume = Mathf.Clamp01(volume);

        private AudioClip GetClipForCue(AudioCue cue)
        {
            int idx = (int)cue;
            if (idx >= 0 && idx < _clipOverrides.Length && _clipOverrides[idx] != null)
            {
                return _clipOverrides[idx];
            }

            return ProceduralAudioSynthesizer.GetClip(cue);
        }

        private void OnDestroy()
        {
            _sourcePool?.Clear();
        }
    }
}
