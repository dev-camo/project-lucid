using Unity.IL2CPP.CompilerServices;
using System.Text;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000309; bodies translated from the 1.10.1 x86-64 binary. A homing ability that projects
	// a cone from the character (or camera) to find a rail, and locks onto it when the rail target action is queued.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_RailsTargeting : CharacterAbility_TargetingHoming<CharacterAbilityDefinition_RailsTargeting>, IAbilityTrigger
	{
		private ConeVolume.ConeDescription m_cone;

		private RailDefinition m_railDefinition;

		// 06001279 (1.10.1 RVA 0x750780)
		protected override bool ProjectFromCameraWhenNoDirectionInput
		{
			get
			{
				return Definition.ProjectFromCameraWhenNoDirectionInput;
			}
		}

		// 0600127a (1.10.1 RVA 0x7507D0)
		protected override AnimationCurve AdditionalSpeedCurve
		{
			get
			{
				return Definition.HomingAdditionalSpeed;
			}
		}

		// 0600127b (1.10.1 RVA 0x750820)
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			m_railDefinition = ProcessManager.GetSystem<DataManager>().RailDefinitions[RailType.Default];
		}

		// 0600127c (1.10.1 RVA 0x750900); the native body inlines the targeting timer update. A queued rail target action
		// locks the pending target, and a refresh request re-projects the cone.
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			if (m_character.HomingPool.IsTargetActive(this))
			{
				return;
			}
			IsTriggered = false;
			if (m_character.Storage.GetValueOnly(ActorFSMKeys.JumpOnRailTracker, false))
			{
				return;
			}
			if (m_character.Storage.GetValueOnly(ActorFSMKeys.RailSwitchTimeElapsed, 0f) > 0f)
			{
				return;
			}
			bool pendingTargetIsValid = m_character.HomingPool.PendingTargetIsValidForAbility(this);
			if (pendingTargetIsValid && brain.ActionStateQueued.GetValue(GameAction.CharacterRailTarget))
			{
				float homingVelocity = Definition.HomingVelocity;
				m_character.HomingPool.LockPendingTarget(homingVelocity, m_character.WorldVelocity, Definition.HomingRotationalVelocity);
				IsTriggered = true;
				m_targetRequiresRefresh = false;
			}
			brain.SetState(GameAction.CharacterRailTarget, false);
			if (!m_targetRequiresRefresh)
			{
				return;
			}
			m_targetRequiresRefresh = false;
			m_character.HomingPool.RemoveTarget(this);
			if (pendingTargetIsValid)
			{
				m_character.HomingPool.ClearTarget();
			}
			ProjectToTargetedObject();
		}

		// 0600127d (1.10.1 RVA 0x751380); projects the cone through the rails and offers the hit rail as a homing target.
		private void ProjectToTargetedObject()
		{
			if (m_character == null)
			{
				return;
			}
			if (m_character.TrackManager == null)
			{
				return;
			}
			if (!m_character.IsActive())
			{
				return;
			}
			if (!CanActivate)
			{
				return;
			}
			Vector3 cameraPosition = m_character.CameraPosition;
			Vector3 cameraForward = m_character.CameraForward;
			Quaternion cameraRotation = m_character.CameraRotation;
			float cameraDistance = 0f;
			if (ShouldProjectFromCamera())
			{
				cameraDistance = Vector3.Dot(cameraForward, m_character.WorldPosition - cameraPosition);
			}
			m_cone.Origin = ShouldProjectFromCamera() ? cameraPosition : m_character.WorldPosition;
			m_cone.Rotation = ShouldProjectFromCamera() ? cameraRotation : Quaternion.LookRotation(m_character.RawIntendedForward, m_character.UpDirection);
			m_cone.Distance = cameraDistance + Definition.HomingDistance;
			m_cone.Step = Definition.HomingProjectDistanceStep;
			m_cone.Tangents = Definition.HomingProjectConeTangents;
			if (!m_character.TrackManager.TryGetProjectedRailHit(m_cone, m_character, ref m_targetedLocation, null, new System.Func<ISurface, bool>(IsSurfaceTargetable)))
			{
				return;
			}
			if (!(m_targetedLocation.m_surface is Component surfaceComponent))
			{
				return;
			}
			if (!TargetedLocationIsValid())
			{
				return;
			}
			m_character.HomingPool.AddPotentialTarget(surfaceComponent.gameObject, m_targetedLocation.m_worldPosition, m_targetedLocation.m_worldRotation, HomingTargetType.Rail, this);
		}

		// 0600127e (1.10.1 RVA 0x751AF0); a surface is targetable unless its metadata explicitly marks it otherwise.
		private bool IsSurfaceTargetable(ISurface surface)
		{
			MetadataGroup group = surface.GetSurfaceMetadata().GetGroup(m_railDefinition.MetadataGroupKey);
			return !group.TryGetValue(m_railDefinition.MetadataTargetableKey, out bool targetable) || targetable;
		}

		// 0600127f (1.10.1 RVA 0x751BF0); resetting while a rail is the active target marks the rail target cancelled.
		public override void Reset()
		{
			ActiveHomingTarget activeTarget = m_character.HomingPool.ActiveTarget;
			if (activeTarget != null && activeTarget.Object.Type == HomingTargetType.Rail)
			{
				m_character.Storage.SetValue(ActorFSMKeys.RailTargetCancelled, true);
			}
			base.Reset();
		}

		// 06001280 (1.10.1 RVA 0x751D30)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append(string.Format("ProjectDistance = {0} ", Definition.HomingDistance));
			extraInfo.Append(string.Format("ProjectConeAngles = {0:F1} ", Definition.HomingProjectConeAngle));
		}

		// 06001281 (1.10.1 RVA 0x751E60); the gizmo drawing calls are absent from the retail binary, only the
		// calculations that fed them remain.
		public override bool OnDrawGizmosSelected()
		{
			bool selected = base.OnDrawGizmosSelected();
			if (selected && m_targetedObject.Target != null)
			{
				ConeVolume.CalculateConeBounds(m_cone);
				_ = m_targetedLocation.m_worldRotation * Vector3.forward;
				_ = m_targetedLocation.m_worldRotation * Vector3.up;
			}
			return selected;
		}

		// 06001282 (1.10.1 RVA 0x751FD0)
		public CharacterAbility_RailsTargeting()
		{
		}
	}
}
