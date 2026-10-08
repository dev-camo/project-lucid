using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000444. Authored cutscene object substitution.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SubstituteObject : MonoBehaviour
    {
        [Header("Substitute spawns using:")]
        [SerializeField] private bool m_realObjectPosition;
        [SerializeField] private bool m_realObjectRotation;
        [Header("Real object returns using:")]
        [SerializeField] protected bool m_substitutePosition;
        [SerializeField] protected bool m_substituteRotation;
        [Space(10f)]
        [SerializeField] protected Transform m_replaceWithTransform;
        [Tooltip("Tick this to have the replacement object swap places with the real object beyond the end of the cutscene. Useful for leaving a boss body on the ground.")]
        [SerializeField] private bool m_reparentToPersistCutsceneEnd;
        protected GameObject m_replacedObject;
        protected Transform m_realAnimationRoot;

        // 0x0600193e. Hide first, then copy the selected world pose, then optionally
        // persist the substitute under the original parent. No null guards are added.
        protected virtual void OnEnable()
        {
            m_replacedObject.gameObject.SetActive(false);
            Transform realTransform = m_replacedObject.transform;
            if (m_realObjectPosition)
                m_replaceWithTransform.position = realTransform.position;
            if (m_realObjectRotation)
                m_replaceWithTransform.rotation = realTransform.rotation;
            if (m_reparentToPersistCutsceneEnd)
                m_replaceWithTransform.SetParent(realTransform.parent, true);
        }

        // 0x0600193f. A persisted substitute bypasses restoration entirely.
        // The original position correction subtracts the animation root's local
        // offset directly; the original rotation multiplication order is preserved.
        protected virtual void OnDisable()
        {
            if (m_reparentToPersistCutsceneEnd)
                return;
            m_replacedObject.gameObject.SetActive(true);
            m_realAnimationRoot.gameObject.SetActive(true);
            Transform realTransform = m_replacedObject.transform;
            if (m_substitutePosition)
                realTransform.position = m_replaceWithTransform.position - m_realAnimationRoot.localPosition;
            if (m_substituteRotation)
                realTransform.rotation = m_replaceWithTransform.rotation * Quaternion.Inverse(m_realAnimationRoot.localRotation);
        }

        // 0x06001940 is the original base-only constructor; all flags default false.
        public SubstituteObject() { }
    }
}
