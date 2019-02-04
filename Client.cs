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
        [SerializeField] private float sendRate = 30;

        private readonly Peer<NetClient> client = new Peer<NetClient>();

        private bool isConnecting;
        private bool connected;
        
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
            connected = false;
            Debug.Log("Disconnected");
        }

        private void ClientConnected(NetConnection connection)
        {
            isConnecting = false;
            connected = true;
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
            NetEntity entity;
            switch (msg.op)
            {
                case NetOp.SystemInfo:
                    msg.res.Write((byte)NetOp.SystemInfo);
                    msg.res.Write((byte)Application.platform);
                    break;
                case NetOp.StringsStart:
                    msg.res.Write((byte)NetOp.StringsStart);
                    client.assetManager.InitializeStringGet(msg.msg.ReadInt32());
                    break;
                case NetOp.StringsData:
                    client.assetManager.StringGet(msg.msg.ReadInt32(), msg.msg.ReadString());
                    
                    if (client.assetManager.Ready) Ready();
                    break;
                case NetOp.AssetsStart:
                    msg.res.Write((byte)NetOp.AssetsStart);
                    msg.res.Write((byte)Application.platform);
                    client.assetManager.InitializeDataGet(msg.msg.ReadInt32());
                    break;
                case NetOp.AssetsData:
                    var start = msg.msg.ReadInt32();
                    var length = msg.msg.ReadInt32();
                    var data = msg.msg.ReadBytes(length);
                    client.assetManager.DataGet(start, length, data);

                    if (client.assetManager.Ready) Ready();
                    break;
                case NetOp.NewEntity:
                    var assetId = msg.msg.ReadInt32();
                    var entityId = msg.msg.ReadInt32();
                    var mine = msg.msg.ReadBoolean();

                    entity = client.entityManager.CreateEntity(client.assetManager, assetId, entityId);
                    entity.Owner = mine;
                    break;
                case NetOp.EntityUpdate:
                    var id = msg.msg.ReadInt32();
                    entity = client.entityManager[id];

                    if (entity != null)
                        entity.Deserialize(msg.msg);
                    else
                        Debug.LogWarning("Entity not found: "+id);
                    break;
                default:
                    Debug.LogWarning("Unknown operation: "+msg.op);
                    break;
            }
        }

        private void Ready()
        {
            var msg = client.NetPeer.CreateMessage();
            msg.Write((byte)NetOp.Ready);
            client.NetPeer.SendMessage(msg, NetDeliveryMethod.ReliableUnordered);
            
            StartCoroutine(SendUpdates());
        }

        private IEnumerator SendUpdates()
        {
            while (connected)
            {
                yield return new WaitForSeconds(1/sendRate);
                foreach (var entity in client.entityManager)
                {
                    if (!entity.Owner || !entity.Dirty) continue;

                    var msg = client.NetPeer.CreateMessage();
                    entity.Serialize(msg);
                    client.NetPeer.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced);
                }
            }
        }
    }
}
