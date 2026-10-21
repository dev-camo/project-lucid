using System;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000276.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineMix_CharacterHalfPipe : CinemachineMix_CharacterBase
    {
        // 0x06000dc2: left blends towards -1, right towards +1; any other stored side faults.
        protected override void InternalUpdate(float deltaTime)
        {
            base.InternalUpdate(deltaTime);
            if (!IsLive()) return;
            CharacterAbility_HalfPipeSide side = m_characterManager.GetCurrentCharacterUnsafe().Storage
                .GetValue<CharacterAbility_HalfPipeSide>(ActorFSMKeys.HalfPipeColliderEntrySide);
            switch (side)
            {
                case CharacterAbility_HalfPipeSide.Left:
                    UpdateBlendWeightings(-1f, deltaTime);
                    break;
                case CharacterAbility_HalfPipeSide.Right:
                    UpdateBlendWeightings(1f, deltaTime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 0x06000dc3
        public CinemachineMix_CharacterHalfPipe() { }
    }
}
