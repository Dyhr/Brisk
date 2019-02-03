using Lidgren.Network;

namespace Brisk
{
    public struct NetMessage
    {
        public NetOutgoingMessage res => response ?? (response = peer.CreateMessage());
        public bool hasResponse => response != null;

        public readonly NetOp op;
        public readonly NetIncomingMessage msg;

        private readonly NetPeer peer;
        private NetOutgoingMessage response;

        public NetMessage(NetPeer peer, byte op, NetIncomingMessage msg)
        {
            this.peer = peer;
            this.op = (NetOp) op;
            this.msg = msg;
            
            response = null;
        }
    }
}