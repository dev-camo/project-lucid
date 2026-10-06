using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "ApplicationStateEvent", menuName = "HardlightProject/ApplicationStateEvent", order = 0)]
    public class ApplicationStateEvent : Hardlight.ScriptableObjectWithGuid
    {
        // Original Game.Runtime 0x06000627, ARM 0x511bb8.
        public static ApplicationStateEvent FindByName(string name) => Hardlight.Utils.ObjectUtils.FindByName<ApplicationStateEvent>(name);

        // Original Game.Runtime 0x06000628, ARM 0x511c0c.
        // The compiler emits the original public, parameterless base-only constructor.
    }
}
