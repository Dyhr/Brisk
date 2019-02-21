using UnityEngine;

namespace Brisk.Entities
{
    [RequireComponent(typeof(NetEntity))]
    public abstract class NetBehaviour : MonoBehaviour
    {
        public NetEntity Entity { get; internal set; }
        public Peer Peer => Entity != null ? Entity.Peer : null;
    }
}