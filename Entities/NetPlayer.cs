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
    }
}