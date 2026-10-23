// Script: MatchSummaryManager.cs
// Mục đích: Quản lý tổng kết lượt chơi (Kills, DamageDealt, Revives, TimesDowned, MatchDuration), đồng bộ qua INetworkSerializable (Mục 16.11 đặc tả kỹ thuật).
// Môi trường thực thi: Server ghi nhận và gửi ClientRpc, mọi Client nhận và hiển thị.

using System;
using System.Collections.Generic;
using Hellfire.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Networking
{
    [Serializable]
    public struct PlayerMatchStats : INetworkSerializable
    {
        public ulong ClientId;
        public FixedString32Bytes PlayerName;
        public int ColorIndex;
        public int Kills;
        public float DamageDealt;
        public int Revives;
        public int TimesDowned;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref ColorIndex);
            serializer.SerializeValue(ref Kills);
            serializer.SerializeValue(ref DamageDealt);
            serializer.SerializeValue(ref Revives);
            serializer.SerializeValue(ref TimesDowned);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class MatchSummaryManager : NetworkBehaviour
    {
        public static MatchSummaryManager Instance { get; private set; }

        public static event Action<PlayerMatchStats[], float> OnMatchSummaryReceived;

        private readonly Dictionary<ulong, PlayerMatchStats> _serverStats = new Dictionary<ulong, PlayerMatchStats>();
        private float _matchStartTime = 0f;
        private bool _matchSummarySent = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleGameStateChanged(GameState prev, GameState next)
        {
            if (!IsServer) return;

            if (next == GameState.Playing)
            {
                _matchStartTime = Time.time;
                _matchSummarySent = false;
                _serverStats.Clear();

                // Khởi tạo mục thống kê cho mọi client đang kết nối
                if (NetworkManager.Singleton != null)
                {
                    foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                    {
                        EnsurePlayerStats(client.ClientId);
                    }
                }
            }
            else if ((next == GameState.Victory || next == GameState.GameOver) && !_matchSummarySent)
            {
                _matchSummarySent = true;
                SendMatchSummary();
            }
        }

        private static void EnsurePlayerStats(ulong clientId)
        {
            if (Instance == null || !Instance.IsServer) return;

            if (!Instance._serverStats.ContainsKey(clientId))
            {
                var identity = PlayerIdentity.GetByClientId(clientId);
                string name = identity != null ? identity.PlayerName.Value.ToString() : $"Người chơi {clientId + 1}";
                int colorIdx = identity != null ? identity.ColorIndex.Value : (int)(clientId % 4);

                Instance._serverStats[clientId] = new PlayerMatchStats
                {
                    ClientId = clientId,
                    PlayerName = new FixedString32Bytes(name),
                    ColorIndex = colorIdx,
                    Kills = 0,
                    DamageDealt = 0f,
                    Revives = 0,
                    TimesDowned = 0
                };
            }
        }

        public static void RecordDamage(ulong attackerId, float damage)
        {
            if (Instance == null || !Instance.IsServer) return;
            EnsurePlayerStats(attackerId);

            if (Instance._serverStats.TryGetValue(attackerId, out var stats))
            {
                stats.DamageDealt += damage;
                Instance._serverStats[attackerId] = stats;
            }
        }

        public static void RecordKill(ulong killerId)
        {
            if (Instance == null || !Instance.IsServer) return;
            EnsurePlayerStats(killerId);

            if (Instance._serverStats.TryGetValue(killerId, out var stats))
            {
                stats.Kills++;
                Instance._serverStats[killerId] = stats;
            }
        }

        public static void RecordRevive(ulong reviverId)
        {
            if (Instance == null || !Instance.IsServer) return;
            EnsurePlayerStats(reviverId);

            if (Instance._serverStats.TryGetValue(reviverId, out var stats))
            {
                stats.Revives++;
                Instance._serverStats[reviverId] = stats;
            }
        }

        public static void RecordDowned(ulong downedPlayerId)
        {
            if (Instance == null || !Instance.IsServer) return;
            EnsurePlayerStats(downedPlayerId);

            if (Instance._serverStats.TryGetValue(downedPlayerId, out var stats))
            {
                stats.TimesDowned++;
                Instance._serverStats[downedPlayerId] = stats;
            }
        }

        private void SendMatchSummary()
        {
            if (!IsServer) return;

            float duration = Mathf.Max(0f, Time.time - _matchStartTime);

            // Cập nhật lại Tên và Màu sắc mới nhất từ PlayerIdentity trước khi gửi
            var list = new List<PlayerMatchStats>();
            foreach (var kvp in _serverStats)
            {
                var stat = kvp.Value;
                var identity = PlayerIdentity.GetByClientId(stat.ClientId);
                if (identity != null)
                {
                    stat.PlayerName = identity.PlayerName.Value;
                    stat.ColorIndex = identity.ColorIndex.Value;
                }
                list.Add(stat);
            }

            PlayerMatchStats[] statsArray = list.ToArray();
            Debug.Log($"[MatchSummaryManager] Server gửi bảng tổng kết ({statsArray.Length} người chơi, thời gian {duration:F1}s)");

            SendMatchSummaryClientRpc(statsArray, duration);
        }

        [ClientRpc]
        private void SendMatchSummaryClientRpc(PlayerMatchStats[] stats, float duration)
        {
            Debug.Log($"[MatchSummaryManager] Client nhận tổng kết {stats.Length} người chơi");
            OnMatchSummaryReceived?.Invoke(stats, duration);
        }
    }
}
