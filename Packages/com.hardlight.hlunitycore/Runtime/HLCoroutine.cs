using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

// Original HLUnityCore.Runtime 02000005; bodies translated from the 1.10.1 x86-64 binary.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public class HLCoroutine : MonoBehaviour
{
	private static HLCoroutine m_instance;

	[Obsolete("HLCoroutine is obsolete, please use CoroutineUtils instead.")]
	public static HLCoroutine Instance
	{
		// 06000004 (1.10.1 RVA 0x1A98930)
		get
		{
			if (m_instance == null)
			{
				GameObject gameObject = new GameObject("HLCoroutine", typeof(HLCoroutine));
				m_instance = gameObject.GetComponent<HLCoroutine>();
				UnityEngine.Object.DontDestroyOnLoad(m_instance);
			}
			return m_instance;
		}
	}

	// 06000005 (1.10.1 RVA 0x1A98B10)
	[Obsolete("HLCoroutine is obsolete, please use CoroutineUtils instead.")]
	public static bool InstanceIsValid()
	{
		return m_instance != null;
	}

	// 06000006 (1.10.1 RVA 0x1A98B80)
	public HLCoroutine()
	{
	}
}
