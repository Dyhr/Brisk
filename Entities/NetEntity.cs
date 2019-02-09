using Brisk.Serialization;
using Lidgren.Network;
using UnityEngine;

namespace Brisk.Entities
{
    public sealed class NetEntity : NetBehaviour
    {
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
        
        public void Serialize(Serializer serializer, NetOutgoingMessage msg, bool reliable, bool unreliable)
        {
            prevPosition = transform.position;
            prevRotation = transform.eulerAngles;
            
            msg.Write((byte)NetOp.EntityUpdate);
            msg.Write(Id);

            foreach (var behaviour in behaviours) 
            {
                if (reliable) serializer.SerializeReliable(behaviour, msg);
                if (unreliable) serializer.SerializeUnreliable(behaviour, msg);
            }
        }

        public void Deserialize(Serializer serializer, NetIncomingMessage msg, bool reliable, bool unreliable)
        {
            foreach (var behaviour in behaviours)
            {
                if (reliable) serializer.DeserializeReliable(behaviour, msg);
                if (unreliable) serializer.DeserializeUnreliable(behaviour, msg);
            }
        }
    }
}