using System;
using ProjectLucid.Offline;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 0200001f; bodies translated from the 1.10.1 x86-64 binary.
	// Shows the current ad's icon, and each launch opens the full screen view on that ad
	// before rotating to the next one.
	// Hook H-065 (TryShowCurrentAd): see the comment there. The original code around it is unchanged.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CrossPromoLaunchButton : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("The content that owns the launch button visuals")]
		protected GameObject m_content;

		[SerializeField]
		[Tooltip("The image to which the icon texture is injected")]
		protected Image m_iconImage;

		[Tooltip("True if the ads should be reset on initialise. If your launch button gets disabled during full screen display, this should be false.")]
		[SerializeField]
		private bool m_forceUseFirstAdIndexOnInitialise = true;

		private CrossPromo m_crossPromo;

		private SystemRef<CrossPromo> m_crossPromoRef;

		private CrossPromoAd[] m_ads = Array.Empty<CrossPromoAd>();

		private int m_currentAd;

		private int m_impressionCount;

		// 060000f3 (1.10.1 RVA 0x1A17F00)
		protected virtual void OnEnable()
		{
			m_content.SetActive(false);
			m_crossPromoRef = ProcessManager.GetSystemRef<CrossPromo>();
			m_crossPromo = m_crossPromoRef.GetSafe();
			if (m_crossPromoRef.TryGet(out m_crossPromo))
			{
				InitialiseCrossPromo(m_crossPromo);
			}
			else
			{
				m_crossPromoRef.OnSystemStartup += InitialiseCrossPromo;
			}
		}

		// 060000f4 (1.10.1 RVA 0x1A18050)
		private void InitialiseCrossPromo(CrossPromo crossPromo)
		{
			m_crossPromo = crossPromo;
			m_crossPromoRef.OnSystemStartup -= InitialiseCrossPromo;
			// The public route is the explicit ICrossPromo.Initialised, which calls back at once
			// (without keeping the handler) when no sync or download is pending; CrossPromo's own
			// Initialised event is private.
			((ICrossPromo)m_crossPromo).Initialised += OnCrossPromoInitialized;
		}

		// 060000f5 (1.10.1 RVA 0x1A181B0)
		protected virtual void OnCrossPromoInitialized(CrossPromoMetaData metaData)
		{
			if (!metaData.IsSystemEnabled || metaData.Ads == null || metaData.Ads.Length == 0)
			{
				return;
			}
			m_impressionCount = 0;
			// Array.Equals is reference equality: only the very same ad list keeps the current ad.
			bool keepCurrentAd = !m_forceUseFirstAdIndexOnInitialise && m_ads != null && m_ads.Equals(metaData.Ads);
			m_ads = metaData.Ads;
			m_currentAd = keepCurrentAd ? m_currentAd : metaData.GetFirstAdIndex();
			TryShowCurrentAd();
		}

		// 060000f6 (1.10.1 RVA 0x1A18500)
		private void Update()
		{
			if (m_crossPromo == null)
			{
				return;
			}
			if (m_content.activeSelf)
			{
				if (!CanShowLaunchButton())
				{
					m_content.SetActive(false);
				}
			}
			else if (m_ads != null)
			{
				TryShowCurrentAd();
			}
		}

		// 060000f7 (1.10.1 RVA 0x1A18570)
		protected virtual bool CanShowLaunchButton()
		{
			return true;
		}

		// 060000f8 (1.10.1 RVA 0x1A18580)
		protected virtual void RequestFullScreenView(int adIndex)
		{
			m_crossPromo.RequestFullScreenView(adIndex);
		}

		// 060000f9 (1.10.1 RVA 0x1A18270)
		private void TryShowCurrentAd()
		{
			if (!CanShowLaunchButton())
			{
				return;
			}
			Sprite previousIcon = m_iconImage.sprite;
			if (previousIcon != null)
			{
				m_iconImage.sprite = null;
				m_crossPromo.ReleaseIconTexture(previousIcon);
			}
			// Offline hook H-065: offline no metadata is ever parsed, so the ad list keeps its initial
			// Array.Empty and Update retries this method every frame while the content is hidden. The
			// original then reads m_ads[m_currentAd] past the end of the empty array (x86-64 0x1A18395..0x1A183AF:
			// no length compare, ArrayBoundsChecks off) and hands that memory to the resource manager as the
			// icon's texture description. The managed read throws instead, so offline we return here with
			// no ad, which is where the offline resource manager's answer (no icon for any request) leads:
			// the icon == null return below, content left hidden.
			if (OfflineAdapters.Active && m_ads.Length == 0)
			{
				return;
			}
			Sprite icon = m_crossPromo.RequestIconTexture(m_ads[m_currentAd].IconTexture);
			if (icon == null)
			{
				return;
			}
			m_iconImage.sprite = icon;
			m_content.SetActive(true);
			m_crossPromo.RaiseImpressionEvent(m_ads[m_currentAd], m_impressionCount);
			m_impressionCount++;
		}

		// 060000fa (1.10.1 RVA 0x1A185C0)
		private void OnDisable()
		{
			m_content.SetActive(false);
			if (m_crossPromo == null)
			{
				m_crossPromoRef.OnSystemStartup -= InitialiseCrossPromo;
				return;
			}
			((ICrossPromo)m_crossPromo).Initialised -= OnCrossPromoInitialized;
			Sprite icon = m_iconImage.sprite;
			if (icon == null)
			{
				return;
			}
			m_iconImage.sprite = null;
			m_crossPromo.ReleaseIconTexture(icon);
		}

		// 060000fb (1.10.1 RVA 0x1A18810)
		public void Action_LaunchCrossPromo()
		{
			RequestFullScreenView(m_currentAd);
			if (m_ads.Length >= 2)
			{
				m_currentAd = m_currentAd + 1 < m_ads.Length ? m_currentAd + 1 : 0;
				TryShowCurrentAd();
			}
		}
	}
}
