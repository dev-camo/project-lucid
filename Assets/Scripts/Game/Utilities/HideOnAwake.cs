using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000a84; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class HideOnAwake : MonoBehaviour
	{
		[SerializeField]
		private MeshRenderer[] m_meshRenderers;

		// 06003c6b (1.10.1 RVA 0x6CB620): HideObjects inlined.
		private void Awake()
		{
			HideObjects();
		}

		// 06003c6c (1.10.1 RVA 0x6CB670): the renderers' whole objects are deactivated.
		public void HideObjects()
		{
			foreach (MeshRenderer meshRenderer in m_meshRenderers)
				meshRenderer.gameObject.SetActive(false);
		}

		// 06003c6d (1.10.1 RVA 0x6CB6C0)
		private void Reset()
		{
			m_meshRenderers = GetComponentsInChildren<MeshRenderer>();
		}
	}
}
