using Brisk.Entities;
using Lidgren.Network;
using UnityEngine;

namespace Brisk.Actions
{
    public abstract class ActionSet : ScriptableObject
    {
        public abstract void Call(NetEntity entity, byte behaviourId, NetIncomingMessage msg, int actionId,
            out object[] args);

        public void Serialize(NetOutgoingMessage msg, object[] args)
        {
            foreach (var arg in args)
            {
                switch (arg.GetType().FullName)
                {
                    case "System.Byte":
                        msg.Write((byte)arg);
                        break;
                    case "System.Int16":
                        msg.Write((short)arg);
                        break;
                    case "System.Int32":
                        msg.Write((int)arg);
                        break;
                    case "System.Int64":
                        msg.Write((long)arg);
                        break;
                    case "System.Boolean":
                        msg.Write((bool)arg);
                        break;
                    case "UnityEngine.Vector3":
                        msg.Write(((Vector3)arg).x);
                        msg.Write(((Vector3)arg).y);
                        msg.Write(((Vector3)arg).z);
                        break;
                    default:
                        Debug.LogError($"Type not supported for serialization: {arg.GetType().FullName}");
                        break;
                }
            }
        }
    }
}