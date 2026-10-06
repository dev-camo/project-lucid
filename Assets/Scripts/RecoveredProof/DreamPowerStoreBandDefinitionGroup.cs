using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "DreamPowerStoreBandDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/DreamPowerStoreBandDefinitionGroup")]
    public class DreamPowerStoreBandDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.DreamPowerStoreBandType, HardlightProject.DreamPowerStoreBandDefinition>
    {
        // Original Game.Runtime 0x06001c65, ARM 0x5224dc.
        protected override HardlightProject.DreamPowerStoreBandType GetElementKey(HardlightProject.DreamPowerStoreBandDefinition data) => data.Type;

        // Original Game.Runtime 0x06001c66, ARM 0x5224e4.
        protected override IEqualityComparer<HardlightProject.DreamPowerStoreBandType> GetKeyComparer() => HardlightEnumComparers.DreamPowerStoreBandTypeComparer;

        // Original Game.Runtime 0x06001c67, ARM 0x522560.
        // Native captures definition in the natural DisplayClass2_0, then uses
        // Array.FindIndex and original ScriptableObjectWithGuid equality.
        // Do not replace the GUID comparison with reference/type equality.
        public int GetIndex(DreamPowerStoreBandDefinition definition) => Array.FindIndex(m_elements, def => def.Data == definition);

        // Original Game.Runtime 0x06001c68, ARM 0x522654.
        // Natural original constructor; documented field initializers precede the genuine base call.
    }
}
