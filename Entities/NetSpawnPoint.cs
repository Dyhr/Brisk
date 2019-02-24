using System.Net;
using UnityEngine;

namespace Brisk.Entities
{
    public class NetSpawnPoint : NetBehaviour
    {
        [SerializeField] private string playerPrefab = "";

        private int assetId;
        
        private void Start()
        {
            if (Peer == null) return;
            
            assetId = Peer.assetManager[playerPrefab];
            if(Peer.IsServer)
                Peer.PlayerConnected += PlayerConnected;
        }

        private void PlayerConnected(IPEndPoint player)
        {
            Peer.CreateEntity(assetId, transform.position, transform.rotation, null, player);
        }
    }
}