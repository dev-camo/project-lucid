using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("Air")]
    [GraphNodeMenuFormat("Character/{0}")]
    public class CharacterState_Air : CharacterState_Ability<CharacterAbility_MovementAir>
    {
        protected CharacterState_Air(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            AbilityJSONCtorArgs args = JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs);
            return new CharacterState_Air(fsm, transitionId, args);
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Character character = user.GetAs<Character>();
            CharacterAbility_MovementAir ability = GetAbility(character);
            CharacterTracker tracker = character.Tracker;
            tracker.ApplyVerticalVelocity(tracker.TrackerVelocityLocal.y);
            SetupFSMValues(character, ability);
            UpdateBoostSuspendTime(character, ability, 0f);
            character.TriggerBurstAttackAir(false);
            CharacterSpinDashRoll rolling = character.SpinDashRoll;
            float deactivateTime = ability.Definition.RollingDeactivationTime;
            rolling.Deactivate(deactivateTime);
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Character character = user.GetAs<Character>();
            CharacterAbility_MovementAir ability = GetAbility(character);
            CharacterAbilityDefinition_MovementAir definition = ability.Definition;
            if (ability.Enabled && ability.UpdateEnabled)
            {
                ProcessAirControl(character, ability, updateContext.DeltaTime);
                float elapsed = character.Storage.AdjustValue(ActorFSMKeys.AirProjectedBufferTimer, updateContext.DeltaTime, 0f);
                if (elapsed > definition.LandingProjectionBufferTime)
                    UpdateOrientation(character, definition.LandingProjectionAlignToWorldUpRate, updateContext.DeltaTime);
            }
            if (!character.UpdateJumpOnRailTracker(updateContext.DeltaTime))
            {
                character.Tracker.ClampToLateralBoundsViaSurfaceRaycast();
                character.Tracker.UpdateOffTracker(updateContext.DeltaTime);
            }
            UpdateDirectionOverride(character, ability, updateContext.DeltaTime);
            UpdateBoostSuspendTime(character, ability, updateContext.DeltaTime);
            CharacterPhysicsUtilities.StickToCollider(character, updateContext.DeltaTime);
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Character character = user.GetAs<Character>();
            CharacterAbility_MovementAir ability = GetAbility(character);
            ResetDirectionOverride(character);
            ClearFSMValues(character);
            UpdateBoostSuspendTime(character, ability, -1f);
            character.SpinDashRoll.ClearDeactivate();
        }

        private void ProcessAirControl(Character character, CharacterAbility_MovementAir ability, float deltaTime)
        {
            float lockedUntil = character.Storage.GetValue(ActorFSMKeys.AirControlsLockTimeEnd, 0f, true);
            if (lockedUntil > 0f)
            {
                if (character.GetTotalFixedTime() < lockedUntil) return;
                character.Storage.RemoveValue<float>(ActorFSMKeys.AirControlsLockTimeEnd);
            }
            bool maintainHeading = MaintainHeading(ability);
            bool disabled = character.Storage.GetValue(ActorFSMKeys.AirDecelerationDisabled, false, true);
            CharacterAbilityDefinition_MovementAir definition = ability.Definition;
            CharacterMovementUtilities.ProcessAirControl(character, ability, definition, maintainHeading, deltaTime, !disabled);
        }

        public override void DebugInfo(Actor actor, StringBuilder stringInfoBuilder)
        {
            base.DebugInfo(actor, stringInfoBuilder);
            Character character = actor as Character;
            if (character != null) character.DebugGetJumpOnRailInfo(stringInfoBuilder);
        }

        private void UpdateOrientation(Character character, float alignToWorldUpRate, float deltaTime)
        {
            Vector3 up = character.WorldUp;
            Vector3 forward = character.ForwardDirection;
            Vector3 projected = Vector3.ProjectOnPlane(forward, up);
            Quaternion current = character.WorldRotation;
            Quaternion target = Quaternion.LookRotation(projected.normalized, up);
            character.SetWorldRotation(Quaternion.RotateTowards(current, target, alignToWorldUpRate * deltaTime));
        }

        private void SetupFSMValues(Character character, CharacterAbility_MovementAir ability)
        {
            bool disableBuffer = character.Storage.GetValue(ActorFSMKeys.AirProjectedBufferDisabled, false, true);
            float bufferTime = disableBuffer ? ability.Definition.LandingProjectionBufferTime : 0f;
            character.Storage.SetValue(ActorFSMKeys.AirProjectedBufferTimer, bufferTime);
            character.Storage.SetValue(ActorFSMKeys.CanQueueJump, true);
            character.Storage.SetValue(ActorFSMKeys.CanQueueBoost, true);
            character.Storage.SetValue(ActorFSMKeys.AirAbilityDirectionOverrideAnimationElapsedSeconds, 0f);
        }

        private void ClearFSMValues(Character character)
        {
            character.Storage.RemoveValue<bool>(ActorFSMKeys.AirProjectedBufferDisabled);
            character.Storage.RemoveValue<float>(ActorFSMKeys.AirProjectedBufferTimer);
            character.Storage.RemoveValue<bool>(ActorFSMKeys.AirDecelerationDisabled);
            character.Storage.RemoveValue<float>(ActorFSMKeys.AirControlsLockTimeEnd);
            character.Storage.RemoveValue<bool>(ActorFSMKeys.JumpOnRailTracker);
            character.Storage.RemoveValue<bool>(ActorFSMKeys.JumpOnRailMagnetism);
            character.Storage.RemoveValue<float>(ActorFSMKeys.JumpOnRailAngleCosineMax);
            character.Storage.RemoveValue<bool>(ActorFSMKeys.CanQueueJump);
            character.Storage.RemoveValue<bool>(ActorFSMKeys.CanQueueBoost);
            character.Storage.RemoveValue<float>(ActorFSMKeys.AirAbilityDirectionOverrideAnimationElapsedSeconds);
            character.Storage.RemoveValue<bool>(ActorFSMKeys.RespawnTeleportToAir);
        }

        protected virtual void UpdateBoostSuspendTime(Character character, CharacterAbility_MovementAir ability, float deltaTime)
        {
            CharacterBoostStamina boost = character.BoostStamina;
            float suspended = boost.SuspendTimer;
            if (deltaTime == 0f)
            {
                if (!(suspended > 0f)) ability.ResetBuffers(true);
                return;
            }
            if (suspended == 0f) return;
            float remaining = suspended - deltaTime;
            float clamped = Mathf.Max(remaining, 0f);
            if (deltaTime > 0f && remaining > 0f) boost.SuspendTimer = clamped;
            else
            {
                boost.SuspendTimer = 0f;
                if (clamped == 0f) ability.ResetBuffers(true);
            }
        }

        private void UpdateDirectionOverride(Character character, CharacterAbility_MovementAir ability, float deltaTime)
        {
            float elapsed = character.Storage.GetValue(ActorFSMKeys.AirAbilityDirectionOverrideAnimationElapsedSeconds, 0f, true);
            bool active = character.IsDirectionOverrideActive();
            CharacterAbilityDefinition_MovementAir definition = ability.Definition;
            Vector3 velocity = character.WorldVelocityNormalised;
            if (active)
            {
                float threshold = character.Constants.MovementOrientateToVelocityMinCosineAngleToWorldUp;
                bool orientate = Vector3.Dot(velocity, character.WorldUp) < threshold;
                character.OrientateToPlane(character.WorldUp, orientate);
            }
            else if (!(elapsed > 0f && elapsed < definition.MinimumDirectionOverrideAnimationDurationSeconds))
            {
                ResetDirectionOverride(character);
                return;
            }
            character.Storage.SetValue(ActorFSMKeys.AirAbilityDirectionOverrideAnimationElapsedSeconds, elapsed + deltaTime);
            Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(character.RightDirection, velocity), velocity);
            character.VisualProxy.OverrideRotation(rotation);
            character.Animator.TrySetFromAnimationType(m_stateAnimationDefinition, (ActorAnimationType)unchecked((int)0xc2920266), true);
        }

        private void ResetDirectionOverride(Character character)
        {
            character.VisualProxy.StopOverridingRotation();
            character.Animator.TrySetFromAnimationType(m_stateAnimationDefinition, (ActorAnimationType)unchecked((int)0xc2920266), false);
            character.Storage.SetValue(ActorFSMKeys.AirAbilityDirectionOverrideAnimationElapsedSeconds, 0f);
        }
    }
}
