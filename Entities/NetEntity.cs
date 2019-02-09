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
        [SyncUnreliable]
        public Vector3 Rotation
        {
            get => transform.eulerAngles;
            set => transform.eulerAngles = value;
        }

        private NetBehaviour[] behaviours;
        private Vector3 prevPosition;
        private Vector3 prevRotation;
        
        private void Awake()
        {
            behaviours = GetComponentsInChildren<NetBehaviour>(true);
        }
        
        public void Serialize(Serializer serializer, NetOutgoingMessage msg)
        {
            prevPosition = transform.position;
            prevRotation = transform.eulerAngles;
            
            msg.Write((byte)NetOp.EntityUpdate);
            msg.Write(Id);

            foreach (var behaviour in behaviours) 
            {
                serializer.SerializeReliable(behaviour, msg);
                serializer.SerializeUnreliable(behaviour, msg);
            }
        }

        public void Deserialize(Serializer serializer, NetIncomingMessage msg)
        {
            foreach (var behaviour in behaviours)
            {
                serializer.DeserializeReliable(behaviour, msg);
                serializer.DeserializeUnreliable(behaviour, msg);
            }
        }
    }
}