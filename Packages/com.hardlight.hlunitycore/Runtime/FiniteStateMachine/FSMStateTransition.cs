namespace Hardlight
{
    public readonly struct FSMStateTransition
    {
        public readonly IFSMTransition Transition;
        public readonly IFSMState ToState;

        // HLUnityCore.Runtime.dll:Hardlight.FSMStateTransition:0x0600042a;
        // arm64 0x1ac65e0 stores both references without other work.
        public FSMStateTransition(IFSMTransition transition, IFSMState toState)
        {
            Transition = transition;
            ToState = toState;
        }
    }
}
