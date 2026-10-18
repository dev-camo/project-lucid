using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
	// Original Game.Runtime 02000097; bodies translated from the 1.10.1 x86-64 binary.
	[ExecuteInEditMode]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AnimationCustomDataCamera : MonoBehaviour
	{
		[SerializeField]
		private Volume m_postProcessingVolume;

		[HideInInspector]
		[Header("Focus Distance")]
		public float FocusDistance;

		[SerializeField]
		private bool m_focusDistanceOverride;

		[SerializeField]
		private float m_focusDistanceMultiplier = 0.01f;

		[HideInInspector]
		[Header("Focal Length")]
		public float FocalLength;

		[SerializeField]
		private bool m_focalLengthOverride;

		[SerializeField]
		private float m_focalLengthMultiplier = 1f;

		[HideInInspector]
		[Header("Aperture")]
		public float FStop;

		[SerializeField]
		private bool m_fStopOverride;

		[SerializeField]
		private float m_fStopMultiplier = 1f;

		[HideInInspector]
		[Header("Near Clip")]
		public float NearClip;

		[SerializeField]
		private bool m_nearClipOverride;

		[SerializeField]
		private float m_nearClipMultiplier = 1f;

		[Header("Far Clip")]
		[HideInInspector]
		public float FarClip;

		[SerializeField]
		private bool m_farClipOverride;

		[SerializeField]
		private float m_farClipMultiplier = 1f;

		// 0600050e/0600050f (1.10.1 RVA 0x52E150/0x52E160)
		public float FocusDistanceOutput { get; private set; }

		// 06000510/06000511 (1.10.1 RVA 0x52E170/0x52E180)
		public float FocalLengthOutput { get; private set; }

		// 06000512/06000513 (1.10.1 RVA 0x52E190/0x52E1A0)
		public float FStopOutput { get; private set; }

		// 06000514/06000515 (1.10.1 RVA 0x52E1B0/0x52E1C0)
		public float NearClipOutput { get; private set; }

		// 06000516/06000517 (1.10.1 RVA 0x52E1D0/0x52E1E0)
		public float FarClipOutput { get; private set; }

		private bool m_volumeProfileProvided;

		// 06000518 (1.10.1 RVA 0x52E1F0): the flag is only ever set, never cleared.
		private void OnEnable()
		{
			if (m_postProcessingVolume == null)
				return;
			m_volumeProfileProvided = true;
		}

		// 06000519 (1.10.1 RVA 0x52E250): an override uses the multiplier as the
		// value itself. Only the depth of field parameters are pushed to the
		// shared profile; the clip outputs are read by other components.
		private void LateUpdate()
		{
			FocusDistanceOutput = m_focusDistanceOverride ? m_focusDistanceMultiplier : FocusDistance * m_focusDistanceMultiplier;
			FocalLengthOutput = m_focalLengthOverride ? m_focalLengthMultiplier : FocalLength * m_focalLengthMultiplier;
			FStopOutput = m_fStopOverride ? m_fStopMultiplier : FStop * m_fStopMultiplier;
			NearClipOutput = m_nearClipOverride ? m_nearClipMultiplier : NearClip * m_nearClipMultiplier;
			FarClipOutput = m_farClipOverride ? m_farClipMultiplier : FarClip * m_farClipMultiplier;

			if (!m_volumeProfileProvided)
				return;

			foreach (VolumeComponent component in m_postProcessingVolume.sharedProfile.components)
			{
				if (component is DepthOfField depthOfField)
				{
					depthOfField.focusDistance.SetValue(new MinFloatParameter(FocusDistanceOutput, FocusDistanceOutput));
					depthOfField.focalLength.SetValue(new MinFloatParameter(FocalLengthOutput, FocalLengthOutput));
					depthOfField.aperture.SetValue(new MinFloatParameter(FStopOutput, FStopOutput));
				}
			}
		}

		// 0600051a (1.10.1 RVA 0x52E550)
		public AnimationCustomDataCamera()
		{
		}
	}
}
