using System;
using PurrNet;
using PurrNet.Transports;
using TriInspector;
using UnityEngine;

namespace Game.Network.Services
{
    public enum NetworkSimulationProfile
    {
        Ideal = 0,            // 0ms, 0% loss
        StandardOnline = 1,   // 50-80ms, 1% loss
        QAStress = 2,         // 100-150ms, 2% loss (Requisito Fase 14)
        Extreme = 3,          // 250-350ms, 8% loss
        Custom = 4
    }

    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración de Simulación")]
    [DeclareBoxGroup("Estado Actual")]
    public sealed class NetworkSimulationController : MonoBehaviour
    {
        [Group("Configuración de Simulación")]
        [SerializeField] private NetworkSimulationProfile _activeProfile = NetworkSimulationProfile.Ideal;

        [Group("Configuración de Simulación")]
        [Range(0, 500)]
        [SerializeField] private int _customMinLatency = 100;

        [Group("Configuración de Simulación")]
        [Range(0, 1000)]
        [SerializeField] private int _customMaxLatency = 150;

        [Group("Configuración de Simulación")]
        [Range(0, 100)]
        [SerializeField] private int _customPacketLossPercent = 2;

        public NetworkSimulationProfile ActiveProfile => _activeProfile;
        public bool IsSimulationActive => _activeProfile != NetworkSimulationProfile.Ideal;

        private UDPTransport _cachedTransport;

        private void Start()
        {
            ResolveTransport();
            ApplyProfile(_activeProfile);
        }

        private UDPTransport ResolveTransport()
        {
            if (_cachedTransport != null) return _cachedTransport;

            var nm = NetworkManager.main;
            if (nm != null && nm.transport is UDPTransport udp)
            {
                _cachedTransport = udp;
            }
            else
            {
                _cachedTransport = FindFirstObjectByType<UDPTransport>();
            }

            if (_cachedTransport != null)
            {
                _cachedTransport.SetStatisticsEnabled(true);
            }

            return _cachedTransport;
        }

        public void ApplyProfile(NetworkSimulationProfile profile)
        {
            _activeProfile = profile;
            var transport = ResolveTransport();
            if (transport == null) return;

            NetworkSimulation sim = NetworkSimulation.@default;
            sim.includeInBuild = true;

            switch (profile)
            {
                case NetworkSimulationProfile.Ideal:
                    sim.simulateLatency = false;
                    sim.simulatePacketLoss = false;
                    break;

                case NetworkSimulationProfile.StandardOnline:
                    sim.simulateLatency = true;
                    sim.minLatency = 50;
                    sim.maxLatency = 80;
                    sim.simulatePacketLoss = true;
                    sim.packetLossChance = 1;
                    break;

                case NetworkSimulationProfile.QAStress:
                    sim.simulateLatency = true;
                    sim.minLatency = 100;
                    sim.maxLatency = 150;
                    sim.simulatePacketLoss = true;
                    sim.packetLossChance = 2;
                    break;

                case NetworkSimulationProfile.Extreme:
                    sim.simulateLatency = true;
                    sim.minLatency = 250;
                    sim.maxLatency = 350;
                    sim.simulatePacketLoss = true;
                    sim.packetLossChance = 8;
                    break;

                case NetworkSimulationProfile.Custom:
                    sim.simulateLatency = _customMinLatency > 0 || _customMaxLatency > 0;
                    sim.minLatency = _customMinLatency;
                    sim.maxLatency = Mathf.Max(_customMinLatency, _customMaxLatency);
                    sim.simulatePacketLoss = _customPacketLossPercent > 0;
                    sim.packetLossChance = Mathf.Clamp(_customPacketLossPercent, 0, 100);
                    break;
            }

            transport.networkSimulation = sim;
            transport.SetStatisticsEnabled(true);
        }

        public void SetCustomSimulation(int minLatencyMs, int maxLatencyMs, int packetLossPercent)
        {
            _customMinLatency = minLatencyMs;
            _customMaxLatency = maxLatencyMs;
            _customPacketLossPercent = packetLossPercent;
            ApplyProfile(NetworkSimulationProfile.Custom);
        }

        public (int minLat, int maxLat, int lossChance) GetCurrentSimulationConfig()
        {
            var transport = ResolveTransport();
            if (transport == null || !transport.networkSimulation.isActive)
            {
                return (0, 0, 0);
            }

            var sim = transport.networkSimulation;
            return (
                sim.simulateLatency ? sim.minLatency : 0,
                sim.simulateLatency ? sim.maxLatency : 0,
                sim.simulatePacketLoss ? sim.packetLossChance : 0
            );
        }

        [Group("Acciones")]
        [Button("Preset: Ideal (0ms)")]
        private void SetIdeal() => ApplyProfile(NetworkSimulationProfile.Ideal);

        [Group("Acciones")]
        [Button("Preset: Estrés QA (100-150ms, 2% Loss)")]
        private void SetQAStress() => ApplyProfile(NetworkSimulationProfile.QAStress);

        [Group("Acciones")]
        [Button("Preset: Extremo (250-350ms, 8% Loss)")]
        private void SetExtreme() => ApplyProfile(NetworkSimulationProfile.Extreme);
    }
}
