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

        [Range(0f, 1f)]
        [SerializeField] private float _musicVolume = 1f;

        [Header("Música de Fondo")]
        [SerializeField] private AudioClip _menuMusicClip;
        [SerializeField] private AudioClip _levelMusicClip;

        [SerializeField] private int _defaultPoolCapacity = 16;
        [SerializeField] private int _maxPoolSize = 32;

        [Header("Overrides de Clips")]
        [SerializeField] private AudioClip[] _clipOverrides = new AudioClip[32];

        private ObjectPool<AudioSource> _sourcePool;
        private readonly List<ActiveSourceTracker> _activeSources = new(32);

        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private AudioSource _incomingSource;
        private AudioSource _outgoingSource;
        private MusicTrack _currentTrack = MusicTrack.None;
        private bool _isDucked;
        private float _fadeDuration = 1.2f;

        private struct ActiveSourceTracker
        {
            public AudioSource Source;
            public float ReleaseTime;
        }

        private bool _isSynthesizerInitialized;
        private bool _isDestroyed;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_isDestroyed || this == null) return;

            if (!_isSynthesizerInitialized)
            {
                _isSynthesizerInitialized = true;
                ProceduralAudioSynthesizer.Initialize();
            }

            if (_musicSourceA == null)
            {
                _musicSourceA = CreateMusicSourceInstance("MusicSource_A");
            }

            if (_musicSourceB == null)
            {
                _musicSourceB = CreateMusicSourceInstance("MusicSource_B");
            }

#if UNITY_EDITOR
            if (_menuMusicClip == null)
            {
                _menuMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets importado/Clunky Electro-Funk Industrial (Take 2).mp3");
            }

            if (_levelMusicClip == null)
            {
                _levelMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets importado/Clunky Electro-Funk Industrial.mp3");
            }
#endif

            if (_sourcePool == null)
            {
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
        }

        private AudioSource CreateMusicSourceInstance(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
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

            UpdateMusicFading();
        }

        public void PlaySfx(AudioCue cue, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            EnsureInitialized();
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
            EnsureInitialized();
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
        public void SetMusicVolume(float volume) => _musicVolume = Mathf.Clamp01(volume);

        public void PlayMusic(MusicTrack track, float fadeDuration = 1.2f)
        {
            if (_isDestroyed || this == null) return;

            EnsureInitialized();

            if (_musicSourceA == null || _musicSourceB == null)
            {
                return;
            }

            if (_currentTrack == track && _incomingSource != null && _incomingSource.isPlaying)
            {
                return;
            }

            AudioClip clip = GetClipForMusicTrack(track);
            if (clip == null)
            {
                StopMusic(fadeDuration);
                return;
            }

            _fadeDuration = Mathf.Max(0.05f, fadeDuration);
            _currentTrack = track;

            _outgoingSource = _incomingSource;
            _incomingSource = (_incomingSource == _musicSourceA) ? _musicSourceB : _musicSourceA;
            if (_incomingSource == null)
            {
                _incomingSource = _musicSourceA;
            }

            if (_incomingSource == null) return;

            _incomingSource.clip = clip;
            _incomingSource.volume = 0f;
            _incomingSource.Play();
        }

        public void StopMusic(float fadeDuration = 1.2f)
        {
            if (_isDestroyed || this == null) return;

            EnsureInitialized();
            _fadeDuration = Mathf.Max(0.05f, fadeDuration);
            _currentTrack = MusicTrack.None;
            _outgoingSource = _incomingSource;
            _incomingSource = null;
        }

        public void SetMusicDucked(bool ducked, float fadeDuration = 0.3f)
        {
            if (_isDestroyed || this == null) return;

            _isDucked = ducked;
            _fadeDuration = Mathf.Max(0.05f, fadeDuration);
        }

        private void UpdateMusicFading()
        {
            if (_isDestroyed || this == null) return;

            float dt = Time.unscaledDeltaTime;
            float targetVolume = _masterVolume * _musicVolume * (_isDucked ? 0.4f : 1f);
            float step = (_fadeDuration > 0.01f ? (1f / _fadeDuration) : 50f) * dt;

            if (_incomingSource != null && _incomingSource.isPlaying)
            {
                _incomingSource.volume = Mathf.MoveTowards(_incomingSource.volume, targetVolume, step);
            }

            if (_outgoingSource != null && _outgoingSource.isPlaying)
            {
                _outgoingSource.volume = Mathf.MoveTowards(_outgoingSource.volume, 0f, step);
                if (_outgoingSource.volume <= 0.001f)
                {
                    _outgoingSource.Stop();
                    _outgoingSource.clip = null;
                }
            }
        }

        private AudioClip GetClipForMusicTrack(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Menu:
                    if (_menuMusicClip == null)
                    {
#if UNITY_EDITOR
                        _menuMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets importado/Clunky Electro-Funk Industrial (Take 2).mp3");
#endif
                    }
                    return _menuMusicClip;

                case MusicTrack.Level:
                    if (_levelMusicClip == null)
                    {
#if UNITY_EDITOR
                        _levelMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets importado/Clunky Electro-Funk Industrial.mp3");
#endif
                    }
                    return _levelMusicClip;

                default:
                    return null;
            }
        }

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
            _isDestroyed = true;
            _sourcePool?.Clear();
        }
    }
}
