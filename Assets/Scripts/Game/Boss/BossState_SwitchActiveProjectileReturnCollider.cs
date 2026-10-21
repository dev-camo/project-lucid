using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000226; bodies translated from the 1.10.1 x86-64 binary.
    // Activates the guardian's phase-four projectile spawn collider and deactivates its projectile return collider.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeDefaultName("SwitchActiveProjectileReturnCollider")]
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossState_SwitchActiveProjectileReturnCollider : ActorState_Effect
    {
        protected BossState_SwitchActiveProjectileReturnCollider(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SwitchActiveProjectileReturnCollider(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Boss boss = user.GetAs<Boss>();
            Transform spawnCollider = boss.GetSpawnPoint<Collider>(ActorAttachPointType.BossGuardian_ProjectileSpawnPoint_Phase4);
            Transform returnCollider = boss.GetSpawnPoint<Collider>(ActorAttachPointType.BossGuardian_ProjectileReturnPoint);
            spawnCollider.gameObject.SetActive(true);
            returnCollider.gameObject.SetActive(false);
        }
    }
}
