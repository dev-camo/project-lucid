using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ProjectLucid.Tests.OriginalInOutTestSupport;
using AnimState = Hardlight.HLUIAnimationInOut.AnimState;
using MotionType = Hardlight.HLAnchorTranslateInOut.MotionType;
using PlayDirection = Hardlight.HLUIAnimationInOut.PlayDirection;
using TranslationDirection = Hardlight.HLAnchorTranslateInOut.TranslationDirection;

namespace ProjectLucid.Tests
{
    // Original HLUnityUI.Runtime Hardlight.HLAnchorTranslateInOut (TypeDef 0x020000a5, dump.cs 11992)
    // with TranslationDirection, MotionType and <RunAnimation>d__15, against the x86-64 bodies
    // 0x1B61A30..0x1B624CA (SetAnchors jump table 0x1B61DD0, SetAnchorTargetsFromConstants 0x1B61FEC).
    // Each case owns a component on its own RectTransform and a separate target RectTransform whose
    // anchors start at min (0.1, 0.2), max (0.7, 0.8). Fixture anchor pairs: clamped out -1, in 0.2;
    // stretch out 0, in 0.9.
    public sealed class OriginalHLAnchorTranslateInOutTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly Owned Owned = new Owned();
            public readonly HLAnchorTranslateInOut Anchor;
            public readonly RectTransform Target;

            public Fixture(string name, bool active)
            {
                try
                {
                    Anchor = Owned.Host(name, active, typeof(RectTransform)).AddComponent<HLAnchorTranslateInOut>();
                    Target = (RectTransform)Owned.Host(name + "-target", false, typeof(RectTransform)).transform;
                    Target.anchorMin = new Vector2(0.1f, 0.2f);
                    Target.anchorMax = new Vector2(0.7f, 0.8f);
                    Set(Anchor, "m_targetTransform", Target);
                    Pairs(-1f, 0.2f, 0f, 0.9f);
                    Set(Anchor, "m_translationCurve", Owned.Linear(0f, 0f, 2f, 1f));
                    WireEvents(Owned, Anchor);
                    Call(Anchor, "Awake");
                }
                catch { Dispose(); throw; }
            }

            public void Pairs(float clampedOut, float clampedIn, float stretchOut, float stretchIn)
            {
                Set(Anchor, "m_clampedAnchorOut", clampedOut);
                Set(Anchor, "m_clampedAnchorIn", clampedIn);
                Set(Anchor, "m_stretchAnchorOut", stretchOut);
                Set(Anchor, "m_stretchAnchorIn", stretchIn);
            }

            public float[] ReadPairs() => new[]
            {
                Get<float>(Anchor, "m_clampedAnchorOut"), Get<float>(Anchor, "m_clampedAnchorIn"),
                Get<float>(Anchor, "m_stretchAnchorOut"), Get<float>(Anchor, "m_stretchAnchorIn"),
            };

            public TranslationDirection Translation { set => Set(Anchor, "translationOnDirection", value); }
            public MotionType Motion { set => Set(Anchor, "motionType", value); }

            public PlayDirection Direction
            {
                get => Get<PlayDirection>(Anchor, "m_playDirection");
                set => Set(Anchor, "m_playDirection", value);
            }

            public float Progress
            {
                get => Get<float>(Anchor, "m_normalisedInOutProgress");
                set => Set(Anchor, "m_normalisedInOutProgress", value);
            }

            public AnimState State { set => Call(Anchor, "set_CurrentState", value); }

            public IEnumerator Run() => (IEnumerator)Call(Anchor, "RunAnimation");

            public void AssertAnchors(float minX, float minY, float maxX, float maxY)
            {
                Assert.That(Target.anchorMin.x, Is.EqualTo(minX).Within(1e-6f), "anchorMin.x");
                Assert.That(Target.anchorMin.y, Is.EqualTo(minY).Within(1e-6f), "anchorMin.y");
                Assert.That(Target.anchorMax.x, Is.EqualTo(maxX).Within(1e-6f), "anchorMax.x");
                Assert.That(Target.anchorMax.y, Is.EqualTo(maxY).Within(1e-6f), "anchorMax.y");
            }

