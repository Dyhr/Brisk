using Brisk.Actions;
using Brisk.Serialization;
using UnityEngine;

namespace Brisk.Entities
{
    public class NetPlayer : NetBehaviour
    {
        [SerializeField] private bool green = false;
        [SyncReliable]
        public bool Green
        {
            set
            {
                Debug.Log("GREEN "+value);
                GetComponentInChildren<Renderer>().sharedMaterial.color = value ? Color.green : Color.white;
                green = value;
            }
            get => green;
        }

        [Action]
        public void ToggleGreen(bool value)
        {
            Green = value;
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.G)) Entity.Peer.Messages.ActionLocal(1,Entity.Id, Entity.Behaviour(this),true); //ToggleGreen(!Green);
        }
    }
}