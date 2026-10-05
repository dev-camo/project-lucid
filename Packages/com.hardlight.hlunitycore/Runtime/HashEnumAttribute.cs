using System;
using UnityEngine;

namespace Hardlight.Utils
{
    public class HashEnumAttribute : PropertyAttribute
    {
        // HLUnityCore.Runtime 0x06001215, ARM64 0x1b3c3cc..0x1b3c3d4:
        // player body only forwards to PropertyAttribute; it never stores or inspects aType.
        public HashEnumAttribute(Type aType = null) { }
    }
}
