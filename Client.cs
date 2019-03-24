using System;
using System.Collections;
using Brisk.Entities;
using Brisk.Messages;
using Lidgren.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Brisk
{
    public class Client : MonoBehaviour
    {
        public event Action ConnectionFailed;
        
        [SerializeField] private Config config = null;
        [SerializeField] private string host = "localhost";
        [SerializeField] private float connectTimeout = 10f;

        public float LoadingProgress => client.assetManager.AssetLoadingProgress;
        
        private readonly Peer client = new Peer();

        private bool isConnecting;
        private Coroutine updateRoutine;

        private IEnumerator Start()
        {
            // TODO sleep physics until ready
            //Physics.autoSimulation = false;
            
            client.Connected += ClientConnected;
            client.Disconnected += ClientDisconnected;
            client.Data += ClientData;
            
            if (string.IsNullOrWhiteSpace(host))
            {
                Debug.LogError("No host server specified");
                yield break;
            }
            
            var success = client.Start<NetClient>(ref config, false, null);

            if (!success) yield break;

            var resolved = client.Connect(host, config.GetInt("port_game"));
            if (!resolved) {
                Debug.Log($"Could not resolve host: {host}:{config.GetInt("port_game")}");
                ConnectionFailed?.Invoke();
            } else {
                isConnecting = true;
                yield return new WaitForSeconds(connectTimeout);

                if (!isConnecting) yield break;
                Debug.Log("Could not connect to server");
                ConnectionFailed?.Invoke();
            }
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
                    StartCoroutine(client.assetManager.DownloadAssetBundle(
                        $"http://{host}:{config.GetInt("port_web")}{msg.msg.ReadString()}", err =>
                        {
                            if (err != null) Debug.LogError(err);
                            if (client.assetManager.Ready) client.Messages.Ready(connection);
                        }));
                    StartCoroutine(client.assetManager.DownloadStrings(
                        $"http://{host}:{config.GetInt("port_web")}{msg.msg.ReadString()}", err =>
                        {
                            if (err != null) Debug.LogError(err);
                            if (client.assetManager.Ready) client.Messages.Ready(connection);
                        }));
                    break;
                case NetOp.NewEntity:
                    NetEntity.Create(
                        client, client.assetManager, msg.msg.ReadInt32(), msg.msg.ReadInt32(), msg.msg.ReadBoolean());
                    break;
                case NetOp.EntityUpdate:
                {
                    var id = msg.msg.ReadInt32();
                    var reliable = msg.msg.ReadBoolean();

                    if (client.entities.TryGetValue(id, out var entity))
                    {
                        if(reliable)
                            entity.DeserializeReliable(config.Serializer, msg.msg);
                        else
                            entity.DeserializeUnreliable(config.Serializer, msg.msg);
                    }
                    else
                    {
                        Debug.LogWarning("Entity not found: "+id);
                    }
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
