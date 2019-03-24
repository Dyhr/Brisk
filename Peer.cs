using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Brisk.Assets;
using Brisk.Entities;
using Brisk.Messages;
using Brisk.Serialization;
using Lidgren.Network;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Brisk
{
    public sealed class Peer
    {
        public delegate void PlayerHandler(IPEndPoint player);
        public event PlayerHandler PlayerConnected;
        public event PlayerHandler PlayerDisconnected;
        
        internal delegate void DataHandler(ref NetMessage msg);
        internal event DataHandler Data;
        internal delegate void ConnectionHandler(NetConnection connection);
        internal event ConnectionHandler Connected;
        internal event ConnectionHandler Disconnected;

        public bool IsServer { get; private set; }
        public Messages.Messages Messages { get; private set; }
        internal int NextEntityId => ++nextEntityId;

        internal int NumberOfConnections => peer.Connections.Count;
        internal int AverageUpdateTime
        {
            get
            {
                if (updateTimes.Count == 0) return 0;
                var avg = (int)updateTimes.Average();
                updateTimes.Clear();
                return avg;
            }
        }

        internal int AverageMessagesSent
        {
            get
            {
                if (messageCount.Count == 0) return 0;
                var sum = (int)messageCount.Sum();
                messageCount.Clear();
                return sum;
            }
        }
        
        internal readonly AssetManager assetManager = new AssetManager();
        internal readonly Dictionary<int, NetEntity> entities = new Dictionary<int, NetEntity>();
        internal readonly List<NetEntity> ownedEntities = new List<NetEntity>();
        internal readonly List<NetEntity> syncEntities = new List<NetEntity>();

        private readonly List<int> messageCount = new List<int>();
        private readonly List<int> memoryUsage = new List<int>();
        private readonly List<long> updateTimes = new List<long>();
        private readonly Stopwatch updateWatch = new Stopwatch();
        private Config peerConfig;
        private Predicate<NetConnection> connectionReady;
        private NetPeer peer;
        private int nextEntityId;
        

        #region Entities
        
        public int GetAssetId(string name) => assetManager[name];

        public void Instantiate(int assetId, Vector3? position = null, Quaternion? rotation = null, Vector3? scale = null)
        {
            switch (peer)
            {
                case NetClient _:
                    Messages.InstantiateEntity(assetId, position, rotation, scale);
                    break;
                case NetServer _:
                    CreateEntity(assetId, position, rotation, scale);
                    break;
                default:
                    Debug.LogError($"Peer is an unknown type: {peer.GetType()}");
                    break;
            }
        }

        internal void DestroyEntity(NetEntity entity)
        {
            switch (peer)
            {
                case NetClient _ when peer.ConnectionsCount > 0:
                    Messages.DestroyEntity(peer.Connections[0], entity.Id);
                    break;
                case NetClient _:
                    Debug.LogWarning("Not connected");
                    break;
                case NetServer _:
                    foreach (var connection in peer.Connections)
                        Messages.DestroyEntity(connection, entity.Id);
                    break;
                default:
                    Debug.LogError($"Peer is an unknown type: {peer.GetType()}");
                    break;
            }
        }

        internal void CreateEntity(int assetId, Vector3? position, Quaternion? rotation, Vector3? scale, IPEndPoint owner = null)
        {
            var entity = NetEntity.Create(this, assetManager, assetId);

            if (position.HasValue) entity.transform.position = position.Value;
            if (rotation.HasValue) entity.transform.rotation = rotation.Value;
            if (scale.HasValue) entity.transform.localScale = scale.Value;
                    
            foreach (var conn in peer.Connections)
            {
                if (!connectionReady(conn)) continue;

                Messages.NewEntity(conn, assetId, entity.Id, Equals(conn.RemoteEndPoint, owner));
                Messages.EntityUpdate(conn, peerConfig.Serializer, entity, true);
                Messages.EntityUpdate(conn, peerConfig.Serializer, entity, false);
            }
        }
        
        #endregion

        #region Lifecycle
        
        internal bool Start<T>(ref Config config, bool isHost, Predicate<NetConnection> connectionPredicate) 
            where T : NetPeer
        {
            connectionReady = connectionPredicate;
            
            // Find some Config in Resources if it's not already assigned
            if (config == null)
            {
                var allSettings = Resources.FindObjectsOfTypeAll<Config>();
                if (allSettings.Length == 0)
                {
                    Debug.LogError(
                        Application.isEditor
                            ? @"No Server Config found. Please create one using the ""Assets/Create/Network/Server Config"" menu item."
                            : @"No Server Config found.");
                    return false;
                }

                if (allSettings.Length > 1)
                {
                    Debug.LogError(
                        "More than one Server Config found in Resources. Please delete or assign one.");
                    return false;
                }

                config = allSettings[0];
            }
            
            if(config != null) config.Load();
            peerConfig = config;
            
            // Prepare a config
            var netConfig = new NetPeerConfiguration(config.GetString("app_name"));
            if (isHost)
            {
                netConfig.Port = config.GetInt("port_game");
                netConfig.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            }
            else
            {
                netConfig.EnableMessageType(NetIncomingMessageType.DiscoveryResponse);
            }

            // Start the server
            peer = (T)Activator.CreateInstance(typeof(T), netConfig);
            IsServer = peer is NetServer;
            try
            {
                peer.Start();
            }
            catch (SocketException e)
            {
                Debug.LogError(e.Message);
                return false;
            }
            
            Messages = new Messages.Messages(peer, config.ActionSet, connectionReady);

            return true;
        }

        internal bool Connect(string host, int port)
        {
            return peer.DiscoverKnownPeer(host, port);
        }

        internal void Stop(string message)
        {
            peer?.Shutdown(message);
        }
        
        #endregion

        #region Main Loop

        internal void Receive()
        {
            if (peer == null) return;
            
            NetIncomingMessage msg;
            while ((msg = peer.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                        Debug.Log(msg.ReadString());
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        Debug.LogWarning(msg.ReadString());
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        Debug.LogError(msg.ReadString());
                        break;
                    case NetIncomingMessageType.Error:
                        Debug.LogError(msg.ReadString());
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        switch (msg.SenderConnection.Status)
                        {
                            case NetConnectionStatus.None:
                                break;
                            case NetConnectionStatus.InitiatedConnect:
                                break;
                            case NetConnectionStatus.ReceivedInitiation:
                                break;
                            case NetConnectionStatus.RespondedAwaitingApproval:
                                break;
                            case NetConnectionStatus.RespondedConnect:
                                break;
                            case NetConnectionStatus.Connected:
                                Connected?.Invoke(msg.SenderConnection);
                                break;
                            case NetConnectionStatus.Disconnecting:
                                break;
                            case NetConnectionStatus.Disconnected:
                                Disconnected?.Invoke(msg.SenderConnection);
                                break;
                            default:
                                Debug.LogWarning($"Unhandled connection status: {msg.SenderConnection.Status}");
                                break;
                        }
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        // TODO connection approval
                        break;
                    case NetIncomingMessageType.Data:
                        var req = new NetMessage(msg.ReadByte(), msg);
                        Data?.Invoke(ref req);
                        break;
                    case NetIncomingMessageType.DiscoveryRequest:
                        var response = peer.CreateMessage();
                        response.Write("Server Name");
                        peer.SendDiscoveryResponse(response, msg.SenderEndPoint);
                        break;
                    case NetIncomingMessageType.DiscoveryResponse:
                        peer.Connect(msg.SenderEndPoint);
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        // TODO keep track of latency
                        break;
                    default:
                        Debug.LogWarning($"Unhandled message type: {msg.MessageType}");
                        break;
                }
                peer.Recycle(msg);
            }
        }

        internal IEnumerator UpdateEntities()
        {
            var updateRate = peerConfig.GetFloat("update_rate");
            
            while (true)
            {
                if(updateTimes.Count == 0)
                    yield return new WaitForSeconds(1/updateRate);
                else
                    yield return new WaitForSeconds(1/updateRate - updateTimes[updateTimes.Count-1] * 1000);
                
                updateWatch.Start();
                
                if (peer.Connections.Count == 0) continue;
                
                foreach (var entity in syncEntities)
                    SendEntity(entity, true);
                
                foreach (var entity in ownedEntities)
                    SendEntity(entity, false);

                peer.FlushSendQueue();
                
                updateWatch.Stop();
                updateTimes.Add(updateWatch.ElapsedMilliseconds);
                updateWatch.Reset();
                messageCount.Add(Messages.Count);
                Messages.ResetCount();
            }
        }

        private void SendEntity(NetEntity entity, bool force)
        {
            SendEntityUpdate(force, ref entity.unreliableBytes, entity.SerializeUnreliable,
                NetDeliveryMethod.UnreliableSequenced);
            SendEntityUpdate(force, ref entity.reliableBytes, entity.SerializeReliable,
                NetDeliveryMethod.ReliableSequenced);
        }

        private void SendEntityUpdate(bool force, ref byte[] entityBytes, 
            Action<Serializer, NetOutgoingMessage> serialize, NetDeliveryMethod deliveryMethod)
        {
            // TODO this is generating garbage
            var msg = peer.CreateMessage();
            msg.Data = new byte[0];
            serialize(peerConfig.Serializer, msg);
            msg.WritePadBits();

            var data = msg.PeekDataBuffer();
            if (!force && CompareBytes(entityBytes, data)) return;
            
            entityBytes = new byte[data.Length];
            data.CopyTo(entityBytes, 0);
            foreach (var connection in peer.Connections) 
            {
                if (connection.Status != NetConnectionStatus.Connected) continue;
                peer.SendMessage(msg, connection, deliveryMethod);
            }
        }

        [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
        private static bool CompareBytes(byte[] arrA, byte[] arrB)
        {
            if (arrA == null || arrB == null) return false;
            if (arrA.Length != arrB.Length) return false;
            for(var i = 0; i < arrA.Length; i++)
                if (arrA[i] != arrB[i]) return false;
            return true;
        }

        #endregion

        #region Actions

        internal void HandleAction(NetIncomingMessage msg, bool global = false)
        {
            var actionId    = msg.ReadInt32();
            var entityId    = msg.ReadInt32();
            var behaviourId = msg.ReadByte();

            var entity = entities[entityId];
            if (entity == null)
            {
                Debug.LogError($"Entity not found with ID: {entityId}");
                return;
            }

            peerConfig.ActionSet.Call(entity, behaviourId, msg, actionId, out var args);

            if (!global) return;
            foreach (var connection in peer.Connections)
            {
                if (!connectionReady(connection)) continue;
                Messages.ActionClient(connection, actionId, entityId, behaviourId, args);
            }
        }

        #endregion
        
        
        internal void OnPlayerConnected(IPEndPoint player)
        {
            PlayerConnected?.Invoke(player);
        }

        internal void OnPlayerDisconnected(IPEndPoint player)
        {
            PlayerDisconnected?.Invoke(player);
        }
    }
}