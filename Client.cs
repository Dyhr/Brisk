using System.Collections;
using Brisk.Config;
using Brisk.Entities;
using Lidgren.Network;
using UnityEngine;

namespace Brisk
{
    public class Client : MonoBehaviour
    {
        [SerializeField] private ServerConfig config = null;
        [SerializeField] private string host = "localhost";
        [SerializeField] private float connectTimeout = 10f;

        private readonly Peer<NetClient> client = new Peer<NetClient>();

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
            
            var success = client.Start(ref config, false);

            if (!success) return;

            var resolved = client.Connect(host, config.Port);
            if (!resolved)
                Debug.Log($"Could not resolve host: {host}:{config.Port}");
            else
                StartCoroutine(CheckNoConnection());
        }

        private IEnumerator CheckNoConnection()
        {
            isConnecting = true;
            yield return new WaitForSeconds(connectTimeout);

            if (isConnecting)
                Debug.Log("Could not connect to server");
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
            updateRoutine = StartCoroutine(client.UpdateEntities(config));
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
                    client.entityManager.CreateEntity(
                        client.assetManager, msg.msg.ReadInt32(), msg.msg.ReadInt32(), msg.msg.ReadBoolean());
                    break;
                case NetOp.EntityUpdate:
                    var id = msg.msg.ReadInt32();
                    var entity = client.entityManager[id];

                    if (entity != null)
                        entity.Deserialize(config.Serializer, msg.msg, true, true);
                    else
                        Debug.LogWarning("Entity not found: "+id);
                    break;
                default:
                    Debug.LogWarning("Unknown operation: "+msg.op);
                    break;
            }
        }
    }
}
