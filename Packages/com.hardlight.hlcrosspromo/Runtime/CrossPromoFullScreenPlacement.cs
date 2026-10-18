using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 0200001c; bodies translated from the 1.10.1 x86-64 binary.
	// Opens the full screen view whenever the cross promo system requests it.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CrossPromoFullScreenPlacement : MonoBehaviour
	{
		[Tooltip("The Full Screen View visual component")]
		[SerializeField]
		private CrossPromoFullScreenView m_fullScreenView;

		private SystemRef<CrossPromo> m_crossPromoRef;

		private CrossPromo m_crossPromo;

		// 060000cd (1.10.1 RVA 0x1A15EA0)
		private void Awake()
		{
			m_crossPromoRef = ProcessManager.GetSystemRef<CrossPromo>();
			if (m_crossPromoRef.TryGet(out m_crossPromo))
			{
				InitialiseCrossPromo(m_crossPromo);
			}
			else
			{
				m_crossPromoRef.OnSystemStartup += InitialiseCrossPromo;
			}
		}

		// 060000ce (1.10.1 RVA 0x1A15FC0)
		private void InitialiseCrossPromo(CrossPromo crossPromo)
		{
			m_crossPromoRef.OnSystemStartup -= InitialiseCrossPromo;
			m_crossPromo = crossPromo;
			m_crossPromo.FullScreenViewRequested += Show;
		}

		// 060000cf (1.10.1 RVA 0x1A16140)
		private void Show(CrossPromoMetaData metaData, int adIndex)
		{
			m_fullScreenView.Show(m_crossPromo, metaData, adIndex);
		}

		// 060000d0 (1.10.1 RVA 0x1A16400)
		private void OnEnable()
		{
			m_fullScreenView.gameObject.SetActive(false);
		}

		// 060000d1 (1.10.1 RVA 0x1A16420)
		private void Update()
		{
			if (m_crossPromo != null && m_fullScreenView.gameObject.activeSelf && Input.GetKeyUp(KeyCode.Escape))
			{
				m_fullScreenView.gameObject.SetActive(false);
				m_crossPromo.RaiseFullScreenClosedEvent();
			}
		}

		// 060000d2 (1.10.1 RVA 0x1A164F0)
		private void OnDestroy()
		{
			if (m_crossPromoRef == null)
			{
				return;
			}
			if (m_crossPromo != null)
			{
				m_crossPromo.FullScreenViewRequested -= Show;
			}
			else
			{
				m_crossPromoRef.OnSystemStartup -= InitialiseCrossPromo;
			}
		}

		// 060000d3 (1.10.1 RVA 0x1A164A0)
		public void Action_Close()
		{
			m_fullScreenView.gameObject.SetActive(false);
			m_crossPromo.RaiseFullScreenClosedEvent();
		}
	}
}
