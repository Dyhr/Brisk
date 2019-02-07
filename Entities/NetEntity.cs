using Brisk.Serialization;
using Lidgren.Network;
using UnityEngine;

namespace Brisk.Entities
{
    public sealed class NetEntity : NetBehaviour
    {
        [SerializeField] private UpdateType updateType = UpdateType.None;
        
        public int Id { get; internal set; } = 0;
        public int AssetId { get; internal set; } = 0;
        public bool Owner { get; internal set; }
        public bool Dirty => prevPosition != transform.position || prevRotation != transform.eulerAngles;

        [SyncUnreliable]
        public Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }

        private NetBehaviour[] behaviours;
        private Vector3 prevPosition;
        private Vector3 prevRotation;
        
        private void Start()
        {
            behaviours = GetComponents<NetBehaviour>();
        }
        
        public void Serialize(NetOutgoingMessage msg)
        {
            prevPosition = transform.position;
            prevRotation = transform.eulerAngles;
            
            msg.Write((byte)NetOp.EntityUpdate);
            msg.Write(Id);
            msg.Write(transform.position.x);
            msg.Write(transform.position.y);
            msg.Write(transform.position.z);
            msg.Write(transform.eulerAngles.x);
            msg.Write(transform.eulerAngles.y);
            msg.Write(transform.eulerAngles.z);
        }

        public void Deserialize(NetIncomingMessage msg)
        {
            transform.position = new Vector3(msg.ReadFloat(),msg.ReadFloat(),msg.ReadFloat());
            transform.eulerAngles = new Vector3(msg.ReadFloat(),msg.ReadFloat(),msg.ReadFloat());
        }
    }
}