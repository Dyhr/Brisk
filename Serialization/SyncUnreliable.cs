using System;

namespace Brisk.Serialization
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SyncUnreliable : Attribute
    {
    }
}