using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Tests.CrossPromoUIShared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.CrossPromoUiAccess;

namespace ProjectLucid.Tests
{
    // Hardlight.CrossPromoFullScreenView (TypeDef 0x0200001d, x86-64 0x1A16160..0x1A17BD0) and its
    // <MoveScrollRect>d__45 iterator. Every case runs on an inactive owned hierarchy: a 100-wide
    // view, a 100-wide ScrollRect over a 300-wide content (normalized position p puts the content
    // at x = 100 - 200p, so 0, 0.5 and 1 are exact), three recording pages, the navigation
    // buttons and a pip group. The ICrossPromo is an in-memory recorder sharing the pages' log.
    // An inactive owner cannot start a coroutine, so paths that reach StartScrollAnimation are
    // checked up to the refused StartCoroutine and the iterator is stepped by hand instead.
    // Not covered: Action_OpenAd, whose
    // Application.OpenURL would leave the test process, and a second Show over existing pips,
    // whose Object.Destroy teardown (old and new pips coexist until the deferred destroy) is
    // refused in EditMode and is left to PlayMode.
    public sealed class OriginalCrossPromoFullScreenViewTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class Fixture : IDisposable
        {
            public readonly CrossPromoUiObjects Objects = new CrossPromoUiObjects("lucid-crosspromo-full-screen-view");
            public readonly List<string> Log = new List<string>();
            public readonly CrossPromoFullScreenView View;
            public readonly ScrollRect Scroller;
            public readonly RectTransform Content;
            public readonly RecordingCrossPromoPage Prev, Current, Next;
            public readonly GameObject PreviousButton, NextButton;
            public readonly RectTransform PipGroup;
            public readonly Sprite EmptyPip, FullPip;
            public readonly RecordingCrossPromo Promo;

            public Fixture()
            {
                GameObject view = Objects.Child("view");
                ((RectTransform)view.transform).sizeDelta = new Vector2(100f, 100f);
                View = view.AddComponent<CrossPromoFullScreenView>();

                GameObject scroller = Objects.Child("scroller", view.transform);
                ((RectTransform)scroller.transform).sizeDelta = new Vector2(100f, 100f);
                Scroller = scroller.AddComponent<ScrollRect>();
                Content = (RectTransform)Objects.Child("content", scroller.transform).transform;
                Content.sizeDelta = new Vector2(300f, 100f);
                Scroller.content = Content;
                Scroller.horizontal = true;
                Scroller.vertical = false;

                Prev = Objects.Child("prev", view.transform, typeof(RecordingCrossPromoPage)).GetComponent<RecordingCrossPromoPage>();
                Current = Objects.Child("current", view.transform, typeof(RecordingCrossPromoPage)).GetComponent<RecordingCrossPromoPage>();
                Next = Objects.Child("next", view.transform, typeof(RecordingCrossPromoPage)).GetComponent<RecordingCrossPromoPage>();
                Prev.Log = Current.Log = Next.Log = Log;

                PreviousButton = Objects.Child("previous-ad", view.transform);
                NextButton = Objects.Child("next-ad", view.transform);
                GameObject pips = Objects.Child("pips", view.transform, typeof(HorizontalLayoutGroup));
                pips.GetComponent<HorizontalLayoutGroup>().spacing = 4f;
                PipGroup = (RectTransform)pips.transform;
                PipGroup.sizeDelta = new Vector2(50f, 8f);
                EmptyPip = Objects.Sprite("empty-pip");
                FullPip = Objects.Sprite("full-pip", 12f);
                Promo = new RecordingCrossPromo(Log);

                Set(View, "m_contentScroller", Scroller);
                Set(View, "m_scrolledContent", Content);
                Set(View, "m_pageBasePrev", Prev);
                Set(View, "m_pageBaseCurrent", Current);
                Set(View, "m_pageBaseNext", Next);
                Set(View, "m_previousAdButton", PreviousButton);
                Set(View, "m_nextAdButton", NextButton);
                Set(View, "m_pipHorizontalGroup", PipGroup);
                Set(View, "m_emptyPip", EmptyPip);
                Set(View, "m_fullPip", FullPip);
            }

            public object Invoke(string method, params object[] args) => Call(typeof(CrossPromoFullScreenView), View, method, args);

            public float Position
            {
                get => Scroller.horizontalNormalizedPosition;
                set => Scroller.horizontalNormalizedPosition = value;
            }

            public float Float(string field) => (float)Get(View, field);

            public int Int(string field) => (int)Get(View, field);

            public void Dispose() => Objects.Dispose();
        }

        private static CrossPromoMetaData Meta(CrossPromoAd[] ads, string firstAd = null, string pressToPlay = null) =>
            new CrossPromoMetaData { IsSystemEnabled = true, Ads = ads, FirstAd = firstAd, PressToPlayString = pressToPlay };

