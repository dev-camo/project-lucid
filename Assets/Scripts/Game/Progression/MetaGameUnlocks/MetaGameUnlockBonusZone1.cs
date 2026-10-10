using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class MetaGameUnlockBonusZone1 : MetaGameUnlockZoneBase
    {
        // Original Game.Runtime06002a6c: no own fields; the genuine zone base stores zone.
        public MetaGameUnlockBonusZone1(GameplayLevelCategory zone) : base(zone)
        {
        }

        // Original06002a6d: original hashed progression key0x57f164e5.
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.BonusZone1;
    }
}
