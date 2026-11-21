using System;
using ExitGames.Client.Photon;
using LiarsBar.Application;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace LiarsBar.Networking
{
    public sealed class PhotonTransport : MonoBehaviour, INetworkTransport, IOnEventCallback
    {
        public event Action<int, byte, string> MessageReceived;
        private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
        private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

        public void OnEvent(EventData ev)
        {
            if (ev.Code >= 1 && ev.Code <= 5 && ev.CustomData is string json)
                MessageReceived?.Invoke(ev.Sender, ev.Code, json);
        }

        public void SendToHost(byte code, string json) =>
            Send(code, json, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient });

        public void SendToAll(byte code, string json) =>
            Send(code, json, new RaiseEventOptions { Receivers = ReceiverGroup.All });

        public void SendToPlayer(int actor, byte code, string json) =>
            Send(code, json, new RaiseEventOptions { TargetActors = new[] { actor } });

        private static void Send(byte code, string json, RaiseEventOptions options)
        {
            if (code < 1 || code > 5) throw new ArgumentOutOfRangeException(nameof(code));
            if (!PhotonNetwork.RaiseEvent(code, json, options, SendOptions.SendReliable))
                throw new InvalidOperationException("Сообщение не удалось отправить.");
        }
    }
}
