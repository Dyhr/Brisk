using System.Collections.Generic;
using Brisk.Assets;
using Brisk.Config;
using Lidgren.Network;
using UnityEngine;

namespace Brisk
{
    public class Server : MonoBehaviour
    {
        [SerializeField] private ServerConfig config = null;
        [SerializeField] private TextAsset level = null;

        private readonly Peer<NetServer> server = new Peer<NetServer>();
        private readonly Dictionary<NetConnection, ConnectionInfo> clients = new Dictionary<NetConnection, ConnectionInfo>();

        private int nextUserId;


        private void Start()
        {
            // Set up some logs and listeners
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
            
            server.Connected += ServerOnConnected;
            server.Disconnected += ServerOnDisconnected;
            server.Data += ServerOnData;
            
            // Load the assets
            server.assetManager.LoadFromFile($"{Application.streamingAssetsPath}/assets");

            // Load the level
            Baseline.Load(server.assetManager, server.entityManager, level);
            Debug.Log($@"Map ""{level.name}"" loaded");
            
            // Actually start the server
            var success = server.Start(ref config, true);
            if (!success) return;
            Debug.Log($"Server running on port {config.Port}");
        }

        private void OnDestroy()
        {
            server.Connected -= ServerOnConnected;
            server.Disconnected -= ServerOnDisconnected;
            server.Data -= ServerOnData;
            Debug.Log("Server shutting down");
            server.Stop("Server shutting down");
        }

        private void FixedUpdate()
        {
            server.Receive();
        }

        private void ServerOnConnected(NetConnection connection)
        {
            clients.Add(connection, new ConnectionInfo(++nextUserId));

            var msg = server.NetPeer.CreateMessage();
            msg.Write((byte) NetOp.SystemInfo);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
            
            msg = server.NetPeer.CreateMessage();
            msg.Write((byte) NetOp.StringsStart);
            msg.Write(server.assetManager.StringsLength);
            connection.SendMessage(msg, NetDeliveryMethod.ReliableUnordered, 0);
            
            Debug.Log(connection.RemoteEndPoint + " connected");
        }

        private void ServerOnDisconnected(NetConnection connection)
        {
            clients.Remove(connection);
            Debug.Log(connection.RemoteEndPoint + " disconnected");
        }

        private void ServerOnData(ref NetMessage msg)
        {
            RuntimePlatform platform;
            NetConnection connection;
            switch (msg.op)
            {
                case NetOp.SystemInfo:
                    platform = (RuntimePlatform) msg.msg.ReadByte();
                    if (server.assetManager.Available(platform))
                    {
                        msg.res.Write((byte)NetOp.AssetsStart);
                        msg.res.Write(server.assetManager.Size(platform));
                    }
                    break;
                case NetOp.StringsStart:
                    connection = msg.msg.SenderConnection;
                    StartCoroutine(server.assetManager.SendStrings( 
                        msg.msg.SenderConnection.AverageRoundtripTime, (i, s) =>
                        {
                            var m = server.NetPeer.CreateMessage();
                            m.Write((byte) NetOp.StringsData);
                            m.Write(i);
                            m.Write(s);

                            connection.SendMessage(m, NetDeliveryMethod.ReliableUnordered, 0);
                        }));
                    break;
                case NetOp.AssetsStart:
                    platform = (RuntimePlatform) msg.msg.ReadByte();
                    if (server.assetManager.Available(platform))
                    {
                        connection = msg.msg.SenderConnection;
                        StartCoroutine(server.assetManager.SendAssetBundle(
                            platform, 
                            msg.msg.SenderConnection.CurrentMTU - 100, 
                            msg.msg.SenderConnection.AverageRoundtripTime, (start, length, data) =>
                            {
                                var m = server.NetPeer.CreateMessage();
                                m.Write((byte) NetOp.AssetsData);
                                m.Write(start);
                                m.Write(length);
                                m.Write(data);

                                connection.SendMessage(m, NetDeliveryMethod.ReliableUnordered, 0);
                            }));
                    }

                    break;
                case NetOp.Ready:
                    clients[msg.msg.SenderConnection].ready = true;
                    Debug.Log(msg.msg.SenderEndPoint + " is ready");

                    foreach (var entity in server.entityManager)
                    {
                        var m = server.NetPeer.CreateMessage();
                        m.Write((byte) NetOp.NewEntity);
                        m.Write(entity.AssetId);
                        m.Write(entity.Id);
                        m.Write(false);
                        msg.msg.SenderConnection.SendMessage(m, NetDeliveryMethod.ReliableUnordered, 0);
                        
                        m = server.NetPeer.CreateMessage();
                        m.Write((byte) NetOp.EntityUpdate);
                        m.Write(entity.Id);
                        m.Write(transform.position.x);
                        m.Write(transform.position.y);
                        m.Write(transform.position.z);
                        msg.msg.SenderConnection.SendMessage(m, NetDeliveryMethod.ReliableUnordered, 0);
                    }

                    var assetId = server.assetManager["PlayerController"];
                    var entityId = msg.msg.SenderEndPoint.Port;

                    server.entityManager.CreateEntity(server.assetManager, assetId, entityId);
                    
                    msg.res.Write((byte) NetOp.NewEntity);
                    msg.res.Write(assetId);
                    msg.res.Write(entityId);
                    msg.res.Write(true);

                    foreach (var conn in clients)
                    {
                        if (conn.Key == msg.msg.SenderConnection) continue;
                        if (!conn.Value.ready) continue;

                        var m = server.NetPeer.CreateMessage();
                        m.Write((byte) NetOp.NewEntity);
                        m.Write(assetId);
                        m.Write(entityId);
                        m.Write(false);
                        conn.Key.SendMessage(m, NetDeliveryMethod.ReliableUnordered, 0);
                    }
                    break;
                case NetOp.EntityUpdate:
                    var id = msg.msg.ReadInt32();
                    var pos = new Vector3(msg.msg.ReadFloat(),msg.msg.ReadFloat(),msg.msg.ReadFloat());
                    server.entityManager[id].transform.position = pos;
                    
                    foreach (var conn in clients)
                    {
                        if (conn.Key == msg.msg.SenderConnection) continue;
                        if (!conn.Value.ready) continue;
                        
                        var m = server.NetPeer.CreateMessage();
                        m.Write((byte) NetOp.EntityUpdate);
                        m.Write(id);
                        m.Write(pos.x);
                        m.Write(pos.y);
                        m.Write(pos.z);
                        conn.Key.SendMessage(m, NetDeliveryMethod.UnreliableSequenced, 0);
                    }
                    break;
                default:
                    Debug.LogWarning("Unknown operation: "+msg.op);
                    break;
            }
        }
    }
}