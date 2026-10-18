using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0200073d. Attaches the character to a transporter spline unless
    // the transporter movement ability is already in use.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterTransporterAttachTrigger : CharacterSplineAttachTrigger
    {
        // 060028c6; no grace period, so only a currently running transporter blocks the attach.
        protected override void DoTriggerEnter(Character character)
        {
            if (ActorAbilityUtilities.IsAbilityInUse(character, ActorAbilityType.Character_MovementTransporter, 0f))
                return;
            base.DoTriggerEnter(character);
        }

        // 060028c7
        protected override GraphStorageKey GetFSMKey() => ActorFSMKeys.TransporterAutoAttach;

        // 060028c8
        public CharacterTransporterAttachTrigger() { }
    }
}
