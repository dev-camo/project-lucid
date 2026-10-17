using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.Utils
{
    // Original HLUnityCore.Runtime 0x020002a7, methods 0x0600113f..0x06001143.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class AndroidBillOfMaterials : AndroidGradleDependency
    {
        [SerializeField]
        private AndroidGradleDependency[] m_versionControlledDependencies;

        public IReadOnlyList<AndroidGradleDependency> VersionControlledDependencies => m_versionControlledDependencies;

        // 0x06001140..0x06001142: no own field initialisation. The (name,
        // version) and (name, versionNumber) constructors, and the single-string
        // constructor with a non-empty dependency, run the virtual Validate from
        // the base and reach the override below while the array is still null;
        // a null or empty dependency skips Validate.
        public AndroidBillOfMaterials(string dependency) : base(dependency) { }

        public AndroidBillOfMaterials(string name, Version version) : base(name, version) { }

        public AndroidBillOfMaterials(string name, string versionNumber) : base(name, versionNumber) { }

        // 0x06001143 (x86-64 0x1b306e0): validates each pinned dependency
        // through its own virtual Validate; the array is not null-checked.
        public override void Validate()
        {
            foreach (AndroidGradleDependency dependency in m_versionControlledDependencies)
                dependency.Validate();
        }
    }
}
