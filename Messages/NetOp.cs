using System;

namespace Brisk.Messages
{
    [Serializable]
    public enum NetOp : byte
    {
        Null              = 0x00,
        Error             = 0x01,
        Warning           = 0x02,
        SystemInfo        = 0x03,
        Ready             = 0x04,
        
        EntityUpdate      = 0x80,
        NewEntity         = 0x81,
        DestroyEntity     = 0x82,
        ShowEntity        = 0x83,
        HideEntity        = 0x84,
        InstantiateEntity = 0x85,
        
        ActionLocal       = 0xA0,
        ActionGlobal      = 0xA1,
        Action            = 0xA2,
    }
}