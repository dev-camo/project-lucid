using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000a1a; methods 060039e3..060039e5.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PlayParticleSystemOnEnable : MonoBehaviour
    {
        [SerializeField]
        private ParticleSystem m_particleSystem;

        private void OnEnable()
        {
            m_particleSystem.Play();
        }

        private void Reset()
        {
            m_particleSystem = GetComponent<ParticleSystem>();
        }
    }
}
