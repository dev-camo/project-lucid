using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "TrackerEnterDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TrackerEnterDefinition")]
    public class TrackerEnterDefinition : TrackerEndDefinition
    {

        public TrackerEnterDefinition() { }
    }
}
