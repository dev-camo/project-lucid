using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 02000772: passes the transform of a collider with the
    // required tag (any tag when empty) to the enter and exit events. A level
    // activation while still triggered sends the exit event with null.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformAssignEventTrigger : MonoBehaviour
    {
        [SerializeField]
        private string m_requiredTag = "Player";
        [SerializeField]
        [Tooltip("Colliding transform is passed as parameter.")]
        private UnityEvent<Transform> m_onTriggerEnter;
        [Tooltip("Colliding transform is passed as parameter unless 'set null on exit' is enabled.")]
        [SerializeField]
        private UnityEvent<Transform> m_onTriggerExit;
        [Tooltip("Passes 'null' as the parameter in onTriggerExit.")]
        [SerializeField]
        private bool m_setNullOnExit;
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
        private bool m_hasTriggered;

        protected virtual bool SetNullOnExit => m_setNullOnExit;

        private void Start()
        {
            m_levelManagerRef.InvokeOnValid(OnLevelManagerValid);
        }

        private void OnDestroy()
        {
            if (m_levelManagerRef.TryGet(out LevelManager levelManager))
                levelManager.RemoveLevelActivatedAction(OnLevelActivated);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsValidTag(other.tag))
                return;
            m_hasTriggered = true;
            m_onTriggerEnter.Invoke(GetTargetTransform(other));
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsValidTag(other.tag))
                return;
            InvokeOnTriggerExit(SetNullOnExit ? null : GetTargetTransform(other));
        }

        private bool IsValidTag(string tag)
        {
            return string.IsNullOrEmpty(m_requiredTag) || m_requiredTag.Equals(tag);
        }

        protected virtual Transform GetTargetTransform(Collider other)
        {
            return other.transform;
        }

        private void OnLevelManagerValid(LevelManager levelManager)
        {
            levelManager.InvokeOnLevelActivated(OnLevelActivated);
        }

        private void OnLevelActivated(LevelManagerLevel _)
        {
            if (m_hasTriggered)
                InvokeOnTriggerExit(null);
        }

        private void InvokeOnTriggerExit(Transform targetTransform)
        {
            m_hasTriggered = false;
            m_onTriggerExit.Invoke(targetTransform);
        }
    }
}
