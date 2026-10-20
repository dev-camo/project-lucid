using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000302; bodies translated from the 1.10.1 x86-64 binary (shared generic
	// bodies at 0xE61410..0xE625E0). Spawns the detection box and keeps it in front of the character or camera.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_HomingAttackBase<T> : CharacterAbility_TargetingHoming<T>, IHomingAttack where T : CharacterAbilityDefinition_HomingAttackBase
	{
		private HomingAttack m_homingAttack;

		private Rigidbody m_rigidbody;

		private Vector3 m_targetZoneSizeAdjustment;

		private Action m_homingAttackObjectEnable;

		private Action m_homingAttackObjectDisable;

		// 0600124b (1.10.1 RVA 0xE61410); the authored size plus the depth adjustment.
		public Vector3 TargetZoneSize
		{
			get
			{
				return (ShouldProjectFromCamera() ? Definition.TargetZoneSizeFromCamera : Definition.TargetZoneSize) + m_targetZoneSizeAdjustment;
			}
		}

		// 0600124c (1.10.1 RVA 0xE61510)
		public Vector3 TargetZoneOffset
		{
			get
			{
				return ShouldProjectFromCamera() ? Definition.CameraTargetZoneOffset : Definition.CharacterTargetZoneOffset;
			}
		}

		// 0600124d (1.10.1 RVA 0xE615F0)
		protected override bool ProjectFromCameraWhenNoDirectionInput
		{
			get
			{
				return Definition.ProjectFromCameraWhenNoDirectionInput;
			}
		}

		// 0600124e (1.10.1 RVA 0xE61660); the detection box starts disabled.
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			m_homingAttack = ObjectUtils.SpawnPrefab(Definition.HomingAttackPrefab, m_character.transform.parent);
			m_homingAttack.Initialise(m_character, this);
			m_rigidbody = m_homingAttack.GetComponent<Rigidbody>();
			m_homingAttackObjectEnable = () => ActivateHomingAttackObject(true);
			m_homingAttackObjectDisable = () => ActivateHomingAttackObject(false);
			EnableHomingTarget(false);
		}

		// 0600124f (1.10.1 RVA 0xE61850)
		public override void Close()
		{
			base.Close();
			UnityEngine.Object.Destroy(m_homingAttack.gameObject);
			m_homingAttack = null;
		}

		// 06001250 (1.10.1 RVA 0xE618D0)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			EnableHomingTarget(true);
		}

		// 06001251 (1.10.1 RVA 0xE61970)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			EnableHomingTarget(false);
		}

		// 06001252 (1.10.1 RVA 0xE619F0)
		public virtual void AddTarget(HomingTarget target)
		{
			if (ValidateTargetType(target))
			{
				HomingObjectMetaData metaData = new HomingObjectMetaData { HomingTarget = target };
				m_character.HomingPool.AddPotentialTarget(target.gameObject, target.WorldPosition, target.WorldRotation, target.TargetType, this, metaData);
			}
		}

		// 06001253 (1.10.1 RVA 0xE61AF0)
		public virtual void RemoveTarget(HomingTarget target)
		{
			if (m_character == null)
			{
				return;
			}
			m_character.HomingPool.RemoveTarget(this, target.TargetType, target.gameObject);
		}

		// 06001254 (1.10.1 RVA 0xE61B90)
		public virtual void OnHomingTargetHit(HomingTarget target)
		{
			m_character.HomingPool.ClearTarget();
			RemoveTarget(target);
			target.DoOnDisable(m_character.ColliderCollision);
		}

		// 06001255 (1.10.1 RVA 0xE61BE0)
		public virtual bool IsHomingTargetValid(HomingTarget target)
		{
			return Enabled && ValidateTargetType(target);
		}

		// 06001256 (1.10.1 RVA 0xE61C10); the detection box reports entries through AddTarget instead.
		public void OnTriggerEnter(Collider otherCollider)
		{
		}

		// 06001257 (1.10.1 RVA 0xE61C20)
		public void OnTriggerExit(Collider otherCollider)
		{
		}

		// 06001258 (1.10.1 RVA 0xE61C30)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			UpdateHomingTargetLocation();
			HandleAttack(brain, deltaTime);
		}

		// 06001259
		protected abstract bool HandleAttack(CharacterBrain brain, float deltaTime);

		// 0600125a (1.10.1 RVA 0xE61D10); the box object follows on the next frame.
		protected void EnableHomingTarget(bool enable)
		{
			m_rigidbody.detectCollisions = enable;
			CoroutineUtils.OnNextFrame(enable ? m_homingAttackObjectEnable : m_homingAttackObjectDisable);
		}

		// 0600125b (1.10.1 RVA 0xE61D50)
		private void ActivateHomingAttackObject(bool enable)
		{
			if (m_homingAttack == null)
			{
				return;
			}
			m_homingAttack.gameObject.SetActive(enable);
		}

		// 0600125c (1.10.1 RVA 0xE61DE0); from the camera, from the character's heading, or along the input direction.
		protected virtual void UpdateHomingTargetLocation()
		{
			if (ShouldProjectFromCamera())
			{
				float sizeAdjustment = Vector3.Dot(m_character.WorldPosition - m_character.CameraPosition, m_character.CameraForward);
				SetHomingTargetTransform(m_character.CameraPosition, m_character.CameraRotation, TargetZoneSize, TargetZoneOffset, sizeAdjustment);
			}
			else if (Definition.ProjectFromCharacterForward)
			{
				m_targetZoneSizeAdjustment = Vector3.zero;
				SetHomingTargetTransform(m_character.WorldPosition, m_character.WorldRotation, TargetZoneSize, TargetZoneOffset, 0f);
			}
			else
			{
				m_targetZoneSizeAdjustment = Vector3.zero;
				Quaternion rotation = Quaternion.LookRotation(m_character.RawIntendedForward, m_character.UpDirection);
				SetHomingTargetTransform(m_character.WorldPosition, rotation, TargetZoneSize, TargetZoneOffset, 0f);
			}
		}

		// 0600125d (1.10.1 RVA 0xE62440); only the depth component of the adjustment is replaced.
		protected void SetHomingTargetTransform(Vector3 position, Quaternion rotation, Vector3 size, Vector3 offset, float sizeAdjustment)
		{
			m_homingAttack.transform.SetPositionAndRotation(position, rotation);
			m_homingAttack.SetColliderDimensions(size, offset);
			m_targetZoneSizeAdjustment.z = sizeAdjustment;
		}

		// 0600125e
		protected abstract bool ValidateTargetType(HomingTarget target);

		// 0600125f (1.10.1 RVA 0xE624D0)
		protected CharacterAbility_HomingAttackBase()
		{
		}
	}
}
