using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000320; complete own fields and method API.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterComponentLookup : ActorComponentLookup
    {
        // 0x04000b88; original instance offset 0x58.
        [SerializeField]
        private SmoothedTransformProxy m_smoothedTransformProxy;
        // 0x04000b89; original instance offset 0x60.
        [SerializeField]
        protected Transform m_blobShadowRigRoot;

        // 0x06001356: direct original field read; preserve CLR-null results.
        public SmoothedTransformProxy SmoothedTransformProxy { get { return m_smoothedTransformProxy; } }

        // 0x06001357: direct original protected field read, without lookup.
        public Transform BlobShadowRigRoot { get { return m_blobShadowRigRoot; } }

        // 0x06001358: both native architectures tail-call the genuine base constructor.
        // No own field initialization, callback, allocation, null test or catch is present.
        public CharacterComponentLookup() { }
    }
}
