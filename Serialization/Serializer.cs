using UnityEngine;

namespace Brisk.Serialization
{
    public abstract class Serializer : ScriptableObject
    {
        public abstract void SerializeReliable<T>(T obj, Lidgren.Network.NetOutgoingMessage msg)
            where T : Entities.NetBehaviour;
        public abstract void DeserializeReliable<T>(T obj, Lidgren.Network.NetIncomingMessage msg)
            where T : Entities.NetBehaviour;
        public abstract void SerializeUnreliable<T>(T obj, Lidgren.Network.NetOutgoingMessage msg)
            where T : Entities.NetBehaviour;
        public abstract void DeserializeUnreliable<T>(T obj, Lidgren.Network.NetIncomingMessage msg)
            where T : Entities.NetBehaviour;
    }
}