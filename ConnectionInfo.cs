using Lidgren.Network;

namespace Brisk
{
    public class ConnectionInfo
    {
        public int id;
        public bool ready;

        public ConnectionInfo(int id)
        {
            this.id = id;
            this.ready = false;
        }
    }
}