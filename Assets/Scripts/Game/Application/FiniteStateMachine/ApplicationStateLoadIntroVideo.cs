// Original Game.Runtime.dll:HardlightProject.ApplicationStateLoadIntroVideo.
// Complete methods: 0x060006b2, 0x060006b3, 0x060006b4.
// arm64 ranges: 0x060006b2 0x62362c..0x623630, 0x060006b3 0x62374c..0x623818, 0x060006b4 0x623818..0x6238a0.
// x86_64 ranges: 0x060006b2 0x649e00..0x649e10, 0x060006b3 0x649f10..0x649fc0, 0x060006b4 0x649fc0..0x64a030.
// The genuine LoadScene base owns the inherited JSON fields, App reference and loading updates.
// Entry invokes its inherited base entry then SetHasGameplayControl(true, true); no eager scene request.
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    public sealed class ApplicationStateLoadIntroVideo : ApplicationStateLoadScene
    {
        private ApplicationStateLoadIntroVideo(FiniteStateMachine fsm, string stateName,
            string sceneName, bool useAppStorage) : base(fsm, stateName, sceneName, useAppStorage) { }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new ApplicationStateLoadIntroVideo(fsm, stateId, args.SceneName, args.UseAppStorage);
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            m_appRef.Get().SetHasGameplayControl(true, true);
        }
    }
}
