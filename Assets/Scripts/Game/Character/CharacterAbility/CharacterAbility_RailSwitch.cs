using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200030a; bodies translated from the 1.10.1 x86-64 binary. While the debounced sideways input
	// holds, a ray is projected from the tracker onto the neighbouring rails of the tracked spline; the nearest hit is stored
	// as RailSwitchNeighbour, and the targeted object with the character's speed under the HomingRailSwitch keys.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_RailSwitch : CharacterAbility_Targeting<CharacterAbilityDefinition_RailSwitch>
	{
		private const float SnapToRailTolerance = 0.1f;

		private List<SurfaceSplineSection> m_targetSections = new List<SurfaceSplineSection>();

		private float m_inputMovement;

		private InputDebounce m_inputDebounce;

		private bool m_inputRequireJump;

		// 06001283 (1.10.1 RVA 0x752010); 06001284 (RVA 0x752020) is the private setter.
		public RailDefinition RailDefinition { get; private set; }

		// 06001285 (1.10.1 RVA 0x752040)
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			m_inputDebounce = new InputDebounce(Definition.ActivationInputThreshold, Definition.ActivationInputTime);
			RailDefinition = ProcessManager.GetSystem<DataManager>(null, true).RailDefinitions[RailType.Default];
		}

		// 06001286 (1.10.1 RVA 0x7521B0)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			m_character.Storage.SetValue<ISurface>(ActorFSMKeys.RailSwitchNeighbour, null);
			m_inputRequireJump = true;
			m_inputDebounce.Reset(true, true);
			UpdateJumpRequirement();
		}

		// 06001287 (1.10.1 RVA 0x752420)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			Reset();
		}

		// 06001288 (1.10.1 RVA 0x752720). Nothing runs unless one of the trigger abilities is active or was left within
		// the grace period, and the character is on a rail or jumping from one.
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			if (!ActorAbilityUtilities.AnyAbilityInUse(m_character, Definition.TriggerAbilities, Definition.TriggerGracePeriod))
			{
				return;
			}
			bool onRail = m_character.Storage.GetValue(ActorFSMKeys.RailActive, false, true);
			bool jumpingFromRail = m_character.Storage.GetValue(ActorFSMKeys.JumpOnRailTracker, false, true);
			if (!onRail && !jumpingFromRail)
			{
				return;
			}
			base.DoOnUpdate(brain, deltaTime);
			m_inputMovement = m_character.ControllerMovement.x;
			m_inputDebounce.Update(m_inputMovement, deltaTime);
			if (!m_inputDebounce.ThresholdExceeded || !m_inputDebounce.TimeExceeded)
			{
				Reset();
				return;
			}
			UpdateJumpRequirement();
			bool jumpSatisfied = !m_inputRequireJump || brain.Jump;
			m_targetRequiresRefresh |= jumpSatisfied;
			if (Vector3.Dot(m_character.ForwardDirection, m_character.CameraForwardOnCharacterPlane) < 0f)
			{
				m_inputMovement = -m_inputMovement;
			}
			if ((m_inputDebounce.ThresholdExceeded && !m_inputDebounce.DirectionMaintained) || m_targetRequiresRefresh)
			{
				m_targetRequiresRefresh = false;
				if (!ProjectToTargetedObject())
				{
					Reset();
					return;
				}
				if (jumpSatisfied)
				{
					m_character.Storage.SetValue(ActorFSMKeys.RailSwitchNeighbour, m_targetedLocation.m_surface);
				}
			}
		}

		// 06001289 (1.10.1 RVA 0x752CE0)
		private bool ProjectToTargetedObject()
		{
			if (!GetProjectedRailTarget())
			{
				return false;
			}
			if (!(m_targetedLocation.m_surface is Component surfaceComponent) || !TargetedLocationIsValid())
			{
				return false;
			}
			float sqrDistance = (m_targetedLocation.m_worldPosition - m_character.Tracker.TrackerPosition).sqrMagnitude;
			if (sqrDistance > Definition.RailProjectDistanceSqr)
			{
				return false;
			}
			float targetSpeed = m_character.WorldVelocityMagnitude;
			SetTargetedObject(surfaceComponent.gameObject, m_targetedLocation.m_worldPosition, m_targetedLocation.m_worldRotation, HomingTargetType.Rail);
			SetFSMTarget(m_targetedObject, targetSpeed);
			return true;
		}

		// 0600128a (1.10.1 RVA 0x752520)
		public void Reset(bool activateCooldown = false)
		{
			m_inputMovement = 0f;
			SetFSMTarget(null, 0f);
			m_targetedObject.Target = null;
			m_targetedLocation.m_surface = null;
			if (activateCooldown && Definition.TurnAroundCooldown > 0f)
			{
				m_character.Storage.SetValue(ActorFSMKeys.RailTurnAroundCooldown, Definition.TurnAroundCooldown);
			}
		}

		// 0600128b (1.10.1 RVA 0x752F10). Looks up the rails neighbouring the tracked spline on the side of the input and
		// keeps the candidate whose location lies nearest the tracker (ties go to the later section).
		private bool GetProjectedRailTarget()
		{
			SurfaceLocation trackerLocation = m_character.Tracker.TrackerLocation;
			ISpline spline = trackerLocation.m_surface as ISpline;
			if (spline == null)
			{
				return false;
			}
			float splineDistance = spline.FindNearestSplineLocation(trackerLocation.m_worldPosition).SplineDistance;
			bool inputRight = m_inputMovement > 0f;
			SurfaceSplineSection.Side side = inputRight != m_character.Tracker.SurfaceReversed ? SurfaceSplineSection.Side.Right : SurfaceSplineSection.Side.Left;
			if (!m_character.TrackManager.TryGetRailNeighbours(spline, splineDistance, side, ref m_targetSections))
			{
				return false;
			}
			m_targetedLocation.m_surface = null;
			SurfaceLocation sectionLocation = default(SurfaceLocation);
			float closestSqrDistance = float.MaxValue;
			foreach (SurfaceSplineSection section in m_targetSections)
			{
				if (!GetTargetSectionLocation(section, ref sectionLocation))
				{
					continue;
				}
				float sqrDistance = (sectionLocation.m_worldPosition - m_character.Tracker.TrackerPosition).sqrMagnitude;
				if (sqrDistance > closestSqrDistance)
				{
					continue;
				}
				m_targetedLocation = sectionLocation;
				closestSqrDistance = sqrDistance;
			}
			return m_targetedLocation.m_surface != null;
		}

		// 0600128c (1.10.1 RVA 0x753500). A ray leaves the tracker along the projected direction and meets the plane
		// through the nearest point of the target spline, whose normal is the character's right axis (negated while the
		// surface is reversed). A section needs a hit ahead of the tracker. It is then accepted when the squared sine of the
		// angle between the ray and the direction to the nearest point found at the hit is below the snap tolerance, or
		// otherwise when that point lies on the side of the character the input points to.
		private bool GetTargetSectionLocation(SurfaceSplineSection targetSection, ref SurfaceLocation targetLocation)
		{
			if (!(targetSection.TargetSpline.Value is Component targetComponent) || targetComponent == null)
			{
				return false;
			}
			Vector3 trackerPosition = m_character.Tracker.TrackerPosition;
			SurfaceLocation nearestLocation = ((ISurface)targetSection.TargetSpline.Value).FindNearestSurfaceLocationFromWorld(trackerPosition);
			Vector3 right = m_character.RightDirection;
			Plane plane = new Plane(m_character.Tracker.SurfaceReversed ? -right : right, nearestLocation.m_worldPosition);
			Ray ray = new Ray(trackerPosition, GetProjectedDirection());
			if (!plane.Raycast(ray, out float enter))
			{
				return false;
			}
			Vector3 point = ray.GetPoint(enter);
			targetLocation = ((ISurface)targetSection.TargetSpline.Value).FindNearestSurfaceLocationFromWorld(point);
			Vector3 toTarget = targetLocation.m_worldPosition - ray.origin;
			// The original divides by the magnitude directly, with no normalization epsilon (0x753C74..0x753C83).
			Vector3 directionToTarget = toTarget / toTarget.magnitude;
			// The native code takes the absolute value of the squared length of the cross product.
			if (Mathf.Abs(Vector3.Cross(ray.direction, directionToTarget).sqrMagnitude) < SnapToRailTolerance)
			{
				return true;
			}
			return (int)Vector3Extensions.SignOfAngle(directionToTarget, m_character.ForwardDirection, m_character.UpDirection) == Math.Sign(m_inputMovement);
		}

		// 0600128d (1.10.1 RVA 0x753D60). The character's forward turned about its own up axis by the projection angle,
		// to the right for input at or above zero (and for NaN input to the left).
		private Vector3 GetProjectedDirection()
		{
			float angle = m_inputMovement >= 0f ? Definition.RailProjectAngle : -Definition.RailProjectAngle;
			return m_character.WorldRotation * (Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward);
		}

		// 0600128e (1.10.1 RVA 0x752380). The jump requirement only applies while the character is within the snap
		// tolerance (squared distance) of the tracker.
		private void UpdateJumpRequirement()
		{
			m_inputRequireJump = (m_character.WorldPosition - m_character.Tracker.TrackerPosition).sqrMagnitude < SnapToRailTolerance
				&& m_character.Constants.RailSwitchRequiresJump;
		}

		// 0600128f (1.10.1 RVA 0x753E80)
		public bool SwitchInProgress()
		{
			if (m_character.Storage.GetValue<ISurface>(ActorFSMKeys.RailSwitchNeighbour, null, true) == null)
			{
				return false;
			}
			return GetFSMTarget(out CharacterTargetObjectData? targetObject, out float _)
				&& targetObject.HasValue
				&& targetObject.GetValueOrDefault().Type == HomingTargetType.Rail;
		}

		// 06001290 (1.10.1 RVA 0x754270)
		protected override float TargetingIntervalSeconds
		{
			get
			{
				return Definition.TargetingIntervalSeconds;
			}
		}

		// 06001291 (1.10.1 RVA 0x7542C0)
		protected override float CooldownTimeSeconds
		{
			get
			{
				return Definition.CooldownTimeSeconds;
			}
		}

		// 06001292 (1.10.1 RVA 0x754310)
		protected override bool UseTimeScaling
		{
			get
			{
				return Definition.UseTimeScaling;
			}
		}

		// 06001293 (1.10.1 RVA 0x753310)
		private void SetFSMTarget(CharacterTargetObjectData? targetObject, float targetSpeed)
		{
			m_character.Storage.SetValue(ActorFSMKeys.HomingRailSwitchObject, targetObject);
			m_character.Storage.SetValue(ActorFSMKeys.HomingRailSwitchSpeed, targetSpeed);
		}

		// 06001294 (1.10.1 RVA 0x754000)
		public bool GetFSMTarget(out CharacterTargetObjectData? targetObject, out float speed)
		{
			targetObject = m_character.Storage.GetValue(ActorFSMKeys.HomingRailSwitchObject, default(CharacterTargetObjectData?), true);
			speed = m_character.Storage.GetValue(ActorFSMKeys.HomingRailSwitchSpeed, 0f, true);
			return targetObject.HasValue;
		}

		// 06001295 (1.10.1 RVA 0x752C70); empty in the retail binary.
		private void UpdateDebug()
		{
		}

		// 06001296 (1.10.1 RVA 0x754360)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append(string.Format("Movement = {0} ", m_inputMovement));
			if (m_targetedLocation.m_surface is Component targetComponent && targetComponent != null)
			{
				extraInfo.Append("Target = " + targetComponent.gameObject.name + " ");
			}
		}

		// 06001297 (1.10.1 RVA 0x7544E0)
		public CharacterAbility_RailSwitch()
		{
		}
	}
}