        // dump.cs TypeDefIndex 13733/13732 and the DummyDll rows: the literal constants, the 21
        // instance fields in order, and <MoveScrollRect>d__45 (8 constants, 21 fields and the
        // two private properties with their getters precede the 12 methods before it).
        [Test]
        public void DeclarationsKeepOriginalConstantsFieldsAndIterator()
        {
            Type type = typeof(CrossPromoFullScreenView);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(type.GetInterfaces(), Is.EquivalentTo(new[] { typeof(IEndDragHandler), typeof(IEventSystemHandler), typeof(IBeginDragHandler) }));
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            string[] constantNames =
            {
                "PageCount", "SnapThreshold", "Accuracy", "TransitionDuration", "Paginator", "Spacing", "SnapDistance", "DefaultButtonLocalisation"
            };
            object[] constantValues = { 3, 0.3333f, 9.999999747378752e-05f, 0.25f, 2f, 0.5f, 0.16664999723434448f, "PLAY FOR FREE" };
            FieldInfo[] fields = type.GetFields(all);
            FieldInfo[] constants = fields.Where(f => f.IsLiteral).ToArray();
            Assert.That(constants.Select(f => f.Name), Is.EqualTo(constantNames));
            for (int i = 0; i < constants.Length; i++)
            {
                Assert.That(constants[i].Attributes, Is.EqualTo(FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault));
                Assert.That(constants[i].GetRawConstantValue(), Is.EqualTo(constantValues[i]), constantNames[i]);
                Assert.That(constants[i].GetRawConstantValue().GetType(), Is.EqualTo(constantValues[i].GetType()), constantNames[i]);
            }
            Assert.That(fields.Where(f => !f.IsLiteral).Select(f => f.Name + ":" + f.FieldType.Name), Is.EqualTo(new[]
            {
                "m_contentScroller:ScrollRect", "m_scrolledContent:RectTransform", "m_pageBasePrev:CrossPromoPageBase",
                "m_pageBaseCurrent:CrossPromoPageBase", "m_pageBaseNext:CrossPromoPageBase", "m_previousAdButton:GameObject",
                "m_nextAdButton:GameObject", "m_pipHorizontalGroup:RectTransform", "m_emptyPip:Sprite", "m_fullPip:Sprite",
                "m_setSizeOfPipFromHorizontalGroupHeight:Boolean", "m_crossPromo:ICrossPromo", "m_ads:CrossPromoAd[]",
                "m_pips:Image[]", "m_currentAd:Int32", "m_startPosition:Single", "m_endPosition:Single",
                "m_startDragPosition:Single", "m_positionAnimator:Coroutine", "m_pageNumber:Int32", "m_pipSpacing:Single"
            }));
            Assert.That(fields.Where(f => !f.IsLiteral).All(f => f.Attributes == FieldAttributes.Private));
            Assert.That(fields.Count(f => f.IsDefined(typeof(SerializeField))), Is.EqualTo(11));
            // Per-field attribute rows of 0x04000085..0x0400008f, in DummyDll metadata order.
            var attributeRows = new Dictionary<string, string[]>
            {
                { "m_contentScroller", new[] { "SerializeField()" } },
                { "m_scrolledContent", new[] { "TooltipAttribute(The rect transform for the Scrolled Content)", "SerializeField()" } },
                { "m_pageBasePrev", new[] { "TooltipAttribute(The left page of the three)", "SerializeField()", "HeaderAttribute(Pages)" } },
                { "m_pageBaseCurrent", new[] { "TooltipAttribute(The center page of the three)", "SerializeField()" } },
                { "m_pageBaseNext", new[] { "TooltipAttribute(The right page of the three)", "SerializeField()" } },
                { "m_previousAdButton", new[] { "SerializeField()", "TooltipAttribute(The previous ad button)", "HeaderAttribute(Navigation)" } },
                { "m_nextAdButton", new[] { "TooltipAttribute(The next ad button)", "SerializeField()" } },
                { "m_pipHorizontalGroup", new[] { "HeaderAttribute(Pips)", "TooltipAttribute(The Horizontal Layout Group of the Pips.)", "SerializeField()" } },
                { "m_emptyPip", new[] { "SerializeField()", "TooltipAttribute(The empty pip sprite.)" } },
                { "m_fullPip", new[] { "SerializeField()", "TooltipAttribute(The full pip sprite.)" } },
                { "m_setSizeOfPipFromHorizontalGroupHeight", new[] { "TooltipAttribute(Should the height of the PipHorizontalGroup control the size of the pips?)", "SerializeField()" } }
            };
            foreach (FieldInfo field in fields.Where(f => !f.IsLiteral))
                Assert.That(AttributeRows(field), Is.EqualTo(attributeRows.TryGetValue(field.Name, out string[] rows) ? rows : new string[0]), field.Name);

            foreach (string handler in new[] { "OnBeginDrag", "OnEndDrag" })
            {
                MethodInfo method = type.GetMethod(handler, all);
                Assert.That((int)method.Attributes, Is.EqualTo(0x01e6), handler);
                Assert.That(method.GetCustomAttributesData().Select(a => a.AttributeType), Is.EqualTo(new[] { typeof(HideInInspector) }), handler);
            }
            Assert.That((int)type.GetMethod("ApplyEasingStyle", all).Attributes, Is.EqualTo(0x0091));

            MethodInfo iterator = type.GetMethod("MoveScrollRect", all);
            Type machine = (Type)iterator.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            Assert.That(machine.Name, Is.EqualTo("<MoveScrollRect>d__45"));
            Assert.That((int)machine.Attributes, Is.EqualTo(0x00100103));
            Assert.That(machine.GetFields(all).Select(f => f.Name + ":" + f.FieldType.Name), Is.EqualTo(new[]
            {
                "<>1__state:Int32", "<>2__current:Object", "<>4__this:CrossPromoFullScreenView", "<progress>5__2:Single", "<durationDelta>5__3:Single"
            }));
        }

