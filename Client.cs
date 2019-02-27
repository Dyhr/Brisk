using System;
using System.Collections;
using Brisk.Config;
using Brisk.Entities;
using Brisk.Messages;
using Lidgren.Network;
using UnityEngine;

namespace Brisk
{
    public class Client : MonoBehaviour
    {
        public event Action ConnectionFailed;
        
        [SerializeField] private ServerConfig config = null;
        [SerializeField] private string host = "localhost";
        [SerializeField] private float connectTimeout = 10f;

        public float LoadingProgress => client.assetManager.AssetLoadingProgress;
        
        private readonly Peer client = new Peer();

        private bool isConnecting;
        private Coroutine updateRoutine;
        
        private void Awake()
        {
            client.Connected += ClientConnected;
            client.Disconnected += ClientDisconnected;
            client.Data += ClientData;
            
            if (string.IsNullOrWhiteSpace(host))
            {
                Debug.LogError("No host server specified");
                return;
            }
            
            var success = client.Start<NetClient>(ref config, false, null);

            if (!success) return;

            var resolved = client.Connect(host, config.Port);
            if (!resolved) {
                Debug.Log($"Could not resolve host: {host}:{config.Port}");
                ConnectionFailed?.Invoke();
            } else {
                StartCoroutine(CheckNoConnection());
            }
        }

        private IEnumerator CheckNoConnection()
        {
            isConnecting = true;
            yield return new WaitForSeconds(connectTimeout);

            if (!isConnecting) yield break;
            Debug.Log("Could not connect to server");
            ConnectionFailed?.Invoke();
        }

        private void ClientDisconnected(NetConnection connection)
        {
            if(updateRoutine != null) StopCoroutine(updateRoutine);
            updateRoutine = null;
            Debug.Log("Disconnected");
        }

        private void ClientConnected(NetConnection connection)
        {
            isConnecting = false;
            updateRoutine = StartCoroutine(client.UpdateEntities());
            Debug.Log("Connected to server: " + connection.RemoteEndPoint);
        }

        private void OnDestroy()
        {
            client.Data -= ClientData;
            client.Stop("Disconnecting");
        }

        private void Update()
        {
            client.Receive();
        }

        private void ClientData(ref NetMessage msg)
        {
            var connection = msg.msg.SenderConnection;
            switch (msg.op)
            {
                case NetOp.SystemInfo:
                    client.Messages.SystemInfo(connection, Application.platform);
                    break;
                case NetOp.StringsStart:
                    client.assetManager.InitializeStringGet(msg.msg.ReadInt32());
                    client.Messages.StringsStart(connection);
                    break;
                case NetOp.StringsData:
                    client.assetManager.StringGet(msg.msg.ReadInt32(), msg.msg.ReadString());
                    if (client.assetManager.Ready) client.Messages.Ready(connection);
                    break;
                case NetOp.AssetsStart:
                    client.Messages.AssetsStart(connection, Application.platform);
                    client.assetManager.InitializeDataGet(msg.msg.ReadInt32());
                    break;
                case NetOp.AssetsData:
                    var start = msg.msg.ReadInt32();
                    var length = msg.msg.ReadInt32();
                    var data = msg.msg.ReadBytes(length);
                    client.assetManager.DataGet(start, length, data);

                    if (client.assetManager.Ready) client.Messages.Ready(connection);
                    break;
                case NetOp.NewEntity:
                    NetEntity.Create(
                        client, client.assetManager, msg.msg.ReadInt32(), msg.msg.ReadInt32(), msg.msg.ReadBoolean());
                    break;
                case NetOp.EntityUpdate:
                {
                    var id = msg.msg.ReadInt32();
                    var entity = client.entities[id];

                    if (entity != null)
                        entity.Deserialize(config.Serializer, msg.msg, true, true);
                    else
                        Debug.LogWarning("Entity not found: "+id);
                    break;
                }
                case NetOp.DestroyEntity:
                {
                    var entityId = msg.msg.ReadInt32();
                    
                    if(client.entities.TryGetValue(entityId, out var entity))
                    {
                        entity.netDestroyed = true;
                        Destroy(entity.gameObject);
                    }
                    break;
                }
                case NetOp.Action:
                    client.HandleAction(msg.msg);
                    break;
                default:
                    Debug.LogWarning("Unknown operation: "+msg.op);
                    break;
            }
        }
    }
}
