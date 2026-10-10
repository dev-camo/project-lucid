using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class ActorAbility
    {
        public abstract AbilityDefinition DefinitionBase { get; }
        public bool Enabled { get; private set; }
        public bool AbilityFinished { get; protected set; }
        protected int AbilityId => (int)DefinitionBase.AbilityType;
        public bool UpdateEnabled { get; set; }

        // Original 0600032b: initialization resets these flags before the virtual
        // definition callback. Actor and AbilityFinished are deliberately untouched.
        public virtual void Initialise(Actor actor, AbilityDefinition definition)
        {
            Enabled = false;
            UpdateEnabled = true;
            definition.CalculateCachedValues();
        }

        public virtual void PostInitialise()
        {
            if (DefinitionBase.StartEnabled)
                Enter();
        }

        // Original 0600032d: repeated entry still invokes the hook.
        public void Enter()
        {
            Enabled = true;
            DoOnEnter();
        }

        public void Update(float deltaTime)
        {
            if (Enabled && UpdateEnabled)
                DoOnUpdate(deltaTime);
        }

        public void Update(CharacterBrain brain, float deltaTime)
        {
            if (Enabled && UpdateEnabled)
                DoOnUpdate(brain, deltaTime);
        }

        // Original post/late phases depend on Enabled alone.
        public void PostUpdate(float deltaTime)
        {
            if (Enabled)
                DoOnPostUpdate(deltaTime);
        }

        public void LateUpdate(float deltaTime)
        {
            if (Enabled)
                DoOnLateUpdate(deltaTime);
        }

        public void Leave()
        {
            if (!Enabled)
                return;
            Enabled = false;
            DoOnLeave();
        }

        public virtual void Close()
        {
            if (!Enabled)
                return;
            Enabled = false;
            DoOnLeave();
        }

        // These optional hooks have genuine RET bodies at ARM 4ef7e8..4ef810.
        // They are original extension points, not unresolved implementation stubs.
        protected virtual void DoOnEnter() { }
        protected virtual void DoOnUpdate(float deltaTime) { }
        protected virtual void DoOnUpdate(CharacterBrain brain, float deltaTime) { }
        protected virtual void DoOnPostUpdate(float deltaTime) { }
        protected virtual void DoOnLateUpdate(float deltaTime) { }
        protected virtual void DoOnLeave() { }
        public virtual void OnEnableUI() { }
        public virtual bool GetUIText(StringBuilder stringInfoBuilder) => false;
        public virtual void RegisterForCollisions() { }
        public virtual void UnregisterForCollisions() { }

        // Original 0600033e: preserve AppendLine and hook order, including the
        // final newline appended after the overridable extra-info callback.
        public void GetDebugInfo(StringBuilder nameInfo, StringBuilder enabledInfo,
            StringBuilder updateInfo, StringBuilder extraInfo)
        {
            nameInfo.AppendLine(GetName());
            enabledInfo.AppendLine(string.Format("{0}", Enabled));
            updateInfo.AppendLine(string.Format("{0}", UpdateEnabled));
            GetDebugExtraInfo(extraInfo);
            extraInfo.AppendLine();
        }

        public virtual bool OnDrawGizmosSelected() => Enabled;
        protected virtual void GetDebugExtraInfo(StringBuilder extraInfo) { }
        private string GetName() => DefinitionBase.AbilityType.ToString();
        public virtual ActorFormType GetFormType() => DefinitionBase.FormType;
        public virtual ActorAnimationDefinition GetAnimationDefinition() => DefinitionBase.AnimationDefinition;
        protected ActorAbility() { }
    }
}
