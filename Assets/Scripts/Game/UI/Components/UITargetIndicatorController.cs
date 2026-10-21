using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200084c; bodies translated from the 1.10.1 x86-64 binary.
	// Drives the chaos aim target indicator: disc, ring, beam and arrows follow the
	// aim target, and ballistic projections preview the air route to it.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Serializable]
	public class UITargetIndicatorController
	{
		private readonly Quaternion LocalToWorldUp = Quaternion.AngleAxis(-90f, Vector3.right);

		[SerializeField]
		private Transform m_beam;

		[SerializeField]
		private Transform m_disc;

		[SerializeField]
		private Transform m_ring;

		[SerializeField]
		private Transform m_arrowsParent;

		[SerializeField]
		private Transform m_arrow0;

		[SerializeField]
		private Transform m_arrow1;

		[SerializeField]
		private Transform m_arrow0Scaler;

		[SerializeField]
		private Transform m_arrow1Scaler;

		[SerializeField]
		private Transform m_arrowsAimVisual;

		[SerializeField]
		private Transform m_projectionAim;

		[SerializeField]
		private Transform m_projectionLocation;

		[SerializeField]
		private Transform m_projectionLocationVisual;

		[SerializeField]
		private LineRendererContainer m_projectionLocationRenderer;

		[SerializeField]
		private Transform m_projectionGround;

		[SerializeField]
		private Transform m_projectionTarget;

		[SerializeField]
		private float m_projectionTime = 2f;

		[SerializeField]
		private float m_projectionSpeedMin = 10f;

		[SerializeField]
		private float m_projectionSpeedMax = 30f;

		[SerializeField]
		private List<ProjectionPlacement> m_projections;

		private Vector3 m_aimPosition;

		private Quaternion m_aimRotation;

		private Vector3 m_groundPosition;

		private Quaternion m_groundRotation;

		private Quaternion? m_overrideRotation;

		private Action<Transform> m_orientateToCamera;

		private float m_arrow0AnimTime;

		private float m_arrow1AnimTime;

		private float m_arrowScaleMultiplier;

		private float m_projectionTotalTime;

		private CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget m_aimTarget;

		private Character m_character;

		// 0600300a (1.10.1 RVA 0x60BAD0)
		public void Initialise(Character character, float startSpeed, float endSpeed, float startGravityMultiplier, float endGravityMultiplier, Action<Transform> orientateToCamera)
		{
			m_character = character;
			m_orientateToCamera = orientateToCamera;
			foreach (ProjectionPlacement projection in m_projections)
				projection.Initialise(startSpeed, endSpeed, startGravityMultiplier, endGravityMultiplier);
		}

		// 0600300b (1.10.1 RVA 0x60BC60)
		public void Set(Vector3 aimPosition, Quaternion aimRotation, Vector3 groundPosition, Quaternion groundRotation, float scaleMultiplier)
		{
			m_aimPosition = aimPosition;
			m_aimRotation = aimRotation;
			m_groundPosition = groundPosition;
			m_groundRotation = groundRotation;
			m_arrowScaleMultiplier = scaleMultiplier;
		}

		// 0600300c (1.10.1 RVA 0x60BCB0). A scale multiplier above one scales both arrows
		// and holds their animation at the start; otherwise the arrow clocks advance. The
		// disc and ring are hidden for ground types when there is no ground target.
		public void UpdateTarget(float deltaTime, CharacterAbilityDefinition_AirChaosAim.ChaosAimTarget target, AbilityAimTargetType aimType, AbilityAimTargetType groundType, float groundDistance, float groundOffset, LayerMask collisionMask)
		{
			m_aimTarget = target;
			Vector3 arrowScale = Vector3.one;
			if (m_arrowScaleMultiplier > 1f)
			{
				arrowScale = Vector3.one * m_arrowScaleMultiplier;
				m_arrow0AnimTime = 0f;
				m_arrow1AnimTime = 0f;
			}
			else
			{
				m_aimTarget.UpdateAnimation(ref m_arrow0AnimTime, ref m_arrow1AnimTime, deltaTime);
			}
			m_overrideRotation = new Quaternion?(m_aimTarget.TargetRotation);
			SetTransform(m_disc, groundType != AbilityAimTargetType.None ? m_aimTarget.DiscTransformType : AbilityAimTransformType.None);
			AbilityAimTransformType ringTransformType = m_aimTarget.RingTransformType;
			if (IsTransformTypeGround(ringTransformType) && groundType == AbilityAimTargetType.None)
				ringTransformType = AbilityAimTransformType.None;
			SetTransform(m_ring, ringTransformType);
			SetTransform(m_beam, m_aimTarget.BeamTransformType);
			m_overrideRotation = null;
			SetTransformAnimation(m_arrow0, m_aimTarget.Arrow0Enabled, m_aimTarget.Arrow0PositionAnimation, m_aimTarget.Arrow0RotationAnimation, m_arrow0AnimTime);
			SetTransformAnimation(m_arrow1, m_aimTarget.Arrow1Enabled, m_aimTarget.Arrow1PositionAnimation, m_aimTarget.Arrow1RotationAnimation, m_arrow1AnimTime);
			m_overrideRotation = new Quaternion?(m_aimTarget.ArrowsRotation);
			SetTransform(m_arrowsParent, m_aimTarget.ArrowsTransformType);
			m_overrideRotation = null;
			m_arrow0Scaler.localScale = arrowScale;
			m_arrow1Scaler.localScale = arrowScale;
			bool projecting = UpdateProjections(aimType, groundDistance, groundOffset, collisionMask, deltaTime);
			m_arrowsAimVisual.gameObject.SetActive(projecting);
		}

		// 0600300d (1.10.1 RVA 0x60CCE0)
		private void ResetProjections()
		{
			m_projectionTotalTime = 0f;
			foreach (ProjectionPlacement projection in m_projections)
				projection.Reset();
			m_projectionLocationVisual.gameObject.SetActive(true);
			m_projectionAim.gameObject.SetActive(true);
			m_projectionLocationRenderer.SetActive(true);
		}

		// 0600300e (1.10.1 RVA 0x60C590). Only an air aim shows projections. The route from
		// the character to the aim point is traced once per cycle; each projection then
		// advances along it at a speed scaled by the route length, and the first one still
		// visible (and, when the route hit something, not below the hit) places the
		// location marker. Returns whether the aim type is Air.
		private bool UpdateProjections(AbilityAimTargetType aimType, float groundDistance, float groundOffset, LayerMask collisionMask, float deltaTime)
		{
			RaycastHit hitInfo = default(RaycastHit);
			bool isAir = aimType == AbilityAimTargetType.Air;
			bool wasActive = m_projectionLocation.gameObject.activeSelf;
			m_projectionLocation.gameObject.SetActive(isAir);
			if (aimType != AbilityAimTargetType.Air)
			{
				m_projectionAim.gameObject.SetActive(false);
				m_projectionGround.gameObject.SetActive(false);
				m_projectionTarget.gameObject.SetActive(false);
				m_projectionLocationRenderer.SetActive(false);
				return aimType == AbilityAimTargetType.Air;
			}
			if (!wasActive || m_projectionTotalTime >= m_projectionTime)
				ResetProjections();
			m_projectionTotalTime += deltaTime;
			Vector3 characterPosition = m_character.WorldPosition;
			Vector3 aimPosition = m_aimPosition;
			float routeDistance = 0f;
			bool routeHit = false;
			foreach (ProjectionPlacement projection in m_projections)
				routeHit |= projection.ProjectForRaycastHit(characterPosition, aimPosition, m_character.Gravity, collisionMask, m_projectionLocationRenderer, ref hitInfo, ref routeDistance);
			SetProjectionTarget(routeHit, hitInfo, groundOffset);
			m_projectionLocationRenderer.Finalise();
			float projectionSpeed = Mathf.Clamp(routeDistance / m_projectionTime, m_projectionSpeedMin, m_projectionSpeedMax);
			foreach (ProjectionPlacement projection in m_projections)
			{
				if (!projection.Update(characterPosition, aimPosition, projectionSpeed, m_character.Gravity, collisionMask, deltaTime))
					continue;
				if (routeHit && projection.RaycastHit && Vector3.Dot(projection.Position - hitInfo.point, m_character.GravityNormalised) > 0f)
					continue;
				SetProjectionLocation(projection.Position, groundDistance, groundOffset, collisionMask);
				return aimType == AbilityAimTargetType.Air;
			}
			m_projectionLocationVisual.gameObject.SetActive(false);
			m_projectionGround.gameObject.SetActive(false);
			return aimType == AbilityAimTargetType.Air;
		}

		// 0600300f (1.10.1 RVA 0x60CF30)
		private void SetProjectionLocation(Vector3 position, float groundDistance, float groundOffset, LayerMask collisionMask)
		{
			RaycastHit hitInfo = default(RaycastHit);
			m_projectionLocation.position = position;
			bool groundHit = Physics.Raycast(position, m_character.GravityNormalised, out hitInfo, groundDistance, collisionMask, QueryTriggerInteraction.Ignore);
			m_projectionGround.gameObject.SetActive(groundHit);
			if (groundHit)
				m_projectionGround.SetPositionAndRotation(hitInfo.point + hitInfo.normal * groundOffset, Quaternion.LookRotation(hitInfo.normal, m_character.ForwardDirection));
		}

		// 06003010 (1.10.1 RVA 0x60CE60)
		private void SetProjectionTarget(bool projectHit, RaycastHit hitInfo, float groundOffset)
		{
			m_projectionTarget.gameObject.SetActive(projectHit);
			if (projectHit)
				m_projectionTarget.SetPositionAndRotation(hitInfo.point + hitInfo.normal * groundOffset, Quaternion.LookRotation(hitInfo.normal, m_character.ForwardDirection));
		}

		// 06003011 (1.10.1 RVA 0x60C430)
		private bool IsTransformTypeGround(AbilityAimTransformType transformType)
		{
			return transformType == AbilityAimTransformType.GroundTranslateRotate
				|| transformType == AbilityAimTransformType.GroundTranslateCameraFacing
				|| transformType == AbilityAimTransformType.GroundTranslate;
		}

		// 06003012 (1.10.1 RVA 0x60BF90). None hides the transform; the others place it on
		// the aim or ground location with the rotation named by the type. AimTranslateWorldUp
		// is scaled by four. Any other value leaves the transform untouched.
		private void SetTransform(Transform transform, AbilityAimTransformType transformType)
		{
			switch (transformType)
			{
				case AbilityAimTransformType.None:
					transform.gameObject.SetActive(false);
					break;
				case AbilityAimTransformType.AimTranslate:
					SetTransform(transform, m_aimPosition, Quaternion.AngleAxis(0f, m_character.WorldUp) * LocalToWorldUp);
					break;
				case AbilityAimTransformType.AimTranslateCameraFacing:
					m_orientateToCamera(transform);
					SetTransform(transform, m_aimPosition, transform.rotation);
					break;
				case AbilityAimTransformType.AimTranslateRotate:
					SetTransform(transform, m_aimPosition, m_aimRotation);
					break;
				case AbilityAimTransformType.AimTranslateWorldUp:
					SetTransform(transform, m_aimPosition, Quaternion.AngleAxis(0f, m_character.WorldUp) * Quaternion.Inverse(LocalToWorldUp), 4f);
					break;
				case AbilityAimTransformType.GroundTranslate:
					SetTransform(transform, m_groundPosition, Quaternion.AngleAxis(0f, m_character.WorldUp) * LocalToWorldUp);
					break;
				case AbilityAimTransformType.GroundTranslateCameraFacing:
					m_orientateToCamera(transform);
					SetTransform(transform, m_groundPosition, transform.rotation);
					break;
				case AbilityAimTransformType.GroundTranslateRotate:
					SetTransform(transform, m_groundPosition, m_groundRotation);
					break;
			}
		}

		// 06003013 (1.10.1 RVA 0x60D0D0)
		private void SetTransformActive(Transform transform, bool active)
		{
			transform.gameObject.SetActive(active);
		}

		// 06003014 (1.10.1 RVA 0x60D100). An override rotation, when set, is applied on
		// top of the given rotation.
		private void SetTransform(Transform transform, Vector3 position, Quaternion rotation, float scale = 1f)
		{
			transform.gameObject.SetActive(true);
			if (m_overrideRotation.HasValue)
				rotation = rotation * m_overrideRotation.Value;
			transform.SetPositionAndRotation(position, rotation);
			transform.localScale = Vector3.one * scale;
		}

		// 06003015 (1.10.1 RVA 0x60C460). Each arrow rides a 2D curve: the rotation curve
		// gives the angle about forward and the position curve the distance along up.
		private void SetTransformAnimation(Transform transform, bool active, AnimationCurve positionAnim, AnimationCurve rotationAnim, float time)
		{
			transform.gameObject.SetActive(active);
			if (active)
			{
				Quaternion rotation = Quaternion.AngleAxis(rotationAnim.Evaluate(time), Vector3.forward);
				Vector3 position = rotation * (Vector3.up * positionAnim.Evaluate(time));
				transform.SetLocalPositionAndRotation(position, rotation);
			}
		}

		// 06003016 (1.10.1 RVA 0x60D2E0)
		public UITargetIndicatorController()
		{
		}
	}
}
