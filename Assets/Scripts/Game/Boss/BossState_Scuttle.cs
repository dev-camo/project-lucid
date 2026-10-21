using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000211; bodies translated from the 1.10.1 x86-64 binary.
    // While the crab scuttles, moves the boss sideways and forwards at the speed the ability's full-speed curve gives for the
    // squared distance to its target (in the boss's local space), turns it towards the current character and drops it onto
    // the track below; the boss stops when the state is left.
    [GraphNodeMenuFormat("Boss/Crab/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("Scuttling")]
    public class BossState_Scuttle : ActorState_Ability<ActorAbility>
    {
        private const float RaycastStartHeightBoost = 20f;
        private const float RaycastDistanceMultiplier = 2f;

        private readonly AbilityDefinition m_abilityDefinition;

        protected BossState_Scuttle(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_Scuttle(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        // The animator's direction inputs and the local velocity take the sign of the target's local offset.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null)
                return;
            BossAbility_Scuttle scuttle = ability as BossAbility_Scuttle;
            Vector3 target = scuttle.CurrentTargetPosition;
            float newY = GetHeightFromRaycast(boss, boss.WorldPosition);
            Vector3 local = boss.transform.InverseTransformPoint(target);
            BossAbilityDefinition_MovementScuttle definition = scuttle.Definition;
            // The product is rounded to single precision before the division, as the native mulss and divss do.
            float normalisedDistanceX = (float)(local.x * local.x) / definition.TargetDistanceForFullSpeedSquared;
            float normalisedDistanceZ = (float)(local.z * local.z) / definition.TargetDistanceForFullSpeedSquared;
            float speedX = definition.FullSpeedCurve.Evaluate(normalisedDistanceX) * definition.ScuttleSpeed;
            float speedZ = definition.FullSpeedCurve.Evaluate(normalisedDistanceZ) * definition.ScuttleSpeed;
            SetBossMovementDirections(boss.Animator,
                local.x > 0f ? normalisedDistanceX : -normalisedDistanceX,
                local.z > 0f ? normalisedDistanceZ : -normalisedDistanceZ);
            local.x = local.x > 0f ? speedX : -speedX;
            local.z = local.z > 0f ? speedZ : -speedZ;
            Character character = ProcessManager.GetSystem<CharacterManager>().GetCurrentCharacterUnsafe();
            float angle = Vector3.SignedAngle(boss.ForwardDirection, character.WorldPosition - boss.WorldPosition, boss.UpDirection);
            float direction = 0f;
            if (Mathf.Abs(angle) > definition.FaceAngleTolerance)
                direction = angle > 0f ? 1f : -1f;
            MoveToward(boss, local, direction * definition.TurnSpeed * context.DeltaTime, newY);
        }

        private void MoveToward(Boss boss, Vector3 destination, float turnAmount, float newY)
        {
            boss.SetWorldRotation(Quaternion.AngleAxis(turnAmount, boss.UpDirection) * boss.WorldRotation);
            boss.SetLocalVelocity(destination);
            Vector3 position = boss.WorldPosition;
            boss.SetWorldPosition(new Vector3(position.x, newY, position.z));
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            user.GetAs<Boss>().SetWorldVelocity(Vector3.zero);
        }

        // The height of the ground below the position along the boss's gravity, or the position's own height without a hit.
        private float GetHeightFromRaycast(Boss boss, Vector3 worldPosition)
        {
            if (Physics.Raycast(new Ray(worldPosition, -boss.UpDirection), out RaycastHit hit,
                (float)(worldPosition.y + RaycastStartHeightBoost) * RaycastDistanceMultiplier, boss.GetTraits().TrackMask))
                return hit.point.y;
            return worldPosition.y;
        }
    }
}
