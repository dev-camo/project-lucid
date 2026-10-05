using System.Collections.Generic;

namespace Hardlight
{
    // Original HLUnityCore.Runtime.dll contracts from IL2CPP metadata v31.1.
    // Interface declarations describe shape only; they contain no inferred behavior.
    public interface IGraphStorage
    {
        public sealed class Var<T>
        {
            public T Value;

            // HLUnityCore.Runtime.dll:Hardlight.IGraphStorage+Var`1:0x060008be;
            // native generic constructor 0xd912e4 writes the supplied value.
            public Var(T value) { Value = value; }
        }

        void Initialise();
        void Clear();
        void SetValue<T>(GraphStorageKey storageKey, T value);
        T GetValue<T>(GraphStorageKey storageKey, T defaultValue = default(T), bool storeDefault = true);
        void RemoveValue<T>(GraphStorageKey storageKey);
        IReadOnlyDictionary<GraphStorageKey, object> GetCollection();
    }

    public interface IGraphUser
    {
        IGraphStorage Storage { get; }
        void DestroyUser();
    }

    public interface IFSMState
    {
        int FSMId { get; }
        int StateId { get; }
        void OnEnter(IGraphUser user, FSMStateChangeAction action);
        void OnLeave(IGraphUser user, FSMStateChangeAction action);
        void Update(IGraphUser user, FSMUpdateContext context);
        bool HasFinished(IGraphUser user);
        bool IsEndState();
        void AddTransition(IFSMTransition transition, IFSMState transitionTo);
        FSMStateChangeAction ShouldTransition(IGraphUser user, FSMUpdateContext context);
        IReadOnlyCollection<FSMStateTransition> GetStateTransitions();
        string SerialiseRuntimeToJSON();
        List<FiniteStateMachine> GetDependencies();
    }

    public interface IFSMTransition
    {
        int FSMId { get; }
        int TransitionId { get; }
        void OnEnter(IGraphUser user, FSMStateChangeAction action);
        void OnLeave(IGraphUser user, FSMStateChangeAction action);
        void OnFire(IGraphUser user, FSMUpdateContext context, FSMStateChangeAction action);
        bool Update(IGraphUser user, FSMUpdateContext context);
        string SerialiseRuntimeToJSON();
        List<FiniteStateMachine> GetDependencies();
    }
}
