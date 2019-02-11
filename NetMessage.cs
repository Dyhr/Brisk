using Lidgren.Network;

namespace Brisk
{
    public struct NetMessage
    {
        public readonly NetOp op;
        public readonly NetIncomingMessage msg;

        public NetMessage(byte op, NetIncomingMessage msg)
        {
            this.op = (NetOp) op;
            this.msg = msg;
        }
    }
}