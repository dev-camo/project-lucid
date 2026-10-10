// Complete original Actor attachment dispatch candidate; real ActorAttachNode required.
// Private, no source/engine/behavior acceptance or substituted dependencies.
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks,false)]
    [RequireComponent(typeof(ActorFormHandler))]
    [Il2CppSetOption(Option.NullChecks,false)]
    public class ActorAttachPoints : MonoBehaviour // original02000048
    {
        [SerializeField] private ActorAttachNode[] m_nodes;
        private readonly Dictionary<ActorAttachPointType,ActorAttachNode> m_nodesDictionary =
            new Dictionary<ActorAttachPointType,ActorAttachNode>(HardlightEnumComparers.ActorAttachPointTypeComparer);
        private ActorFormHandler m_actorFormHandler;
        private ActorFormType m_currentForm;

        public void Awake() // original06000372: initialize only first occurrence of each key.
        {
            m_actorFormHandler = GetComponent<ActorFormHandler>();
            if (m_nodes.Length == 0) { enabled = false; return; }
            foreach (ActorAttachNode node in m_nodes)
            {
                if (m_nodesDictionary.ContainsKey(node.AttachPointType)) continue;
                node.Initialise();
                m_nodesDictionary[node.AttachPointType] = node;
            }
        }

        public bool Attach(Actor actor, ActorAttachPointType attachPointType,
            Transform attachTransform, bool syncOrientationWithVelocity) // original06000373
        {
            return NodeExecute(attachPointType,
                node => node.Add(actor,attachTransform,syncOrientationWithVelocity));
        }

        public bool Detach(ActorAttachPointType attachPointType, Transform detachTransform,
            bool keepPosition = false, bool keepRotation = false) // original06000374, naturallambda uses n.
        {
            return NodeExecute(attachPointType,n => n.Remove(detachTransform,keepPosition,keepRotation));
        }

        public bool TryGetNodeTransform(ActorAttachPointType attachPointType, out Transform nodeTransform)
        {
            bool foundTransform = false;
            Transform innerTransform = null;
            bool executed = NodeExecute(attachPointType,
                node => foundTransform = node.TryGetCurrentTransform(out innerTransform));
            nodeTransform = innerTransform;
            return executed && foundTransform;
        }

        private bool NodeExecute(ActorAttachPointType attachPointType, Action<ActorAttachNode> action)
        {
            if (m_nodesDictionary.TryGetValue(attachPointType,out ActorAttachNode node))
            {
                action(node);
                return true;
            }
            return false;
        }

        private void LateUpdate() // original06000377: publish form before any node updates.
        {
            ActorFormType formType = m_actorFormHandler.CurrentFormType;
            ActorFormType previousForm = m_currentForm;
            m_currentForm = formType;
            foreach (ActorAttachNode node in m_nodes)
            {
                if (formType != previousForm) node.UpdateForm(formType);
                node.Update();
            }
        }

        public ActorAttachPoints() { } // original06000378; only dictionary initializer.
    }
}
