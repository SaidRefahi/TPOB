using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace Game.Network.UI
{
    [DisallowMultipleComponent]
    public sealed class PerformanceMonitor : MonoBehaviour
    {
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private Vector2 _position = new Vector2(10, 10);

        private float _fpsAccumulator;
        private int _fpsFrames;
        private float _fpsTimeLeft = 0.25f;

        private float _currentFps;
        private long _monoUsedMemoryMb;
        private long _gcTotalMemoryMb;
        private int _gcGen0Collections;
        private int _gcGen1Collections;
        private int _gcGen2Collections;

        private string _cachedFpsText = "FPS: --";
        private string _cachedMemoryText = "Mono: -- MB";
        private string _cachedGcText = "GC Gen0: 0 | Gen1: 0 | Gen2: 0";
        private string _cachedZeroGcStatus = "Zero-GC: OK (0 allocs)";

        private long _lastGcMemory;

        public bool ShowOverlay
        {
            get => _showOverlay;
            set => _showOverlay = value;
        }

        public float CurrentFps => _currentFps;
        public long MonoMemoryMb => _monoUsedMemoryMb;
        public int Gen0Collections => _gcGen0Collections;

        private void Awake()
        {
            _lastGcMemory = GC.GetTotalMemory(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                _showOverlay = !_showOverlay;
            }

            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrames++;
            _fpsTimeLeft -= Time.unscaledDeltaTime;

            if (_fpsTimeLeft <= 0f)
            {
                _currentFps = _fpsFrames / Mathf.Max(0.001f, _fpsAccumulator);
                _fpsAccumulator = 0f;
                _fpsFrames = 0;
                _fpsTimeLeft = 0.25f;

                UpdateMetricsStrings();
            }
        }

        private void UpdateMetricsStrings()
        {
            _monoUsedMemoryMb = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
            long currentGcMemory = GC.GetTotalMemory(false);
            _gcTotalMemoryMb = currentGcMemory / (1024 * 1024);

            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);

            long memoryDelta = currentGcMemory - _lastGcMemory;
            _lastGcMemory = currentGcMemory;

            _gcGen0Collections = gen0;
            _gcGen1Collections = gen1;
            _gcGen2Collections = gen2;

            _cachedFpsText = $"FPS: {_currentFps:0.0}";
            _cachedMemoryText = $"Mono: {_monoUsedMemoryMb} MB (Total: {_gcTotalMemoryMb} MB)";
            _cachedGcText = $"GC Gen0: {gen0} | Gen1: {gen1} | Gen2: {gen2}";

            if (memoryDelta <= 0)
            {
                _cachedZeroGcStatus = "<color=#55FF55><b>● Zero-GC: OK (0 B/s)</b></color>";
            }
            else
            {
                _cachedZeroGcStatus = $"<color=#FFAA00><b>▲ GC Delta: +{memoryDelta / 1024} KB/s</b></color>";
            }
        }

        private void OnGUI()
        {
            if (!_showOverlay) return;

            float width = 230f;
            float height = 110f;
            Rect rect = new Rect(Screen.width - width - _position.x, _position.y, width, height);

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, width - 16, height - 12));

            string fpsColor = _currentFps >= 55f ? "#55FF55" : (_currentFps >= 30f ? "#FFAA00" : "#FF5555");
            GUILayout.Label($"<color={fpsColor}><b>{_cachedFpsText}</b></color> <size=10>[F3 para ocultar]</size>");
            GUILayout.Label(_cachedMemoryText);
            GUILayout.Label(_cachedGcText);
            GUILayout.Label(_cachedZeroGcStatus);

            GUILayout.EndArea();
        }
    }
}
