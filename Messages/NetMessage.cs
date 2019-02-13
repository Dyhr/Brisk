using Lidgren.Network;

namespace Brisk.Messages
{
    public struct NetMessage
    {
        public readonly NetOp op;
        public readonly NetIncomingMessage msg;

        public NetConnection Connection => msg.SenderConnection;

        public NetMessage(byte op, NetIncomingMessage msg)
        {
            this.op = (NetOp) op;
            this.msg = msg;
        }
    }
}