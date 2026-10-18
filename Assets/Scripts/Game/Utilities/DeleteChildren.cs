using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009fd; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DeleteChildren : MonoBehaviour
    {
        [HideInInspector]
        [SerializeField]
        private bool m_forceDelete;

        // 0x06003960/0x06003961
        public bool ForceDelete
        {
            get => m_forceDelete;
            set => m_forceDelete = value;
        }

        // 0x06003962 (0x6B0410): the body ships although retail callers are compiled out.
        // Children go last to first; the name is read even when forceDelete is set.
        [Conditional("UNITY_EDITOR")]
        public void DeleteAllChildren(bool forceDelete)
        {
            for (int i = transform.childCount - 1; i >= 0; --i)
            {
                if (transform.GetChild(i).name.Contains("(Clone)") || forceDelete)
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }
    }
}
