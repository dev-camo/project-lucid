using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000448. Character-specific cutscene substitution.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SubstituteObjectCharacter : SubstituteObject
    {
        [SerializeField] private bool m_waitForSwap;
        private CharacterManager m_characterManager;
        private Character m_characterReplaced;
        private StackableDataHandle m_stackableDataHandle;

        // 0x0600194a. Register through the original process reference; base hiding
        // occurs only after a valid current character becomes available.
        protected override void OnEnable()
        {
            ProcessManager.GetSystemRef<CharacterManager>(null, true).InvokeOnValid(Initialise);
        }

        // 0x0600194b. The authored wait flag controls immediate callback delivery.
        private void Initialise(CharacterManager characterManager)
        {
            m_characterManager = characterManager;
            m_characterManager.RegisterOnCharacterChange(OnCharacterChange, !m_waitForSwap);
        }

        // 0x0600194c. A previous handle is removed from the newly current character,
        // exactly as shipped. Capture the visual roots before changing overrides.
        private void HideRealCharacter()
        {
            if (m_characterManager == null || !m_characterManager.TryGetCurrentCharacter(out Character character))
                return;
            m_characterReplaced = character;
            m_realAnimationRoot = m_characterReplaced.VisualProxy.AnimationRoot;
            m_replacedObject = m_characterReplaced.VisualProxy.gameObject;
            if (m_stackableDataHandle != null)
                m_characterReplaced.RemoveModifierOverrides(m_stackableDataHandle);
            m_stackableDataHandle = m_characterReplaced.AddModifierOverride((int)GameplayModifierType.ControlsEnabled, false);
            m_characterReplaced.AllowCollisions(false);
            m_characterReplaced.StopAllActorPFX();
            base.OnEnable();
        }

        // 0x0600194d / 0x0600194e expose the inherited substitute reference directly.
        public Transform GetSubstitutedTransform() => m_replaceWithTransform;
        public void SetSubstitutedTransform(Transform replacedTransform) => m_replaceWithTransform = replacedTransform;

        // 0x0600194f is a tailcall on both CPU slices, including the four-byte ARM
        // body. It ignores its argument and queries the manager's current character.
        private void OnCharacterChange(Character newCharacter) => HideRealCharacter();

        // 0x06001950. Add a true controls override without removing or storing the
        // old false handle. Restoration and release keep their original fault order.
        protected override void OnDisable()
        {
            m_characterReplaced.AddModifierOverride((int)GameplayModifierType.ControlsEnabled, true);
            if (m_substitutePosition)
                m_characterReplaced.SetWorldPosition(m_replaceWithTransform.position);
            if (m_substituteRotation)
                m_characterReplaced.SetWorldRotation(m_replaceWithTransform.rotation);
            base.OnDisable();
            m_characterReplaced.VisualProxy.SnapToLocation(m_characterReplaced.WorldPosition, m_characterReplaced.WorldRotation);
            m_characterReplaced.AllowCollisions(true);
            m_characterReplaced.ToggleFormRenderers(true);
            m_characterReplaced.EndImpulses();
            m_characterReplaced = null;
            m_characterManager.ReleaseOnCharacterChange(OnCharacterChange);
        }

        // 0x06001951 is the genuine base-only constructor.
        public SubstituteObjectCharacter() { }
    }
}
