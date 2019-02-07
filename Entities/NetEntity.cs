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
        
        public void Serialize(Serializer serializer, NetOutgoingMessage msg)
        {
            prevPosition = transform.position;
            prevRotation = transform.eulerAngles;
            
            msg.Write((byte)NetOp.EntityUpdate);
            msg.Write(Id);

            serializer.SerializeUnreliable(this, msg);
        }

        public void Deserialize(Serializer serializer, NetIncomingMessage msg)
        {
            serializer.DeserializeUnreliable(this, msg);
        }
    }
}