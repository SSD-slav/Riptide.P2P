using System;

namespace RiptideNetworking.Experimental.P2P
{
    public class OnReadyArgs : EventArgs
    {
        
    }
    public class P2PNetwork
    {
        private P2PTransport _transport;
        public long MyGUID => _transport.myGUID;
        public event EventHandler<MessageArgs> MessageReceived;
        public event EventHandler<OnReadyArgs> OnReady;

        public P2PNetwork(string EntryPeerIP, P2PTransport transport = null)
        {
            if (transport == null)
                _transport = new BasKad(EntryPeerIP);
            else
                _transport = transport;
            _transport.MassageReceived += MessageReceived;
            _transport.OnReady += OnReady;
        }

        public void SendToPeer(long GUID, Message message)
        {
            _transport.SendToPeer(GUID, message);
        }

        public void BroudcastToNetwork(Message message)
        {
            _transport.BroudcastToNetwork(message);
        }

        public void Tick()
        {
            _transport.Tick();
        }
    }
}
