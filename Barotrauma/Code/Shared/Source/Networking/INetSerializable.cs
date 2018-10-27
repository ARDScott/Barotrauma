﻿using Lidgren.Network;

namespace Barotrauma.Networking
{
    interface INetSerializable { }

    /// <summary>
    /// Interface for entities that the clients can send information of to the server
    /// </summary>
    interface IClientSerializable : INetSerializable
    {
#if CLIENT
        void ClientWrite(NetBuffer msg, object[] extraData = null);
#endif
        void ServerRead(ClientNetObject type, NetBuffer msg, Client c);        
    }

    /// <summary>
    /// Interface for entities that the server can send information of to the clients
    /// </summary>
    interface IServerSerializable : INetSerializable
    {
        void ServerWrite(NetBuffer msg, Client c, object[] extraData = null);
#if CLIENT
        void ClientRead(ServerNetObject type, NetBuffer msg, float sendingTime);
#endif
    }
}
