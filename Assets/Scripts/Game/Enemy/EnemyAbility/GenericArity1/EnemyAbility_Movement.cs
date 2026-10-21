using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000639; bodies translated from the 1.10.1 x86-64 binary (shared generic bodies at
    // 0x117F560..0x117FB00). A movement ability turns the enemy about its up direction and drives its forward speed
    // from the brain's movement impulse, each scaled by a curve over how long that impulse axis has been unchanged.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class EnemyAbility_Movement<T> : EnemyAbility where T : EnemyAbilityDefinition_Movement
    {
        // 0600218d (1.10.1 RVA 0x117F560), 0600218e (1.10.1 RVA 0x117F570)
        public T Definition { get; private set; }

        // Declared by the original; no native body accesses its offset 0x30..0x3b (the other fields are used).
        private Vector3 m_previousPosition;
        protected Vector2 m_thisFrameMovement;
        private Vector2 m_previousFrameMovement;
        private float m_timeSpentAccelerating;
        private float m_timeSpentTurningDirection;

        // 0600218f (1.10.1 RVA 0x117F590)
        public override AbilityDefinition DefinitionBase => Definition;

        // 06002190 (1.10.1 RVA 0x117F5A0)
        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            Definition = definition as T;
        }

        // 06002191 (1.10.1 RVA 0x117F650): triggered while the brain is steering and the target is not locked. The
        // native code evaluates both reads without branching, so the source operand order is not recoverable.
        protected override void DoOnUpdate(float deltaTime)
        {
            IsTriggered = m_enemy.BrainMovement != Vector2.zero && !m_enemy.BrainTargetLocked;
        }

        // 06002192 (1.10.1 RVA 0x117F6E0): the native compiler inlines CompareMovement. The turn is a rotation about
        // the up direction applied in world space; the forward speed is set along the local z axis, keeping the
        // local y velocity, unless the enemy is playing its alert animation.
        public override void UpdateMovement(float deltaTime)
        {
            m_previousFrameMovement = m_thisFrameMovement;
            m_thisFrameMovement = m_enemy.BrainMovement;
            CompareMovement(deltaTime);

            float turnCurve = Definition.TurnSameDirectionCurve.Evaluate(m_timeSpentTurningDirection);
            float turnDegrees = turnCurve * m_thisFrameMovement.x * Definition.TurnSpeed * deltaTime;
            m_enemy.SetWorldRotation(Quaternion.AngleAxis(turnDegrees, m_enemy.UpDirection) * m_enemy.WorldRotation);

            if (m_enemy.AlertAnimation)
            {
                m_enemy.SetLocalVelocity(Vector3.zero);
                return;
            }
            float forward = m_thisFrameMovement.y * Definition.ForwardSpeed * Definition.AccelerationCurve.Evaluate(m_timeSpentAccelerating);
            m_enemy.SetLocalVelocity(new Vector3(0f, m_enemy.LocalVelocity.y, forward));
        }

        // 06002193 (1.10.1 RVA 0x117F8F0): an axis that kept exactly the previous frame's value keeps counting up,
        // any change restarts it.
        private void CompareMovement(float deltaTime)
        {
            m_timeSpentTurningDirection = m_thisFrameMovement.x == m_previousFrameMovement.x ? m_timeSpentTurningDirection + deltaTime : 0f;
            m_timeSpentAccelerating = m_thisFrameMovement.y == m_previousFrameMovement.y ? m_timeSpentAccelerating + deltaTime : 0f;
        }

        // 06002194 (1.10.1 RVA 0x117F940): the movement animation takes the definition's forward speed, not the
        // measured one. An enemy with a detached visual proxy also reports sideways travel (+1 reversing along a
        // spline, -1 otherwise) while it moves on the local x or z axis.
        public override void UpdateMovementAnimation(ActorAnimationDefinition animation)
        {
            if (m_enemy.BrainTargetSpotted)
            {
                m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.BoolSet, true);
                m_enemy.ConsumeSightingReactionTrigger();
            }
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement, Definition.ForwardSpeed);
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement_Turning, m_thisFrameMovement.x != 0f);
            if (!m_enemy.Definition.Traits.ShouldDetachVisualProxy)
            {
                return;
            }

            float lateral = -1f;
            if (m_enemy.Navigation is EnemyNavigationSpline spline)
            {
                lateral = spline.GetReversing() ? 1f : -1f;
            }
            bool moving = !(MathUtilities.WithinTolerance(m_enemy.LocalVelocity.x, 0f, 0.0001f)
                && MathUtilities.WithinTolerance(m_enemy.LocalVelocity.z, 0f, 0.0001f));
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement_BoolSet, moving);
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement_Lateral, moving ? lateral : 0f);
        }

        // 06002195 (1.10.1 RVA 0x117FB00)
        protected EnemyAbility_Movement() { }
    }
}
