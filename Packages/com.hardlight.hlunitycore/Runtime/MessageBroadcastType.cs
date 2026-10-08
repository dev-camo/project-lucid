using System;

namespace Hardlight
{
    [Flags]
    public enum MessageBroadcastType
    {
        Unbound = 1,
        Bound = 2,
        Both = 3
    }
}
