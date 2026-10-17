using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000af (TypeDefIndex 11999); x86-64 bodies.
	[Il2CppSetOption(Option.NullChecks, false)]
	[AddComponentMenu("Hardlight UI/Navigation/Close All Menu Pages")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CloseAllMenuPages : MonoBehaviour
	{
		// 0x06000513 (0x1b63020)
		public void CloseAllPages()
		{
			MenuSystem.Instance.CloseAllPages(UISystemClosureReason.StandardClose);
		}

		// 0x06000514 (0x1b63080)
		public CloseAllMenuPages()
		{
		}
	}
}
