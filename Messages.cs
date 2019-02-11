using System;
using Brisk.Entities;
using Brisk.Serialization;
using Lidgren.Network;
using UnityEngine;

namespace Brisk
{
    public class Messages
    {
        internal int Count { get; private set; }

        private readonly NetPeer peer;


        internal void ResetCount() => Count = 0;
        
        internal Messages(NetPeer peer)
        {
            this.peer = peer;
        }

        private void SendMessage(NetConnection connection, NetOp op, NetDeliveryMethod method, Action<NetOutgoingMessage> data = null)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) op);
            data?.Invoke(msg);
            connection.SendMessage(msg, method, 0);
            Count++;
        }

        internal void SystemInfo(NetConnection connection)
        {
            SendMessage(connection, NetOp.SystemInfo, NetDeliveryMethod.ReliableUnordered);
        }

        public void SystemInfo(NetConnection connection, RuntimePlatform platform)
        {
            SendMessage(connection, NetOp.SystemInfo, NetDeliveryMethod.ReliableUnordered, msg =>
            {
                msg.Write((byte) platform);
            });
        }

        internal void StringsStart(NetConnection connection, int size)
        {
            SendMessage(connection, NetOp.StringsStart, NetDeliveryMethod.ReliableUnordered, msg =>
            {
                msg.Write(size);
            });
        }

        internal void StringsStart(NetConnection connection)
        {
            SendMessage(connection, NetOp.StringsStart, NetDeliveryMethod.ReliableUnordered);
        }

        internal void StringsData(NetConnection connection, int id, string str)
        {
            SendMessage(connection, NetOp.StringsData, NetDeliveryMethod.ReliableUnordered, msg =>
            {
                msg.Write(id);
                msg.Write(str);
            });
        }

        internal void AssetsStart(NetConnection connection, int size)
        {
            SendMessage(connection, NetOp.AssetsStart, NetDeliveryMethod.ReliableUnordered, msg =>
            {
                msg.Write(size);
            });
        }

        internal void AssetsStart(NetConnection connection, RuntimePlatform platform)
        {
            SendMessage(connection, NetOp.AssetsStart, NetDeliveryMethod.ReliableUnordered, msg =>
            {
                msg.Write((byte) platform);
            });
        }

        internal void AssetsData(NetConnection connection, int start, int length, byte[] data)
        {
            SendMessage(connection, NetOp.AssetsData, NetDeliveryMethod.ReliableUnordered, msg =>
            {
                msg.Write(start);
                msg.Write(length);
                msg.Write(data);
            });
        }

        internal void NewEntity(NetConnection connection, int assetId, int entityId, bool owner)
        {
            SendMessage(connection, NetOp.NewEntity, NetDeliveryMethod.ReliableUnordered, msg =>
            {
                msg.Write(assetId);
                msg.Write(entityId);
                msg.Write(owner);
            });
        }

        internal void EntityUpdate(NetConnection connection, Serializer serializer, NetEntity entity)
        {
            var msg = peer.CreateMessage();
            entity.Serialize(serializer, msg, true, true);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
            Count++;
        }

        internal void Ready(NetConnection connection)
        {
            SendMessage(connection, NetOp.Ready, NetDeliveryMethod.ReliableUnordered);
        }
    }
}