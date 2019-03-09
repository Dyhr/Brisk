using Brisk.Actions;
using Brisk.Assets;
using Brisk.Messages;
using Brisk.Serialization;
using Lidgren.Network;
using UnityEngine;

namespace Brisk.Entities
{
    [DisallowMultipleComponent]
    public class NetEntity : NetBehaviour
    {
        public new Peer Peer { get; private set; }
        public int Id { get; private set; }
        public int AssetId { get; private set; }
        public bool Owner { get; private set; }

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
        [SyncUnreliable]
        public Vector3 Scale
        {
            get => transform.localScale;
            set => transform.localScale = value;
        }
        //[SyncUnreliable]
        public Vector3 Velocity
        {
            get => rigidbody != null ? rigidbody.velocity : Vector3.zero;
            set
            {
                if (rigidbody != null) rigidbody.velocity = value;
            }
        }
        //[SyncUnreliable]
        public Vector3 AngularVelocity
        {
            get => rigidbody != null ? rigidbody.angularVelocity : Vector3.zero;
            set
            {
                if (rigidbody != null) rigidbody.angularVelocity = value;
            }
        }
        
        internal bool netDestroyed;
        internal byte[] unreliableBytes;
        internal byte[] reliableBytes;

        private NetBehaviour[] behaviours;
        private new Rigidbody rigidbody;
        

        internal static NetEntity Create(Peer peer, AssetManager assets, int assetId, int entityId = 0, bool mine = false)
        {
            if (entityId == 0) entityId = peer.NextEntityId;
            
            var asset = assets[assetId];
            if (asset == null)
            {
                Debug.LogError("Asset not found for id: "+assetId);
                return null;
            }
            
            var gameObject = Instantiate(asset);
            var entity = gameObject.GetComponent<NetEntity>();

            if (entity == null)
            {
                Debug.LogError("Cannot create asset without an Entity for id: "+assetId);
                DestroyImmediate(gameObject);
                return null;
            }

            entity.Peer = peer;
            entity.Id = entityId;
            entity.AssetId = assetId;
            entity.Owner = mine;
            
            if (entity.Owner) peer.ownedEntities.Add(entity);
            peer.entities.Add(entityId, entity);
            return entity;
        }
        
        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
            behaviours = GetComponentsInChildren<NetBehaviour>(true);
            foreach (var behaviour in behaviours) behaviour.Entity = this;
        }

        public void SerializeReliable(Serializer serializer, NetOutgoingMessage msg)
        {
            msg.Write((byte)NetOp.EntityUpdate);
            msg.Write(Id);
            msg.Write(true);

            foreach (var behaviour in behaviours) 
                serializer.SerializeReliable(behaviour, msg);
        }
        public void SerializeUnreliable(Serializer serializer, NetOutgoingMessage msg)
        {
            msg.Write((byte)NetOp.EntityUpdate);
            msg.Write(Id);
            msg.Write(false);

            foreach (var behaviour in behaviours) 
                serializer.SerializeUnreliable(behaviour, msg);
        }

        public void DeserializeReliable(Serializer serializer, NetIncomingMessage msg)
        {
            foreach (var behaviour in behaviours)
                serializer.DeserializeReliable(behaviour, msg);
        }
        public void DeserializeUnreliable(Serializer serializer, NetIncomingMessage msg)
        {
            foreach (var behaviour in behaviours)
                serializer.DeserializeUnreliable(behaviour, msg);
        }

        private void OnApplicationQuit()
        {
            netDestroyed = true;
        }

        private void OnDestroy()
        {
            if (Peer == null) return;
            if (!netDestroyed)
                Peer.DestroyEntity(this);
            Peer.entities.Remove(Id);
        }

        [Action(false)]
        public void Destroy()
        {
            netDestroyed = true;
            Destroy(gameObject);
        }

        public NetBehaviour Behaviour(byte behaviourId)
        {
            return behaviourId >= behaviours.Length ? null : behaviours[behaviourId];
        }

        public byte Behaviour(NetBehaviour behaviour)
        {
            for(byte i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == behaviour)
                    return i;
            }
            return byte.MaxValue;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (Peer == null) return;
            if (Peer.IsServer) return;
            var entity = other.gameObject.GetComponent<NetEntity>();
            if (entity != null && entity.Owner)
                Peer.syncEntities.Add(this);
        }
    }
}