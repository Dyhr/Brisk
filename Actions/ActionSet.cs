using Brisk.Entities;
using UnityEngine;

namespace Brisk.Actions
{
    public abstract class ActionSet : ScriptableObject
    {
        public abstract void Call(NetBehaviour bhr, int actionId);
    }
}