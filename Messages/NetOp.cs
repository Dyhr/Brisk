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
        
        AssetsStart       = 0x10,
        AssetsData        = 0x11,
        StringsStart      = 0x12,
        StringsData       = 0x13,
        ActionsStart      = 0x14,
        ActionsData       = 0x15,
        
        EntityUpdate      = 0x80,
        NewEntity         = 0x81,
        RemoveEntity      = 0x82,
        ShowEntity        = 0x83,
        HideEntity        = 0x84,
        InstantiateEntity = 0x85,
        
        ActionLocal       = 0xA0,
        ActionGlobal      = 0xA1,
    }
}