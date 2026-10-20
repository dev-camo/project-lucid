using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200030c; bodies translated from the 1.10.1 x86-64 binary (shared generic
	// bodies at 0xE68BA0..0xE690D0). A targeting ability that registers with the character's homing pool.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_TargetingHoming<T> : CharacterAbility_Targeting<T>, IHomingAbility, IAbility, IAbilityTrigger where T : CharacterAbilityDefinition_Targeting
	{
		private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);

		// 060012a2/060012a3 (1.10.1 RVA 0xE68BA0/0xE68BB0)
		public bool IsTriggered { get; set; }

		// 060012a4 (1.10.1 RVA 0xE68BC0)
		protected virtual bool ProjectFromCameraWhenNoDirectionInput
		{
			get
			{
				return false;
			}
		}

		// 060012a5 (1.10.1 RVA 0xE68BD0)
		public virtual bool OverridesTargeting
		{
			get
			{
				return false;
			}
		}

		// 060012a6 (1.10.1 RVA 0xE68BE0); a heading override that asks for it wins over the input test.
		public bool ShouldProjectFromCamera()
		{
			if (m_cameraManagerRef.IsValid())
			{
				CameraHeadingOverride headingOverride = m_cameraManagerRef.Get().GetCameraSettingOverride<CameraHeadingOverride>(CameraSettingType.HeadingOverride);
				if (headingOverride != null && headingOverride.Settings.OverrideTargetingHoming)
				{
					return true;
				}
			}
			return ProjectFromCameraWhenNoDirectionInput && !m_character.HasControllerMovement;
		}

		// 060012a7 (1.10.1 RVA 0xE68CB0)
		public CharacterAbilityDefinition_Targeting TargetingDefinition
		{
			get
			{
				return Definition;
			}
		}

		// 060012a8 (1.10.1 RVA 0xE68D20)
		protected override float TargetingIntervalSeconds
		{
			get
			{
				return Definition.TargetingIntervalSeconds;
			}
		}

		// 060012a9 (1.10.1 RVA 0xE68D90)
		protected override float CooldownTimeSeconds
		{
			get
			{
				return Definition.CooldownTimeSeconds;
			}
		}

		// 060012aa (1.10.1 RVA 0xE68E00)
		protected override bool UseTimeScaling
		{
			get
			{
				return Definition.UseTimeScaling;
			}
		}

		// 060012ab (1.10.1 RVA 0xE68E70); never assigned here, the homing attack supplies the authored curve.
		protected virtual AnimationCurve AdditionalSpeedCurve { get; }

		// 060012ac (1.10.1 RVA 0xE68E80); only an idle, active character takes the target.
		protected bool TrySetTargetImmediately(CharacterTargetObjectData targetObject, Vector3 restoreVelocity, float targetSpeed, float rotationalSpeed = 0f)
		{
			if (m_character.IsActive() && !m_character.HomingPool.IsTargetActive())
			{
				m_character.HomingPool.SetTargetImmediately(this, targetObject, restoreVelocity, targetSpeed, rotationalSpeed);
				return true;
			}
			return false;
		}

		// 060012ad (1.10.1 RVA 0xE68F20)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			if (IsTriggered)
			{
				IsTriggered = m_character.HomingPool.IsTargetActive(this);
			}
		}

		// 060012ae (1.10.1 RVA 0xE68FA0)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			m_character.HomingPool.RemoveTarget(this);
		}

		// 060012af (1.10.1 RVA 0xE69000)
		public virtual void Reset()
		{
			if (m_character.HomingPool.PendingTargetIsValidForAbility(this))
			{
				m_character.HomingPool.ClearTarget();
			}
		}

		// 060012b0 (1.10.1 RVA 0xE69050); a curve needs two keys to contribute.
		public virtual float GetAdditionalSpeed(float time)
		{
			if (AdditionalSpeedCurve == null || AdditionalSpeedCurve.length < 2)
			{
				return 0f;
			}
			return AdditionalSpeedCurve.Evaluate(time);
		}

		// 060012b1 (1.10.1 RVA 0xE690C0)
		public bool CanActivate
		{
			get
			{
				return m_character.IsTargetingAvailable();
			}
		}

		// 060012b2 (1.10.1 RVA 0xE690D0)
		protected CharacterAbility_TargetingHoming()
		{
		}
	}
}
