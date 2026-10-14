using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

// Original Game.Runtime 02000009; bodies translated from the 1.10.1 x86-64 binary.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public sealed class AppleTVRemoteSetter : MonoBehaviour
{
	[SerializeField]
	private bool m_setOnAwake;

	[ShowIf("m_setOnAwake", (string)null)]
	[SerializeField]
	private bool m_allowExitToHome;

	// 06000017 (1.10.1 RVA 0x506EE0)
	private void Awake()
	{
	}

	// 06000018 (1.10.1 RVA 0x506EF0)
	public void AllowExitToHome(bool active)
	{
	}

	// 06000019 (1.10.1 RVA 0x506F00)
	public AppleTVRemoteSetter()
	{
	}
}