        // get_PreviousAd 0x1A16680 (ad 0 wraps to Length - 1) and get_NextAd 0x1A166A0 (the last ad
        // wraps to 0); RefreshContent 0x1A17010 inlines both.
        [Test]
        public void PreviousAndNextAdWrapAroundTheAdList()
        {
            using (var fixture = new Fixture())
            {
                Set(fixture.View, "m_ads", Ads("a", "b", "c"));
                PropertyInfo previous = typeof(CrossPromoFullScreenView).GetProperty("PreviousAd", BindingFlags.Instance | BindingFlags.NonPublic);
                PropertyInfo next = typeof(CrossPromoFullScreenView).GetProperty("NextAd", BindingFlags.Instance | BindingFlags.NonPublic);
                int[] expectedPrevious = { 2, 0, 1 };
                int[] expectedNext = { 1, 2, 0 };
                for (int current = 0; current < 3; current++)
                {
                    Set(fixture.View, "m_currentAd", current);
                    Assert.That(previous.GetValue(fixture.View), Is.EqualTo(expectedPrevious[current]), "previous of " + current);
                    Assert.That(next.GetValue(fixture.View), Is.EqualTo(expectedNext[current]), "next of " + current);
                }
                Set(fixture.View, "m_ads", Ads("only"));
                Set(fixture.View, "m_currentAd", 0);
                Assert.That(previous.GetValue(fixture.View), Is.EqualTo(0));
                Assert.That(next.GetValue(fixture.View), Is.EqualTo(0));
            }
        }

        // ApplyEasingStyle 0x1A17950: t doubled; below 1 it is 0.5*t^3, otherwise
        // 0.5*((t-2)^3 + 2). 0.4 maps to 0.256 (smoothstep would give 0.352) and 0.75 to 0.9375.
        [Test]
        public void EasingIsCubicInOutOverTheDoubledProgress()
        {
            float[] inputs = { 0f, 0.25f, 0.4f, 0.5f, 0.75f, 1f };
            float[] expected = { 0f, 0.0625f, 0.256f, 0.5f, 0.9375f, 1f };
            for (int i = 0; i < inputs.Length; i++)
                Assert.That((float)Call(typeof(CrossPromoFullScreenView), null, "ApplyEasingStyle", inputs[i]),
                    Is.EqualTo(expected[i]).Within(1e-6f), "t = " + inputs[i]);
            Assert.That((float)Call(typeof(CrossPromoFullScreenView), null, "ApplyEasingStyle", float.NaN), Is.NaN);
        }

        // PrepareButtonText 0x1A169F0: null or empty text becomes "PLAY FOR FREE"; the text is set
        // on the previous, current and next page in that order.
        [Test]
        public void ButtonTextFallsBackToPlayForFreeOnEveryPage()
        {
            using (var fixture = new Fixture())
            {
                foreach (string text in new[] { null, "" })
                {
                    fixture.Log.Clear();
                    fixture.Invoke("PrepareButtonText", text);
                    Assert.That(fixture.Log, Is.EqualTo(new[] { "prev.SetText(PLAY FOR FREE)", "current.SetText(PLAY FOR FREE)", "next.SetText(PLAY FOR FREE)" }));
                }
                fixture.Log.Clear();
                fixture.Invoke("PrepareButtonText", "JOUER");
                Assert.That(fixture.Log, Is.EqualTo(new[] { "prev.SetText(JOUER)", "current.SetText(JOUER)", "next.SetText(JOUER)" }));
            }
        }

        // ManageAdScrollingUiAndFunctionality 0x1A16980: the buttons, the pip group and horizontal
        // scrolling all need at least two ads.
        [Test]
        public void ScrollingUiNeedsAtLeastTwoAds()
        {
            using (var fixture = new Fixture())
            {
                foreach (int count in new[] { 0, 1, 2, 5 })
                {
                    bool expected = count >= 2;
                    fixture.PreviousButton.SetActive(!expected);
                    fixture.NextButton.SetActive(!expected);
                    fixture.PipGroup.gameObject.SetActive(!expected);
                    fixture.Scroller.horizontal = !expected;
                    fixture.Invoke("ManageAdScrollingUiAndFunctionality", count);
                    Assert.That(fixture.PreviousButton.activeSelf, Is.EqualTo(expected), count + " ads");
                    Assert.That(fixture.NextButton.activeSelf, Is.EqualTo(expected), count + " ads");
                    Assert.That(fixture.PipGroup.gameObject.activeSelf, Is.EqualTo(expected), count + " ads");
                    Assert.That(fixture.Scroller.horizontal, Is.EqualTo(expected), count + " ads");
                }
            }
        }

