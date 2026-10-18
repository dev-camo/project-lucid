using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0200073c. Publishes a spline to the character's
    // FSM storage under the key chosen by the concrete trigger while it is inside.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterSplineAttachTrigger : CharacterTrigger
    {
        [SerializeField]
        [Tooltip("The target spline to attach to.")]
        private Spline m_spline;
        [Tooltip("If false, only triggers when the incoming direction is within 180 degrees of this transform's forward.")]
        [SerializeField]
        private bool m_biDirectional;
        [Tooltip("Flag to use deprecated legacy logic for scenes that have not been corrected to use their orientation.")]
        [SerializeField]
        private bool m_useLegacyLogic = true;

        // 060028c0
        protected override void Awake() => base.Awake();

        // 060028c1; unless legacy, the character is aligned with the trigger and its
        // speed redirected along the trigger's forward before the spline is attached.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            if (m_spline == null) return;
            if (!m_biDirectional &&
                Vector3.Dot(transform.position - character.WorldPosition, transform.forward) < 0f) return;
            if (!m_useLegacyLogic)
            {
                character.SetWorldRotation(transform.rotation);
                character.SetWorldVelocity(transform.forward * character.WorldVelocityMagnitude);
            }
            character.Storage.SetValue<ISpline>(GetFSMKey(), m_spline.GetSpline());
        }

        // 060028c2; the original removes the stored value as ISurface, not ISpline.
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            if (m_spline == null) return;
            character.Storage.RemoveValue<ISurface>(GetFSMKey());
        }

        // 060028c3
        protected abstract GraphStorageKey GetFSMKey();

        // 060028c4
        public void ActivateSpline(bool active)
        {
            if (m_spline == null) return;
            m_spline.gameObject.SetActive(active);
        }

        // 060028c5; the legacy flag defaults to true.
        protected CharacterSplineAttachTrigger() { }
    }
}
