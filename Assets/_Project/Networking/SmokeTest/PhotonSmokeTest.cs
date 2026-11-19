using System.Collections.Generic;
using ExitGames.Client.Photon;
using Newtonsoft.Json;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LiarsBar.Networking.SmokeTest
{
    /// <summary>
    /// Этап 1: смоук-тест Photon PUN 2 в WebGL.
    /// Подключение, создание/вход в комнату по 4-буквенному коду, список игроков,
    /// пинг через RaiseEvent с JSON-полезной нагрузкой (заодно проверяет Newtonsoft в сборке).
    /// UI строится из кода, чтобы сцена не зависела от префабов. Тексты на латинице:
    /// в стандартном шрифте TMP нет кириллицы (атлас — этап 4).
    /// Основа для RoomService на этапе 13.
    /// </summary>
    public sealed class PhotonSmokeTest : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        private const byte PingEventCode = 100;
        private const byte PongEventCode = 101;
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // без O и I
        private const int CodeLength = 4;
        private const int MaxCreateAttempts = 5;
        private const int MaxLogLines = 14;

        private TMP_Text _statusText;
        private TMP_Text _playersText;
        private TMP_Text _logText;
        private TMP_InputField _codeInput;
        private Button _createButton;
        private Button _joinButton;
        private Button _pingButton;
        private Button _leaveButton;

        private readonly List<string> _logLines = new List<string>();
        private readonly Dictionary<int, double> _pingSentAt = new Dictionary<int, double>();
        private int _createAttempts;
        private int _pingSeq;
        private float _nextRefresh;

        [JsonObject(MemberSerialization.Fields)]
        private sealed class PingMessage
        {
            public int Seq;
            public string From;
        }

        private void Start()
        {
            UnityEngine.Application.runInBackground = true;
            BuildUi();

            PhotonNetwork.NickName = "Player-" + Random.Range(100, 1000);
            Log($"I am {PhotonNetwork.NickName}. Connecting...");
            PhotonNetwork.ConnectUsingSettings();
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            Refresh();
        }

        // ---------- Действия кнопок ----------

        private void OnCreateClicked()
        {
            _createAttempts = 0;
            TryCreateRoom();
        }

        private void TryCreateRoom()
        {
            _createAttempts++;
            var code = GenerateCode();
            Log($"Creating room {code} (attempt {_createAttempts})...");
            var options = new RoomOptions { MaxPlayers = 6, IsVisible = false, IsOpen = true };
            PhotonNetwork.CreateRoom(code, options);
        }

        private void OnJoinClicked()
        {
            var code = (_codeInput.text ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length != CodeLength)
            {
                Log($"Enter a {CodeLength}-letter code.");
                return;
            }
            Log($"Joining room {code}...");
            PhotonNetwork.JoinRoom(code);
        }

        private void OnPingClicked()
        {
            var seq = ++_pingSeq;
            _pingSentAt[seq] = Time.realtimeSinceStartupAsDouble;
            var json = JsonConvert.SerializeObject(new PingMessage { Seq = seq, From = PhotonNetwork.NickName });
            var ok = PhotonNetwork.RaiseEvent(PingEventCode, json,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
            Log(ok ? $"Ping #{seq} sent" : $"Ping #{seq} failed to send");
        }

        private void OnLeaveClicked()
        {
            PhotonNetwork.LeaveRoom();
        }

        private static string GenerateCode()
        {
            var chars = new char[CodeLength];
            for (var i = 0; i < CodeLength; i++)
                chars[i] = CodeAlphabet[Random.Range(0, CodeAlphabet.Length)];
            return new string(chars);
        }

        // ---------- Колбэки Photon ----------

        public override void OnConnectedToMaster()
        {
            Log($"Connected. Region: {PhotonNetwork.CloudRegion}, protocol: {PhotonNetwork.NetworkingClient.LoadBalancingPeer.TransportProtocol}");
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Log($"Disconnected: {cause}");
        }

        public override void OnCreatedRoom()
        {
            Log($"Room created: {PhotonNetwork.CurrentRoom.Name}");
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            if (returnCode == ErrorCode.GameIdAlreadyExists && _createAttempts < MaxCreateAttempts)
            {
                Log("Code taken, trying another...");
                TryCreateRoom();
                return;
            }
            Log($"Create failed ({returnCode}): {message}");
        }

        public override void OnJoinedRoom()
        {
            _codeInput.text = PhotonNetwork.CurrentRoom.Name;
            Log($"Joined room {PhotonNetwork.CurrentRoom.Name} as actor #{PhotonNetwork.LocalPlayer.ActorNumber}");
            Refresh();
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            string text;
            switch (returnCode)
            {
                case ErrorCode.GameDoesNotExist: text = "Room not found"; break;
                case ErrorCode.GameFull: text = "Room is full"; break;
                case ErrorCode.GameClosed: text = "Game already started"; break;
                default: text = message; break;
            }
            Log($"Join failed ({returnCode}): {text}");
        }

        public override void OnLeftRoom()
        {
            Log("Left room");
            Refresh();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Log($"+ {newPlayer.NickName} (#{newPlayer.ActorNumber}) joined");
            Refresh();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Log($"- {otherPlayer.NickName} (#{otherPlayer.ActorNumber}) left");
            Refresh();
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            Log($"Master client is now {newMasterClient.NickName}");
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != PingEventCode && photonEvent.Code != PongEventCode) return;

            var json = photonEvent.CustomData as string;
            PingMessage msg;
            try
            {
                msg = JsonConvert.DeserializeObject<PingMessage>(json);
            }
            catch (System.Exception e)
            {
                Log($"Bad payload: {e.Message}");
                return;
            }

            if (photonEvent.Code == PingEventCode)
            {
                Log($"Ping #{msg.Seq} from {msg.From}, replying");
                var reply = JsonConvert.SerializeObject(new PingMessage { Seq = msg.Seq, From = PhotonNetwork.NickName });
                PhotonNetwork.RaiseEvent(PongEventCode, reply,
                    new RaiseEventOptions { TargetActors = new[] { photonEvent.Sender } }, SendOptions.SendReliable);
            }
            else
            {
                var rtt = _pingSentAt.TryGetValue(msg.Seq, out var sentAt)
                    ? $"{(Time.realtimeSinceStartupAsDouble - sentAt) * 1000:0} ms"
                    : "?";
                Log($"Pong #{msg.Seq} from {msg.From}: {rtt}");
            }
        }

        // ---------- Отрисовка ----------

        private void Refresh()
        {
            if (_statusText == null) return;

            var room = PhotonNetwork.CurrentRoom;
            _statusText.text =
                $"State: {PhotonNetwork.NetworkClientState}\n" +
                $"Region: {(string.IsNullOrEmpty(PhotonNetwork.CloudRegion) ? "-" : PhotonNetwork.CloudRegion)}   " +
                $"RTT: {(PhotonNetwork.IsConnected ? PhotonNetwork.GetPing() + " ms" : "-")}\n" +
                $"Room: {(room != null ? room.Name : "-")}";

            if (room != null)
            {
                var sb = new System.Text.StringBuilder($"Players {room.PlayerCount}/{room.MaxPlayers}:\n");
                foreach (var p in room.Players.Values)
                {
                    sb.Append("  #").Append(p.ActorNumber).Append(' ').Append(p.NickName);
                    if (p.IsMasterClient) sb.Append(" [host]");
                    if (p.IsLocal) sb.Append(" (you)");
                    sb.Append('\n');
                }
                _playersText.text = sb.ToString();
            }
            else
            {
                _playersText.text = "Not in a room";
            }

            var ready = PhotonNetwork.IsConnectedAndReady;
            var inRoom = PhotonNetwork.InRoom;
            _createButton.interactable = ready && !inRoom;
            _joinButton.interactable = ready && !inRoom;
            _codeInput.interactable = !inRoom;
            _pingButton.interactable = inRoom;
            _leaveButton.interactable = inRoom;
        }

        private void Log(string line)
        {
            Debug.Log("[SmokeTest] " + line);
            _logLines.Add($"{System.DateTime.Now:HH:mm:ss} {line}");
            while (_logLines.Count > MaxLogLines) _logLines.RemoveAt(0);
            if (_logText != null) _logText.text = string.Join("\n", _logLines);
        }

        // ---------- Построение UI ----------

        private void BuildUi()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.10f, 0.08f, 0.07f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = 20;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddText(panel.transform, "Photon smoke test", 64, 90, FontStyles.Bold);
            _statusText = AddText(panel.transform, "", 40, 170);

            var resources = new TMP_DefaultControls.Resources();
            var inputGo = TMP_DefaultControls.CreateInputField(resources);
            inputGo.transform.SetParent(panel.transform, false);
            inputGo.AddComponent<LayoutElement>().preferredHeight = 130;
            _codeInput = inputGo.GetComponent<TMP_InputField>();
            _codeInput.characterLimit = CodeLength;
            _codeInput.contentType = TMP_InputField.ContentType.Alphanumeric;
            _codeInput.textComponent.fontSize = 64;
            _codeInput.textComponent.alignment = TextAlignmentOptions.Center;
            ((TMP_Text)_codeInput.placeholder).text = "CODE";
            ((TMP_Text)_codeInput.placeholder).fontSize = 64;
            ((TMP_Text)_codeInput.placeholder).alignment = TextAlignmentOptions.Center;
            _codeInput.onValidateInput += (text, index, ch) => char.ToUpperInvariant(ch);

            var row1 = AddRow(panel.transform);
            _createButton = AddButton(row1, "Create", OnCreateClicked);
            _joinButton = AddButton(row1, "Join", OnJoinClicked);

            var row2 = AddRow(panel.transform);
            _pingButton = AddButton(row2, "Ping", OnPingClicked);
            _leaveButton = AddButton(row2, "Leave", OnLeaveClicked);

            _playersText = AddText(panel.transform, "", 40, 330);
            _logText = AddText(panel.transform, "", 30, 0);
            _logText.GetComponent<LayoutElement>().flexibleHeight = 1;
            _logText.color = new Color(0.8f, 0.8f, 0.7f);
        }

        private static TMP_Text AddText(Transform parent, string text, float size, float height, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Truncate;
            if (height > 0) go.GetComponent<LayoutElement>().preferredHeight = height;
            return tmp;
        }

        private static Transform AddRow(Transform parent)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 140;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 20;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            return go.transform;
        }

        private static Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            go.name = label + "Button";
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = 52;
            text.color = Color.black;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }
    }
}
