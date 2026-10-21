using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002b6; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_AirChaosDash : CharacterAbility_AirChaos<CharacterAbilityDefinition_AirChaosDash>
	{
		private Transform m_targetTransform;

		private Vector3 m_targetPosition;

		private Quaternion m_targetRotation;

		private Quaternion m_startRotation;

		private bool m_snapToTarget;

		// 0600105e (1.10.1 RVA 0x7415F0). Every air component is replaced by a copy, so
		// the dash direction written later never touches the shared definition data.
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			int count = m_airComponents.Count;
			for (int i = 0; i < count; i++)
			{
				CharacterAbilityDefinition_Air.AirComponent source = m_airComponents[i];
				m_airComponents[i] = new CharacterAbilityDefinition_Air.AirComponent
				{
					StartTime = source.StartTime,
					BlendCurve = source.BlendCurve,
					Direction = source.Direction,
					SpeedCurve = source.SpeedCurve
				};
			}
		}

		// 0600105f (1.10.1 RVA 0x73EB40). A wall target shorter than MinimumTime is
		// refused and leaves the total time zero; the target is otherwise pulled back
		// along the dash (or world up for steep ceilings) by OffsetDistance.
		public void Activate(AbilityAimTargetType aimType, Transform targetTransform, Vector3 targetPosition, Quaternion targetRotation, Vector3 targetNormal, CameraAbilityDefinition cameraDefinition, bool snapToTarget)
		{
			Vector3 toTarget = targetPosition - m_character.WorldPosition;
			float distance = toTarget.magnitude;
			m_totalTimeSeconds = distance / DefinitionAirChaos.DashSpeed;
			if (aimType == AbilityAimTargetType.Wall)
			{
				if (DefinitionAirChaos.MinimumTime > m_totalTimeSeconds)
				{
					m_totalTimeSeconds = 0f;
					return;
				}
				Vector3 worldUp = m_character.WorldUp;
				Vector3 offsetDirection = DefinitionAirChaos.OffsetCosineAngleToWorldUp > Vector3.Dot(targetNormal, worldUp)
					? worldUp
					: toTarget / distance;
				toTarget -= offsetDirection * DefinitionAirChaos.OffsetDistance;
			}
			m_targetTransform = targetTransform;
			m_targetPosition = m_character.WorldPosition + toTarget;
			if (m_targetTransform != null)
				m_targetPosition -= m_targetTransform.position;
			m_targetRotation = targetRotation;
			m_startRotation = m_character.WorldRotation;
			m_snapToTarget = snapToTarget;
			Vector3 localVelocity = m_character.WorldToLocalRotation * (toTarget / m_totalTimeSeconds);
			foreach (CharacterAbilityDefinition_Air.AirComponent component in m_airComponents)
				component.Direction = localVelocity;
			SetAirActive(true);
			m_character.Storage.SetValue(ActorFSMKeys.AirAbilityActive, this);
			CameraDefinitionOverride = cameraDefinition;
		}

		// 06001060 (1.10.1 RVA 0x741760)
		private void GetTargetPositionAndRotation(out Vector3 targetPosition, out Quaternion targetRotation)
		{
			targetPosition = m_targetPosition;
			if (m_targetTransform != null)
				targetPosition += m_targetTransform.position;
			targetRotation = m_targetRotation;
		}

		// 06001061 (1.10.1 RVA 0x741830)
		private void UpdateDirection()
		{
			if (m_targetTransform == null)
				return;
			Vector3 toTarget = m_targetTransform.position + m_targetPosition - m_character.WorldPosition;
			Vector3 localVelocity = m_character.WorldToLocalRotation * (toTarget.normalized * DefinitionAirChaos.DashSpeed);
			foreach (CharacterAbilityDefinition_Air.AirComponent component in m_airComponents)
				component.Direction = localVelocity;
		}

		// 06001062 (1.10.1 RVA 0x741B00). Moves the camera target transform onto the
		// dash target; either argument being null stops after the base override.
		protected override void SetCameraOverrides(CameraAbilityDefinition cameraDefinition, Transform targetTransform)
		{
			base.SetCameraOverrides(cameraDefinition, targetTransform);
			if (cameraDefinition == null || targetTransform == null)
				return;
			GetTargetPositionAndRotation(out Vector3 position, out Quaternion rotation);
			targetTransform.SetPositionAndRotation(position, rotation);
		}

		// 06001063 (1.10.1 RVA 0x741C60). The inlined Vector3.ProjectOnPlane keeps the
		// forward direction unchanged when the plane normal is degenerate.
		private void OnCharacterPositionSnap()
		{
			Vector3 worldUp = m_character.WorldUp;
			m_character.SetWorldRotation(Quaternion.LookRotation(Vector3.ProjectOnPlane(m_character.ForwardDirection, worldUp), worldUp));
		}

		// 06001064 (1.10.1 RVA 0x741D70). Ending an active dash with time still on the
		// clock snaps the character onto the target and launches it along the target
		// rotation at the slope-dependent exit speed.
		protected override void SetAirActive(bool active)
		{
			bool wasActive = AirActive;
			base.SetAirActive(active);
			if (!wasActive || active)
				return;
			if (m_totalTimeSeconds == 0f)
				return;
			if (m_character.AreModifiersBlocked())
				return;
			GetTargetPositionAndRotation(out Vector3 position, out Quaternion rotation);
			m_character.SetWorldPosition(position);
			m_character.SetWorldRotation(rotation);
			float exitSpeed = DefinitionAirChaos.ExitSpeedBySlopeCurve.Evaluate(ActorMovementUtilities.GetSlopeAngle(m_character));
			m_character.SetWorldVelocity(m_targetRotation * Vector3.forward * exitSpeed);
			m_character.SetAirControlsLockTime(m_character.Constants.ChaosDashAirControlsLockTime);
		}

		// 06001065 (1.10.1 RVA 0x741F90)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			RigidbodyForceTrigger.OnCharacterPositionSnap += OnCharacterPositionSnap;
		}

		// 06001066 (1.10.1 RVA 0x7420E0). Inside the last MinimumTime seconds the
		// character is placed on the target instead of steering towards it.
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			if (!AirActive)
				return;
			float progress = m_totalTimeSeconds > m_elapsedTimeSeconds ? m_elapsedTimeSeconds / m_totalTimeSeconds : 1f;
			m_character.VisualProxy.OverrideRotation(Quaternion.Slerp(m_startRotation, m_targetRotation, progress));
			if (MathUtilities.WithinTolerance(Mathf.Min(deltaTime + m_elapsedTimeSeconds, m_totalTimeSeconds), m_totalTimeSeconds, DefinitionAirChaos.MinimumTime))
			{
				GetTargetPositionAndRotation(out Vector3 position, out Quaternion rotation);
				m_character.SetWorldPosition(position);
				m_character.SetWorldRotation(rotation);
			}
			else
			{
				UpdateDirection();
			}
		}

		// 06001067 (1.10.1 RVA 0x7422C0). The total time is cleared before the first
		// SetAirActive(false), so that call takes the early exit; the base call then
		// ends the air state a second time.
		protected override void DoOnLeave()
		{
			m_totalTimeSeconds = 0f;
			SetAirActive(false);
			if (m_snapToTarget)
			{
				GetTargetPositionAndRotation(out Vector3 position, out Quaternion rotation);
				m_character.SetWorldPosition(position);
				m_character.SetWorldRotation(rotation);
				m_character.SetWorldVelocity(Vector3.zero);
			}
			m_character.VisualProxy.StopOverridingRotation();
			RigidbodyForceTrigger.OnCharacterPositionSnap -= OnCharacterPositionSnap;
			base.DoOnLeave();
		}

		// 06001068 (1.10.1 RVA 0x742540)
		public CharacterAbility_AirChaosDash()
		{
		}
	}
}
