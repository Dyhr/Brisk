using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Brisk.Assets;
using Brisk.Config;
using Brisk.Entities;
using Brisk.Messages;
using Brisk.Web;
using Lidgren.Network;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Brisk
{
    public class Server : MonoBehaviour
    {
        [SerializeField] private ServerConfig config = null;
        [SerializeField] private TextAsset level = null;

        private readonly Peer server = new Peer();
        private readonly Dictionary<NetConnection, ConnectionInfo> clients = new Dictionary<NetConnection, ConnectionInfo>();
        private WebServer webServer;

        private int nextUserId;


        private void Start()
        {
            // Set up some logs and listeners
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.None);
            
            server.Connected += ServerOnConnected;
            server.Disconnected += ServerOnDisconnected;
            server.Data += ServerOnData;
            
            // Load the assets
            server.assetManager.LoadFromFile($"{Application.streamingAssetsPath}/assets");

            // Load the level
            Baseline.Load(server, server.assetManager, level);
            Debug.Log($@"Map ""{level.name}"" loaded");
            
            // Actually start the server
            var success = server.Start<NetServer>(ref config, true, ConnectionReady);
            if (!success) return;
            
            // Start the web server
            webServer = new WebServer(3550, HttpHandler);
            webServer.Run();
            Debug.Log($"Web server listening on port {webServer.Port}");
            
            // Start the routines
            StartCoroutine(server.UpdateEntities());
            StartCoroutine(StatusReport());
            
            Debug.Log($"Server running on port {config.Port}");
        }

        private string HttpHandler(HttpListenerRequest arg)
        {
            Debug.Log(arg.Url);
            return "Whatever";
        }

        private bool ConnectionReady(NetConnection connection)
        {
            return clients.TryGetValue(connection, out var info) && info.ready;
        }

        private void OnDestroy()
        {
            webServer.Stop();
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

            server.Messages.SystemInfo(connection);
            
            Debug.Log(connection.RemoteEndPoint + " connected");
        }

        private void ServerOnDisconnected(NetConnection connection)
        {
            clients.Remove(connection);
            Debug.Log(connection.RemoteEndPoint + " disconnected");
        }

        private IEnumerator StatusReport()
        {
            while (true)
            {
                yield return new WaitForSeconds(config.StatusReportTime);

                var updateTime = server.AverageUpdateTime;
                
                Debug.LogFormat("Connected users: {0}. Messages sent: {4}. \n" +
                                "Memory usage: {1}KB. Update time: {2}ms. Performance score: {3:0.00}.",
                    server.NumberOfConnections, 
                    GC.GetTotalMemory(true) / 1024, 
                    updateTime, 
                    1-updateTime/(1000/config.UpdateRate),
                    server.AverageMessagesSent);
            }
        }

        private void ServerOnData(ref NetMessage msg)
        {
            var connection = msg.Connection;
            switch (msg.op)
            {
                case NetOp.SystemInfo:
                {
                    var platform = (RuntimePlatform) msg.msg.ReadByte();
                    if (server.assetManager.Available(platform))
                        server.Messages.AssetsStart(connection, server.assetManager.Size(platform));
                    // TODO handle unknown platforms
                    server.Messages.StringsStart(connection, server.assetManager.StringsLength);
                    break;
                }
                case NetOp.StringsStart:
                    StartCoroutine(server.assetManager.SendStrings( 
                        msg.msg.SenderConnection.AverageRoundtripTime, 
                        (i, s) => server.Messages.StringsData(connection, i, s)));
                    break;
                case NetOp.AssetsStart:
                {
                    var platform = (RuntimePlatform) msg.msg.ReadByte();
                    if (server.assetManager.Available(platform))
                        StartCoroutine(server.assetManager.SendAssetBundle(
                            platform, 
                            msg.msg.SenderConnection.CurrentMTU - 100, 
                            msg.msg.SenderConnection.AverageRoundtripTime, 
                            (start, length, data) => server.Messages.AssetsData(connection, start, length, data)));
                    break;
                }
                case NetOp.Ready:
                {
                    clients[msg.msg.SenderConnection].ready = true;
                    Debug.Log(msg.msg.SenderEndPoint + " is ready");

                    foreach (var entity in server.entities.Values)
                    {
                        server.Messages.NewEntity(connection, entity.AssetId, entity.Id, false);
                        server.Messages.EntityUpdate(connection, config.Serializer, entity);
                    }
                    
                    server.OnPlayerConnected(msg.msg.SenderEndPoint);
                    break;
                }
                case NetOp.EntityUpdate:
                {
                    var id = msg.msg.ReadInt32();
                    
                    if(server.entities.TryGetValue(id, out var entity)) {
                        entity.Deserialize(config.Serializer, msg.msg, true, true);
                    
                        foreach (var conn in clients)
                        {
                            if (conn.Key == msg.msg.SenderConnection) continue;
                            if (!conn.Value.ready) continue;
                            
                            server.Messages.EntityUpdate(conn.Key, config.Serializer, entity);
                        }
                    }
                    break;
                }
                case NetOp.InstantiateEntity:
                {
                    var assetId = msg.msg.ReadInt32();
                    var hasPos = msg.msg.ReadBoolean();
                    var hasRot = msg.msg.ReadBoolean();
                    var hasSca = msg.msg.ReadBoolean();
                    var pos = hasPos
                        ? (Vector3?) new Vector3(msg.msg.ReadFloat(),msg.msg.ReadFloat(),msg.msg.ReadFloat())  
                        : null;
                    var rot = hasRot 
                        ? (Quaternion?) new Quaternion(msg.msg.ReadFloat(),msg.msg.ReadFloat(),msg.msg.ReadFloat(),msg.msg.ReadFloat())  
                        : null;
                    var sca = hasSca
                        ? (Vector3?) new Vector3(msg.msg.ReadFloat(),msg.msg.ReadFloat(),msg.msg.ReadFloat())  
                        : null;

                    server.CreateEntity(assetId, pos, rot, sca);
                    break;
                }
                case NetOp.DestroyEntity:
                {
                    var entityId = msg.msg.ReadInt32();

                    if (server.entities.TryGetValue(entityId, out var entity))
                        Destroy(entity.gameObject);
                    break;
                }
                case NetOp.ActionLocal:
                    server.HandleAction(msg.msg, false);
                    break;
                case NetOp.ActionGlobal:
                    server.HandleAction(msg.msg, true);
                    break;
                default:
                    Debug.LogWarning("Unknown operation: "+msg.op);
                    break;
            }
        }
    }
}