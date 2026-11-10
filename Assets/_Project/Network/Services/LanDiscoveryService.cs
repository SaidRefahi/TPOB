using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Interfaces;
using UnityEngine;

namespace Game.Network.Services
{
    public sealed class LanDiscoveryService : ILanDiscoveryService
    {
        private const int DiscoveryPort = 47777;
        private const string ProtocolHeader = "TPOB_LAN";
        private const float BroadcastIntervalSeconds = 1.2f;
        private const float GameTimeoutSeconds = 4.0f;

        private CancellationTokenSource _broadcastCts;
        private CancellationTokenSource _listenCts;
        private UdpClient _broadcastClient;
        private UdpClient _listenClient;

        private readonly List<LanGameInfo> _discoveredGames = new List<LanGameInfo>();
        private readonly object _lock = new object();

        public event Action<LanGameInfo> OnGameDiscovered;
        public event Action<LanGameInfo> OnGameLost;

        public IReadOnlyList<LanGameInfo> DiscoveredGames
        {
            get
            {
                lock (_lock)
                {
                    return _discoveredGames.ToArray();
                }
            }
        }

        public bool IsBroadcasting => _broadcastCts != null && !_broadcastCts.IsCancellationRequested;
        public bool IsListening => _listenCts != null && !_listenCts.IsCancellationRequested;

        public void StartBroadcasting(string hostName, ushort gamePort, string roomCode = "TPOB-LAN")
        {
            StopBroadcasting();

            _broadcastCts = new CancellationTokenSource();
            BroadcastLoopAsync(hostName, gamePort, roomCode, _broadcastCts.Token).Forget();
        }

        public void StopBroadcasting()
        {
            if (_broadcastCts != null)
            {
                _broadcastCts.Cancel();
                _broadcastCts.Dispose();
                _broadcastCts = null;
            }

            if (_broadcastClient != null)
            {
                try { _broadcastClient.Close(); } catch { }
                try { _broadcastClient.Dispose(); } catch { }
                _broadcastClient = null;
            }
        }

        private async UniTaskVoid BroadcastLoopAsync(string hostName, ushort gamePort, string roomCode, CancellationToken ct)
        {
            try
            {
                _broadcastClient = new UdpClient();
                _broadcastClient.EnableBroadcast = true;
                var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

                string safeHostName = string.IsNullOrWhiteSpace(hostName) ? "TPOB Host" : hostName.Replace('|', '-');
                string message = $"{ProtocolHeader}|{safeHostName}|{gamePort}|{roomCode}";
                byte[] data = Encoding.UTF8.GetBytes(message);

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await _broadcastClient.SendAsync(data, data.Length, endpoint);
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LanDiscovery] Broadcast send exception: {ex.Message}");
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(BroadcastIntervalSeconds), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LanDiscovery] Broadcast loop exception: {ex.Message}");
            }
            finally
            {
                StopBroadcasting();
            }
        }

        public void StartListening()
        {
            StopListening();

            lock (_lock)
            {
                _discoveredGames.Clear();
            }

            _listenCts = new CancellationTokenSource();
            ListenLoopAsync(_listenCts.Token).Forget();
            PruneLoopAsync(_listenCts.Token).Forget();
        }

        public void StopListening()
        {
            if (_listenCts != null)
            {
                _listenCts.Cancel();
                _listenCts.Dispose();
                _listenCts = null;
            }

            if (_listenClient != null)
            {
                try { _listenClient.Close(); } catch { }
                try { _listenClient.Dispose(); } catch { }
                _listenClient = null;
            }

            lock (_lock)
            {
                _discoveredGames.Clear();
            }
        }

        private async UniTaskVoid ListenLoopAsync(CancellationToken ct)
        {
            try
            {
                _listenClient = new UdpClient();
                _listenClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listenClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                _listenClient.EnableBroadcast = true;

                while (!ct.IsCancellationRequested)
                {
                    var result = await _listenClient.ReceiveAsync();
                    if (ct.IsCancellationRequested) break;

                    string message = Encoding.UTF8.GetString(result.Buffer);
                    if (!message.StartsWith(ProtocolHeader)) continue;

                    string[] parts = message.Split('|');
                    if (parts.Length < 4) continue;

                    string hostName = parts[1];
                    if (!ushort.TryParse(parts[2], out ushort port)) continue;
                    string roomCode = parts[3];
                    string ip = result.RemoteEndPoint.Address.ToString();

                    await UniTask.SwitchToMainThread(ct);

                    RegisterDiscoveredGame(hostName, ip, port, roomCode);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LanDiscovery] Listen loop exception: {ex.Message}");
            }
        }

        private void RegisterDiscoveredGame(string hostName, string ip, ushort port, string roomCode)
        {
            float now = Time.realtimeSinceStartup;
            LanGameInfo updatedInfo = new LanGameInfo(hostName, ip, port, roomCode, now);

            bool isNew = true;
            lock (_lock)
            {
                for (int i = 0; i < _discoveredGames.Count; i++)
                {
                    if (_discoveredGames[i].IpAddress == ip && _discoveredGames[i].Port == port)
                    {
                        _discoveredGames[i] = updatedInfo;
                        isNew = false;
                        break;
                    }
                }

                if (isNew)
                {
                    _discoveredGames.Add(updatedInfo);
                }
            }

            if (isNew)
            {
                OnGameDiscovered?.Invoke(updatedInfo);
            }
        }

        private async UniTaskVoid PruneLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
                if (ct.IsCancellationRequested) break;

                float now = Time.realtimeSinceStartup;
                List<LanGameInfo> lostGames = null;

                lock (_lock)
                {
                    for (int i = _discoveredGames.Count - 1; i >= 0; i--)
                    {
                        if (now - _discoveredGames[i].LastSeenTimestamp > GameTimeoutSeconds)
                        {
                            lostGames ??= new List<LanGameInfo>();
                            lostGames.Add(_discoveredGames[i]);
                            _discoveredGames.RemoveAt(i);
                        }
                    }
                }

                if (lostGames != null)
                {
                    await UniTask.SwitchToMainThread(ct);
                    for (int i = 0; i < lostGames.Count; i++)
                    {
                        OnGameLost?.Invoke(lostGames[i]);
                    }
                }
            }
        }

        public void Dispose()
        {
            StopBroadcasting();
            StopListening();
        }
    }
}
