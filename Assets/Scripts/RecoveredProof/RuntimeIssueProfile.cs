using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "RuntimeIssueProfile", menuName = "HardlightProject/Config/RuntimeIssueProfile")]
    public class RuntimeIssueProfile : ScriptableObjectWithGuid
    {
        // Game.Runtime 0x06002028: allocate before the genuine GUID-base constructor.
        public List<RuntimeIssueAttribute> m_attributes = new List<RuntimeIssueAttribute>();
    }
}
