using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0200073b. Attaches the character to a rail spline unless a rail
    // is already active.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterRailAttachTrigger : CharacterSplineAttachTrigger
    {
        // 060028bd; reads RailActive with a false default and storeDefault set, so an absent flag
        // is stored as false.
        protected override void DoTriggerEnter(Character character)
        {
            if (character.Storage.GetValue(ActorFSMKeys.RailActive, false, true))
                return;
            base.DoTriggerEnter(character);
        }

        // 060028be
        protected override GraphStorageKey GetFSMKey() => ActorFSMKeys.RailAutoAttach;

        // 060028bf; the legacy-logic default comes from CharacterSplineAttachTrigger.
        public CharacterRailAttachTrigger() { }
    }
}
