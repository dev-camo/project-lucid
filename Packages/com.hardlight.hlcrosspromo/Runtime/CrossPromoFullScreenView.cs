using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 0200001d; bodies translated from the 1.10.1 x86-64 binary.
	// Three pages (previous, current, next) sit side by side in the scrolled content, so a
	// horizontal normalized position of 0, 0.5 or 1 docks page 1, 2 or 3.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CrossPromoFullScreenView : MonoBehaviour, IEndDragHandler, IEventSystemHandler, IBeginDragHandler
	{
		private const int PageCount = 3;

		private const float SnapThreshold = 0.3333f;

		private const float Accuracy = 0.0001f;

		private const float TransitionDuration = 0.25f;

		private const float Paginator = 2f;

		private const float Spacing = 0.5f;

		private const float SnapDistance = 0.16665f;

		private const string DefaultButtonLocalisation = "PLAY FOR FREE";

		[SerializeField]
		private ScrollRect m_contentScroller;

		[Tooltip("The rect transform for the Scrolled Content")]
		[SerializeField]
		private RectTransform m_scrolledContent;

		[Tooltip("The left page of the three")]
		[SerializeField]
		[Header("Pages")]
		private CrossPromoPageBase m_pageBasePrev;

		[Tooltip("The center page of the three")]
		[SerializeField]
		private CrossPromoPageBase m_pageBaseCurrent;

		[Tooltip("The right page of the three")]
		[SerializeField]
		private CrossPromoPageBase m_pageBaseNext;

		[SerializeField]
		[Tooltip("The previous ad button")]
		[Header("Navigation")]
		private GameObject m_previousAdButton;

		[Tooltip("The next ad button")]
		[SerializeField]
		private GameObject m_nextAdButton;

		[Header("Pips")]
		[Tooltip("The Horizontal Layout Group of the Pips.")]
		[SerializeField]
		private RectTransform m_pipHorizontalGroup;

		[SerializeField]
		[Tooltip("The empty pip sprite.")]
		private Sprite m_emptyPip;

		[SerializeField]
		[Tooltip("The full pip sprite.")]
		private Sprite m_fullPip;

		[Tooltip("Should the height of the PipHorizontalGroup control the size of the pips?")]
		[SerializeField]
		private bool m_setSizeOfPipFromHorizontalGroupHeight;

		private ICrossPromo m_crossPromo;

		private CrossPromoAd[] m_ads;

		private Image[] m_pips;

		private int m_currentAd;

		private float m_startPosition;

		private float m_endPosition;

		private float m_startDragPosition;

		private Coroutine m_positionAnimator;

		private int m_pageNumber;

		private float m_pipSpacing;

		// 060000d5 (1.10.1 RVA 0x1A16680)
		private int PreviousAd => m_currentAd == 0 ? m_ads.Length - 1 : m_currentAd - 1;

		// 060000d6 (1.10.1 RVA 0x1A166A0)
		private int NextAd => m_currentAd == m_ads.Length - 1 ? 0 : m_currentAd + 1;

		// 060000d7 (1.10.1 RVA 0x1A166D0)
		private void Awake()
		{
			m_scrolledContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, GetComponent<RectTransform>().rect.width * PageCount);
			m_pipSpacing = m_pipHorizontalGroup.GetComponent<HorizontalLayoutGroup>().spacing;
		}

		// 060000d8 (1.10.1 RVA 0x1A16760)
		private void OnDisable()
		{
			ReleaseImageTexture(m_pageBasePrev);
			ReleaseImageTexture(m_pageBaseCurrent);
			ReleaseImageTexture(m_pageBaseNext);
			if (m_crossPromo != null)
			{
				m_crossPromo.Initialised -= CrossPromoOnInitialised;
			}
		}

		// 060000d9 (1.10.1 RVA 0x1A16880)
		private void ReleaseImageTexture(CrossPromoPageBase page)
		{
			Sprite sprite = page.GetSprite();
			page.SetSprite(null);
			page.SetReadyState(false);
			if (m_crossPromo != null)
			{
				m_crossPromo.ReleaseFullScreenTexture(sprite);
			}
		}

		// 060000da (1.10.1 RVA 0x1A16160)
		public void Show(ICrossPromo crossPromo, CrossPromoMetaData metaData, int indexFirstAdToShow)
		{
			m_crossPromo = crossPromo;
			m_ads = metaData.Ads;
			m_currentAd = indexFirstAdToShow;
			if (m_currentAd < 0)
			{
				m_currentAd = metaData.GetFirstAdIndex();
			}
			ManageAdScrollingUiAndFunctionality(m_ads.Length);
			PrepareButtonText(metaData.PressToPlayString);
			SetupAdPips();
			// A ready CrossPromo refreshes through the Initialised add below, which calls back at
			// once with the resolved m_currentAd. A promo still syncing refreshes here with the
			// caller's raw index instead, so a negative index reaches m_pips[-1] in RefreshContent
			// (unchecked in the original build, where ArrayBoundsChecks is off).
			if (!m_crossPromo.IsReady())
			{
				RefreshContent(indexFirstAdToShow);
			}
			m_crossPromo.Initialised += CrossPromoOnInitialised;
			gameObject.SetActive(true);
		}

		// 060000db (1.10.1 RVA 0x1A171E0)
		private void CrossPromoOnInitialised(CrossPromoMetaData metaData)
		{
			RefreshContent(m_currentAd);
		}

		// 060000dc (1.10.1 RVA 0x1A16980)
		private void ManageAdScrollingUiAndFunctionality(int numOfAds)
		{
			bool canScroll = numOfAds >= 2;
			m_previousAdButton.SetActive(canScroll);
			m_nextAdButton.SetActive(canScroll);
			m_pipHorizontalGroup.gameObject.SetActive(canScroll);
			m_contentScroller.horizontal = canScroll;
		}

		// 060000dd (1.10.1 RVA 0x1A169F0)
		private void PrepareButtonText(string localisedText)
		{
			if (string.IsNullOrEmpty(localisedText))
			{
				localisedText = DefaultButtonLocalisation;
			}
			m_pageBasePrev.SetText(localisedText);
			m_pageBaseCurrent.SetText(localisedText);
			m_pageBaseNext.SetText(localisedText);
		}

		// 060000de (1.10.1 RVA 0x1A16A80)
		private void SetupAdPips()
		{
			int numOfAds = m_ads.Length;
			m_pips = new Image[numOfAds];
			foreach (Transform child in m_pipHorizontalGroup.transform)
			{
				Object.Destroy(child.gameObject);
			}
			for (int i = 0; i < numOfAds; i++)
			{
				GameObject pip = new GameObject(string.Format("Ad Pip {0}", i));
				pip.transform.SetParent(m_pipHorizontalGroup.transform);
				m_pips[i] = pip.AddComponent<Image>();
				pip.GetComponent<RectTransform>().localScale = Vector3.one;
				m_pips[i].sprite = m_emptyPip;
				if (m_setSizeOfPipFromHorizontalGroupHeight)
				{
					float pipSize = m_pipHorizontalGroup.rect.height;
					((RectTransform)m_pips[i].transform).sizeDelta = new Vector2(pipSize, pipSize);
				}
			}
			m_pipHorizontalGroup.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, numOfAds * m_emptyPip.rect.width + (numOfAds - 1) * m_pipSpacing);
		}

		// 060000df (1.10.1 RVA 0x1A17010)
		private void RefreshContent(int indexOfAdToShow)
		{
			m_pips[m_currentAd].sprite = m_emptyPip;
			m_currentAd = indexOfAdToShow;
			m_pips[m_currentAd].sprite = m_fullPip;
			RequestFullScreenTexture(m_pageBasePrev, PreviousAd);
			RequestFullScreenTexture(m_pageBaseCurrent, m_currentAd);
			RequestFullScreenTexture(m_pageBaseNext, NextAd);
			// Re-centre on page 2, which now shows the current ad.
			m_contentScroller.horizontalNormalizedPosition = 0.5f;
			m_startPosition = 0.5f;
			m_endPosition = 0.5f;
			m_pageNumber = 2;
			m_crossPromo.RaiseFullScreenImpression(m_ads[m_currentAd]);
		}

		// 060000e0 (1.10.1 RVA 0x1A171F0)
		private void RequestFullScreenTexture(CrossPromoPageBase page, int adIndex)
		{
			Sprite previousSprite = page.GetSprite();
			Sprite sprite = m_crossPromo.RequestFullScreenTexture(m_ads[adIndex].FullScreenTexture);
			if (sprite == null)
			{
				return;
			}
			page.SetSprite(sprite);
			page.SetReadyState(sprite != null);
			m_crossPromo.ReleaseFullScreenTexture(previousSprite);
		}

		// 060000e1 (1.10.1 RVA 0x1A17400)
		[HideInInspector]
		public void OnBeginDrag(PointerEventData data)
		{
			m_startDragPosition = m_contentScroller.horizontalNormalizedPosition;
		}

		// 060000e2 (1.10.1 RVA 0x1A17430)
		[HideInInspector]
		public void OnEndDrag(PointerEventData data)
		{
			int dockedPage = DetermineDockedPageNumber();
			if (dockedPage != 0)
			{
				m_pageNumber = dockedPage;
				return;
			}
			float position = m_contentScroller.horizontalNormalizedPosition;
			float direction = Mathf.Sign(position - m_startDragPosition);
			if (Mathf.Abs(m_startDragPosition - position) >= SnapDistance)
			{
				MoveToPage(m_pageNumber + (int)direction);
				return;
			}
			// A short drag settles on the page whose window, shifted towards the drag
			// direction, contains the release position; with no match the previous end
			// position is kept.
			m_startPosition = position;
			for (int i = 0; i < PageCount; i++)
			{
				float pagePosition = i / Paginator;
				if ((direction > 0f && position >= pagePosition - Spacing + SnapDistance && position <= pagePosition + SnapDistance)
					|| (direction < 0f && position >= pagePosition - SnapDistance && position <= pagePosition + Spacing - SnapDistance))
				{
					m_endPosition = pagePosition;
					break;
				}
			}
			if (Mathf.Abs(position - m_endPosition) >= Accuracy)
			{
				StartScrollAnimation();
			}
		}

		// 060000e3 (1.10.1 RVA 0x1A178C0); <MoveScrollRect>d__45.MoveNext 060000ef (RVA 0x1A17BF0).
		// The first frame is drawn before the elapsed time is first compared.
		private IEnumerator MoveScrollRect()
		{
			float progress = 0f;
			float durationDelta = Mathf.Abs(m_startPosition - m_endPosition) * TransitionDuration;
			do
			{
				float easedProgress = ApplyEasingStyle(progress / durationDelta);
				m_contentScroller.horizontalNormalizedPosition = Mathf.Lerp(m_startPosition, m_endPosition, easedProgress);
				yield return null;
				progress += Time.deltaTime;
			}
			while (progress <= durationDelta);
			m_contentScroller.horizontalNormalizedPosition = m_endPosition;
			m_pageNumber = DetermineDockedPageNumber();
			if (m_pageNumber == 1)
			{
				RefreshContent(PreviousAd);
			}
			else if (m_pageNumber == 3)
			{
				RefreshContent(NextAd);
			}
		}

		// 060000e4 (1.10.1 RVA 0x1A17950): cubic ease-in-out.
		private static float ApplyEasingStyle(float t)
		{
			t *= 2f;
			if (t < 1f)
			{
				return 0.5f * t * t * t;
			}
			t -= 2f;
			return 0.5f * (t * t * t + 2f);
		}

		// 060000e5 (1.10.1 RVA 0x1A17710)
		private int DetermineDockedPageNumber()
		{
			float position = m_contentScroller.horizontalNormalizedPosition;
			for (int i = 0; i < PageCount; i++)
			{
				float pagePosition = i / Paginator;
				// The native test skips a page only when the distance is at least Accuracy,
				// so a NaN position docks on page 1.
				if (Mathf.Abs(position - pagePosition) >= Accuracy)
				{
					continue;
				}
				m_contentScroller.horizontalNormalizedPosition = pagePosition;
				return i + 1;
			}
			return 0;
		}

		// 060000e6 (1.10.1 RVA 0x1A179B0)
		private void StartScrollAnimation(float targetPosition)
		{
			m_endPosition = targetPosition;
			m_startPosition = m_contentScroller.horizontalNormalizedPosition;
			StartScrollAnimation();
		}

		// 060000e7 (1.10.1 RVA 0x1A17810)
		private void StartScrollAnimation()
		{
			if (m_positionAnimator != null)
			{
				StopCoroutine(m_positionAnimator);
			}
			m_positionAnimator = StartCoroutine(MoveScrollRect());
		}

		// 060000e8 (1.10.1 RVA 0x1A177B0)
		private void MoveToPage(int pageNumber)
		{
			if (pageNumber < 1 || pageNumber > PageCount || m_pageNumber == pageNumber)
			{
				return;
			}
			StartScrollAnimation((pageNumber - 1) / Paginator);
		}

		// 060000e9 (1.10.1 RVA 0x1A179F0)
		public void Action_GoToPreviousAd()
		{
			StartScrollAnimation(0f);
		}

		// 060000ea (1.10.1 RVA 0x1A17A30)
		public void Action_GoToNextAd()
		{
			StartScrollAnimation(1f);
		}

		// 060000eb (1.10.1 RVA 0x1A17A70)
		public void Action_OpenAd()
		{
			CrossPromoAd ad = m_ads[m_currentAd];
			Application.OpenURL(ad.StoreURL);
			m_crossPromo.RaiseFullScreenButtonClicked(ad);
		}
	}
}