            public void Dispose() => Owned.Dispose();
        }

        // dump.cs 11992: public (not sealed), NullChecks before ArrayBoundsChecks, then
        // RequireComponent(RectTransform); the tooltip on the target, HashEnum after SerializeField on
        // the two enum fields; the iterator ordinal 15 counts the two nested enums and eight fields.
        [Test]
        public void AnchorRetainsWholeDeclaration()
        {
            Type type = typeof(HLAnchorTranslateInOut);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(HLUIAnimationInOut)));
            Assert.That((int)type.Attributes, Is.EqualTo(0x100001), "public beforefieldinit");
            OriginalHLUIAnimationInOutTests.CheckOptions(type, 1, 2);
            CustomAttributeData[] attributes = type.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Length, Is.EqualTo(3));
            Assert.That(attributes[2].AttributeType, Is.EqualTo(typeof(RequireComponent)));
            Assert.That(attributes[2].ConstructorArguments.Single().Value, Is.EqualTo(typeof(RectTransform)));

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "m_targetTransform", "motionType", "translationOnDirection", "m_clampedAnchorOut", "m_clampedAnchorIn",
                "m_stretchAnchorOut", "m_stretchAnchorIn", "m_translationCurve",
            }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                typeof(RectTransform), typeof(MotionType), typeof(TranslationDirection), typeof(float), typeof(float),
                typeof(float), typeof(float), typeof(HLCurve),
            }));
            Assert.That(fields.All(f => f.IsPrivate), Is.True);
            Assert.That(fields[0].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(fields[0].GetCustomAttributesData()[0].ConstructorArguments.Single().Value, Is.EqualTo("If this transform is null, it will select itself at runtime"));
            for (int i = 1; i <= 2; i++)
            {
                CustomAttributeData[] data = fields[i].GetCustomAttributesData().ToArray();
                Assert.That(data.Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField), typeof(HashEnumAttribute) }), fields[i].Name);
                Assert.That(data[1].ConstructorArguments.Single().Value, Is.EqualTo(fields[i].FieldType), fields[i].Name);
            }
            for (int i = 3; i < fields.Length; i++)
                Assert.That(fields[i].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }), fields[i].Name);

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[]
            {
                "Awake", "OnEnable", "AnimateIn", "AnimateOut", "ForceFinishAnimation", "RunAnimation",
                "SetAnchorTargetsFromConstants", "SetAnchors", "SetFinalState",
            }));
            Assert.That(methods.Select(m => (int)m.Attributes).ToArray(), Is.EqualTo(new[] { 0xC4, 0xC4, 0xC6, 0xC6, 0xC6, 0xC4, 0x86, 0x86, 0xC4 }));
            Assert.That(methods[7].GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "clamped", "stretched" }));
            Assert.That(type.GetConstructors(Declared).Single().GetParameters(), Is.Empty);

            Assert.That(type.GetNestedTypes(Declared).Select(t => t.Name).ToArray(), Is.EqualTo(new[] { "TranslationDirection", "MotionType", "<RunAnimation>d__15" }));
            Assert.That(Enum.GetNames(typeof(TranslationDirection)), Is.EqualTo(new[] { "LeftToRight", "RightToLeft", "TopToBottom", "BottomToTop" }));
            Assert.That(Enum.GetValues(typeof(TranslationDirection)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(Enum.GetNames(typeof(MotionType)), Is.EqualTo(new[] { "Simple", "Bounce", "Stretch" }));
            Assert.That(Enum.GetValues(typeof(MotionType)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
            OriginalHLUIAnimationInOutTests.CheckIterator(type, "<RunAnimation>d__15", "<animatingIn>5__2", "<timer>5__3", "<endTime>5__4");
        }

        // .ctor 0x060004e8 @0x1B620D0 stores 0xbf800000 (-1) at +0xC0 m_clampedAnchorOut and 0x3f800000
        // (1) at +0xCC m_stretchAnchorIn before MonoBehaviour..ctor; the in/out partners stay zero.
        [Test]
        public void ConstructionSeedsTheClampedOutAndStretchInAnchors()
        {
            using (var owned = new Owned())
            {
                var anchor = owned.Host("Anchor-defaults", false, typeof(RectTransform)).AddComponent<HLAnchorTranslateInOut>();
                Assert.That(RawBits(Get<float>(anchor, "m_clampedAnchorOut")), Is.EqualTo(unchecked((int)0xbf800000)));
                Assert.That(Get<float>(anchor, "m_clampedAnchorIn"), Is.EqualTo(0f));
                Assert.That(Get<float>(anchor, "m_stretchAnchorOut"), Is.EqualTo(0f));
                Assert.That(RawBits(Get<float>(anchor, "m_stretchAnchorIn")), Is.EqualTo(0x3f800000));
                Assert.That(Get(anchor, "motionType"), Is.EqualTo(MotionType.Simple));
                Assert.That(Get(anchor, "translationOnDirection"), Is.EqualTo(TranslationDirection.LeftToRight));
                using (var serialized = new SerializedObject(anchor))
                {
                    Assert.That(serialized.FindProperty("m_clampedAnchorOut").floatValue, Is.EqualTo(-1f));
                    Assert.That(serialized.FindProperty("m_stretchAnchorIn").floatValue, Is.EqualTo(1f));
                }
            }
        }

        // Awake 0x060004df @0x1B61A30: base Awake, then a null target takes `transform as RectTransform`
        // (exact class compare at 0x1B61ABB); an assigned target is kept.
        [Test]
        public void AwakeDefaultsTheTargetToItsOwnRectTransform()
        {
            using (var fixture = new Fixture("Anchor-awake", false))
            {
                Set(fixture.Anchor, "m_targetTransform", null);
                Call(fixture.Anchor, "Awake");
                Assert.That(Get(fixture.Anchor, "m_targetTransform"), Is.SameAs(fixture.Anchor.transform));
                Set(fixture.Anchor, "m_targetTransform", fixture.Target);
                Call(fixture.Anchor, "Awake");
                Assert.That(Get(fixture.Anchor, "m_targetTransform"), Is.SameAs(fixture.Target));
            }
        }

        // SetAnchors 0x060004e6 @0x1B61C90 with (clamped 0.3, stretched 0.9) from min (0.1, 0.2), max
        // (0.7, 0.8): LeftToRight min.x = clamped, max.x = stretched; RightToLeft min.x = stretched,
        // max.x = clamped; TopToBottom min.y = stretched, max.y = clamped; BottomToTop min.y = clamped,
        // max.y = stretched; any other value (0x1B61CA5 ja) changes nothing. The other axis is kept.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(-1)]
        public void SetAnchorsPlacesTheClampedAndStretchedEdgesByDirection(int direction)
        {
            using (var fixture = new Fixture("Anchor-set", false))
            {
                fixture.Translation = (TranslationDirection)direction;
                fixture.Anchor.SetAnchors(0.3f, 0.9f);
                switch (direction)
                {
                    case 0: fixture.AssertAnchors(0.3f, 0.2f, 0.9f, 0.8f); break;
                    case 1: fixture.AssertAnchors(0.9f, 0.2f, 0.3f, 0.8f); break;
                    case 2: fixture.AssertAnchors(0.1f, 0.9f, 0.7f, 0.3f); break;
                    case 3: fixture.AssertAnchors(0.1f, 0.3f, 0.7f, 0.9f); break;
                    default: fixture.AssertAnchors(0.1f, 0.2f, 0.7f, 0.8f); break;
                }
            }
        }

        // SetAnchorTargetsFromConstants 0x060004e5 @0x1B61F10 from min (0.1, 0.2), max (0.4, 0.7):
        // width 0.3, height 0.5, stored as (clamped out, clamped in, stretch out, stretch in):
        // LeftToRight (0 - w, min.x, 0, max.x); RightToLeft (w + 1, max.x, 1, min.x);
        // TopToBottom (h + 1, max.y, 1, min.y); BottomToTop (0 - h, min.y, 0, max.y); others keep all.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(7)]
        public void SetAnchorTargetsFromConstantsDerivesBothPairsFromTheCurrentAnchors(int direction)
        {
            using (var fixture = new Fixture("Anchor-targets", false))
            {
                fixture.Target.anchorMin = new Vector2(0.1f, 0.2f);
                fixture.Target.anchorMax = new Vector2(0.4f, 0.7f);
                fixture.Pairs(5f, 6f, 7f, 8f);
                fixture.Translation = (TranslationDirection)direction;
                fixture.Anchor.SetAnchorTargetsFromConstants();
                float width = 0.4f - 0.1f;
                float height = 0.7f - 0.2f;
                float[] expected;
                switch (direction)
                {
                    case 0: expected = new[] { 0f - width, 0.1f, 0f, 0.4f }; break;
                    case 1: expected = new[] { width + 1f, 0.4f, 1f, 0.1f }; break;
                    case 2: expected = new[] { height + 1f, 0.7f, 1f, 0.2f }; break;
                    case 3: expected = new[] { 0f - height, 0.2f, 0f, 0.7f }; break;
                    default: expected = new[] { 5f, 6f, 7f, 8f }; break;
                }
                float[] actual = fixture.ReadPairs();
                for (int i = 0; i < 4; i++)
                    Assert.That(actual[i], Is.EqualTo(expected[i]).Within(1e-6f), "pair " + i);
                fixture.AssertAnchors(0.1f, 0.2f, 0.4f, 0.7f);
            }
        }

        // 0x1B61F76..0x1B61F7C: LeftToRight computes `0 - width` (xorps; subss), so a zero-width span
        // stores +0, where a negation would store -0.
        [Test]
        public void ZeroWidthLeftToRightStoresPositiveZero()
        {
            using (var fixture = new Fixture("Anchor-zero-width", false))
            {
                fixture.Target.anchorMin = new Vector2(0.5f, 0.2f);
                fixture.Target.anchorMax = new Vector2(0.5f, 0.7f);
                fixture.Anchor.SetAnchorTargetsFromConstants();
                Assert.That(RawBits(Get<float>(fixture.Anchor, "m_clampedAnchorOut")), Is.EqualTo(0));
            }
        }

        // SetFinalState 0x060004e7 @0x1B62000: state, then SetAnchors with the pair, then the completion
        // event, and only then progress (1 in, 0 out), so listeners still see the old progress; the in
        // branch ends with ScheduleAutoAnimateOut.
        [TestCase(true)]
        [TestCase(false)]
        public void SetFinalStateAppliesTheAnchorsBeforeTheEventAndProgressAfterIt(bool animatingIn)
        {
            using (var fixture = new Fixture("Anchor-final", false))
            {
                fixture.Progress = 0.25f;
                string anchorsAtEvent = null;
                Set(fixture.Anchor, animatingIn ? "m_onAnimateInComplete" : "m_onAnimateOutComplete",
                    fixture.Owned.Event("complete", () => { anchorsAtEvent = fixture.Target.anchorMin.x.ToString("R", CultureInfo.InvariantCulture) + "," + fixture.Target.anchorMax.x.ToString("R", CultureInfo.InvariantCulture); return Snapshot(fixture.Anchor); }));
                Call(fixture.Anchor, "SetFinalState", animatingIn);
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { animatingIn ? "complete:In 0.25" : "complete:Out 0.25" }));
                Assert.That(anchorsAtEvent, Is.EqualTo(animatingIn ? "0.2,0.9" : "-1,0"));
                Assert.That(fixture.Progress, Is.EqualTo(animatingIn ? 1f : 0f));
            }
        }

        // ForceFinishAnimation 0x060004e3 @0x1B61E40: stop, then SetFinalState(direction == In); None
        // and Out both finish out, and the direction is never cleared.
        [TestCase(0, 3)]
        [TestCase(1, 1)]
        [TestCase(2, 3)]
        public void ForceFinishFinishesInOnlyForAnInDirectionAndKeepsTheDirection(int direction, int state)
        {
            using (var fixture = new Fixture("Anchor-force", false))
            {
                fixture.Direction = (PlayDirection)direction;
                fixture.Anchor.ForceFinishAnimation();
                Assert.That(fixture.Anchor.CurrentState, Is.EqualTo((AnimState)state));
                Assert.That(fixture.Direction, Is.EqualTo((PlayDirection)direction));
                if (state == 1) fixture.AssertAnchors(0.2f, 0.2f, 0.9f, 0.8f);
                else fixture.AssertAnchors(-1f, 0.2f, 0f, 0.8f);
            }
        }

        // OnEnable 0x060004e0 @0x1B61BE0: an initial In sets progress 1 and the in pair, an initial Out
        // progress 0 and the out pair (no events, unlike the alpha and scale variants), then the inlined
        // base adopts the state and dispatches a different target.
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(0)]
        public void OnEnablePlacesTheInitialPairWithoutEvents(int initial)
        {
            using (var fixture = new Fixture("Anchor-onenable", false))
            {
                fixture.Progress = 0.5f;
                Set(fixture.Anchor, "m_initialState", (AnimState)initial);
                Set(fixture.Anchor, "m_animateToStateOnEnable", AnimState.In);
                Call(fixture.Anchor, "OnEnable");
                Assert.That(fixture.Owned.Log, Is.Empty);
                Assert.That(fixture.Anchor.CurrentState, Is.EqualTo((AnimState)initial));
                if (initial == 1)
                {
                    Assert.That(fixture.Progress, Is.EqualTo(1f));
                    fixture.AssertAnchors(0.2f, 0.2f, 0.9f, 0.8f);
                    Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.None), "same target as the initial state");
                }
                else
                {
                    Assert.That(fixture.Progress, Is.EqualTo(initial == 3 ? 0f : 0.5f));
                    if (initial == 3) fixture.AssertAnchors(-1f, 0.2f, 0f, 0.8f);
                    else fixture.AssertAnchors(0.1f, 0.2f, 0.7f, 0.8f);
                    Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.In));
                }
            }
        }

        // Loop body 0x1B622C5..0x1B62427 with the curve held at v (constant curve, end time 2):
        // Simple uses Lerp for both edges, Bounce LerpUnclamped for both, Stretch Lerp for the clamped
        // edge and LerpUnclamped for the stretched one; motion 3 leaves both locals at zero and still
        // calls SetAnchors(0, 0). LeftToRight puts clamped on min.x and stretched on max.x.
        // Cases: motion 0..3 at v = 1.5, then motion 0..2 at v = -0.5.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void LoopStepLerpsEachEdgeByTheMotionType(int index)
        {
            var cases = new[]
            {
                (0, 1.5f, 0.2f, 0.9f),
                (1, 1.5f, -1f + 1.2f * 1.5f, 0.9f * 1.5f),
                (2, 1.5f, 0.2f, 0.9f * 1.5f),
                (3, 1.5f, 0f, 0f),
                (0, -0.5f, -1f, 0f),
                (1, -0.5f, -1f + 1.2f * -0.5f, 0.9f * -0.5f),
                (2, -0.5f, -1f, 0.9f * -0.5f),
            };
            (int motion, float value, float clamped, float stretched) = cases[index];
            using (var fixture = new Fixture("Anchor-motion", false))
            {
                Set(fixture.Anchor, "m_translationCurve", fixture.Owned.Constant(value, 2f));
                fixture.Motion = (MotionType)motion;
                fixture.Direction = PlayDirection.In;
                fixture.State = AnimState.AnimatingOut;
                IEnumerator run = fixture.Run();
                Assert.That(run.GetType().Name, Is.EqualTo("<RunAnimation>d__15"));
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(run.Current, Is.Null, "a reversal goes straight into the loop");
                Assert.That(fixture.Target.anchorMin.x, Is.EqualTo(clamped).Within(1e-5f), "clamped");
                Assert.That(fixture.Target.anchorMax.x, Is.EqualTo(stretched).Within(1e-5f), "stretched");
                Assert.That(fixture.Target.anchorMin.y, Is.EqualTo(0.2f));
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { "onAnimateIn:AnimatingIn 0" }));
            }
        }

        // State 0 matches HLAlphaInOut: Out from progress 0.25 resumes at (1 - 0.25) * 2 = 1.5 and,
        // not reversing, yields the out delay wait; the following step samples at end - timer and
        // stores progress = (2 - timer) / 2.
        [Test]
        public void OutRunResumesFromProgressAfterTheOutDelay()
        {
            using (var fixture = new Fixture("Anchor-out", false))
            {
                fixture.Direction = PlayDirection.Out;
                fixture.State = AnimState.In;
                fixture.Progress = 0.25f;
                IEnumerator run = fixture.Run();
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(run.Current, Is.SameAs(Get(fixture.Anchor, "m_animateOutDelayWait")));
                Assert.That(fixture.Anchor.CurrentState, Is.EqualTo(AnimState.AnimatingOut));
                Assert.That(Hoisted(run, "timer").GetValue(run), Is.EqualTo(1.5f));
                float delta = Time.deltaTime;
                Assume.That(delta, Is.LessThan(0.5f), "one EditMode delta stays inside the remaining 0.5 seconds of the run");
                Assert.That(run.MoveNext(), Is.True);
                float time = 2f - (1.5f + delta);
                Assert.That(fixture.Progress, Is.EqualTo(time / 2f).Within(1e-6f));
                // Simple motion: Lerp(-1, 0.2, time / 2) on min.x and Lerp(0, 0.9, time / 2) on max.x.
                Assert.That(fixture.Target.anchorMin.x, Is.EqualTo(-1f + 1.2f * (time / 2f)).Within(1e-5f));
                Assert.That(fixture.Target.anchorMax.x, Is.EqualTo(0.9f * (time / 2f)).Within(1e-5f));
            }
        }

        // 0x1B621F5..0x1B6223D: after the last body the run calls SetFinalState(animatingIn), clears
        // +0x80 and returns false, leaving the direction set.
        [Test]
        public void RunFinishesInAndKeepsTheDirection()
        {
            using (var fixture = new Fixture("Anchor-end", false))
            {
                fixture.Direction = PlayDirection.In;
                fixture.State = AnimState.AnimatingOut;
                fixture.Progress = 1f;
                IEnumerator run = fixture.Run();
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(run.MoveNext(), Is.False);
                Assert.That(fixture.Anchor.CurrentState, Is.EqualTo(AnimState.In));
                fixture.AssertAnchors(0.2f, 0.2f, 0.9f, 0.8f);
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.In));
                Assert.That(fixture.Owned.Log.Last(), Does.StartWith("onAnimateInComplete:In "));
                Assert.Throws<NotSupportedException>(() => run.Reset());
            }
        }
    }
}