        // Awake 0x1A166D0 sizes the content to three view widths (the width lane of get_rect,
        // xmm1, times 3 at 0x1A1671D) and caches the pip group's spacing; the view is 100x80 so a
        // height-based size (240) is told apart. Show 0x1A16160 with a promo that is not ready: the ad list, ManageAdScrolling,
        // the button text, SetupAdPips 0x1A16A80 (pips named "Ad Pip {i}", empty sprite, square
        // from the group height, group width n*emptyPipWidth + (n-1)*spacing, m_emptyPip read at
        // 0x1A16F4B: the 12-wide full pip would give 44), then RefreshContent
        // 0x1A17010 with the caller's index: pips, the three page requests of
        // RequestFullScreenTexture 0x1A171F0 (a null sprite leaves the page untouched), the
        // centred position and page 2, the impression; finally Initialised and activation.
        [Test]
        public void ShowWithAPromoNotReadyBuildsPipsAndRefreshesTheRequestedAd()
        {
            using (var fixture = new Fixture())
            {
                ((RectTransform)fixture.View.transform).sizeDelta = new Vector2(100f, 80f);
                fixture.Content.sizeDelta = new Vector2(200f, 100f);
                fixture.Invoke("Awake");
                Assert.That(fixture.Content.rect.width, Is.EqualTo(300f));
                Assert.That(fixture.Float("m_pipSpacing"), Is.EqualTo(4f));

                Set(fixture.View, "m_setSizeOfPipFromHorizontalGroupHeight", true);
                var sprites = new Dictionary<string, Sprite> { { "full-a", fixture.Objects.Sprite("page-a") }, { "full-c", fixture.Objects.Sprite("page-c") } };
                fixture.Promo.FullScreenSprites = texture => sprites.TryGetValue(texture.URL, out Sprite sprite) ? sprite : null;
                fixture.View.gameObject.SetActive(false);
                fixture.PreviousButton.SetActive(false);
                CrossPromoAd[] ads = Ads("a", "b", "c");

                fixture.View.Show(fixture.Promo, Meta(ads, pressToPlay: "PLAY"), 1);

                Assert.That(fixture.Log, Is.EqualTo(new[]
                {
                    "prev.SetText(PLAY)", "current.SetText(PLAY)", "next.SetText(PLAY)", "IsReady",
                    "prev.GetSprite", "RequestFullScreenTexture(full-a)", "prev.SetSprite(page-a)", "prev.SetReadyState(True)", "ReleaseFullScreenTexture(null)",
                    "current.GetSprite", "RequestFullScreenTexture(full-b)",
                    "next.GetSprite", "RequestFullScreenTexture(full-c)", "next.SetSprite(page-c)", "next.SetReadyState(True)", "ReleaseFullScreenTexture(null)",
                    "RaiseFullScreenImpression(b)", "add_Initialised"
                }));
                Assert.That(Get(fixture.View, "m_crossPromo"), Is.SameAs(fixture.Promo));
                Assert.That(Get(fixture.View, "m_ads"), Is.SameAs(ads));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(1));
                Assert.That(fixture.PreviousButton.activeSelf && fixture.NextButton.activeSelf && fixture.Scroller.horizontal, Is.True);

                var pips = (Image[])Get(fixture.View, "m_pips");
                Assert.That(pips.Length, Is.EqualTo(3));
                Assert.That(fixture.PipGroup.childCount, Is.EqualTo(3));
                for (int i = 0; i < 3; i++)
                {
                    Transform pip = fixture.PipGroup.GetChild(i);
                    Assert.That(pip.name, Is.EqualTo("Ad Pip " + i));
                    Assert.That(pip.GetComponent<Image>(), Is.SameAs(pips[i]));
                    Assert.That(pip.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(((RectTransform)pip).sizeDelta, Is.EqualTo(new Vector2(8f, 8f)));
                    Assert.That(pips[i].sprite, Is.SameAs(i == 1 ? fixture.FullPip : fixture.EmptyPip), "pip " + i);
                }
                Assert.That(fixture.PipGroup.rect.width, Is.EqualTo(3 * 10f + 2 * 4f));

                Assert.That(fixture.Position, Is.EqualTo(0.5f));
                Assert.That(fixture.Float("m_startPosition"), Is.EqualTo(0.5f));
                Assert.That(fixture.Float("m_endPosition"), Is.EqualTo(0.5f));
                Assert.That(fixture.Int("m_pageNumber"), Is.EqualTo(2));
                Assert.That(fixture.Promo.InitialisedHandlers.GetInvocationList().Single().Method.Name, Is.EqualTo("CrossPromoOnInitialised"));
                Assert.That(fixture.View.gameObject.activeSelf, Is.True);
                Assert.That(fixture.View.gameObject.activeInHierarchy, Is.False, "the owned root stays inactive");
            }
        }

