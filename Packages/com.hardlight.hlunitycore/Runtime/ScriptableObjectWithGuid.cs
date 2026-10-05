using Unity.IL2CPP.CompilerServices;
using UnityEngine;
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ScriptableObjectWithGuid : ScriptableObject
    {
        [InspectorReadOnly]
        [SerializeField]
        protected string m_guid = "";
        // HLUnityCore.Runtime.dll 0x06000e65: assigns the length-zero original
        // string literal before Unity ScriptableObject construction.
        protected ScriptableObjectWithGuid() { }
        // HLUnityCore.Runtime.dll 0x06000e5d.
        public string GetGUID() { return m_guid; }
        // HLUnityCore.Runtime.dll 0x06000e5e: uses the authored operator's null rules.
        public bool Equals(ScriptableObjectWithGuid other)
        {
            if (other == null) return false;
            return other.m_guid == m_guid;
        }
        // HLUnityCore.Runtime.dll 0x06000e5f.
        public override bool Equals(object obj)
        {
            if (!(obj is ScriptableObjectWithGuid other) || other == null) return false;
            return other.m_guid == m_guid;
        }
        // HLUnityCore.Runtime.dll 0x06000e60: no null fallback.
        public override int GetHashCode() { return m_guid.GetHashCode(); }
        // HLUnityCore.Runtime.dll 0x06000e61: reference equality precedes Unity
        // null checks. Both checks execute; the later rhs check invokes this
        // authored operator again, as in the original named native body.
        public static bool operator ==(ScriptableObjectWithGuid lhs, ScriptableObjectWithGuid rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return true;
            bool lhsIsNull = (Object)lhs == null;
            bool rhsIsNull = (Object)rhs == null;
            if (lhsIsNull) return lhsIsNull && rhsIsNull;
            if (rhs == null) return false;
            return rhs.m_guid == lhs.m_guid;
        }
        // HLUnityCore.Runtime.dll 0x06000e62.
        public static bool operator !=(ScriptableObjectWithGuid lhs, ScriptableObjectWithGuid rhs) { return !(lhs == rhs); }
        // HLUnityCore.Runtime.dll 0x06000e63: original body is a single RET.
        protected virtual void Reset() { }
        // HLUnityCore.Runtime.dll 0x06000e64: original body is a single RET.
        protected virtual void OnValidate() { }
    }
}
