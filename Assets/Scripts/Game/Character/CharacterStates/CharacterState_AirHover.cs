using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [GraphNodeDefaultName("AirHover")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Character/{0}")]
    public class CharacterState_AirHover : CharacterState_Air
    {
        public enum HeadingDirection { None = 0, Up = 1, Down = 2 }

        protected CharacterState_AirHover(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            AbilityJSONCtorArgs args = JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs);
            return new CharacterState_AirHover(fsm, transitionId, args);
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Character character = user.GetAs<Character>();
            if (!character.TryGetHoverData(out HoverData hoverData)) return;
            float distance = GetHeightDifference(character, hoverData);
            HeadingDirection heading = character.Storage.GetValue(ActorFSMKeys.HoverHeadingDirection, HeadingDirection.None, true);
            heading = UpdateHeading(heading, distance, hoverData);
            character.Storage.SetValue(ActorFSMKeys.HoverHeadingDirection, heading);
            Vector3 velocity = character.LocalVelocity;
            float target = heading == HeadingDirection.Up ? 1f : heading == HeadingDirection.Down ? -1f : velocity.y;
            float speed = hoverData.MoveSpeed;
            float curve = hoverData.DistanceSpeedCurve.Evaluate(Mathf.Abs(distance));
            target = (target * speed) * curve;
            float damping = character.Storage.GetValue(ActorFSMKeys.HoverDampingVelocity, 0f, true);
            velocity.y = Mathf.SmoothDamp(velocity.y, target, ref damping, hoverData.SmoothTimeSeconds,
                float.PositiveInfinity, updateContext.DeltaTime);
            // Original mutates only the local damping copy; no storage writeback.
            character.SetLocalVelocity(velocity);
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Character character = user.GetAs<Character>();
            character.Storage.RemoveValue<HeadingDirection>(ActorFSMKeys.HoverHeadingDirection);
        }

        private float GetHeightDifference(Character character, HoverData hoverData) =>
            hoverData.Origin.InverseTransformPoint(character.WorldPosition).y - hoverData.TargetHeight;

        private HeadingDirection UpdateHeading(HeadingDirection currentHeading, float distanceFromHeight, HoverData hoverData)
        {
            // These negative predicates retain the original unordered branches.
            if (currentHeading == HeadingDirection.None)
                return !(hoverData.OscillateHeightBounds >= distanceFromHeight) ? HeadingDirection.Down : HeadingDirection.Up;
            if (currentHeading == HeadingDirection.Up)
                return !(distanceFromHeight >= hoverData.OscillateHeightBounds) ? HeadingDirection.Up : HeadingDirection.Down;
            if (currentHeading == HeadingDirection.Down)
                return distanceFromHeight < -hoverData.OscillateHeightBounds ? HeadingDirection.Up : HeadingDirection.Down;
            return currentHeading;
        }
    }
}
