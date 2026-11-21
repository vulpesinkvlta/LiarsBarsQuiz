using System;
using System.Collections.Generic;
using LiarsBar.Application;
using LiarsBar.Application.Sessions;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Views;

namespace LiarsBar.Networking
{
    public sealed class RemoteMatchSession : IMatchSession
    {
        private readonly INetworkTransport _transport;
        private readonly int _host;
        private readonly Queue<NetworkMessage> _messages = new Queue<NetworkMessage>();
        private bool _busy;
        private bool _pending;
        private bool _draining;
        private bool _started;

        public RemoteMatchSession(INetworkTransport transport, int host, int seat)
        {
            _transport = transport;
            _host = host;
            ViewerSeat = seat;
        }

        public int ViewerSeat { get; }
        public PlayerView CurrentView { get; private set; }
        public event Action<PlayerView> ViewChanged;
        public event Action<IReadOnlyList<GameEvent>> EventsRaised;
        public event Action<ActionError> ActionRejected;

        public void Start()
        {
            if (_started) return;
            _started = true;
            _transport.MessageReceived += Receive;
        }

        private void Receive(int actor, byte code, string json)
        {
            if (actor != _host || (code != MessageCodec.EventsCode && code != MessageCodec.ViewCode)) return;
            var message = MessageCodec.Decode(code, json);
            if (message is ViewMessage snapshot && snapshot.View?.ViewerSeat != ViewerSeat) return;
            _messages.Enqueue(message);
            Drain();
        }

        private void Drain()
        {
            if (_draining) return;
            _draining = true;
            try
            {
                while (!_busy && _messages.Count > 0)
                {
                    var message = _messages.Dequeue();
                    if (message is EventsMessage batch) EventsRaised?.Invoke(batch.Events);
                    else if (message is ViewMessage snapshot)
                    {
                        CurrentView = snapshot.View;
                        _pending = false;
                        ViewChanged?.Invoke(CurrentView);
                    }
                }
            }
            finally { _draining = false; }
        }

        public void Submit(IGameAction action)
        {
            if (!_started || _busy || _pending || CurrentView?.IsMyTurn != true) return;
            var json = MessageCodec.Encode(new ActionMessage { Action = action }, out var code);
            _pending = true;
            try { _transport.SendToHost(code, json); }
            catch
            {
                _pending = false;
                throw;
            }
        }

        public void SetPresentationBusy(bool busy)
        {
            _busy = busy;
            if (!busy) Drain();
        }

        public void Dispose()
        {
            _transport.MessageReceived -= Receive;
            _messages.Clear();
            _started = false;
        }
    }
}
