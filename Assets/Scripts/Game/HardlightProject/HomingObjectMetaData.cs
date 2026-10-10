using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original 02000301, all three methods/one field. This keeps the
    // genuine HomingTarget component dependency; no replacement target component.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HomingObjectMetaData : TargetObjectMetaData
    {
        public HomingTarget HomingTarget;
        public override bool IsTargetable => HomingTarget.IsTargetable; // 06001248
        public override void DoOnTargeted() => HomingTarget.DoOnTargeted(); // 06001249
        public HomingObjectMetaData() { } // 0600124a, genuine base ctor only.
    }
}
