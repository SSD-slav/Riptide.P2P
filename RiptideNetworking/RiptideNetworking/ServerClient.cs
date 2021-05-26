﻿using System;
using System.Net;

namespace RiptideNetworking
{
    /// <summary>Represents a server's connection to a client.</summary>
    public class ServerClient
    {
        /// <summary>The numeric ID.</summary>
        public ushort Id { get; private set; }
        /// <summary>The round trip time of the connection. -1 if not calculated yet.</summary>
        public short RTT => Rudp.RTT;
        /// <summary>The smoothed round trip time of the connection. -1 if not calculated yet.</summary>
        public short SmoothRTT => Rudp.SmoothRTT;
        /// <summary>Whether or not the client is currently in the process of connecting.</summary>
        public bool IsConnecting => connectionState == ConnectionState.connecting;
        /// <summary>Whether or not the client is currently connected.</summary>
        public bool IsConnected => connectionState == ConnectionState.connected;
        /// <summary>The remote endpoint.</summary>
        public readonly IPEndPoint remoteEndPoint;

        internal Rudp Rudp { get; private set; }
        internal SendLockables SendLockables => Rudp.SendLockables;
        internal bool HasTimedOut => (DateTime.UtcNow - lastHeartbeat).TotalMilliseconds > server.ClientTimeoutTime;

        private DateTime lastHeartbeat;
        private readonly Server server;
        private ConnectionState connectionState = ConnectionState.notConnected;

        internal ServerClient(Server server, IPEndPoint endPoint, ushort id)
        {
            this.server = server;
            remoteEndPoint = endPoint;
            Id = id;
            Rudp = new Rudp(server.Send, this.server.LogName);
            lastHeartbeat = DateTime.UtcNow;

            connectionState = ConnectionState.connecting;
            SendWelcome();
        }

        internal void Disconnect()
        {
            connectionState = ConnectionState.notConnected;
        }

        #region Messages
        internal void SendAck(ushort forSeqId)
        {
            Message message = Message.CreateInternal(forSeqId == Rudp.SendLockables.LastReceivedSeqId ? HeaderType.ack : HeaderType.ackExtra);
            message.Add(Rudp.SendLockables.LastReceivedSeqId); // Last remote sequence ID
            message.Add(Rudp.SendLockables.AcksBitfield); // Acks

            if (forSeqId == Rudp.SendLockables.LastReceivedSeqId)
                server.Send(message, this);
            else
            {
                message.Add(forSeqId);
                server.Send(message, this);
            }
        }

        internal void HandleAck(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();

            Rudp.AckMessage(remoteLastReceivedSeqId);
            Rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        internal void HandleAckExtra(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetUShort();

            Rudp.AckMessage(ackedSeqId);
            Rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        internal void SendHeartbeat(byte pingId)
        {
            Message message = Message.CreateInternal(HeaderType.heartbeat);
            message.Add(pingId);

            server.Send(message, this);
        }

        internal void HandleHeartbeat(Message message)
        {
            SendHeartbeat(message.GetByte());

            Rudp.RTT = message.GetShort();
            lastHeartbeat = DateTime.UtcNow;
        }

        internal void SendWelcome()
        {
            Message message = Message.CreateInternal(HeaderType.welcome);
            message.Add(Id);

            server.Send(message, this, 5);
        }

        internal void HandleWelcomeReceived(Message message)
        {
            if (connectionState == ConnectionState.connected)
                return;

            ushort id = message.GetUShort();

            if (Id != id)
                RiptideLogger.Log(server.LogName, $"Client has assumed incorrect ID: {id}");

            connectionState = ConnectionState.connected;
            server.OnClientConnected(new ServerClientConnectedEventArgs(this));
        }
        #endregion
    }
}
