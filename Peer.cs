using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using Brisk.Assets;
using Brisk.Config;
using Brisk.Entities;
using Lidgren.Network;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Brisk
{
    internal sealed class Peer<T> where T : NetPeer
    {
        public delegate void DataHandler(ref NetMessage msg);
        public event DataHandler Data;
        public delegate void ConnectionHandler(NetConnection connection);
        public event ConnectionHandler Connected;
        public event ConnectionHandler Disconnected;

        public Messages Messages { get; private set; }

        public int NumberOfConnections => peer.Connections.Count;
        public int AverageUpdateTime
        {
            get
            {
                if (updateTimes.Count == 0) return 0;
                var avg = (int)updateTimes.Average();
                updateTimes.Clear();
                return avg;
            }
        }
        
        public readonly AssetManager assetManager = new AssetManager();
        public readonly EntityManager entityManager = new EntityManager();

        private readonly List<int> messageCount = new List<int>();
        private readonly List<int> memoryUsage = new List<int>();
        private readonly List<long> updateTimes = new List<long>();
        private readonly Stopwatch updateWatch = new Stopwatch();
        private NetPeer peer;

        #region Lifecycle
        
        public bool Start(ref ServerConfig config, bool isHost)
        {
            // Find some ServerConfig in Resources if it's not already assigned
            if (config == null)
            {
                var allSettings = Resources.FindObjectsOfTypeAll<ServerConfig>();
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
                        "More than one Server Config found in Resources. Please delete on or assign one to this Server component");
                    return false;
                }

                config = allSettings[0];
            }
            
            // Prepare a config
            var netConfig = new NetPeerConfiguration(config.AppName);
            if (isHost)
            {
                netConfig.Port = config.Port;
                netConfig.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            }
            else
            {
                netConfig.EnableMessageType(NetIncomingMessageType.DiscoveryResponse);
            }

            // Start the server
            peer = (T)Activator.CreateInstance(typeof(T), netConfig);
            try
            {
                peer.Start();
            }
            catch (SocketException e)
            {
                Debug.LogError(e.Message);
                return false;
            }
            
            Messages = new Messages(peer);

            return true;
        }

        public bool Connect(string host, int port)
        {
            return peer.DiscoverKnownPeer(host, port);
        }

        public void Stop(string message)
        {
            peer?.Shutdown(message);
        }
        
        #endregion

        #region Main Loop

        public void Receive()
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

        public IEnumerator UpdateEntities(ServerConfig config)
        {
            while (true)
            {
                if(updateTimes.Count == 0)
                    yield return new WaitForSeconds(1/config.UpdateRate);
                else
                    yield return new WaitForSeconds(1/config.UpdateRate - updateTimes[updateTimes.Count-1] * 1000);
                
                updateWatch.Start();
                
                if (peer.Connections.Count == 0) continue;
                
                foreach (var entity in entityManager)
                {
                    if (!entity.Dirty) continue;

                    var unreliableMsg = peer.CreateMessage();
                    entity.Serialize(config.Serializer, unreliableMsg, true, true);
                    var reliableMsg = peer.CreateMessage();
                    entity.Serialize(config.Serializer, reliableMsg, true, true);
                
                    foreach (var connection in peer.Connections) {
                        if (connection.Status != NetConnectionStatus.Connected) continue;
                        peer.SendMessage(unreliableMsg, connection, NetDeliveryMethod.UnreliableSequenced);
                        peer.SendMessage(reliableMsg, connection, NetDeliveryMethod.ReliableSequenced);
                    }
                }

                peer.FlushSendQueue();
                
                updateWatch.Stop();
                updateTimes.Add(updateWatch.ElapsedMilliseconds);
                updateWatch.Reset();
            }
        }
        
        #endregion
    }
}