// Original HLUnityUI.Runtime Hardlight.GUIBlockerBoxColliderResizer 020000c5 (MethodDefs
// 0x0600055c..5f), translated from the x86-64 bodies (0x1B65F10..0x1B660A0) and metadata.
// No shipped .prefab/.unity/.asset in the 2026-10-05 reference export references the
// exported script GUID 6a3a622db7fc0d3c6d74f7bea6f4082a, so the script stays unbound
// with a fresh GUID.
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [RequireComponent(typeof(BoxCollider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class GUIBlockerBoxColliderResizer : UIBehaviour
    {
        [SerializeField]
        private RectTransform m_trackedTransform;
        [SerializeField]
        private BoxCollider m_boxCollider;

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateCollider();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            UpdateCollider();
        }

        // 0x0600055e: one unit deep, centred on the tracked rect. The centre reads the
        // collider's size back after setting it, and each component is read separately
        // (pivots at 0x1B65FF8/0x1B66007, sizes at 0x1B66019/0x1B66028).
        private void UpdateCollider()
        {
            if (m_trackedTransform == null || m_boxCollider == null) return;

            m_boxCollider.size = new Vector3(m_trackedTransform.rect.width, m_trackedTransform.rect.height, 1f);
            Vector2 pivotOffset = new Vector2(0.5f - m_trackedTransform.pivot.x, 0.5f - m_trackedTransform.pivot.y);
            m_boxCollider.center = new Vector3(m_boxCollider.size.x * pivotOffset.x, m_boxCollider.size.y * pivotOffset.y, 0f);
        }
    }
}
