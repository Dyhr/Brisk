using Brisk.Actions;
using Brisk.Messages;
using Brisk.Serialization;
using Lidgren.Network;
using UnityEngine;

namespace Brisk.Entities
{
    public class NetEntity : NetBehaviour
    {
        public Peer Peer { get; internal set; }
        public int Id { get; internal set; }
        public int AssetId { get; internal set; }
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

        private void Start()
        {
            if(Owner) Peer.Messages.ActionLocal(0, Id);
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

        [Action]
        public void Destroy()
        {
            Destroy(gameObject);
        }
    }
}