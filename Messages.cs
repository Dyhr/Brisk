using System;
using Brisk.Entities;
using Brisk.Serialization;
using Lidgren.Network;
using UnityEngine;

namespace Brisk
{
    public class Messages
    {
        private readonly NetPeer peer;
        
        internal Messages(NetPeer peer)
        {
            this.peer = peer;
        }

        private void SendMeesage(NetConnection connection, NetOp op, NetDeliveryMethod method, Action<NetOutgoingMessage> data)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) op);
            data(msg);
            connection.SendMessage(msg, method, 0);
        }

        internal void SystemInfo(NetConnection connection)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) NetOp.SystemInfo);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        public void SystemInfo(NetConnection connection, RuntimePlatform platform)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) NetOp.SystemInfo);
            msg.Write((byte) platform);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        internal void StringsStart(NetConnection connection, int size)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) NetOp.StringsStart);
            msg.Write(size);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        internal void StringsStart(NetConnection connection)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) NetOp.StringsStart);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        internal void StringsData(NetConnection connection, int id, string str)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) NetOp.StringsData);
            msg.Write(id);
            msg.Write(str);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        internal void AssetsStart(NetConnection connection, int size)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) NetOp.AssetsStart);
            msg.Write(size);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        internal void AssetsStart(NetConnection connection, RuntimePlatform platform)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) NetOp.AssetsStart);
            msg.Write((byte) platform);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        internal void AssetsData(NetConnection connection, int start, int length, byte[] data)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) NetOp.AssetsData);
            msg.Write(start);
            msg.Write(length);
            msg.Write(data);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        internal void NewEntity(NetConnection connection, int assetId, int entityId, bool owner)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte) NetOp.NewEntity);
            msg.Write(assetId);
            msg.Write(entityId);
            msg.Write(owner);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        internal void EntityUpdate(NetConnection connection, Serializer serializer, NetEntity entity)
        {
            var msg = peer.CreateMessage();
            entity.Serialize(serializer, msg, true, true);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }

        internal void Ready(NetConnection connection)
        {
            var msg = peer.CreateMessage();
            msg.Write((byte)NetOp.Ready);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
        }
    }
}