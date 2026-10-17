// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMStateMoveGameObject; TypeDef 0x020000b7.
// 0x06000443 .ctor: x86_64 0x1ac5a20.
// 0x06000444 ConstructInstance: x86_64 0x1ac5a70.
// 0x06000445 SerialiseRuntimeToJSON: x86_64 0x1ac5b50.
// 0x06000446 DoUpdate: x86_64 0x1ac5bd0.
// Reconstructed from shipping metadata and the native implementation.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMStateMoveGameObject : FSMState
    {
        [Serializable]
        [GraphNodeDefaultName("MoveGameObject")]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class JSONCtorArgs
        {
            public Vector3 Translation;
            public Vector3 Rotation;
        }
        private readonly Vector3 m_translation;
        private readonly Vector3 m_rotation;

        public FSMStateMoveGameObject(FiniteStateMachine fsm, FSMIdentifier stateId, Vector3 translation, Vector3 rotation)
            : base(fsm, stateId) { m_translation = translation; m_rotation = rotation; }
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new FSMStateMoveGameObject(fsm, stateId, args.Translation, args.Rotation);
        }
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        { Translation = m_translation, Rotation = m_rotation });
        // The user must be the GameObject user (plain cast). Both rates scale by
        // the raw delta for every update type: Translate (self space), then Rotate.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            Transform transform = user.GetAs<FSMUserBehaviourGameObject>().gameObject.transform;
            transform.Translate(m_translation * context.DeltaTime);
            transform.Rotate(m_rotation * context.DeltaTime);
        }
    }
}
