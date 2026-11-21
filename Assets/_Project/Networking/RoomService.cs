using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application;
using LiarsBar.Application.Sessions;
using LiarsBar.Core.Config;
using Photon.Pun;
using Photon.Realtime;

namespace LiarsBar.Networking
{
    public interface IHostRoomService : IRoomService
    {
        LocalMatchSession StartHost(MatchFactory factory, GameRulesConfig rules, int bots);
    }

    public static class NetworkServices
    {
        public static IHostRoomService Attach(UnityEngine.GameObject owner)
        {
            owner.AddComponent<PhotonTransport>();
            return owner.AddComponent<RoomService>();
        }
    }

    public sealed class RoomService : MonoBehaviourPunCallbacks, IHostRoomService
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private string _joinCode;
        private bool _closing;
        private int _attempts;
        private HostMatchConnection _hostConnection;
        private PhotonTransport Transport => GetComponent<PhotonTransport>();

        public LocalMatchSession StartHost(MatchFactory factory, GameRulesConfig rules, int bots)
        {
            var room = PhotonNetwork.CurrentRoom;
            if (!IsHost || !room.IsOpen || _hostConnection != null) return null;
            var players = PhotonNetwork.PlayerList.OrderByDescending(p => p.IsLocal)
                .ThenBy(p => p.ActorNumber).ToArray();
            if (bots < 0 || players.Length + bots < rules.MinPlayers || players.Length + bots > rules.MaxPlayers)
                return null;
            var roster = players.Select(p => new KeyValuePair<int, string>(p.ActorNumber, p.NickName)).ToArray();
            var session = factory.CreateHost(roster, bots);
            try
            {
                room.IsOpen = false;
                _hostConnection = new HostMatchConnection(session, Transport, roster, rules);
                return session;
            }
            catch
            {
                StopHost();
                session.Dispose();
                room.IsOpen = true;
                throw;
            }
        }

        private void StopHost()
        {
            _hostConnection?.Dispose();
            _hostConnection = null;
        }
        public event Action Changed;
        public bool Busy { get; private set; }
        public bool IsHost => PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;
        public string Code => PhotonNetwork.CurrentRoom?.Name;
        public string Status { get; private set; }
        public IReadOnlyList<string> Players => PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber)
            .Select(p => $"{p.ActorNumber}. {p.NickName}").ToArray();

        public void Create(string name) => Begin(name, null);
        public void Join(string name, string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length != 4 || !code.All(c => Alphabet.Contains(c)))
            {
                UpdateState(false, "Введите четыре латинские буквы кода.");
                return;
            }
            Begin(name, code);
        }

        private void Begin(string name, string joinCode)
        {
            if (Busy || _closing || PhotonNetwork.InRoom) return;
            _joinCode = joinCode;
            _attempts = 0;
            PhotonNetwork.NickName = string.IsNullOrWhiteSpace(name) ? "Игрок" : name.Trim();
            UpdateState(true, "Подключение…");
            if (PhotonNetwork.IsConnectedAndReady) Enter();
            else if (!PhotonNetwork.ConnectUsingSettings()) UpdateState(false, "Не удалось подключиться.");
        }

        public override void OnConnectedToMaster()
        {
            if (Busy && !_closing) Enter();
        }

        private void Enter()
        {
            bool sent;
            if (_joinCode == null)
            {
                _attempts++;
                var code = new string(Enumerable.Range(0, 4)
                    .Select(_ => Alphabet[UnityEngine.Random.Range(0, Alphabet.Length)]).ToArray());
                sent = PhotonNetwork.CreateRoom(code, new RoomOptions { MaxPlayers = 6, IsVisible = false });
            }
            else sent = PhotonNetwork.JoinRoom(_joinCode);
            if (!sent) UpdateState(false, "Сервер не принял запрос. Попробуйте ещё раз.");
        }

        public override void OnCreateRoomFailed(short code, string message)
        {
            if (_closing) return;
            if (code == ErrorCode.GameIdAlreadyExists && _attempts < 8) Enter();
            else UpdateState(false, "Не удалось создать комнату. Попробуйте ещё раз.");
        }

        public override void OnJoinRoomFailed(short code, string message) =>
            UpdateState(false, code == ErrorCode.GameDoesNotExist ? "Комната не найдена." :
                code == ErrorCode.GameFull ? "Все места заняты." :
                code == ErrorCode.GameClosed ? "Игра уже идёт." : "Не удалось войти в комнату.");

        public override void OnJoinedRoom() => UpdateState(false, "");
        public override void OnPlayerEnteredRoom(Player player) => Changed?.Invoke();
        public override void OnPlayerLeftRoom(Player player) => Changed?.Invoke();
        public override void OnMasterClientSwitched(Player player) => Changed?.Invoke();

        public void Close()
        {
            StopHost();
            _closing = PhotonNetwork.NetworkClientState != ClientState.Disconnected &&
                PhotonNetwork.NetworkClientState != ClientState.PeerCreated;
            UpdateState(_closing, "");
            if (_closing) PhotonNetwork.Disconnect();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            StopHost();
            var status = _closing ? "" : "Связь с сервером потеряна. Попробуйте ещё раз.";
            _closing = false;
            UpdateState(false, status);
        }

        private void UpdateState(bool busy, string status)
        {
            Busy = busy;
            Status = status;
            Changed?.Invoke();
        }
    }
}
