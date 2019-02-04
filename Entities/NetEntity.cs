using Lidgren.Network;
using UnityEngine;

namespace Brisk.Entities
{
    public sealed class NetEntity : MonoBehaviour
    {
        [SerializeField] private UpdateType updateType = UpdateType.None;
        
        public int Id { get; internal set; } = 0;
        public int AssetId { get; internal set; } = 0;
        public bool Owner { get; internal set; }
        public bool Dirty => prevPosition != transform.position;
        
        private NetBehaviour[] behaviours;
        private Vector3 prevPosition;
        
        private void Start()
        {
            behaviours = GetComponents<NetBehaviour>();
        }
        
        public void Serialize(NetOutgoingMessage msg)
        {
            prevPosition = transform.position;
            
            msg.Write((byte)NetOp.EntityUpdate);
            msg.Write(Id);
            msg.Write(transform.position.x);
            msg.Write(transform.position.y);
            msg.Write(transform.position.z);
        }
    }
}