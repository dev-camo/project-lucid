// Original Game.Runtime.dll:HardlightProject.ApplicationStateIntroVideoCleanup.
// Complete methods: 0x06000690, 0x06000691, 0x06000692.
// arm64 ranges: 0x06000690 0x6220cc..0x62218c, 0x06000691 0x62218c..0x622218, 0x06000692 0x622218..0x6222a0.
// x86_64 ranges: 0x06000690 0x6489d0..0x648a70, 0x06000691 0x648a70..0x648ae0, 0x06000692 0x648ae0..0x648b50.
// The unchanged original App reference initializer precedes FSM base registration.
// Base entry precedes App lookup and SetHasGameplayControl(false, false).
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    public class ApplicationStateIntroVideoCleanup : FSMState
    {
        // Original field 0x04000402@0x28. Native .ctor resolves the App ref before
        // base registration, as this field initializer does.
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);

        // Game.Runtime.dll 0x06000690; ARM64 0x6220cc.
        private ApplicationStateIntroVideoCleanup(FiniteStateMachine fsm, string stateName)
            : base(fsm, stateName) { }

        // 0x06000691; ARM64 0x62218c. Preserve original ID/name conversion; JSON is ignored.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId,
            string jsonCtorArgs) => new ApplicationStateIntroVideoCleanup(fsm, transitionId);

        // 0x06000692; ARM64 0x622218. Base enter precedes checked App lookup and
        // original SetHasGameplayControl(false, false).
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            m_appRef.Get().SetHasGameplayControl(false, false);
        }
    }
}
