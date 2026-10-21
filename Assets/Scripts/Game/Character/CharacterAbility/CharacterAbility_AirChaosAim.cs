using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002b5; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_AirChaosAim : CharacterAbility_AirChaos<CharacterAbilityDefinition_AirChaosAim>, IHomingAttack
	{
		private readonly Quaternion ForwardToNormalRotation = Quaternion.AngleAxis(90f, Vector3.right);

		private const float AimLerpDistanceSqrTolerance = 0.1f;

		private ConeVolume.ConeDescription m_cone;

		private SurfaceLocation m_targetedLocation;

		private bool m_controllerMovementHasBeenActive;

		private Vector3 m_aimTransformPositionLastFrame;

		private HomingAttack m_homingAttack;

		private readonly Dictionary<Collider, CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition> m_homingTargetAimDefinitions =
			new Dictionary<Collider, CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition>();

		private readonly List<Collider> m_homingTargets = new List<Collider>();

		private bool m_homingTargetsDirty;

		private RailDefinition m_railDefinition;

		private TransporterDefinition m_transporterDefinition;

		// The native code tests this by reference (is not null) in DoOnUpdate, the homing loop of
		// UpdateTarget and UpdateUI, and with the Unity-object comparison everywhere else.
		private CharacterCollisionData m_hitCollisionData;

		private AbilityAimTargetType m_aimType;

		private Transform m_aimTransform;

		private Vector3 m_aimPosition;

		private Vector3 m_aimOffset;

		private Vector3 m_aimNormal;

		private Quaternion m_aimRotation;

		private AbilityAimTargetType m_groundType;

		private Vector3 m_groundPosition;

		private Quaternion m_groundRotation;

		private GameObject m_debugTargetObject;

		private bool m_cameraTargetTimerActive;

		private float m_cameraTargetTimer;

		private bool m_aimLerpActive;

		private Vector3 m_aimLerpPosition;

		private Vector3 m_aimLerpVelocity;

		private bool m_aimLerpScaleActive;

		private float m_aimLerpScaleTime;

		private Vector2 m_targetImpulse;

		private Vector2 m_targetImpulsePrevious;

		private CharacterAbility_AirChaosDash m_abilityAirChaosDash;

		private CharacterAbility_Knockback m_abilityKnockback;

		private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();

		// 0600103a (1.10.1 RVA 0x73CCC0). Both definitions are the Default entries.
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			DataManager dataManager = ProcessManager.GetSystem<DataManager>();
			m_railDefinition = dataManager.RailDefinitions[RailType.Default];
			m_transporterDefinition = dataManager.TransporterDefinitions[TransporterType.Default];
			m_homingAttack = ObjectUtils.SpawnPrefab(DefinitionAirChaos.HomingAttackPrefab, m_character.transform.parent);
			m_homingAttack.Initialise(m_character, this);
			m_homingAttack.gameObject.SetActive(false);
		}

		// 0600103b (1.10.1 RVA 0x73CE70). The ability UI is destroyed by the character base close.
		public override void Close()
		{
			base.Close();
			UnityEngine.Object.Destroy(m_homingAttack.gameObject);
			m_homingAttack = null;
		}

		// 0600103c (1.10.1 RVA 0x73CF20)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			m_abilityAirChaosDash = m_character.GetAbility<CharacterAbility_AirChaosDash>(ActorAbilityType.Character_AirChaosDash);
			m_abilityKnockback = m_character.GetAbility<CharacterAbility_Knockback>(ActorAbilityType.Character_Knockback);
		}

		// 0600103d (1.10.1 RVA 0x73D020). Releasing the aim dashes to the aimed target.
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget target = null;
			bool wasActive = AirActive;
			base.DoOnUpdate(brain, deltaTime);
			if (AirActive && brain.AirCancel)
			{
				SetAirActive(false);
				m_character.EndImpulses();
				return;
			}
			if (!wasActive || AirActive)
				return;
			if (m_character.IsExitingLevel())
				return;
			if (m_character.AreModifiersBlocked())
				return;
			if (m_abilityAirChaosDash == null)
				return;
			if (m_abilityKnockback != null && m_abilityKnockback.IsTriggered && !m_abilityKnockback.MinDurationExceeded())
				return;
			bool snapToTarget;
			if (DefinitionAirChaos.TargetTypes.TryGetValue(m_aimType, out target))
			{
				snapToTarget = true;
				if (target.RaycastGraceDistance > 0f
					&& UpdateTarget(
						m_character.WorldPosition + m_character.ForwardDirection * DefinitionAirChaos.RaycastDistance,
						m_character.ForwardDirection,
						target.RaycastGraceDistance))
				{
					snapToTarget = DefinitionAirChaos.TargetTypes.TryGetValue(m_aimType, out target);
				}
			}
			else
			{
				snapToTarget = false;
			}
			if (m_hitCollisionData is not null && m_hitCollisionData.HasTargetingModifier)
			{
				snapToTarget = m_hitCollisionData.TargetingModifier.SnapToTarget;
			}
			else if (snapToTarget && target.OrientateCharacterOnExit)
			{
				snapToTarget = false;
				m_aimRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(m_character.ForwardDirection, m_aimNormal), m_aimNormal);
			}
			else
			{
				snapToTarget = false;
				Vector3 worldUp = m_character.WorldUp;
				m_aimRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(m_character.ForwardDirection, worldUp), worldUp);
			}
			OnTargetTypeLeave(m_aimType);
			CameraAbilityDefinition cameraOverride = GetCameraOverride(target);
			m_abilityAirChaosDash.Activate(m_aimType, m_aimTransform, m_aimPosition, m_aimRotation, m_aimNormal, cameraOverride, snapToTarget);
		}

		// 0600103e (1.10.1 RVA 0x73F080)
		protected override void DoOnLateUpdate(float deltaTime)
		{
			base.DoOnLateUpdate(deltaTime);
			if (!IsActivated)
				return;
			m_character.SetWorldRotation(m_cameraManagerRef.Get().CinemachineBrain.transform.rotation);
			m_character.VisualProxy.OverrideRotation(m_character.WorldRotation);
			m_homingAttack.transform.SetPositionAndRotation(m_character.CameraPosition, m_character.CameraRotation);
			UpdateTarget(m_character.WorldPosition, m_character.ForwardDirection, DefinitionAirChaos.RaycastDistance);
			OnTargetTypeUpdate(deltaTime);
		}

		// 0600103f (1.10.1 RVA 0x73F390)
		public override void DoOnActivation()
		{
			base.DoOnActivation();
			m_aimType = AbilityAimTargetType.None;
			m_cameraManagerRef.Get().SubscribeMovementSecondary();
			m_homingAttack.gameObject.SetActive(true);
			m_controllerMovementHasBeenActive = false;
			m_aimTransformPositionLastFrame = Vector3.zero;
			m_aimLerpActive = false;
		}

		// 06001040 (1.10.1 RVA 0x73F480). The homing target list is left as it is.
		public override void DoOnDeactivation()
		{
			base.DoOnDeactivation();
			m_cameraManagerRef.Get().UnsubscribeMovementSecondary();
			m_homingAttack.gameObject.SetActive(false);
			m_homingTargetAimDefinitions.Clear();
			m_homingTargetsDirty = false;
			m_character.VisualProxy.StopOverridingRotation();
		}

		// 06001041 (1.10.1 RVA 0x73F540)
		private bool RaycastHitIsValid(RaycastHit hitColliderInfo, out CharacterCollisionData hitCollisionData)
		{
			hitCollisionData = hitColliderInfo.collider.GetComponent<CharacterCollisionData>();
			return hitCollisionData == null || !hitCollisionData.HasTargetingModifier || hitCollisionData.TargetingModifier.IsValid;
		}

		// 06001042 (1.10.1 RVA 0x73F5F0). The first sorted hit that is a valid target wins;
		// a miss clears the hit and the cached collision data.
		private bool TryGetHitCollider(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hitColliderInfo)
		{
			int hitCount = ColliderTrackingUtilities.RaycastSort(out RaycastHit[] hits, origin, direction, maxDistance, DefinitionAirChaos.CollisionMask, QueryTriggerInteraction.Ignore);
			for (int i = 0; i < hitCount; i++)
			{
				hitColliderInfo = hits[i];
				if (RaycastHitIsValid(hitColliderInfo, out m_hitCollisionData))
					return true;
			}
			hitColliderInfo = default(RaycastHit);
			m_hitCollisionData = null;
			return false;
		}

		// 06001043 (1.10.1 RVA 0x73F830)
		private bool ControllerMovementActive()
		{
			Vector2 controllerMovement = m_cameraManagerRef.Get().ControllerMovement;
			if (controllerMovement.WithinTolerance(Vector2.zero))
				return false;
			return !controllerMovement.WithinTolerance(m_targetImpulsePrevious);
		}

		// 06001044 (1.10.1 RVA 0x73D3B0). Returns whether the aim target (type or
		// transform) changed. A rail or transporter in the cone replaces a farther aim hit,
		// and the best aligned on-screen homing target is then taken. The cone angle
		// cosines are compared by magnitude, and each accepted homing candidate raises the
		// alignment threshold for the next one.
		private bool UpdateTarget(Vector3 origin, Vector3 direction, float maxDistance)
		{
			bool controllerMovementActive = ControllerMovementActive();
			if (m_aimType == AbilityAimTargetType.Rail && !controllerMovementActive)
				return false;
			bool hit = TryGetHitCollider(origin, direction, maxDistance, out RaycastHit hitInfo);
			AbilityAimTargetType previousAimType = m_aimType;
			Transform previousAimTransform = m_aimTransform;
			if (hit)
			{
				m_aimTransform = hitInfo.transform;
				m_aimPosition = hitInfo.point;
				m_aimOffset = hitInfo.normal * DefinitionAirChaos.OffsetDistance;
				m_aimNormal = hitInfo.normal;
				m_aimRotation = Quaternion.LookRotation(hitInfo.normal, m_character.UpDirection);
				m_groundPosition = m_aimPosition + m_aimOffset;
				m_groundRotation = m_aimRotation;
				if (m_hitCollisionData != null && DefinitionAirChaos.TrackMask.IncludesLayer(m_hitCollisionData.gameObject))
				{
					m_aimType = AbilityAimTargetType.Track;
					m_groundType = AbilityAimTargetType.Track;
				}
				else
				{
					float normalDotUp = Vector3.Dot(m_aimNormal, m_character.WorldUp);
					if (DefinitionAirChaos.LedgeCosAngleMin >= normalDotUp
						&& normalDotUp >= DefinitionAirChaos.LedgeCosAngleMax
						&& TryGetLedgeHit(out RaycastHit ledgeHit))
					{
						SetTransformFromHit(ledgeHit, out m_aimPosition, out m_aimOffset, out m_aimNormal, out m_aimRotation);
						m_aimType = AbilityAimTargetType.Ledge;
						m_groundType = AbilityAimTargetType.Track;
					}
					else
					{
						m_aimType = AbilityAimTargetType.Wall;
						m_groundType = AbilityAimTargetType.Wall;
					}
				}
			}
			else
			{
				m_aimTransform = null;
				m_aimPosition = m_character.WorldPosition + m_character.ForwardDirection * DefinitionAirChaos.WorldDistance;
				m_aimOffset = Vector3.zero;
				m_aimNormal = m_character.UpDirection;
				m_aimRotation = Quaternion.identity;
				m_aimType = AbilityAimTargetType.Air;
				ProjectToGround();
			}
			float aimDistanceSqr = (m_aimPosition + m_aimOffset - m_character.WorldPosition).sqrMagnitude;
			float homingDistanceSqr = aimDistanceSqr;
			bool railFound = TryGetRailHit(out AbilityAimTargetType railTargetType) || TryGetTransporterHit(out railTargetType);
			if (railFound && m_character.WorldPositionIsOnScreen(m_targetedLocation.m_worldPosition, DefinitionAirChaos.HomingTargetScreenBorders))
			{
				float railDistanceSqr = (m_targetedLocation.m_worldPosition - m_character.WorldPosition).sqrMagnitude;
				if (aimDistanceSqr > railDistanceSqr)
				{
					m_aimTransform = null;
					m_aimPosition = m_targetedLocation.m_worldPosition;
					m_aimOffset = Vector3.zero;
					m_aimRotation = m_targetedLocation.m_worldRotation * ForwardToNormalRotation;
					m_aimType = railTargetType;
					m_groundType = railTargetType;
					homingDistanceSqr = railDistanceSqr;
				}
			}
			if (m_homingTargetsDirty)
			{
				RemoveHomingTargetChildren();
				m_homingTargetsDirty = false;
			}
			float coneDistance = DefinitionAirChaos.HomingTargetCone.Distance;
			Vector2 coneTangents = DefinitionAirChaos.HomingTargetCone.Tangents;
			Vector3 coneOrigin = m_character.WorldPosition + m_character.WorldRotation * DefinitionAirChaos.HomingTargetZoneOffset;
			if (!railFound)
				homingDistanceSqr = DefinitionAirChaos.HomingTargetProjectDistanceSqr;
			Quaternion worldToLocalRotation = m_character.WorldToLocalRotation;
			float minimumAlignment = Mathf.Min(
				Mathf.Abs(DefinitionAirChaos.HomingTargetProjectConeCosAngle.x),
				Mathf.Abs(DefinitionAirChaos.HomingTargetProjectConeCosAngle.y));
			foreach ((Collider collider, CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition aimDefinition) in m_homingTargetAimDefinitions)
			{
				Transform colliderTransform = collider.transform;
				Vector3 targetPosition = colliderTransform.TransformPoint(aimDefinition.OffsetLocal);
				if (!m_character.WorldPositionIsOnScreen(targetPosition, DefinitionAirChaos.HomingTargetScreenBorders))
					continue;
				Vector3 closestPoint = collider.ClosestPoint(m_character.CameraPosition);
				if ((closestPoint - m_character.CameraPosition).sqrMagnitude > homingDistanceSqr)
					continue;
				if (!ConeVolume.IsPositionInsideCone(targetPosition, coneOrigin, coneTangents, coneDistance, worldToLocalRotation))
					continue;
				Vector3 cameraToTarget = targetPosition - m_character.CameraPosition;
				float cameraDistance = cameraToTarget.magnitude;
				Vector3 cameraDirection = cameraToTarget / cameraDistance;
				float alignment = Vector3.Dot(m_character.CameraForward, cameraDirection);
				if (minimumAlignment > alignment)
					continue;
				if (Physics.Raycast(m_character.CameraPosition, cameraDirection, out RaycastHit blockingHit, cameraDistance, DefinitionAirChaos.CollisionMask, QueryTriggerInteraction.Ignore)
					&& RaycastHitIsValid(blockingHit, out CharacterCollisionData _))
					continue;
				m_aimTransform = colliderTransform;
				m_aimPosition = colliderTransform.position;
				m_aimOffset = targetPosition - m_aimPosition;
				m_aimRotation = colliderTransform.rotation;
				m_hitCollisionData = m_aimTransform.GetComponent<CharacterCollisionData>();
				if (m_hitCollisionData is not null && m_hitCollisionData.HasTargetingModifier)
				{
					CharacterTargetingModifier targetingModifier = m_hitCollisionData.TargetingModifier;
					m_aimType = targetingModifier.AimType;
					m_groundType = targetingModifier.GroundType;
					m_aimRotation = m_aimRotation * targetingModifier.AimRotationOffset;
				}
				else
				{
					m_aimType = AbilityAimTargetType.HomingTarget;
					foreach (KeyValuePair<HomingTargetType, AbilityAimTargetType> mapping in DefinitionAirChaos.MappingHomingToAimTargetTypes)
					{
						if (aimDefinition.HomingTypes.Contains(mapping.Key))
						{
							m_aimType = mapping.Value;
							break;
						}
					}
					ProjectToGround();
				}
				minimumAlignment = alignment;
			}
			bool changed = false;
			if (m_aimType != previousAimType || m_aimTransform != previousAimTransform)
			{
				OnTargetTypeLeave(previousAimType);
				OnTargetTypeEnter(m_aimType);
				changed = true;
			}
			m_controllerMovementHasBeenActive |= controllerMovementActive;
			m_aimTransformPositionLastFrame = m_aimTransform != null ? m_aimTransform.position : Vector3.zero;
			return changed;
		}

		// 06001045 (1.10.1 RVA 0x740550)
		private void OnTargetTypeEnter(AbilityAimTargetType aimType)
		{
			m_aimType = aimType;
			if (DefinitionAirChaos.TargetTypes.TryGetValue(aimType, out CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget target))
			{
				m_character.TriggerAnimationEnter(target.AnimationDefinition);
				m_cameraTargetTimerActive = true;
				m_cameraTargetTimer = DefinitionAirChaos.CameraTargetDelayOnEnter;
			}
		}

		// 06001046 (1.10.1 RVA 0x73F1E0). While the controller moves the camera the
		// target timer restarts; once it runs out the camera override for the target applies.
		private void OnTargetTypeUpdate(float deltaTime)
		{
			if (!DefinitionAirChaos.TargetTypes.TryGetValue(m_aimType, out CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget target))
				return;
			if (!target.RequiresLerpToTarget)
				m_aimLerpActive = false;
			bool controllerMovementActive = ControllerMovementActive();
			bool timerActive = m_cameraTargetTimerActive;
			if (controllerMovementActive)
			{
				if (!timerActive)
				{
					m_cameraTargetTimerActive = true;
					m_cameraTargetTimer = DefinitionAirChaos.CameraTargetDelayOnStay;
					SetCameraOverrides(null, null);
				}
				return;
			}
			if (timerActive)
			{
				m_cameraTargetTimer = Mathf.Max(m_cameraTargetTimer - deltaTime, 0f);
				if (m_cameraTargetTimer == 0f)
				{
					m_cameraTargetTimerActive = false;
					SetCameraOverrides(target.CameraTargetDefinition, m_aimTransform);
				}
			}
			if (m_aimTransform != null)
				m_character.UpdateCustomProxy(m_character.WorldPosition, m_aimTransform);
			else
				m_character.UpdateCustomProxy(m_character.WorldPosition, m_aimPosition);
		}

		// 06001047 (1.10.1 RVA 0x73E870)
		private void OnTargetTypeLeave(AbilityAimTargetType aimType)
		{
			if (DefinitionAirChaos.TargetTypes.TryGetValue(aimType, out CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget target))
			{
				m_character.TriggerAnimationLeave(target.AnimationDefinition);
				m_cameraTargetTimerActive = false;
				m_cameraTargetTimer = 0f;
				SetCameraOverrides(null, null);
			}
		}

		// 06001048 (1.10.1 RVA 0x73F900). Casts down onto the track from above and in
		// front of the aim point.
		private bool TryGetLedgeHit(out RaycastHit hitInfo)
		{
			Vector3 gravityNormalised = m_character.GravityNormalised;
			Vector3 forward = Vector3.ProjectOnPlane(m_character.ForwardDirection, gravityNormalised).normalized;
			Vector3 rayOrigin = m_aimPosition - gravityNormalised * DefinitionAirChaos.LedgeDistance + forward * DefinitionAirChaos.LedgeOffset;
			return Physics.Raycast(rayOrigin, gravityNormalised, out hitInfo, DefinitionAirChaos.LedgeDistance, DefinitionAirChaos.TrackMask, QueryTriggerInteraction.Ignore);
		}

		// 06001049 (1.10.1 RVA 0x73FF20)
		private bool TryGetRailHit(out AbilityAimTargetType targetType)
		{
			m_cone.Origin = m_character.WorldPosition;
			m_cone.Rotation = m_character.WorldRotation;
			m_cone.Distance = DefinitionAirChaos.HomingRailProjectDistance;
			m_cone.Step = DefinitionAirChaos.HomingRailProjectDistanceStep;
			m_cone.Tangents = DefinitionAirChaos.HomingRailProjectConeTangents;
			targetType = AbilityAimTargetType.Rail;
			return m_character.TrackManager.TryGetProjectedRailHit(m_cone, m_character, ref m_targetedLocation, null, IsSurfaceTargetableRail);
		}

		// 0600104a (1.10.1 RVA 0x740600)
		private bool IsSurfaceTargetableRail(ISurface surface)
		{
			MetadataGroup metadataGroup = surface.GetSurfaceMetadata().GetGroup(m_railDefinition.MetadataGroupKey);
			if (metadataGroup != null)
				return metadataGroup.TryGetValue(m_railDefinition.MetadataTargetableKey, out bool targetable) && targetable;
			return false;
		}

		// 0600104b (1.10.1 RVA 0x740070)
		private bool TryGetTransporterHit(out AbilityAimTargetType targetType)
		{
			m_cone.Origin = m_character.WorldPosition;
			m_cone.Rotation = m_character.WorldRotation;
			m_cone.Distance = DefinitionAirChaos.HomingRailProjectDistance;
			m_cone.Step = DefinitionAirChaos.HomingRailProjectDistanceStep;
			m_cone.Tangents = DefinitionAirChaos.HomingRailProjectConeTangents;
			targetType = AbilityAimTargetType.Transporter;
			return m_character.TrackManager.TryGetProjectedTransporterHit(m_cone, m_character, ref m_targetedLocation, null, IsSurfaceTargetableTransporter);
		}

		// 0600104c (1.10.1 RVA 0x740710)
		private bool IsSurfaceTargetableTransporter(ISurface surface)
		{
			MetadataGroup metadataGroup = surface.GetSurfaceMetadata().GetGroup(m_transporterDefinition.MetadataGroupKey);
			if (metadataGroup != null)
				return metadataGroup.TryGetObject(m_transporterDefinition.MetadataMotionKey, out TrackerMotionDefinition motionDefinition) && motionDefinition != null;
			return false;
		}

		// 0600104d (1.10.1 RVA 0x73FD00). The ground type is Track only when a track
		// collider lies under the aim point within GroundDistance; otherwise it is None
		// and the ground position and rotation keep their previous values.
		private void ProjectToGround()
		{
			Vector3 gravityNormalised = m_character.GravityNormalised;
			AbilityAimTargetType groundType = AbilityAimTargetType.None;
			if (Physics.Raycast(m_aimPosition - gravityNormalised * DefinitionAirChaos.OffsetDistance, gravityNormalised, out RaycastHit hitInfo, DefinitionAirChaos.GroundDistance, DefinitionAirChaos.TrackMask, QueryTriggerInteraction.Ignore))
			{
				Vector3 normal = hitInfo.normal;
				m_groundPosition = hitInfo.point;
				m_groundRotation = Quaternion.LookRotation(normal, m_character.ForwardDirection);
				m_groundPosition = normal * DefinitionAirChaos.OffsetDistance + m_groundPosition;
				groundType = AbilityAimTargetType.Track;
			}
			m_groundType = groundType;
		}

		// 0600104e (1.10.1 RVA 0x73FC20)
		private void SetTransformFromHit(RaycastHit hitInfo, out Vector3 position, out Vector3 offset, out Vector3 normal, out Quaternion rotation)
		{
			normal = hitInfo.normal;
			position = hitInfo.point;
			offset = DefinitionAirChaos.OffsetDistance * normal;
			rotation = Quaternion.LookRotation(normal, m_character.ForwardDirection);
		}

		// 0600104f (1.10.1 RVA 0x73E930). Without a camera override on the hit collider
		// the target's slope override is used. Otherwise the aim rotation takes the
		// collider's rotation, turned around when the override is direction-reversed and
		// the collider axis faces against the current aim forward.
		private CameraAbilityDefinition GetCameraOverride(CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget target)
		{
			CameraAbilityOverrideDefinition overrideDefinition;
			if (target == null)
				return null;
			if (m_hitCollisionData == null
				|| !m_hitCollisionData.HasCameraOverride
				|| !m_hitCollisionData.CameraOverrideMetadata.TryGetObject(target.CameraOverrideMetadataKey, out overrideDefinition)
				|| overrideDefinition == null)
			{
				return target.GetCameraOverride(ActorMovementUtilities.GetSlopeAngle(m_aimRotation, m_character));
			}
			Quaternion rotation = m_hitCollisionData.transform.rotation;
			Vector3 axis = rotation * overrideDefinition.AxisAlignment;
			if (overrideDefinition.ReverseByDirection)
			{
				Vector3 aimForward = m_aimRotation * Vector3.forward;
				if (0f > Vector3.Dot(axis, aimForward))
					rotation = rotation * Actor.RotationReverseLocal;
			}
			m_aimRotation = rotation;
			return overrideDefinition;
		}

		// 06001050 (1.10.1 RVA 0x740870)
		protected override void SetAirActive(bool active)
		{
			bool wasActive = AirActive;
			base.SetAirActive(active);
			if (wasActive && !active)
				EndBrainImpulse();
		}

		// 06001051 (1.10.1 RVA 0x7408D0). Drives the target indicator; the aim point
		// eases towards the target and the arrows scale in once it has nearly arrived.
		public bool UpdateUI(float deltaTime, UITargetIndicatorController transforms, Action<Transform> orientateToCamera)
		{
			if (!AirActive)
				return false;
			Vector3 aimTarget = m_aimPosition + m_aimOffset;
			float scale;
			if (m_aimLerpActive)
			{
				float distanceSqr = (aimTarget - m_aimLerpPosition).sqrMagnitude;
				m_aimLerpPosition = Vector3.SmoothDamp(m_aimLerpPosition, aimTarget, ref m_aimLerpVelocity, DefinitionAirChaos.AimLerpSmoothTime, DefinitionAirChaos.AimLerpMaxSpeed, deltaTime);
				if (distanceSqr > AimLerpDistanceSqrTolerance)
				{
					m_aimLerpScaleActive = AimLerpDistanceSqrTolerance >= (aimTarget - m_aimLerpPosition).sqrMagnitude;
					m_aimLerpScaleTime = 0f;
				}
				else if (m_aimLerpScaleActive)
				{
					m_aimLerpScaleTime += deltaTime;
				}
				if (!m_cameraTargetTimerActive)
					ApplyTargetingImpulse(aimTarget, deltaTime);
				scale = m_aimLerpScaleActive ? DefinitionAirChaos.AimLerpScaleAnim.Evaluate(m_aimLerpScaleTime) : 1f;
			}
			else
			{
				m_aimLerpActive = true;
				m_aimLerpPosition = aimTarget;
				m_aimLerpVelocity = Vector3.zero;
				m_aimLerpScaleActive = false;
				scale = 1f;
			}
			Quaternion aimRotation = m_aimRotation;
			if (m_hitCollisionData is not null && m_hitCollisionData.HasTargetingModifier)
				aimRotation = aimRotation * m_hitCollisionData.TargetingModifier.UIRotationOffset;
			transforms.Set(m_aimLerpPosition, aimRotation, m_groundPosition, m_groundRotation, scale);
			if (DefinitionAirChaos.TargetTypes.TryGetValue(m_aimType, out CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget target))
			{
				transforms.UpdateTarget(deltaTime, target, m_aimType, m_groundType, DefinitionAirChaos.GroundDistance, DefinitionAirChaos.OffsetDistance, DefinitionAirChaos.CollisionMask);
				return true;
			}
			return false;
		}

		// 06001052 (1.10.1 RVA 0x740D20). The camera is nudged towards the target by the
		// signed angle between its forward and the direction to the target, once around
		// world up and once around the character's left axis.
		private void ApplyTargetingImpulse(Vector3 targetPosition, float deltaTime)
		{
			Transform cameraTransform = m_cameraManagerRef.Get().MainCamera.transform;
			Vector3 worldUp = m_character.WorldUp;
			Vector3 negativeRight = -m_character.RightDirection;
			Vector3 toTarget = targetPosition - cameraTransform.position;
			Vector3 cameraForward = cameraTransform.forward;
			m_targetImpulsePrevious = m_targetImpulse;
			m_targetImpulse.x = DefinitionAirChaos.CameraTargetHorizontalInputValue.Evaluate(Vector3Extensions.SignedVectorAngleOnPlane(toTarget, cameraForward, worldUp)) * deltaTime;
			m_targetImpulse.y = DefinitionAirChaos.CameraTargetVerticalInputValue.Evaluate(Vector3Extensions.SignedVectorAngleOnPlane(toTarget, cameraForward, negativeRight)) * deltaTime;
			if (m_cameraManagerRef.TryGet(out CinemachineCameraManager cameraManager))
				cameraManager.ApplyImpulse(m_targetImpulse);
		}

		// 06001053 (1.10.1 RVA 0x740EF0). Debug only; the call sites are compiled out of retail.
		[Conditional("BUILD_DEVELOPMENT")]
		private void SetTargetObject()
		{
			if (m_debugTargetObject == null)
			{
				m_debugTargetObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				m_debugTargetObject.name = "AirChaosAimTarget";
			}
			bool debugEnabled = m_character.DebugIsEnabled();
			m_debugTargetObject.SetActive(debugEnabled);
			if (debugEnabled)
				m_debugTargetObject.transform.SetPositionAndRotation(m_aimPosition, m_aimRotation);
		}

		// 06001054 (1.10.1 RVA 0x740FF0)
		public Vector3 TargetZoneSize => DefinitionAirChaos.HomingTargetZoneSize;

		// 06001055 (1.10.1 RVA 0x741040)
		public Vector3 TargetZoneOffset => DefinitionAirChaos.HomingTargetZoneOffset;

		// 06001056 (1.10.1 RVA 0x741090)
		public void AddTarget(HomingTarget target)
		{
		}

		// 06001057 (1.10.1 RVA 0x7410A0)
		public void RemoveTarget(HomingTarget target)
		{
		}

		// 06001058 (1.10.1 RVA 0x7410B0)
		public bool IsHomingTargetValid(HomingTarget target)
		{
			return false;
		}

		// 06001059 (1.10.1 RVA 0x7410C0)
		public void OnHomingTargetHit(HomingTarget target)
		{
		}

		// 0600105a (1.10.1 RVA 0x7410D0). The first homing definition whose target type
		// is found on the collider (or its parents) and passes its enemy/homing type
		// filters registers the collider.
		public void OnTriggerEnter(Collider otherCollider)
		{
			foreach (CharacterAbilityDefinition_AirChaosAim.HomingTargetAimDefinition definition in DefinitionAirChaos.HomingTargetDefinitions)
			{
				Type targetType = definition.TargetType.Type;
				Component component = otherCollider.gameObject.GetComponentInParent(targetType);
				if (component == null && !otherCollider.TryGetComponent(targetType, out component))
					continue;
				if (component is Enemy enemy)
				{
					if (!enemy.enabled)
						continue;
					if (definition.EnemyTypes.Count > 0 && !definition.EnemyTypes.Contains(enemy.Type))
						continue;
				}
				if (component is HomingTarget homingTarget && definition.HomingTypes.Count > 0 && !definition.HomingTypes.Contains(homingTarget.TargetType))
					continue;
				m_homingTargetsDirty |= m_homingTargetAimDefinitions.TryAdd(otherCollider, definition);
				break;
			}
		}

		// 0600105b (1.10.1 RVA 0x741410)
		public void OnTriggerExit(Collider otherCollider)
		{
			m_homingTargetAimDefinitions.Remove(otherCollider);
		}

		// 0600105c (1.10.1 RVA 0x740270). Drops every homing target that has another
		// registered target among its transform ancestors; each collider is compared with the
		// whole list, itself included.
		private void RemoveHomingTargetChildren()
		{
			m_homingTargets.Clear();
			m_homingTargets.AddRange(m_homingTargetAimDefinitions.Keys);
			foreach (Collider collider in m_homingTargets)
			{
				foreach (Collider other in m_homingTargets)
				{
					if (other.transform.IsParentOf(collider.transform))
					{
						m_homingTargetAimDefinitions.Remove(collider);
						break;
					}
				}
			}
		}

		// 0600105d (1.10.1 RVA 0x741460)
		public CharacterAbility_AirChaosAim()
		{
		}
	}
}
