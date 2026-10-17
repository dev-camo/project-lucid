using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x0200005c (TypeDefIndex 11916); x86-64 bodies.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class ViewBase : MonoBehaviour
	{
		public FastAction OnSetupComplete;
		[SerializeField]
		private RectTransform m_rectTransform;
		[HideInInspector]
		public ViewParameters ViewParameters;

		// 0x060002b2 (0x1b4d060): the parameters are stored before the subclass sees them.
		public void Setup(ViewParameters viewParams = null)
		{
			ViewParameters = viewParams;
			OnSetup(viewParams);
			OnSetupComplete.Invoke();
		}

		// 0x060002b3
		protected abstract void OnSetup(ViewParameters viewParams);

		// 0x060002b4 (0x1b4d0a0): falls back to the own RectTransform while the field is Unity-null.
		public RectTransform RectTransform
		{
			get
			{
				if (m_rectTransform == null)
				{
					m_rectTransform = GetComponent<RectTransform>();
				}
				return m_rectTransform;
			}
		}

		// 0x060002b5 (0x1b4d140)
		public virtual void SetActive(bool value)
		{
			gameObject.SetActive(value);
		}

		// 0x060002b6 (0x1b4d170)
		protected ViewBase()
		{
		}
	}
}
