using System;
using Brisk.Actions;
using Brisk.Entities;
using Brisk.Serialization;
using Lidgren.Network;
using UnityEngine;

namespace Brisk.Messages
{
    public sealed class Messages
    {
        internal int Count { get; private set; }

        private readonly NetPeer peer;
        private readonly ActionSet actionSet;
        private readonly Predicate<NetConnection> ready;


        internal void ResetCount() => Count = 0;
        
        internal Messages(NetPeer peer, ActionSet actionSet, Predicate<NetConnection> ready)
        {
            this.peer = peer;
            this.actionSet = actionSet;
            this.ready = ready;
        }

        private void SendMessage(NetConnection connection, NetOp op, NetDeliveryMethod method, bool onlyReady, Action<NetOutgoingMessage> data = null)
        {
            if (onlyReady && ready != null && !ready(connection)) return;
            
            var msg = peer.CreateMessage();
            msg.Write((byte) op);
            data?.Invoke(msg);
            connection.SendMessage(msg, method, 0);
            Count++;
        }

        internal void SystemInfo(NetConnection connection)
        {
            SendMessage(connection, NetOp.SystemInfo, NetDeliveryMethod.ReliableUnordered, false);
        }

        internal void SystemInfo(NetConnection connection, RuntimePlatform platform)
        {
            SendMessage(connection, NetOp.SystemInfo, NetDeliveryMethod.ReliableUnordered, false, msg =>
            {
                msg.Write((byte) platform);
            });
        }

        internal void StringsStart(NetConnection connection, int size)
        {
            SendMessage(connection, NetOp.StringsStart, NetDeliveryMethod.ReliableUnordered, false, msg =>
            {
                msg.Write(size);
            });
        }

        internal void StringsStart(NetConnection connection)
        {
            SendMessage(connection, NetOp.StringsStart, NetDeliveryMethod.ReliableUnordered, false);
        }

        internal void StringsData(NetConnection connection, int id, string str)
        {
            SendMessage(connection, NetOp.StringsData, NetDeliveryMethod.ReliableUnordered, false, msg =>
            {
                msg.Write(id);
                msg.Write(str);
            });
        }

        internal void AssetsStart(NetConnection connection, int size)
        {
            SendMessage(connection, NetOp.AssetsStart, NetDeliveryMethod.ReliableUnordered, false, msg =>
            {
                msg.Write(size);
            });
        }

        internal void AssetsStart(NetConnection connection, RuntimePlatform platform)
        {
            SendMessage(connection, NetOp.AssetsStart, NetDeliveryMethod.ReliableUnordered, false, msg =>
            {
                msg.Write((byte) platform);
            });
        }

        internal void AssetsData(NetConnection connection, int start, int length, byte[] data)
        {
            SendMessage(connection, NetOp.AssetsData, NetDeliveryMethod.ReliableUnordered, false, msg =>
            {
                msg.Write(start);
                msg.Write(length);
                msg.Write(data);
            });
        }

        internal void InstantiateEntity(int assetId, Vector3? position = null, Quaternion? rotation = null, Vector3? scale = null)
        {
            switch (peer)
            {
                case NetClient _ when peer.ConnectionsCount > 0:
                    SendMessage(peer.Connections[0], NetOp.InstantiateEntity, NetDeliveryMethod.ReliableUnordered, false, msg =>
                    {
                        msg.Write(assetId);
                        msg.Write(position.HasValue);
                        msg.Write(rotation.HasValue);
                        msg.Write(scale.HasValue);
                        if (position.HasValue)
                        {
                            msg.Write(position.Value.x);
                            msg.Write(position.Value.y);
                            msg.Write(position.Value.z);
                        }
                        if (rotation.HasValue)
                        {
                            msg.Write(rotation.Value.x);
                            msg.Write(rotation.Value.y);
                            msg.Write(rotation.Value.z);
                            msg.Write(rotation.Value.w);
                        }
                        if (scale.HasValue)
                        {
                            msg.Write(scale.Value.x);
                            msg.Write(scale.Value.y);
                            msg.Write(scale.Value.z);
                        }
                    });
                    break;
                case NetClient _:
                    Debug.LogWarning("Not connected");
                    break;
                case NetServer _:
                    Debug.LogWarning("InstantiateEntity is called from the client");
                    break;
                default:
                    Debug.LogError($"Peer is an unknown type: {peer.GetType()}");
                    break;
            }
        }

        internal void NewEntity(NetConnection connection, int assetId, int entityId, bool owner)
        {
            SendMessage(connection, NetOp.NewEntity, NetDeliveryMethod.ReliableUnordered, true, msg =>
            {
                msg.Write(assetId);
                msg.Write(entityId);
                msg.Write(owner);
            });
        }

        internal void EntityUpdate(NetConnection connection, Serializer serializer, NetEntity entity)
        {
            if (ready != null && !ready(connection)) return;
            
            var msg = peer.CreateMessage();
            entity.Serialize(serializer, msg, true, true);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
            Count++;
        }

        internal void Ready(NetConnection connection)
        {
            SendMessage(connection, NetOp.Ready, NetDeliveryMethod.ReliableUnordered, false);
        }

        public void ActionLocal(int actionId, int entityId, byte behaviourId, params object[] args)
        {
            switch (peer)
            {
                case NetClient _ when peer.ConnectionsCount > 0:
                    SendMessage(peer.Connections[0], NetOp.ActionLocal, NetDeliveryMethod.ReliableUnordered, true, 
                        msg =>
                        {
                            msg.Write(actionId);
                            msg.Write(entityId);
                            msg.Write(behaviourId);
                            actionSet.Serialize(msg, args);
                        });
                    break;
                case NetClient _:
                    Debug.LogWarning("Not connected");
                    break;
                case NetServer _:
                    /*foreach (var connection in peer.Connections)
                    {
                        SendMessage(connection, NetOp.ActionLocal, NetDeliveryMethod.ReliableUnordered, true, msg =>
                        {
                            msg.Write(actionId);
                            msg.Write(entityId);
                            msg.Write(behaviourId);
                            actionSet.Serialize(msg, args);
                        });
                    }*/
                    Debug.LogWarning("Local actions are called from the client");
                    break;
                default:
                    Debug.LogError($"Peer is an unknown type: {peer.GetType()}");
                    break;
            }
        }
    }
}