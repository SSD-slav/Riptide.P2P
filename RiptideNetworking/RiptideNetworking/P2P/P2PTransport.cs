using System;

namespace RiptideNetworking.Experimental.P2P
{
    public class MessageArgs : EventArgs
    {
        public Message message;

        public MessageArgs(Message message)
        {
            this.message = message;
        }
    }

    public abstract class P2PTransport
    {
        public long myGUID;
        public string ip;
        public ushort port;
        public event EventHandler<MessageArgs> MassageReceived;
        public event EventHandler<OnReadyArgs> OnReady;

        public P2PTransport(string ip, ushort port = 7550)
        {
            this.ip = ip;
            this.port = port;
        }

        protected P2PTransport() { port = 7550; }

        internal void MsgReceived(MessageArgs e)
        {
            MassageReceived.Invoke(this, e);
        }
        internal void Ready(OnReadyArgs e)
        {
            OnReady.Invoke(this, e);
        }
        public abstract void SendToPeer(long GUID, Message message);
        public abstract void BroudcastToNetwork(Message message);
        public abstract void Tick();
    }
}
