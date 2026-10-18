using Cinemachine;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020000a1; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[ExecuteInEditMode]
	public class AnimatorToCinemachineVirtualCamera : MonoBehaviour
	{
		[SerializeField]
		private AnimationCustomDataCamera m_animatedCameraData;

		[SerializeField]
		private CinemachineVirtualCamera m_cinemachineVirtualCamera;

		[SerializeField]
		private Camera m_testCamera;

		// 06000537 (1.10.1 RVA 0x52F190): the virtual camera takes the animated
		// focal length as a field of view for a 24 mm sensor (0x29a7500).
		private void LateUpdate()
		{
			if (m_animatedCameraData == null)
				return;
			if (m_testCamera != null)
				m_testCamera.focalLength = m_animatedCameraData.FocalLengthOutput;
			if (m_cinemachineVirtualCamera == null)
				return;
			m_cinemachineVirtualCamera.m_Lens.FieldOfView = Camera.FocalLengthToFieldOfView(m_animatedCameraData.FocalLengthOutput, 24f);
		}

		// 06000538 (1.10.1 RVA 0x52F280)
		public AnimatorToCinemachineVirtualCamera()
		{
		}
	}
}
