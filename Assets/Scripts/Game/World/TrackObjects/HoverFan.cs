using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000ac7: lends the character this fan's hover data
    // while it is inside the trigger.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Collider))]
    public class HoverFan : CharacterTrigger
    {
        [SerializeField] private HoverData m_hoverData;
        private StackableDataHandle m_characterHoverFanHandle;

        // 06003e3c: each entry adds a new override and replaces the stored handle;
        // an earlier handle is not removed first.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            m_characterHoverFanHandle = character.AddModifierOverride((int)GameplayModifierType.HoverData, m_hoverData);
        }

        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            character.RemoveModifierOverrides(m_characterHoverFanHandle);
            m_characterHoverFanHandle = null;
        }

        // 06003e3e: a flat box at the target height, and one at each oscillation
        // bound along this transform's up axis.
        private void OnDrawGizmosSelected()
        {
            Collider fanCollider = GetComponent<Collider>();
            if (fanCollider == null || m_hoverData.Origin == null)
                return;
            Vector3 boundsSize = fanCollider.bounds.size;
            Vector3 size = new Vector3(boundsSize.x, 0f, boundsSize.z);
            Gizmos.color = Color.red;
            Vector3 targetPosition = m_hoverData.Origin.position;
            targetPosition.y += m_hoverData.TargetHeight;
            Gizmos.DrawWireCube(targetPosition, size);
            Gizmos.color = Color.grey;
            Gizmos.DrawWireCube(targetPosition + transform.up * m_hoverData.OscillateHeightBounds, size);
            Gizmos.DrawWireCube(targetPosition - transform.up * m_hoverData.OscillateHeightBounds, size);
        }
    }
}
