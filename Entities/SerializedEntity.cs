using UnityEngine;

namespace Network.Entities
{
    public class SerializedEntity : ScriptableObject
    {
        public string title;
        public byte[] data;
    }
}