        // Show 0x1A16160 against the interface: a negative index resolves through
        // GetFirstAdIndex, IsReady true skips the direct RefreshContent (the jne at 0x1A16340)
        // and an empty button text falls back. The recorder keeps the handler even though it is
        // ready, which the real CrossPromo never does (see the next case); raising it here
        // checks CrossPromoOnInitialised 0x1A171E0 on its own: it refreshes the current ad (with
        // two ads the next ad wraps back to 0). OnDisable 0x1A16760 then releases each page's
        // sprite (ReleaseImageTexture 0x1A16880) before leaving Initialised.
        [Test]
        public void ShowSkipsTheDirectRefreshWhenReadyAndDisableReleasesEveryPage()
        {
            using (var fixture = new Fixture())
            {
                Sprite page = fixture.Objects.Sprite("page");
                fixture.Promo.Ready = true;
                fixture.Promo.FullScreenSprites = texture => page;
                CrossPromoAd[] ads = Ads("a", "b");

                fixture.View.Show(fixture.Promo, Meta(ads, firstAd: "b", pressToPlay: ""), -1);

                Assert.That(fixture.Log, Is.EqualTo(new[]
                {
                    "prev.SetText(PLAY FOR FREE)", "current.SetText(PLAY FOR FREE)", "next.SetText(PLAY FOR FREE)", "IsReady", "add_Initialised"
                }));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(1));
                var pips = (Image[])Get(fixture.View, "m_pips");
                Assert.That(pips.Select(p => p.sprite), Is.EqualTo(new[] { fixture.EmptyPip, fixture.EmptyPip }));
                Assert.That(((RectTransform)pips[0].transform).sizeDelta, Is.Not.EqualTo(new Vector2(8f, 8f)), "pips keep their size unless asked");

                fixture.Log.Clear();
                fixture.Promo.InitialisedHandlers(Meta(ads));
                Assert.That(fixture.Log, Is.EqualTo(new[]
                {
                    "prev.GetSprite", "RequestFullScreenTexture(full-a)", "prev.SetSprite(page)", "prev.SetReadyState(True)", "ReleaseFullScreenTexture(null)",
                    "current.GetSprite", "RequestFullScreenTexture(full-b)", "current.SetSprite(page)", "current.SetReadyState(True)", "ReleaseFullScreenTexture(null)",
                    "next.GetSprite", "RequestFullScreenTexture(full-a)", "next.SetSprite(page)", "next.SetReadyState(True)", "ReleaseFullScreenTexture(null)",
                    "RaiseFullScreenImpression(b)"
                }));
                Assert.That(pips.Select(p => p.sprite), Is.EqualTo(new[] { fixture.EmptyPip, fixture.FullPip }));

                fixture.Log.Clear();
                fixture.Invoke("OnDisable");
                Assert.That(fixture.Log, Is.EqualTo(new[]
                {
                    "prev.GetSprite", "prev.SetSprite(null)", "prev.SetReadyState(False)", "ReleaseFullScreenTexture(page)",
                    "current.GetSprite", "current.SetSprite(null)", "current.SetReadyState(False)", "ReleaseFullScreenTexture(page)",
                    "next.GetSprite", "next.SetSprite(null)", "next.SetReadyState(False)", "ReleaseFullScreenTexture(page)",
                    "remove_Initialised"
                }));
                Assert.That(fixture.Promo.InitialisedHandlers, Is.Null);
            }
        }

        // Show 0x1A16160 with a ready promo that, like CrossPromo's explicit Initialised add
        // 0x1A0E6B0 (no sync in progress and no pending downloads), calls the handler back during
        // the add instead of keeping it: the content is refreshed inside Show with the resolved
        // index (GetFirstAdIndex of "b" is 1), before the view is activated.
        [Test]
        public void ShowWithAReadyCrossPromoRefreshesThroughTheImmediateInitialisedCallback()
        {
            using (var fixture = new Fixture())
            {
                Sprite page = fixture.Objects.Sprite("page");
                CrossPromoAd[] ads = Ads("a", "b");
                fixture.Promo.Ready = true;
                fixture.Promo.CallBackOnAddWhenReady = true;
                fixture.Promo.MetaData = Meta(ads);
                fixture.View.gameObject.SetActive(false);
                bool activeDuringRefresh = true;
                fixture.Promo.FullScreenSprites = texture =>
                {
                    activeDuringRefresh &= fixture.View.gameObject.activeSelf;
                    return page;
                };

                fixture.View.Show(fixture.Promo, Meta(ads, firstAd: "b"), -1);

                Assert.That(fixture.Log, Is.EqualTo(new[]
                {
                    "prev.SetText(PLAY FOR FREE)", "current.SetText(PLAY FOR FREE)", "next.SetText(PLAY FOR FREE)", "IsReady", "add_Initialised",
                    "prev.GetSprite", "RequestFullScreenTexture(full-a)", "prev.SetSprite(page)", "prev.SetReadyState(True)", "ReleaseFullScreenTexture(null)",
                    "current.GetSprite", "RequestFullScreenTexture(full-b)", "current.SetSprite(page)", "current.SetReadyState(True)", "ReleaseFullScreenTexture(null)",
                    "next.GetSprite", "RequestFullScreenTexture(full-a)", "next.SetSprite(page)", "next.SetReadyState(True)", "ReleaseFullScreenTexture(null)",
                    "RaiseFullScreenImpression(b)"
                }));
                Assert.That(activeDuringRefresh, Is.False, "the refresh runs before SetActive(true)");
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(1));
                var pips = (Image[])Get(fixture.View, "m_pips");
                Assert.That(pips.Select(p => p.sprite), Is.EqualTo(new[] { fixture.EmptyPip, fixture.FullPip }));
                Assert.That(fixture.Promo.InitialisedHandlers, Is.Null, "a ready system keeps no handler");
                Assert.That(fixture.View.gameObject.activeSelf, Is.True);
            }
        }

        // Show 0x1A16160 with a promo still syncing and a negative index: m_currentAd resolves
        // through GetFirstAdIndex, but RefreshContent is called with the raw index (esi = r14d at
        // 0x1A16345). RefreshContent 0x1A17010 empties the resolved pip, stores -1 as m_currentAd
        // (0x1A17063) and then indexes m_pips[-1]. The original build has ArrayBoundsChecks off,
        // so that read is unchecked there; under Mono it throws before Initialised is joined or
        // the view is activated.
        [Test]
        public void ShowWhileSyncingPassesANegativeIndexStraightToTheRefresh()
        {
            using (var fixture = new Fixture())
            {
                fixture.View.gameObject.SetActive(false);
                CrossPromoAd[] ads = Ads("a", "b", "c");

                Assert.Throws<IndexOutOfRangeException>(() => fixture.View.Show(fixture.Promo, Meta(ads, firstAd: "b", pressToPlay: "PLAY"), -1));

                Assert.That(fixture.Log, Is.EqualTo(new[] { "prev.SetText(PLAY)", "current.SetText(PLAY)", "next.SetText(PLAY)", "IsReady" }));
                Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(-1));
                var pips = (Image[])Get(fixture.View, "m_pips");
                Assert.That(pips.Select(p => p.sprite), Is.EqualTo(new[] { fixture.EmptyPip, fixture.EmptyPip, fixture.EmptyPip }));
                Assert.That(fixture.Promo.InitialisedHandlers, Is.Null);
                Assert.That(fixture.View.gameObject.activeSelf, Is.False);
            }
        }

        // OnDisable 0x1A16760 and ReleaseImageTexture 0x1A16880 before any Show: the pages are
        // still cleared, and the missing promo is neither released to nor unsubscribed from.
        [Test]
        public void DisableBeforeShowClearsPagesWithoutAPromo()
        {
            using (var fixture = new Fixture())
            {
                fixture.Current.Sprite = fixture.Objects.Sprite("stale");
                fixture.Current.Ready = true;
                fixture.Invoke("OnDisable");
                Assert.That(fixture.Log, Is.EqualTo(new[]
                {
                    "prev.GetSprite", "prev.SetSprite(null)", "prev.SetReadyState(False)",
                    "current.GetSprite", "current.SetSprite(null)", "current.SetReadyState(False)",
                    "next.GetSprite", "next.SetSprite(null)", "next.SetReadyState(False)"
                }));
                Assert.That(fixture.Current.Sprite, Is.Null);
                Assert.That(fixture.Current.Ready, Is.False);
            }
        }

        // DetermineDockedPageNumber 0x1A17710: a position within Accuracy (1e-4) of 0, 0.5 or 1 is
        // snapped exactly onto it and docks page 1, 2 or 3; anything else returns 0 and is left.
        // The NaN polarity of the native skip ("ucomiss distance, Accuracy; jae": NaN docks page 1)
        // is not exercised, because the position is read from a real ScrollRect.
        [Test]
        public void DockedPageNumberSnapsOnlyWithinAccuracy()
        {
            using (var fixture = new Fixture())
            {
                var cases = new[]
                {
                    (set: 0.5f, page: 2, after: 0.5f), (set: 1f, page: 3, after: 1f), (set: 0f, page: 1, after: 0f),
                    (set: 0.25f, page: 0, after: 0.25f), (set: 0.00008f, page: 1, after: 0f), (set: 0.6f, page: 0, after: 0.6f),
                    (set: 0.99993f, page: 3, after: 1f), (set: 0.5f, page: 2, after: 0.5f), (set: 0.99985f, page: 0, after: 0.99985f)
                };
                foreach (var row in cases)
                {
                    fixture.Position = row.set;
                    float before = fixture.Position;
                    Assert.That(before, Is.EqualTo(row.set).Within(1e-6f), "fixture position " + row.set);
                    Assert.That(fixture.Invoke("DetermineDockedPageNumber"), Is.EqualTo(row.page), "position " + row.set);
                    Assert.That(fixture.Position, Is.EqualTo(row.after).Within(1e-6f), "position after " + row.set);
                    if (row.page != 0) Assert.That(fixture.Position, Is.EqualTo(row.after), "snapped exactly");
                }
            }
        }

        // OnBeginDrag 0x1A17400 records the drag start; OnEndDrag 0x1A17430 on a docked position
        // only stores the docked page and starts no animation.
        [Test]
        public void DragOnADockedPageOnlyRecordsThePage()
        {
            using (var fixture = new Fixture())
            {
                fixture.Position = 0.25f;
                fixture.View.OnBeginDrag(null);
                Assert.That(fixture.Float("m_startDragPosition"), Is.EqualTo(0.25f).Within(1e-6f));
                Set(fixture.View, "m_endPosition", 0.7f);
                foreach (var row in new[] { (position: 1f, page: 3), (position: 0f, page: 1), (position: 0.5f, page: 2) })
                {
                    fixture.Position = row.position;
                    fixture.View.OnEndDrag(null);
                    Assert.That(fixture.Int("m_pageNumber"), Is.EqualTo(row.page));
                    Assert.That(fixture.Float("m_endPosition"), Is.EqualTo(0.7f), "no animation target is chosen");
                }
            }
        }

        // OnEndDrag 0x1A17430 off a docked page. A drag of at least SnapDistance moves one page in
        // its direction through MoveToPage 0x1A177B0 (nothing past pages 1..3). A shorter drag
        // keeps the release as the start and picks the first page whose window, skewed towards
        // the drag direction, holds it: [p - 0.33335, p + 0.16665] moving right and
        // [p - 0.16665, p + 0.33335] moving left (the native bounds at 0x29A8010..0x29A802C).
        // 0.6 -> 0.7 lands on page 3 and 0.4 -> 0.3 on page 1, although page 2 is nearer in
        // both. Each chosen target reaches StartScrollAnimation, whose StartCoroutine the
        // inactive owner refuses.
        [Test]
        public void ReleaseOffAPageChoosesItsTargetFromTheDragDirection()
        {
            using (var fixture = new Fixture())
            {
                var cases = new[]
                {
                    (page: 2, start: 0.5f, release: 0.75f, end: 1f, startPosition: 0.75f),
                    (page: 2, start: 0.5f, release: 0.2f, end: 0f, startPosition: 0.2f),
                    (page: 2, start: 0.5f, release: 0.6f, end: 0.5f, startPosition: 0.6f),
                    (page: 2, start: 0.6f, release: 0.7f, end: 1f, startPosition: 0.7f),
                    (page: 2, start: 0.4f, release: 0.3f, end: 0f, startPosition: 0.3f),
                    (page: 1, start: 0.2f, release: 0.1f, end: 0f, startPosition: 0.1f)
                };
                foreach (var row in cases)
                {
                    Set(fixture.View, "m_pageNumber", row.page);
                    Set(fixture.View, "m_endPosition", -5f);
                    Set(fixture.View, "m_startPosition", -5f);
                    fixture.Position = row.start;
                    fixture.View.OnBeginDrag(null);
                    fixture.Position = row.release;
                    LogAssert.Expect(LogType.Error, RefusedCoroutine);
                    fixture.View.OnEndDrag(null);
                    string label = row.start + " -> " + row.release;
                    Assert.That(fixture.Float("m_endPosition"), Is.EqualTo(row.end), label);
                    Assert.That(fixture.Float("m_startPosition"), Is.EqualTo(row.startPosition).Within(1e-6f), label);
                    Assert.That(fixture.Int("m_pageNumber"), Is.EqualTo(row.page), label + " keeps the page until the animation docks");
                }

                // Past the last rightward bound (1.16665 at 0x29A802C) no window matches: the
                // fall-through to 0x1A176DF keeps the old end position and still animates towards
                // it. The fixture's ScrollRect does not clamp, so 1.2 is read back as set.
                Set(fixture.View, "m_pageNumber", 3);
                Set(fixture.View, "m_endPosition", -5f);
                Set(fixture.View, "m_startPosition", -5f);
                fixture.Position = 1.1f;
                fixture.View.OnBeginDrag(null);
                fixture.Position = 1.2f;
                Assert.That(fixture.Position, Is.EqualTo(1.2f).Within(1e-6f), "fixture position 1.2");
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                fixture.View.OnEndDrag(null);
                Assert.That(fixture.Float("m_endPosition"), Is.EqualTo(-5f), "no window: the end position is kept");
                Assert.That(fixture.Float("m_startPosition"), Is.EqualTo(1.2f).Within(1e-6f));
                Assert.That(fixture.Int("m_pageNumber"), Is.EqualTo(3));

                // From page 3 a long drag right asks for page 4, which MoveToPage ignores.
                Set(fixture.View, "m_pageNumber", 3);
                Set(fixture.View, "m_endPosition", -5f);
                fixture.Position = 0.8f;
                fixture.View.OnBeginDrag(null);
                fixture.Position = 0.99f;
                fixture.View.OnEndDrag(null);
                Assert.That(fixture.Float("m_endPosition"), Is.EqualTo(-5f));
                LogAssert.NoUnexpectedReceived();
            }
        }

        // MoveToPage 0x1A177B0 ignores pages outside 1..3 and the current page; otherwise it
        // targets (page - 1) / 2 from the current position. Action_GoToPreviousAd 0x1A179F0 and
        // Action_GoToNextAd 0x1A17A30 target 0 and 1 without those checks.
        [Test]
        public void PageMovesTargetHalfStepsAndSkipTheCurrentPage()
        {
            using (var fixture = new Fixture())
            {
                fixture.Position = 0.25f;
                Set(fixture.View, "m_pageNumber", 2);
                foreach (int ignored in new[] { 0, 2, 4, -1 })
                {
                    Set(fixture.View, "m_endPosition", -5f);
                    fixture.Invoke("MoveToPage", ignored);
                    Assert.That(fixture.Float("m_endPosition"), Is.EqualTo(-5f), "page " + ignored);
                }
                foreach (var row in new[] { (page: 1, end: 0f), (page: 3, end: 1f) })
                {
                    LogAssert.Expect(LogType.Error, RefusedCoroutine);
                    fixture.Invoke("MoveToPage", row.page);
                    Assert.That(fixture.Float("m_endPosition"), Is.EqualTo(row.end), "page " + row.page);
                    Assert.That(fixture.Float("m_startPosition"), Is.EqualTo(0.25f).Within(1e-6f));
                }

                Set(fixture.View, "m_pageNumber", 1);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                fixture.View.Action_GoToPreviousAd();
                Assert.That(fixture.Float("m_endPosition"), Is.EqualTo(0f));
                Set(fixture.View, "m_pageNumber", 3);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                fixture.View.Action_GoToNextAd();
                Assert.That(fixture.Float("m_endPosition"), Is.EqualTo(1f));
                Assert.That(Get(fixture.View, "m_positionAnimator"), Is.Null);
            }
        }

        // <MoveScrollRect>d__45.MoveNext 0x1A17BF0, first step: the duration is
        // |start - end| * TransitionDuration and the first frame is drawn at progress 0 (eased 0,
        // so the start position) before any elapsed time is compared.
        [Test]
        public void ScrollAnimationDrawsItsFirstFrameAtTheStart()
        {
            using (var fixture = new Fixture())
            {
                fixture.Position = 0.5f;
                Set(fixture.View, "m_startPosition", 0.2f);
                Set(fixture.View, "m_endPosition", 0.8f);
                var animation = (IEnumerator)fixture.Invoke("MoveScrollRect");
                Assert.That(animation.MoveNext(), Is.True);
                Assert.That(animation.Current, Is.Null);
                Assert.That(fixture.Position, Is.EqualTo(0.2f).Within(1e-6f));
                Assert.That((float)Get(animation, "<durationDelta>5__3"), Is.EqualTo(0.15f).Within(1e-6f));
                Assert.That((float)Get(animation, "<progress>5__2"), Is.EqualTo(0f));
                Assert.That(Get(animation, "<>4__this"), Is.SameAs(fixture.View));
            }
        }

        // <MoveScrollRect>d__45.MoveNext 0x1A17BF0, later steps: the loop goes on while
        // durationDelta >= progress ("ucomiss durationDelta, progress; jae" at 0x1A17C86), so
        // progress equal to the duration still draws a frame (eased 1, the end position).
        [Test]
        public void ScrollAnimationContinuesWhileProgressEqualsTheDuration()
        {
            using (var fixture = new Fixture())
            {
                Set(fixture.View, "m_startPosition", 0.2f);
                Set(fixture.View, "m_endPosition", 0.8f);
                var animation = (IEnumerator)fixture.Invoke("MoveScrollRect");
                Assert.That(animation.MoveNext(), Is.True);
                float progress = 0.25f;
                float duration = progress + Time.deltaTime;
                Set(animation, "<progress>5__2", progress);
                Set(animation, "<durationDelta>5__3", duration);
                Assert.That(animation.MoveNext(), Is.True, "progress == durationDelta continues");
                Assert.That((float)Get(animation, "<progress>5__2"), Is.EqualTo(duration));
                Assert.That(fixture.Position, Is.EqualTo(0.8f).Within(1e-6f));
            }
        }

        // <MoveScrollRect>d__45.MoveNext 0x1A17BF0, the last step once progress passes the
        // duration: the end position is set (0x1A17CAF) and the inlined DetermineDockedPageNumber
        // stores the docked page. Page 1 refreshes PreviousAd (0x1A17D07), page 3 NextAd
        // (0x1A17E70), page 2 stores 2 (0x1A17E0E) and no page stores 0 (0x1A17E7A); a refresh
        // re-centres on page 2. A ready promo that keeps its handler lets Show build three pips
        // on ad 1 without refreshing; m_pageNumber starts at 3 so every store is observable.
        [Test]
        public void ScrollAnimationDocksAndRefreshesOnTheOuterPages()
        {
            var cases = new[]
            {
                (start: 0.5f, end: 0f, current: 0, page: 2, position: 0.5f, log: new[]
                {
                    "prev.GetSprite", "RequestFullScreenTexture(full-c)", "current.GetSprite", "RequestFullScreenTexture(full-a)",
                    "next.GetSprite", "RequestFullScreenTexture(full-b)", "RaiseFullScreenImpression(a)"
                }),
                (start: 0.5f, end: 1f, current: 2, page: 2, position: 0.5f, log: new[]
                {
                    "prev.GetSprite", "RequestFullScreenTexture(full-b)", "current.GetSprite", "RequestFullScreenTexture(full-c)",
                    "next.GetSprite", "RequestFullScreenTexture(full-a)", "RaiseFullScreenImpression(c)"
                }),
                (start: 0.2f, end: 0.5f, current: 1, page: 2, position: 0.5f, log: new string[0]),
                (start: 0.5f, end: 0.25f, current: 1, page: 0, position: 0.25f, log: new string[0])
            };
            foreach (var row in cases)
            {
                using (var fixture = new Fixture())
                {
                    string label = row.start + " -> " + row.end;
                    fixture.Promo.Ready = true;
                    fixture.View.Show(fixture.Promo, Meta(Ads("a", "b", "c")), 1);
                    Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(1));
                    Set(fixture.View, "m_pageNumber", 3);
                    Set(fixture.View, "m_startPosition", row.start);
                    Set(fixture.View, "m_endPosition", row.end);
                    var animation = (IEnumerator)fixture.Invoke("MoveScrollRect");
                    Assert.That(animation.MoveNext(), Is.True, label);
                    Set(animation, "<progress>5__2", 1f);
                    fixture.Log.Clear();

                    Assert.That(animation.MoveNext(), Is.False, label);

                    Assert.That(fixture.Log, Is.EqualTo(row.log), label);
                    Assert.That(fixture.Int("m_currentAd"), Is.EqualTo(row.current), label);
                    Assert.That(fixture.Int("m_pageNumber"), Is.EqualTo(row.page), label);
                    Assert.That(fixture.Position, Is.EqualTo(row.position).Within(1e-6f), label);
                    var pips = (Image[])Get(fixture.View, "m_pips");
                    bool refreshed = row.log.Length != 0;
                    for (int i = 0; i < pips.Length; i++)
                        Assert.That(pips[i].sprite, Is.SameAs(refreshed && i == row.current ? fixture.FullPip : fixture.EmptyPip), label + " pip " + i);
                }
            }
        }
    }
}
