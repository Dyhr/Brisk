﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Brisk.Assets;
using Brisk.Config;
using Brisk.Messages;
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
            Baseline.Load(server, server.assetManager, server.entityManager, level);
            Debug.Log($@"Map ""{level.name}"" loaded");
            
            // Actually start the server
            var success = server.Start<NetServer>(ref config, true);
            if (!success) return;
            
            // Start the routines
            StartCoroutine(server.UpdateEntities(config));
            StartCoroutine(StatusReport());
            
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
            RuntimePlatform platform;
            var connection = msg.Connection;
            switch (msg.op)
            {
                case NetOp.SystemInfo:
                    platform = (RuntimePlatform) msg.msg.ReadByte();
                    if (server.assetManager.Available(platform))
                        server.Messages.AssetsStart(connection, server.assetManager.Size(platform));
                    // TODO handle unknown platforms
                    server.Messages.StringsStart(connection, server.assetManager.StringsLength);
                    break;
                case NetOp.StringsStart:
                    StartCoroutine(server.assetManager.SendStrings( 
                        msg.msg.SenderConnection.AverageRoundtripTime, 
                        (i, s) => server.Messages.StringsData(connection, i, s)));
                    break;
                case NetOp.AssetsStart:
                    platform = (RuntimePlatform) msg.msg.ReadByte();
                    if (server.assetManager.Available(platform))
                        StartCoroutine(server.assetManager.SendAssetBundle(
                            platform, 
                            msg.msg.SenderConnection.CurrentMTU - 100, 
                            msg.msg.SenderConnection.AverageRoundtripTime, 
                            (start, length, data) => server.Messages.AssetsData(connection, start, length, data)));
                    break;
                case NetOp.Ready:
                    clients[msg.msg.SenderConnection].ready = true;
                    Debug.Log(msg.msg.SenderEndPoint + " is ready");

                    foreach (var entity in server.entityManager.AllEntities())
                    {
                        server.Messages.NewEntity(connection, entity.AssetId, entity.Id, false);
                        server.Messages.EntityUpdate(connection, config.Serializer, entity);
                    }

                    var assetId = server.assetManager["PlayerController"];
                    var entityId = msg.msg.SenderEndPoint.Port;

                    server.entityManager.CreateEntity(server, server.assetManager, assetId, entityId, false);
                    server.Messages.NewEntity(connection, assetId, entityId, true);

                    foreach (var conn in clients)
                    {
                        if (conn.Key == msg.msg.SenderConnection) continue;
                        if (!conn.Value.ready) continue;

                        server.Messages.NewEntity(conn.Key, assetId, entityId, false);
                    }
                    break;
                case NetOp.EntityUpdate:
                    var id = msg.msg.ReadInt32();
                    var e = server.entityManager[id];

                    e.Deserialize(config.Serializer, msg.msg, true, true);
                    
                    foreach (var conn in clients)
                    {
                        if (conn.Key == msg.msg.SenderConnection) continue;
                        if (!conn.Value.ready) continue;
                        
                        server.Messages.EntityUpdate(conn.Key, config.Serializer, e);
                    }
                    break;
                case NetOp.ActionLocal:
                    HandleAction(msg.msg, true);
                    break;
                case NetOp.ActionGlobal:
                    HandleAction(msg.msg, false);
                    break;
                default:
                    Debug.LogWarning("Unknown operation: "+msg.op);
                    break;
            }
        }

        private void HandleAction(NetIncomingMessage msg, bool local)
        {
            Debug.Log(msg.ReadInt32());
            Debug.Log(msg.ReadInt32());
        }
    }
}