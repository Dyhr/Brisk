using Brisk.Actions;
using Brisk.Serialization;
using UnityEngine;

namespace Brisk.Entities
{
    public class NetPlayer : NetBehaviour
    {
        [Action(false)]
        public void Shoot()
        {
            Peer.Instantiate(Peer.GetAssetId("CanisterPlasma"), transform.position, transform.rotation);
        }
    }
}