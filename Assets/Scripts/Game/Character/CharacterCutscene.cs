using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    // Original Game.Runtime type 02000322. This preserves the shipping skin-swap
    // path and its fault/order behavior; gameplay integration is still unaccepted.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterCutscene : MonoBehaviour
    {
        [SerializeField] private CharacterDefinition m_characterDefinition;
        [SerializeField] private GameObject m_characterPrefab;
        private CharacterRigLookup m_rigOriginal;
        private CharacterRigLookup m_rigReplacement;
        private bool m_usesDefaultSkin;
        private AsyncOperationHandle<GameObject> m_characterHandle;
        private SubstituteObjectCharacter m_substituteObjectCharacter;
        private RuntimeAnimatorController m_animatorController;
        private static readonly SystemRef<AddressableManager> s_addressableManagerRef =
            ProcessManager.GetSystemRef<AddressableManager>(null, true);
        private static readonly SystemRef<CutsceneManager> s_cutsceneManagerRef =
            ProcessManager.GetSystemRef<CutsceneManager>(null, true);

        // 0600135f: a default skin only sets the flag and returns. The shipping
        // method never clears an earlier true flag on a subsequent nondefault call.
        public void LoadCharacterSkin()
        {
            if (m_characterDefinition.CurrentSkin == m_characterDefinition.CharacterSkins[0])
            {
                m_usesDefaultSkin = true;
                return;
            }
            if (!s_addressableManagerRef.TryGet(out AddressableManager addressableManager))
                return;

            m_characterHandle = addressableManager.LoadAssetInstance(
                m_characterDefinition.CurrentSkin.MeshPrefab, out GameObject replacement, transform);
            m_rigOriginal = m_characterPrefab.GetComponent<CharacterRigLookup>();
            m_rigOriginal.Initialize();
            m_rigReplacement = replacement.GetComponent<CharacterRigLookup>();
            m_rigReplacement.Initialize();
            m_rigReplacement.CharacterAnimator.updateMode = m_rigOriginal.CharacterAnimator.updateMode;
            m_substituteObjectCharacter = GetComponentInChildren<SubstituteObjectCharacter>();

            // Two separate required lookups and subscriptions preserve partial work
            // if the second lookup or callback registration faults.
            s_cutsceneManagerRef.Get().OnCutsceneFinishing += OnCutsceneFinishing;
            s_cutsceneManagerRef.Get().OnCutsceneFinished += OnCutsceneFinished;
        }

        // 06001360: the out value is cleared even when the default-skin branch exits.
        public bool TryGetReplacement(Object boundObject, out Object replacement)
        {
            replacement = null;
            if (m_usesDefaultSkin)
                return false;
            return m_rigOriginal.TryGetReplacement(boundObject, m_rigReplacement, out replacement);
        }

        // 06001361: keep the original default-skin gate and original rig receiver.
        public void MoveAttachments()
        {
            if (m_usesDefaultSkin)
                return;
            m_rigOriginal.MoveAttachments(m_rigReplacement);
        }

        // 06001362: reposition first, then remap the substitution target. A successful
        // replacement uses a throwing Transform cast, including acceptance of null.
        public void SetPositions()
        {
            if (m_usesDefaultSkin)
                return;
            m_rigOriginal.SetPositions(m_rigReplacement);
            if (m_substituteObjectCharacter == null)
                return;
            if (TryGetReplacement(m_substituteObjectCharacter.GetSubstitutedTransform(), out Object replacement))
                m_substituteObjectCharacter.SetSubstitutedTransform((Transform)replacement);
        }

        // 06001363 and natural callback 06001368: preserve ghost materials, save and
        // clear the controller, destroy the authored prefab, then enable the current
        // replacement animator next frame. The callback reads the field at execution.
        public void ReplacementComplete()
        {
            if (m_usesDefaultSkin)
                return;
            if (m_rigOriginal.IsGhosted())
                m_rigReplacement.ApplyGhostedMaterials();
            m_animatorController = m_rigReplacement.CharacterAnimator.runtimeAnimatorController;
            m_rigReplacement.CharacterAnimator.runtimeAnimatorController = null;
            Object.Destroy(m_characterPrefab);
            Hardlight.Utils.CoroutineUtils.OnNextFrame(() => m_rigReplacement.CharacterAnimator.enabled = true);
        }

        // 06001364: unsubscribe before the optional Addressable lookup; release even
        // an invalid/default handle through the original manager with no new guard.
        public void OnCutsceneFinished()
        {
            s_cutsceneManagerRef.Get().OnCutsceneFinished -= OnCutsceneFinished;
            if (s_addressableManagerRef.TryGet(out AddressableManager addressableManager))
                addressableManager.ReleaseHandle(m_characterHandle);
        }

        // 06001365: removing the event occurs before either early exit. Original code
        // restores the controller only when a nondefault substitution component exists.
        private void OnCutsceneFinishing()
        {
            s_cutsceneManagerRef.Get().OnCutsceneFinishing -= OnCutsceneFinishing;
            if (m_usesDefaultSkin || m_substituteObjectCharacter == null)
                return;
            m_substituteObjectCharacter.enabled = false;
            m_rigReplacement.CharacterAnimator.runtimeAnimatorController = m_animatorController;
        }

        // 06001366 is the genuine base-only constructor. The two original static
        // reference initializers above reconstruct 06001367 in their shipped order.
        public CharacterCutscene() { }
    }
}
