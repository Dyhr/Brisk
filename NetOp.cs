using System;

namespace Network
{
    [Serializable]
    public enum NetOp : byte
    {
        Null         = 0x00,
        Error        = 0x01,
        Warning      = 0x02,
        SystemInfo   = 0x03,
        Ready        = 0x04,
        
        AssetsStart  = 0x10,
        AssetsData   = 0x11,
        StringsStart = 0x10,
        StringsData  = 0x11,
    }
}