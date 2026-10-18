using System;
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000a22 and its iterator <Start>d__1 (02000a23); bodies
	// translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class SequentialActivator : MonoBehaviour
	{
		[SerializeField]
		private GameObject[] m_objectsToActivate = Array.Empty<GameObject>();

		// 060039fe, MoveNext 06003a02 (0x6B3D30): one frame passes before each object is
		// activated, so the last activation happens on the final resume.
		private IEnumerator Start()
		{
			foreach (GameObject targetGameObject in m_objectsToActivate)
			{
				yield return null;
				targetGameObject.SetActive(true);
			}
		}
	}
}
