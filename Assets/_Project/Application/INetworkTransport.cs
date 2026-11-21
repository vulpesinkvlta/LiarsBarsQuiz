using System;
using System.Collections.Generic;

namespace LiarsBar.Application
{
    public interface INetworkTransport
    {
        event Action<int, byte, string> MessageReceived;
        void SendToHost(byte code, string json);
        void SendToAll(byte code, string json);
        void SendToPlayer(int actor, byte code, string json);
    }

    public interface IRoomService
    {
        event Action Changed;
        bool Busy { get; }
        bool IsHost { get; }
        string Code { get; }
        string Status { get; }
        IReadOnlyList<string> Players { get; }
        void Create(string name);
        void Join(string name, string code);
        void Close();
    }
}
