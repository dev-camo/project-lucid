// Original Game.Runtime.dll:HardlightProject.ApplicationStateBootFairplay.
// Complete methods: 0x06000642, 0x06000643, 0x06000644.
// arm64 ranges: 0x06000642 0x513120..0x513160, 0x06000643 0x513160..0x513204, 0x06000644 0x513204..0x51320c.
// x86_64 ranges: 0x06000642 0x53ca60..0x53ca90, 0x06000643 0x53ca90..0x53cb10, 0x06000644 0x53cb10..0x53cb20.
// Original entry calls FairPlay.RegisterWithFairPlay directly; it does not call base.DoOnEnter.
// JSON and user/action are unused. Original Apple registration remains here for preservation.
using Apple.Arcade;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplicationStateBootFairplay : FSMState
    {
        private ApplicationStateBootFairplay(FiniteStateMachine fsm, string stateName)
            : base(fsm, stateName) { }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new ApplicationStateBootFairplay(fsm, transitionId);
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            FairPlay.RegisterWithFairPlay();
        }
    }
}
