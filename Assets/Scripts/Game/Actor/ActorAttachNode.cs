// Complete original attach-node class candidate, including its two genuine nested types.
// Native-derived; real Actor and Unity pose behavior remain unaccepted dependencies.
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ActorAttachNode : ISerializationCallbackReceiver // original02000045
    {
        [HideInInspector] public string Name;
        [SerializeField] private ActorAttachPointType m_attachPointType;
        [SerializeField] private List<AttachNodeForm> m_nodeForms;
        private List<ChildInfo> m_children = new List<ChildInfo>();
        private AttachNodeForm m_currentNodeForm;
        private bool m_changedSinceLastUpdate;

        public ActorAttachPointType AttachPointType => m_attachPointType; // original0600035b

        public void Initialise() // 35c; authored forms supply all transforms and offsets.
        {
            foreach (AttachNodeForm form in m_nodeForms) form.Initialise();
        }

        public void UpdateForm(ActorFormType formType) // 35d
        {
            AttachNodeForm previous = m_currentNodeForm;
            m_currentNodeForm = null;
            foreach (AttachNodeForm form in m_nodeForms)
            {
                if (form.AllForms || form.FormType == formType)
                {
                    m_currentNodeForm = form;
                    break;
                }
            }
            // Keep a pending form change latched until an actual update consumes it.
            if (previous != m_currentNodeForm) m_changedSinceLastUpdate = true;
        }

        public void Add(Actor actor, Transform child, bool syncOrientationWithVelocity) // 35e
        {
            if (TryGetChildInfo(child, out ChildInfo existing, out int index)) return;
            Vector3 localPosition = child.localPosition;
            Quaternion localRotation = child.localRotation;
            ChildInfo info = new ChildInfo(actor, child, localPosition, localRotation,
                syncOrientationWithVelocity);
            // Native code reloads the children list after the transform getters.
            m_children.Add(info);
            if (m_currentNodeForm == null) return;
            Transform parent = m_currentNodeForm.Transform;
            if (parent == null) return;
            Vector3 parentPosition = parent.position;
            Quaternion parentRotation = parent.rotation;
            Quaternion worldRotation = parentRotation * m_currentNodeForm.Quaternion;
            UpdateChildPosition(info, parentPosition, parentRotation, worldRotation,
                m_changedSinceLastUpdate);
        }

        private bool TryGetChildInfo(Transform child, out ChildInfo info, out int index) // 35f
        {
            index = 0;
            foreach (ChildInfo candidate in m_children)
            {
                if (candidate.Transform == child)
                {
                    info = candidate;
                    return true;
                }
                index++;
            }
            info = default;
            return false;
        }

        public void Update() // 360
        {
            if (m_currentNodeForm != null)
            {
                // A populated form with no children keeps the pending change latched.
                if (m_children.Count == 0) return;
                Transform parent = m_currentNodeForm.Transform;
                Vector3 parentPosition = parent.position;
                Quaternion parentRotation = parent.rotation;
                Quaternion worldRotation = parentRotation * m_currentNodeForm.Quaternion;
                foreach (ChildInfo child in m_children)
                    UpdateChildPosition(child, parentPosition, parentRotation, worldRotation,
                        m_changedSinceLastUpdate);
                m_changedSinceLastUpdate = false;
            }
            else if (m_changedSinceLastUpdate)
            {
                foreach (ChildInfo child in m_children) child.Transform.gameObject.SetActive(false);
                m_changedSinceLastUpdate = false;
            }
        }

        private void UpdateChildPosition(ChildInfo child, Vector3 parentPosition,
            Quaternion parentRotation, Quaternion worldRotation, bool setActive) // 361
        {
            Vector3 position = parentPosition + parentRotation *
                (m_currentNodeForm.Offset + child.LocalPosition);
            Transform target;
            if (!child.SyncOrientationWithVelocity)
            {
                target = child.Transform;
                target.SetPositionAndRotation(position, worldRotation * child.LocalRotation);
            }
            else if (child.Actor.WorldVelocityMagnitude > 0.0001f)
            {
                // Ordered comparisons retain the native NaN fallbacks. LookRotation
                // consumes fresh Actor axes after the inverse-parent calculation.
                if (Mathf.Abs(Vector3.Dot(child.Actor.WorldVelocityNormalised,
                    child.Actor.UpDirection)) < 0.9999f)
                {
                    Quaternion localRotation = Quaternion.Inverse(parentRotation) * worldRotation;
                    worldRotation = Quaternion.LookRotation(child.Actor.WorldVelocityNormalised,
                        child.Actor.UpDirection) * localRotation;
                }
                target = child.Transform;
                target.SetPositionAndRotation(position, worldRotation * child.LocalRotation);
            }
            else
            {
                target = child.Transform;
                target.position = position;
            }
            if (setActive) target.gameObject.SetActive(true);
        }

        public void Remove(Transform child, bool keepPosition = false, bool keepRotation = false) // 362
        {
            if (!TryGetChildInfo(child, out ChildInfo info, out int index)) return;
            if (!keepPosition && !keepRotation)
                child.SetLocalPositionAndRotation(info.LocalPosition, info.LocalRotation);
            else if (keepPosition)
            {
                if (!keepRotation) child.localRotation = info.LocalRotation;
            }
            else child.localPosition = info.LocalPosition;
            // Removal follows successful restoration of the stored local pose.
            m_children.RemoveAt(index);
        }

        public void OnBeforeSerialize() { Name = m_attachPointType.GetString(); } // 363
        public void OnAfterDeserialize() { Name = m_attachPointType.GetString(); } // 364

        public bool TryGetCurrentTransform(out Transform innerTransform) // 365
        {
            AttachNodeForm form = m_currentNodeForm;
            innerTransform = form != null ? form.Transform : null;
            // A selected form counts as present even when its Transform is null.
            return form != null;
        }

        public ActorAttachNode() { } // 366; only the children-list initializer.

        private struct ChildInfo // original02000046; readonly fields, ordinary struct.
        {
            public readonly Actor Actor;
            public readonly Transform Transform;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly bool SyncOrientationWithVelocity;
            public ChildInfo(Actor actor, Transform transform, Vector3 localPosition,
                Quaternion localRotation, bool syncOrientationWithVelocity) // 367
            {
                Actor = actor;
                Transform = transform;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                SyncOrientationWithVelocity = syncOrientationWithVelocity;
            }
        }

        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public class AttachNodeForm : ISerializationCallbackReceiver // original02000047
        {
            [HideInInspector] public string Name;
            [SerializeField] private Transform m_transform;
            [SerializeField] private Vector3 m_offset;
            [SerializeField] private Vector3 m_rotation;
            [SerializeField] private ActorFormType m_formType;
            [SerializeField] private bool m_allForms;
            public Transform Transform => m_transform; // 368
            public Vector3 Offset => m_offset; // 369
            public ActorFormType FormType => m_formType; // 36a
            public bool AllForms => m_allForms; // 36b
            public Quaternion Quaternion { get; private set; } // 36c/36d
            public void Initialise() { Quaternion = UnityEngine.Quaternion.Euler(m_rotation); } // 36e
            public void OnBeforeSerialize() { Name = m_formType.GetString(); } // 36f
            public void OnAfterDeserialize() { Name = m_formType.GetString(); } // 370
            public AttachNodeForm() { } // 371; all fields retain their original defaults.
        }
    }
}
