using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020004a6; CalculateCachedValues 0x540270, constructor 0x5402F0. Groups the
    // attach, catch and release settings of a catch-release toy. Native code inlines the three part
    // constructors into this one, and of the three CalculateCachedValues calls only the attach one has
    // a body (the cached cosine of its alignment tolerance).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class CatchReleaseDefinition
    {
        [Tooltip("Character will enter this form, if set, on entering the state.")]
        public ActorFormType FormType = ActorFormType.None;
        [Tooltip("Maps actor animation type to animation parameters.")]
        public ActorAnimationDefinition AnimationDefinition;
        public TimeCategory TimeCategory = TimeCategory.Environment;
        [Tooltip("Defines the catch release attach logic.")]
        public CatchReleaseDefinition_Attach Attach = new CatchReleaseDefinition_Attach();
        [Tooltip("Defines the catch release catch logic.")]
        public CatchReleaseDefinition_Catch Catch = new CatchReleaseDefinition_Catch();
        [Tooltip("Defines the catch release release logic.")]
        public CatchReleaseDefinition_Release Release = new CatchReleaseDefinition_Release();

        // 06001a91
        public void CalculateCachedValues()
        {
            Attach.CalculateCachedValues();
            Catch.CalculateCachedValues();
            Release.CalculateCachedValues();
        }

        // 06001a92
        public CatchReleaseDefinition() { }
    }
}
