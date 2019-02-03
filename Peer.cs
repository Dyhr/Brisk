using System;
using System.Net.Sockets;
using Brisk.Assets;
using Brisk.Config;
using Brisk.Entities;
using Lidgren.Network;
using UnityEngine;

namespace Brisk
{
    internal sealed class Peer<T> where T : NetPeer
    {
        public delegate void DataHandler(ref NetMessage msg);
        public event DataHandler Data;
        public delegate void ConnectionHandler(NetConnection connection);
        public event ConnectionHandler Connected;
        public event ConnectionHandler Disconnected;
        
        public T NetPeer => peer as T;
        
        public readonly AssetManager assetManager = new AssetManager();
        public readonly EntityManager entityManager = new EntityManager();

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

            return true;
        }

        public bool Connect(string host, int port)
        {
            return peer.DiscoverKnownPeer(host, port);
        }

        public void Stop(string message)
        {
            peer.Shutdown(message);
        }
        
        #endregion

        #region Messages


        #endregion

        #region Main Loop

        public void Receive()
        {
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
                        var req = new NetMessage(peer, msg.ReadByte(), msg);
                        Data?.Invoke(ref req);
                        if (req.hasResponse) peer.SendMessage(req.res, msg.SenderConnection, NetDeliveryMethod.ReliableUnordered);
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

        #endregion
    }
}