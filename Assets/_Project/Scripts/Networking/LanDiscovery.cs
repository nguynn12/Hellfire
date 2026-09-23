// Script: LanDiscovery.cs
// Mục đích: Tự động phát hiện phòng chơi trong mạng LAN qua UDP Broadcast (Mục 2.2).
// Môi trường thực thi: Cả hai (Host gửi broadcast, Client lắng nghe broadcast).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Hellfire.Networking
{
    [Serializable]
    public struct LanRoomInfo
    {
        public string RoomName;
        public string HostIp;
        public ushort Port;
        public int CurrentPlayers;
        public int MaxPlayers;
        public float LastSeenTime;

        public override string ToString()
        {
            return $"{RoomName} ({HostIp}:{Port}) [{CurrentPlayers}/{MaxPlayers}]";
        }
    }

    [DisallowMultipleComponent]
    public class LanDiscovery : MonoBehaviour
    {
        public static LanDiscovery Instance { get; private set; }

        [Header("LAN Discovery Settings")]
        [SerializeField] private ushort _broadcastPort = 47777;
        [SerializeField] private float _broadcastInterval = 1.0f;
        [SerializeField] private float _roomTimeout = 3.5f;

        private const string MessageHeader = "HELLFIRE_LAN_ROOM";
        private const char MessageDelimiter = '|';

        private UdpClient _udpSender;
        private UdpClient _udpReceiver;
        private Thread _receiverThread;
        private bool _isBroadcasting;
        private bool _isListening;
        private float _broadcastTimer;

        private string _activeRoomName = "Hellfire Room";
        private ushort _gamePort = 7777;
        private int _maxPlayers = 4;
        private int _currentPlayers = 1;

        private readonly ConcurrentQueue<LanRoomInfo> _receivedRoomQueue = new ConcurrentQueue<LanRoomInfo>();
        private readonly Dictionary<string, LanRoomInfo> _discoveredRooms = new Dictionary<string, LanRoomInfo>();

        public event Action<LanRoomInfo> OnRoomFound;
        public event Action<string> OnRoomLost;
        public event Action<IReadOnlyList<LanRoomInfo>> OnRoomListUpdated;

        public IReadOnlyDictionary<string, LanRoomInfo> DiscoveredRooms => _discoveredRooms;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // Xử lý các phòng nhận được từ thread UDP sang Main Thread của Unity
            bool listChanged = false;

            while (_receivedRoomQueue.TryDequeue(out var roomInfo))
            {
                string key = $"{roomInfo.HostIp}:{roomInfo.Port}";
                bool isNew = !_discoveredRooms.ContainsKey(key);

                roomInfo.LastSeenTime = Time.time;
                _discoveredRooms[key] = roomInfo;
                listChanged = true;

                if (isNew)
                {
                    OnRoomFound?.Invoke(roomInfo);
                }
            }

            // Xóa các phòng đã hết hạn (timeout)
            if (_isListening)
            {
                var keysToRemove = new List<string>();
                foreach (var kvp in _discoveredRooms)
                {
                    if (Time.time - kvp.Value.LastSeenTime > _roomTimeout)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _discoveredRooms.Remove(key);
                    listChanged = true;
                    OnRoomLost?.Invoke(key);
                }
            }

            if (listChanged)
            {
                var list = new List<LanRoomInfo>(_discoveredRooms.Values);
                OnRoomListUpdated?.Invoke(list);
            }

            // Host phát broadcast định kỳ
            if (_isBroadcasting)
            {
                _broadcastTimer += Time.deltaTime;
                if (_broadcastTimer >= _broadcastInterval)
                {
                    _broadcastTimer = 0f;
                    BroadcastRoomInfo();
                }
            }
        }

        public void StartBroadcasting(string roomName, ushort gamePort, int maxPlayers, int currentPlayers)
        {
            StopBroadcasting();

            _activeRoomName = string.IsNullOrWhiteSpace(roomName) ? "Hellfire Room" : roomName;
            _gamePort = gamePort;
            _maxPlayers = maxPlayers;
            _currentPlayers = currentPlayers;

            try
            {
                _udpSender = new UdpClient();
                _udpSender.EnableBroadcast = true;
                _isBroadcasting = true;
                _broadcastTimer = _broadcastInterval; // Phát ngay lập tức
                Debug.Log($"[LanDiscovery] Bắt đầu broadcast phòng: {_activeRoomName} trên cổng UDP {_broadcastPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LanDiscovery] Lỗi khởi tạo UDP sender: {ex.Message}");
            }
        }

        public void UpdatePlayerCount(int currentPlayers)
        {
            _currentPlayers = currentPlayers;
        }

        private void BroadcastRoomInfo()
        {
            if (_udpSender == null || !_isBroadcasting)
            {
                return;
            }

            try
            {
                // Cấu trúc gói tin: HELLFIRE_LAN_ROOM|RoomName|Port|CurrentPlayers|MaxPlayers
                string payload = $"{MessageHeader}{MessageDelimiter}{_activeRoomName}{MessageDelimiter}{_gamePort}{MessageDelimiter}{_currentPlayers}{MessageDelimiter}{_maxPlayers}";
                byte[] data = Encoding.UTF8.GetBytes(payload);
                var endpoint = new IPEndPoint(IPAddress.Broadcast, _broadcastPort);
                _udpSender.Send(data, data.Length, endpoint);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LanDiscovery] Gặp lỗi khi gửi broadcast: {ex.Message}");
            }
        }

        public void StopBroadcasting()
        {
            _isBroadcasting = false;
            if (_udpSender != null)
            {
                try
                {
                    _udpSender.Close();
                    _udpSender.Dispose();
                }
                catch { }
                _udpSender = null;
            }
        }

        public void StartListening()
        {
            StopListening();
            _discoveredRooms.Clear();

            try
            {
                _udpReceiver = new UdpClient();
                _udpReceiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpReceiver.Client.Bind(new IPEndPoint(IPAddress.Any, _broadcastPort));
                _isListening = true;

                _receiverThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "Hellfire_LanDiscovery_Receiver"
                };
                _receiverThread.Start();
                Debug.Log($"[LanDiscovery] Bắt đầu lắng nghe phòng LAN trên cổng {_broadcastPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LanDiscovery] Lỗi khởi tạo UDP receiver: {ex.Message}");
            }
        }

        private void ListenLoop()
        {
            var endPoint = new IPEndPoint(IPAddress.Any, 0);

            while (_isListening && _udpReceiver != null)
            {
                try
                {
                    byte[] data = _udpReceiver.Receive(ref endPoint);
                    string message = Encoding.UTF8.GetString(data);

                    if (message.StartsWith(MessageHeader))
                    {
                        string[] parts = message.Split(MessageDelimiter);
                        if (parts.Length >= 5)
                        {
                            var roomInfo = new LanRoomInfo
                            {
                                RoomName = parts[1],
                                HostIp = endPoint.Address.ToString(),
                                Port = ushort.TryParse(parts[2], out var p) ? p : (ushort)7777,
                                CurrentPlayers = int.TryParse(parts[3], out var cp) ? cp : 1,
                                MaxPlayers = int.TryParse(parts[4], out var mp) ? mp : 4,
                                LastSeenTime = 0f
                            };

                            _receivedRoomQueue.Enqueue(roomInfo);
                        }
                    }
                }
                catch (SocketException)
                {
                    // Socket bị đóng khi StopListening được gọi
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LanDiscovery] Lỗi đọc gói tin: {ex.Message}");
                }
            }
        }

        public void StopListening()
        {
            _isListening = false;

            if (_udpReceiver != null)
            {
                try
                {
                    _udpReceiver.Close();
                    _udpReceiver.Dispose();
                }
                catch { }
                _udpReceiver = null;
            }

            if (_receiverThread != null && _receiverThread.IsAlive)
            {
                try
                {
                    _receiverThread.Join(200);
                }
                catch { }
                _receiverThread = null;
            }
        }

        private void OnDestroy()
        {
            StopBroadcasting();
            StopListening();
        }

        private void OnApplicationQuit()
        {
            StopBroadcasting();
            StopListening();
        }
    }
}
