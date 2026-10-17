using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000b0 (TypeDefIndex 12000); x86-64 bodies.
	[Il2CppSetOption(Option.NullChecks, false)]
	[AddComponentMenu("Hardlight UI/Navigation/Close Dialog Page")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CloseDialogPage : MonoBehaviour
	{
		[SerializeField]
		private bool m_closeAllDialogs;

		// 0x06000515 (0x1b63090)
		public void ClosePage()
		{
			if (m_closeAllDialogs)
			{
				DialogSystem.Instance.CloseAllPages(UISystemClosureReason.StandardClose);
			}
			else
			{
				DialogSystem.Instance.CloseCurrentPage();
			}
		}

		// 0x06000516 (0x1b63120)
		public CloseDialogPage()
		{
		}
	}
}
