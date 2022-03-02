using System.Net;
using RiptideNetworking.Transports;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RiptideNetworking.Experimental.P2P
{

    /// <summary>
    /// Basic Kademlia
    /// </summary>
    public class BasKad : P2PTransport
    {
        struct Peer
        {
            public Client client;
            public long GUID;

            public Peer(long guid, Client client)
            {
                GUID = guid;
                this.client = client;
            }
        }

        enum MsgId : ushort
        {
            ToAll,
            ToPeer,
            NewPeer,
            NewPeerToAll,
            PeerLeft
        }

        private Server server = new Server();
        private List<Peer> peers = new List<Peer>();
        private long MyGUID = 0;

        private long peersInNetworkCount = 1;
        private List<long> EmptySpacesInNetwork = new List<long>();


        public BasKad(string EntryPeerIP, ushort port = 7550)
        {
            ip = EntryPeerIP;
            this.port = port;

            server.MessageReceived += ServerMsgReceived;
            server.Start(port, 100);
            
            Client client = new Client();

            client.Connected += (sender, args) =>
            {
                Console.WriteLine("Debug 1");
                Message message = Message.Create(MessageSendMode.reliable, MsgId.NewPeer);
                message.Add(new WebClient().DownloadString("https://api.ipify.org"));
                client.Send(message);
            };
            client.MessageReceived += (sender, args) =>
            {
                Message message = args.Message;
                peersInNetworkCount = message.GetLong();
                MyGUID = message.GetLong();
                EmptySpacesInNetwork = message.GetLongs().ToList();
                client.Disconnect();
            };
            client.Connect($"{ip}:{port}");
        }


        class Msg
        {
            public long peerGUID;
            public byte[] bytes;
            public long milliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            public Msg(long peerGUID, byte[] bytes)
            {
                this.peerGUID = peerGUID;
                this.bytes = bytes;
            }
        }

        private List<Msg> MsgBuffer = new List<Msg>();

        bool CheckMessage(long GUID, byte[] bytes)
        {
            Msg msg = new Msg(GUID, bytes);
            foreach (var ms in MsgBuffer)
            {
                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - ms.milliseconds >
                    Math.Max(10 * peersInNetworkCount, 144000)) //0,4 hours; 24 minutes
                {
                    MsgBuffer.Remove(ms);
                }
                else
                {
                    if (ms.peerGUID == msg.peerGUID && ms.bytes == msg.bytes)
                    {
                        return false;
                    }
                }
            }

            MsgBuffer.Add(msg);
            return true;
        }

        void MsgToAll(Message msg, ushort FromClientId = ushort.MaxValue)
        {
            long GUID = msg.GetLong();
            byte[] bytes = msg.GetBytes(true);

            if (GUID == MyGUID) return;

            //this part is so message doesn't go in a loop
            foreach (var peer in peers)
            {
                if (CheckMessage(GUID, bytes))
                {
                    Message Msg = Message.Create(MessageSendMode.reliable, MsgId.ToAll);
                    Msg.Add(GUID);
                    Msg.Add(bytes, true, true);

                    server.Send(Msg, peer.client.Id);
                }
            }

            Message message = Message.Create();
            message.AddBytes(bytes, false);
            message.PrepareForUse((HeaderType)message.SendMode, (ushort)bytes.Length);

            MsgReceived(new MessageArgs(message));
        }

        void MsgToPeer(Message msg, ushort FromClientId = ushort.MaxValue)
        {
            long GUID = msg.GetLong();
            if (GUID == MyGUID)
            {
                byte[] bytes = msg.GetBytes(true);

                Message message = Message.Create();
                message.AddBytes(bytes, false);
                message.PrepareForUse((HeaderType)message.SendMode, (ushort)bytes.Length);

                MsgReceived(new MessageArgs(message));
            }
            else
            {
                Message message = Message.Create(MessageSendMode.reliable, MsgId.ToPeer);
                message.Add(GUID);
                message.Add(msg.GetBytes(true), true, true);

                ushort id = ushort.MaxValue;
                long dst = long.MaxValue;

                for (int i = 0; i < peers.Count; i++)
                {
                    long dst1 = Math.Abs(peers[i].GUID - GUID);

                    if (dst > dst1)
                    {
                        id = peers[i].client.Id;
                        dst = dst1;
                    }
                }

                if (id != ushort.MaxValue)
                    server.Send(message, id);
            }
        }

        void MsgNewPeer(Message msg, ushort FromClientId = ushort.MaxValue)
        {
            long NewGUID;
            if (EmptySpacesInNetwork.Count != 0)
            {
                NewGUID = EmptySpacesInNetwork[0];
                EmptySpacesInNetwork.RemoveAt(0);
            }
            else
            {
                NewGUID = peersInNetworkCount;
            }

            peersInNetworkCount++;
            Message message = Message.Create(MessageSendMode.reliable, MsgId.NewPeerToAll);
            message.Add(peersInNetworkCount);
            message.Add(NewGUID);
            message.Add(EmptySpacesInNetwork.ToArray());
            long[] GUIDs = new long[64];
            string toIP = msg.GetString();
            for (int i = 0; i < 64; i++)
            {
                GUIDs[i] = NewGUID ^ (1 << i);
                if (MyGUID == GUIDs[i])
                {
                    Client client = new Client();
                    client.Connect($"{toIP}:{port}");
                    client.MessageReceived += ClientMsgReceived;
                    peers.Add(new Peer(NewGUID, client));
                    if (peers.Count == 1) Ready(new OnReadyArgs());
                }
            }

            server.Send(message, FromClientId);

            Message message2 = Message.Create(MessageSendMode.reliable, MsgId.NewPeerToAll);
            message2.Add(NewGUID);
            message2.Add(toIP);
            message2.Add(GUIDs);
            server.SendToAll(message2);
        }

        void MsgNewPeerToAll(Message msg, ushort FromClientId = ushort.MaxValue)
        {
            long GUID = msg.GetLong();
            peersInNetworkCount++;
            EmptySpacesInNetwork.Remove(GUID);

            string toIP = msg.GetString();
            long[] guids = msg.GetLongs();

            foreach (var guid in guids)
            {
                if (MyGUID == guid)
                {
                    Client client = new Client();
                    client.Connect($"{toIP}:{port}");
                    client.MessageReceived += ClientMsgReceived;
                    peers.Add(new Peer(GUID, client));
                    if (peers.Count == 1) Ready(new OnReadyArgs());
                    break;
                }
            }

            foreach (var peer in peers)
            {
                if (CheckMessage(GUID, BitConverter.GetBytes((ushort)MsgId.NewPeerToAll)))
                {
                    Message Msg = Message.Create(MessageSendMode.reliable, MsgId.NewPeerToAll);
                    Msg.Add(GUID);
                    Msg.Add(toIP);
                    Msg.Add(guids);
                    server.Send(Msg, peer.client.Id);
                }
            }
        }

        void MsgPeerLeft(Message msg, ushort FromClientId = ushort.MaxValue)
        {
            long GUID = msg.GetLong();
            peersInNetworkCount--;
            EmptySpacesInNetwork.Add(GUID);

            foreach (var peer in peers)
            {
                if (CheckMessage(GUID, BitConverter.GetBytes((ushort)MsgId.PeerLeft)))
                {
                    Message Msg = Message.Create(MessageSendMode.reliable, MsgId.PeerLeft);
                    Msg.Add(GUID);

                    server.Send(Msg, peer.client.Id);
                }
            }
        }

        private void ClientMsgReceived(object sender, ClientMessageReceivedEventArgs e)
        {
            switch ((MsgId)e.MessageId)
            {
                case MsgId.ToAll:
                    MsgToAll(e.Message);
                    break;

                case MsgId.ToPeer:
                    MsgToPeer(e.Message);
                    break;

                case MsgId.NewPeer:
                    MsgNewPeer(e.Message);
                    break;

                case MsgId.NewPeerToAll:
                    MsgNewPeerToAll(e.Message);
                    break;

                case MsgId.PeerLeft:
                    MsgPeerLeft(e.Message);
                    break;

            }
        }

        private void ServerMsgReceived(object sender, ServerMessageReceivedEventArgs e)
        {
            switch ((MsgId)e.MessageId)
            {
                case MsgId.ToAll:
                    MsgToAll(e.Message, e.FromClientId);
                    break;

                case MsgId.ToPeer:
                    MsgToPeer(e.Message, e.FromClientId);
                    break;

                case MsgId.NewPeer:
                    MsgNewPeer(e.Message, e.FromClientId);
                    break;

                case MsgId.NewPeerToAll:
                    MsgNewPeerToAll(e.Message, e.FromClientId);
                    break;

                case MsgId.PeerLeft:
                    MsgPeerLeft(e.Message, e.FromClientId);
                    break;

            }
        }

        public override void SendToPeer(long GUID, Message message)
        {
            Message msg = Message.Create(MessageSendMode.reliable, (ushort)MsgId.ToPeer);

            msg.Add(GUID);
            msg.Add(message.Bytes, true, true);

            server.SendToAll(msg);
        }

        public override void BroudcastToNetwork(Message message)
        {
            Message msg = Message.Create(MessageSendMode.reliable, (ushort)MsgId.ToAll);

            msg.Add(MyGUID);
            msg.Add(message.Bytes, true, true);

            server.SendToAll(msg);
        }

        public override void Tick()
        {
            server.Tick();
            foreach (var peer in peers)
            {
                peer.client.Tick();
            }
        }
    }
}
