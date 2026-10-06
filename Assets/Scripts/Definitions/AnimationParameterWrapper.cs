using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
    public class AnimationParameterWrapper
    {
        [UnityEngine.SerializeField]
        private string m_parameterName;
        [NonSerialized]
        private int m_parameterHash = -1;

        public string ParameterName => m_parameterName;

        // Original 060038d3 caches the first nonempty name's hash. Editing the name
        // does not invalidate it; an empty name returns -1 without clearing the cache.
        public bool TryGetAnimationHash(out int hash)
        {
            if (string.IsNullOrEmpty(m_parameterName))
            {
                hash = -1;
                return false;
            }
            if (m_parameterHash == -1)
                m_parameterHash = Animator.StringToHash(m_parameterName);
            hash = m_parameterHash;
            return true;
        }

        public AnimationParameterWrapper() { }
    }
}
