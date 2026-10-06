using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "WaypointDefinition", menuName = "HardlightProject/DefinitionData/Definitions/WaypointDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class WaypointDefinition : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField]
        private HardlightProject.WaypointType m_type;

        [UnityEngine.SerializeField]
        private Hardlight.SerializableDictionary<HardlightProject.WaypointContainerType, HardlightProject.WaypointSettings> m_settingsByContainerType = new Hardlight.SerializableDictionary<WaypointContainerType, WaypointSettings>(HardlightEnumComparers.WaypointContainerTypeComparer);

        // Original Game.Runtime 0x06001fba, ARM 0x532eb4.
        public HardlightProject.WaypointType Type => m_type;

        // Original Game.Runtime 0x06001fbb, ARM 0x532ebc.
        public bool TryGetSettingsByContainerType(WaypointContainerType containerType, out WaypointSettings waypointSettings) => m_settingsByContainerType.TryGetValue(containerType, out waypointSettings);
        // Original Game.Runtime 0x06001fbc, ARM 0x532f28.
        // Natural original constructor; documented field initializers precede the genuine base call.
    }
}
