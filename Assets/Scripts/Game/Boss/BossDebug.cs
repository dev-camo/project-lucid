using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020001f5; 0x700A20..0x700BC0.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossDebug : ActorDebug
    {
        protected override string ContainerIdentifierName => "uicontaineridentifier_boss_debug_info"; // 06000b64

        // 06000b65: the base Init (inlined natively) stores the actor, then the info toggle is bound.
        public override void Init(Actor actor)
        {
            base.Init(actor);
            ControlMapping.Subscribe(GameInput.BossDebugInfo, OnBossInfo, InputTrigger.Down, -1);
        }

        // 06000b66: the axis amount is ignored.
        private void OnBossInfo(float amount) => ToggleDebugInfo();

        private void OnDestroy() => ControlMapping.Unsubscribe(GameInput.BossDebugInfo, OnBossInfo, -1);
    }
}
