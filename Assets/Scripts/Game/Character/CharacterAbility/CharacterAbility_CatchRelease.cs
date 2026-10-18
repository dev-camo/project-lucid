using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020002c2 with closure classes 020002c3 (DoTriggerExit) and 020002c4
    // (DoTriggerReset); bodies translated from the 1.10.1 x86-64 binary. The trigger volume hands the
    // character to the ability, which pins it to the catch point until the release input.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbility_CatchRelease : CharacterAbility<CharacterAbilityDefinition_CatchRelease>, IAbilityTrigger
    {
        // 060010b6 / 060010b7 (1.10.1 RVA 0x743690 / 0x7436A0)
        public bool IsTriggered { get; private set; }

        private CatchReleaseDefinition m_data;
        private Collider m_collider;
        private bool m_isReversed;
        private float m_attachTime;
        private Vector3 m_attachPosition;
        private Vector3 m_attachDirection;
        private Vector3 m_attachOffset;
        private Vector3 m_characterPosition;
        private float m_releaseSpeed;

        // 060010b8 (1.10.1 RVA 0x7436B0)
        private CatchReleaseDefinition_Catch.Events Events =>
            m_isReversed ? m_data.Catch.PointReverse.Events : m_data.Catch.PointForward.Events;

        // 060010b9 (1.10.1 RVA 0x7436E0). The native body inlines the CharacterAbility base, which
        // refreshes the gravity multiplier from the definition.
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            IsTriggered = false;
            m_attachTime = 0f;
        }

        // 060010ba (1.10.1 RVA 0x743730). The attach time only counts while triggered.
        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            if (IsTriggered)
                m_attachTime += deltaTime;
        }

        // 060010bb (1.10.1 RVA 0x743790)
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            IsTriggered = false;
        }

        // 060010bc (1.10.1 RVA 0x7437B0). The character attaches only when its speed (the speed along the
        // alignment direction, or its full speed when UseSpeedMagnitude is set) reaches SpeedMin and, with
        // a tolerance, its heading lies within the alignment cone. Entering resets the ability before the
        // catch point is recorded.
        public void DoTriggerEnter(CatchReleaseDefinition data, Collider collider)
        {
            if (IsTriggered)
                return;

            CatchReleaseDefinition_Attach attach = data.Attach;
            Transform colliderTransform = collider.transform;
            Vector3 direction = attach.AlignmentAngleTolerance > 0f
                ? colliderTransform.rotation * attach.AlignmentDirection
                : colliderTransform.forward;
            float directionalSpeed = Vector3.Dot(m_character.WorldVelocity, direction);
            float speed = attach.UseSpeedMagnitude ? m_character.WorldVelocityMagnitude : Mathf.Abs(directionalSpeed);
            if (attach.SpeedMin > speed)
                return;
            if (attach.AlignmentAngleTolerance > 0f
                && attach.AlignmentCosAngleTolerance > Vector3.Dot(m_character.WorldVelocityNormalised, direction))
                return;

            Enter();
            m_character.Storage.SetValue(ActorFSMKeys.CatchReleaseActive, true);
            m_data = data;
            m_collider = collider;
            float directionDot = attach.OrientateFromVelocity
                ? directionalSpeed
                : Vector3.Dot(direction, m_character.ForwardDirection);
            if (m_data.Attach.IsDynamic)
                m_collider.transform.localPosition = Vector3.zero;
            m_isReversed = m_data.Catch.ReverseOnDirection && directionDot < 0f;
            m_releaseSpeed = m_data.Release.SpeedCurve.Evaluate(speed);
            m_collider.enabled = false;
            IsTriggered = true;
            float attachSpeed = m_data.Attach.SpeedCurve.Evaluate(speed);
            float normalisedSpeed = m_data.Attach.SpeedNormalised > 0f ? attachSpeed / m_data.Attach.SpeedNormalised : 1f;
            Events.OnEnter(directionDot < 0f ? -normalisedSpeed : normalisedSpeed);
            m_attachPosition = m_collider.transform.position;
            m_attachDirection = m_character.WorldVelocityNormalised;
            if (m_data.Attach.SnapToAttachPoint)
            {
                m_character.SetWorldPosition(m_attachPosition);
                m_character.SetWorldVelocity(Vector3.zero);
            }
            if (m_data.Catch.RemainTime != 0f)
                m_character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, m_data.Catch.RemainTime);
        }

        // 060010bd (1.10.1 RVA 0x743D50)
        public void SnapToAttachPosition()
        {
            m_collider.transform.position = m_attachPosition;
        }

        // 060010be (1.10.1 RVA 0x743D90)
        public bool HasInputReleaseType(CatchInputReleaseType inputReleaseType)
        {
            return m_data != null && m_data.Release.InputType == inputReleaseType;
        }

        // 060010bf (1.10.1 RVA 0x743DB0)
        public void OnAttach()
        {
            SetupParentConstraints();
            m_character.SetWorldVelocity(Vector3.zero);
            m_characterPosition = m_character.WorldPosition;
        }

        // 060010c0 (1.10.1 RVA 0x743E30). The attach offset is the attach position in the catch point's
        // frame (undoing its angle offset), snapped per axis, lowered by the character height when the
        // character hangs below the point, and for a dynamic pivot extended by the current distance.
        // The character is parented with that offset minus the catch point's position offset.
        private void SetupParentConstraints()
        {
            CatchReleaseDefinition_Catch.CatchPoint point = m_isReversed ? m_data.Catch.PointReverse : m_data.Catch.PointForward;
            Transform colliderTransform = m_collider.transform;
            Vector3 localAttachPosition = colliderTransform.InverseTransformPoint(m_attachPosition);
            Quaternion rotation = Quaternion.Euler(point.AngleOffset);
            m_attachOffset = Quaternion.Inverse(rotation) * localAttachPosition;
            m_attachOffset = m_data.Attach.SnapToAxis.Snap(m_attachOffset, Vector3.zero);
            float characterHeight = m_character.Bounds.size.y;
            Vector3 offset = m_attachOffset;
            if (m_data.Attach.OffsetToTopOfCharacter)
                offset.y -= characterHeight;
            Vector3 positionOffset = point.PositionOffset;
            if (m_data.Attach.IsDynamic)
            {
                Vector3 toCharacter = m_character.WorldPosition - m_attachPosition;
                float distance = toCharacter.magnitude - (m_data.Attach.OffsetToTopOfCharacter ? characterHeight : 0f);
                colliderTransform.position = m_character.WorldPosition;
                m_attachOffset += Vector3.up * distance;
                Events.OnUpdateConstraints(distance);
            }
            m_character.FakeParent.StartParenting(colliderTransform, offset - positionOffset, rotation, point.IgnoreRotation);
            CatchConnector connector = m_data.Catch.Connector;
            if (connector != null)
                connector.SetPivots(colliderTransform, m_character.FakeParent.transform, m_attachOffset);
        }

        // 060010c1 (1.10.1 RVA 0x744250). Derives the velocity from the distance moved since the last update.
        public void OnUpdate(float deltaTime)
        {
            m_character.SetWorldVelocity((m_character.WorldPosition - m_characterPosition) / deltaTime);
            m_characterPosition = m_character.WorldPosition;
        }

        // 060010c2 (1.10.1 RVA 0x7442C0). Rotation is evaluated before position. Release points that fix the
        // position add the attach offset, turned half a circle about gravity unless the catch was reversed.
        // Speed scales the velocity direction by the attach-time multiplier curve.
        private void SetCharacterReleaseTransform()
        {
            CatchReleaseDefinition_Release release = m_data.Release;
            CatchReleasePoints transformPoints = release.TransformPoints;
            Quaternion rotation = transformPoints.EvaluateRotation(m_character, m_isReversed, m_attachDirection, m_collider.transform);
            Vector3 position = transformPoints.EvaluatePosition(m_character, m_isReversed);
            if (!transformPoints.IsPositionFree)
            {
                Vector3 offset = m_isReversed
                    ? m_attachOffset
                    : Quaternion.AngleAxis(180f, m_character.GravityNormalised) * m_attachOffset;
                position += offset;
            }
            CatchReleasePoints velocityPoints = m_data.Release.VelocityPoints.HasPoints ? m_data.Release.VelocityPoints : transformPoints;
            Quaternion velocityRotation = velocityPoints.EvaluateRotation(m_character, m_isReversed, m_attachDirection, m_collider.transform);
            Vector3 velocityDirection = velocityRotation * Vector3.forward;
            float multiplier = m_data.Release.MultiplierCurve.Evaluate(m_attachTime);
            if (m_data.Release.UseWorldSpeed)
                m_releaseSpeed = m_data.Release.SpeedCurve.Evaluate(m_character.WorldVelocityMagnitude);
            Vector3 velocity = velocityDirection * (multiplier * m_releaseSpeed);
            m_character.SetAsTrigger(false);
            m_character.SetWorldPosition(position);
            m_character.SetWorldRotation(rotation);
            m_character.SetWorldVelocity(velocity);
        }

        // 060010c3 (1.10.1 RVA 0x7445B0)
        private void RemoveParentConstraints()
        {
            if (m_character.FakeParent.Evaluate(out Vector3 position, out Quaternion _))
            {
                m_character.SetWorldPosition(position);
                m_character.FakeParent.Stop();
            }
        }

        // 060010c4 (1.10.1 RVA 0x744630) with closure 020002c3. Controls and the velocity are locked for the
        // release times; the exit event is raised on the next frame with the return speed relative to the
        // attach speed (-1 when the attach has no normalised speed). The closure is created before the
        // IsTriggered test, so both captured locals are declared first.
        public void DoTriggerExit()
        {
            CatchReleaseDefinition_Catch.Events events;
            float returnSpeedNormalised;
            if (!IsTriggered)
                return;

            Leave();
            RemoveParentConstraints();
            if (!m_character.AreModifiersBlocked())
            {
                SetCharacterReleaseTransform();
                AddModifierOverride((int)GameplayModifierType.ControlsEnabled, false, m_data.Release.ControlsDisabledTime, m_data.TimeCategory);
                AddModifierOverride((int)DirectionModifierType.Override, m_character.WorldVelocity, m_data.Release.VelocityLockTime, m_data.TimeCategory);
                m_character.Storage.SetValue(ActorFSMKeys.AirDecelerationDisabled, false);
            }
            events = Events;
            returnSpeedNormalised = m_data.Attach.SpeedNormalised > 0f
                ? -m_data.Release.ReturnSpeed / m_data.Attach.SpeedNormalised
                : -1f;
            CoroutineUtils.OnNextFrame(() => events.OnExit(returnSpeedNormalised));
        }

        // 060010c5 (1.10.1 RVA 0x744950) with closure 020002c4. The next-frame body resets both catch
        // points' events, clears the active flag and re-enables the trigger collider once the character has
        // left the optional leave checker.
        public void DoTriggerReset(CatchReleaseDefinition data, Collider collider)
        {
            CoroutineUtils.OnNextFrame(() =>
            {
                data.Catch.PointReverse.Events.OnReset();
                data.Catch.PointForward.Events.OnReset();
                m_character.Storage.RemoveValue<bool>(ActorFSMKeys.CatchReleaseActive);
                if (m_character == null || data.Release.LeaveChecker == null)
                    OnCharacterLeave(collider);
                else
                    data.Release.LeaveChecker.Activate(m_character.transform, () => OnCharacterLeave(collider));
            });
        }

        // 060010c6 (1.10.1 RVA 0x744A40)
        public void OnRelease()
        {
            CatchConnector connector = m_data.Catch.Connector;
            if (connector != null)
                connector.ClearPivots();
            DoTriggerExit();
        }

        // 060010c7 (1.10.1 RVA 0x744AC0)
        private void OnCharacterLeave(Collider collider)
        {
            collider.enabled = true;
        }

        // 060010c8 (1.10.1 RVA 0x744AE0)
        public override ActorFormType GetFormType()
        {
            return m_data != null && m_data.FormType != ActorFormType.None ? m_data.FormType : base.GetFormType();
        }

        // 060010c9 (1.10.1 RVA 0x744B10). Reference null tests, as a null-coalescing expression compiles to.
        public override ActorAnimationDefinition GetAnimationDefinition()
        {
            return m_data?.AnimationDefinition ?? base.GetAnimationDefinition();
        }

        // 060010ca (1.10.1 RVA 0x744B70)
        public CharacterAbility_CatchRelease() { }
    }
}
