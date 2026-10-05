using System;

namespace Hardlight
{
    public sealed class GraphUnityObjectAttribute : GraphAttributeBase
    {
        public readonly Type UnityObjectType;
        // HLUnityCore.Runtime.dll 0x06000c49; ARM64 0x1b0aac4.
        // Retain the supplied Type, including null and non-Unity types.
        public GraphUnityObjectAttribute(Type type) { UnityObjectType = type; }
    }

    public sealed class GraphUnityObjectPopupAttribute : GraphAttributeBase
    {
        public readonly Type UnityObjectType;
        public readonly bool IncludeChildren;
        // 0x06000c4a; ARM64 0x1b0aaf8. No filtering/validation.
        public GraphUnityObjectPopupAttribute(Type type, bool includeChildren = true)
        {
            UnityObjectType = type;
            IncludeChildren = includeChildren;
        }
    }

    public sealed class GraphUnitySceneAttribute : GraphAttributeBase
    {
        // 0x06000c4b; ARM64 0x1b0ab40. Original base constructor only.
        public GraphUnitySceneAttribute() { }
    }
}
