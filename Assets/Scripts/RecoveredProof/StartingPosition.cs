using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a9a: all four declarations and both serialized fields.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class StartingPosition
    {
        [SerializeField] private Transform m_location;
        [SerializeField] private LevelStartPositionDefinition m_identifier;

        // Original 0x06003d0b; ARM 0x6ad9b8. Return the authored reference unchanged.
        public Transform Location => m_location;

        // Original 0x06003d0c; ARM 0x6ad9c0.
        public LevelStartPositionDefinition Identifier => m_identifier;

        // Original 0x06003d0d; ARM 0x6ad9c8 and x86 0x6d23d0 both return immediately.
        // The shipped method does not replace either serialized reference.
        public void SetData(Transform location, LevelStartPositionDefinition definition) { }

        // Original 0x06003d0e; ARM 0x6ad9cc. Natural Object-only constructor.
        public StartingPosition() { }
    }
}
