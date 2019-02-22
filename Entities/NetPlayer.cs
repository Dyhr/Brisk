using Brisk.Actions;
using Brisk.Serialization;
using UnityEditor;
using UnityEngine;

namespace Brisk.Entities
{
    public class NetPlayer : NetBehaviour
    {
        [Action(false)]
        public void Shoot()
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit))
            {
                var dir = hit.point - transform.position;
                dir.y = 0;
                dir.Normalize();
                
                Peer.Instantiate(Peer.GetAssetId("CanisterPlasma"), transform.position + dir+Vector3.up*.5f, Quaternion.LookRotation(dir));
            }
        }
    }
}