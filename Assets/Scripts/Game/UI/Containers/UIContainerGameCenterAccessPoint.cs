using System.Collections;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 020008bb..020008bd (iterators); bodies 0x6317F0..0x6323C0 (1.10.1 x86-64).
    // Positions the content over the Game Center access point and shows the access point half a second
    // after the first update that sees it available but not yet shown, fading the glyph in afterwards.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerGameCenterAccessPoint : UIContainer
    {
        [SerializeField]
        private GameCenterAccessPoint.AccessPointLocation m_location;
        [SerializeField]
        private bool m_showHighlights = true;
        [SerializeField]
        private RectTransform m_groupContent;
        [SerializeField]
        private CanvasGroup m_canvasGroup;
        [SerializeField]
        private CanvasScaler m_canvasScaler;
        public readonly Bindable<bool> Visible = new Bindable<bool>();
        public readonly Bindable<bool> Available = new Bindable<bool>();
        public readonly Bindable<bool> ShouldDisplayGlyph = new Bindable<bool>();
        private const float DelayAppearanceForSeconds = 0.5f;
        private const float GlyphFadeInTime = 0.25f;
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        private readonly SystemRef<GameCenterAccessPointManager> m_gameCenterAccessPointRef = ProcessManager.GetSystemRef<GameCenterAccessPointManager>(null, true);
        private readonly SystemRef<GameCenter> m_gameCenterRef = ProcessManager.GetSystemRef<GameCenter>(null, true);
        private bool m_delayedDisplayTriggered;
        private Coroutine m_delayedDisplayRoutine;
        private readonly WaitForSeconds m_delayCallToGCAP = new WaitForSeconds(DelayAppearanceForSeconds);
        private readonly WaitForEndOfFrame m_fadeInGlyph = new WaitForEndOfFrame();
        private bool m_cachedVisible;
        private bool m_cachedAvailable;

        // 0600325b: the manager is marked active; the canvas scaler is only used on macOS and the
        // glyph is displayed on iOS and tvOS.
        private void Start()
        {
            Visible.Value = false;
            m_cachedVisible = false;
            m_gameCenterAccessPointRef.Get().IsActive = true;
            m_canvasScaler.enabled = Application.platform == RuntimePlatform.OSXPlayer;
            ShouldDisplayGlyph.Value = Application.platform == RuntimePlatform.IPhonePlayer
                || Application.platform == RuntimePlatform.tvOS;
        }

        // 0600325c: once available, the first update with the access point hidden starts the delayed
        // show. While it is shown the content follows the access point's screen rectangle, keeping the
        // current anchored position or size for a zero (or, for size, non-positive) coordinate.
        private void Update()
        {
            if (m_gameCenterAccessPointRef.IsNull())
                return;
            GameCenterAccessPointManager manager = m_gameCenterAccessPointRef.GetSafe();
            ValidateAvailability(manager);
            if (!Available.Value)
                return;
            if (!manager.IsAccessPointShown() && !m_delayedDisplayTriggered)
            {
                m_delayedDisplayTriggered = true;
                this.SafeStopCoroutine(ref m_delayedDisplayRoutine);
                m_delayedDisplayRoutine = StartCoroutine(DelayAppearance(manager));
                return;
            }
            if (!manager.IsAccessPointShown() && m_delayedDisplayTriggered)
                return;
            float originX = manager.GetOriginScreenCoordinateX();
            float originY = manager.GetOriginScreenCoordinateY();
            m_groupContent.anchoredPosition = new Vector2(
                originX != 0f ? originX : m_groupContent.anchoredPosition.x,
                originY != 0f ? -originY : m_groupContent.anchoredPosition.y);
            float width = manager.GetScreenCoordinateWidth();
            float height = manager.GetScreenCoordinateHeight();
            m_groupContent.sizeDelta = new Vector2(
                width > 0f ? width : m_groupContent.sizeDelta.x,
                height > 0f ? height : m_groupContent.sizeDelta.y);
        }

        // 0600325d / MoveNext 06003265: show the access point after the delay, then fade the glyph in.
        private IEnumerator DelayAppearance(GameCenterAccessPointManager gameCenterAccessPointManager)
        {
            yield return m_delayCallToGCAP;
            gameCenterAccessPointManager.ShowAccessPoint(m_location, m_showHighlights);
            Visible.Value = true;
            // The original clears the cache right after showing, so a later logout never hides the container.
            m_cachedVisible = false;
            yield return ShowGlyph();
        }

        // 0600325e / MoveNext 0600326b
        private IEnumerator ShowGlyph()
        {
            float timer = 0f;
            while (timer < GlyphFadeInTime)
            {
                m_canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / GlyphFadeInTime);
                timer += Time.deltaTime;
                yield return m_fadeInGlyph;
            }
        }

        // 0600325f
        private void OnDestroy()
        {
            this.SafeStopCoroutine(ref m_delayedDisplayRoutine);
            if (m_gameCenterAccessPointRef.TryGet(out GameCenterAccessPointManager manager))
            {
                manager.HideAccessPoint();
                manager.IsActive = false;
            }
        }

        // 06003260: gameplay control is released before the Game Center overlay opens.
        public void Action_ShowGameCenter()
        {
            if (m_gameCenterAccessPointRef.IsNull())
                return;
            m_appRef.Get().SetHasGameplayControl(false);
            m_gameCenterAccessPointRef.Get().ShowGameCenter();
        }

        // 06003261: m_cachedVisible is only ever assigned false (Start, DelayAppearance and here), so
        // the Visible = false branch is dead in the original. Once the access point has been reported
        // available the Available bindable is never cleared.
        private void ValidateAvailability(GameCenterAccessPointManager gameCenterAccessPointManager)
        {
            if (!m_gameCenterRef.IsValid() || !m_gameCenterRef.Get().IsLoggedIn())
            {
                if (m_cachedVisible)
                {
                    Visible.Value = false;
                    m_cachedVisible = false;
                }
            }
            bool available = gameCenterAccessPointManager.IsAccessPointAvailable();
            if (!Available.Value && m_cachedAvailable != available)
            {
                Available.Value = available;
                m_cachedAvailable = available;
            }
        }

        // 06003262
        public UIContainerGameCenterAccessPoint() { }
    }
}
