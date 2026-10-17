using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of HLScrollRefreshWidget (HLUnityUI.Runtime 0x0600007c..84) against the
    // shipped x86-64 bodies. Every case owns its HideAndDontSave roots and destroys them
    // in TearDown. The widget has no [ExecuteAlways], so its Awake and OnDestroy never run
    // on their own in edit mode and are invoked explicitly. The animation cases keep their
    // root active so the legacy Animation exposes its states; the widget's HLScrollRect,
    // when there is one, stays on a separate inactive root.
    [TestFixture]
    public sealed class OriginalHLScrollRefreshWidgetTests
    {
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwned()
        {
            foreach (Object item in owned)
                if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
        }

        sealed class Rig
        {
            public HLScrollRefreshWidget Widget;
            public Animation Animation;
            public AnimationClip Pull;
            public AnimationClip Load;
            public AnimationClip End;
            public int Cancelled;
            public int Completed;
            public readonly List<Action<bool>> Activations = new List<Action<bool>>();
        }

        static object Get(HLScrollRefreshWidget widget, string name) => typeof(HLScrollRefreshWidget).GetField(name, Instance).GetValue(widget);
        static void Set(HLScrollRefreshWidget widget, string name, object value) => typeof(HLScrollRefreshWidget).GetField(name, Instance).SetValue(widget, value);

        static void Call(HLScrollRefreshWidget widget, string name)
        {
            try { typeof(HLScrollRefreshWidget).GetMethod(name, Instance).Invoke(widget, null); }
            catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        GameObject NewRoot(string name, bool active)
        {
            var root = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            owned.Add(root);
            root.SetActive(active);
            return root;
        }

        AnimationClip Clip(string name)
        {
            // One second long, so normalizedTime maps onto time.
            var clip = new AnimationClip { name = name, legacy = true, hideFlags = HideFlags.HideAndDontSave };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            owned.Add(clip);
            return clip;
        }

        Rig Build(bool withAnimation)
        {
            var rig = new Rig();
            GameObject root = NewRoot("ProjectLucid.HLScrollRefreshWidget.Owned", withAnimation);
            rig.Widget = root.AddComponent<HLScrollRefreshWidget>();
            rig.Widget.OnCancel = new UnityEvent();
            rig.Widget.OnComplete = new UnityEvent();
            rig.Widget.OnCancel.AddListener(() => rig.Cancelled++);
            rig.Widget.OnComplete.AddListener(() => rig.Completed++);
            rig.Widget.OnActivate.AddListener(callback => rig.Activations.Add(callback));
            if (!withAnimation)
                return rig;
            rig.Animation = root.AddComponent<Animation>();
            rig.Animation.playAutomatically = false;
            rig.Pull = Clip("pull");
            rig.Load = Clip("load");
            rig.End = Clip("end");
            rig.Animation.AddClip(rig.Pull, "pull");
            rig.Animation.AddClip(rig.Load, "load");
            rig.Animation.AddClip(rig.End, "end");
            Set(rig.Widget, "m_animation", rig.Animation);
            Set(rig.Widget, "m_animationNamePull", "pull");
            Set(rig.Widget, "m_animationNameLoad", "load");
            Set(rig.Widget, "m_animationNameEnd", "end");
            return rig;
        }

        static void AssertState(AnimationState state, float speed, WrapMode wrapMode)
        {
            Assert.That(state.enabled, Is.True, state.name);
            Assert.That(state.weight, Is.EqualTo(1f), state.name);
            Assert.That(state.speed, Is.EqualTo(speed), state.name);
            Assert.That(state.wrapMode, Is.EqualTo(wrapMode), state.name);
        }

        // dump.cs TypeDefIndex 11841/11840 and DummyDll metadata: ActivateEvent is declared
        // first (member 0, which makes OnEndDrag ordinal 19 for <OnEndDrag>b__19_0), the
        // field order, types and attribute order, and the constructor's 0.15/0.6 defaults
        // (0x1B3C546) with a fresh ActivateEvent.
        [Test]
        public void DeclarationKeepsTheDumpedFieldsNestedEventAndDefaults()
        {
            Type type = typeof(HLScrollRefreshWidget);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("HLScrollRefreshWidget"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(type.GetCustomAttributesData(), Is.Empty);

            Type activate = type.GetNestedTypes(Declared).Single();
            Assert.That(activate, Is.EqualTo(typeof(HLScrollRefreshWidget.ActivateEvent)));
            Assert.That(activate.IsNestedPublic && activate.IsSerializable, Is.True);
            Assert.That(activate.BaseType, Is.EqualTo(typeof(UnityEvent<Action<bool>>)));

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "OnActivate", "OnCancel", "OnComplete", "m_scrollRect", "m_dragThreshold", "m_activationPercentage",
                "m_animation", "m_animationNamePull", "m_animationNameLoad", "m_animationNameEnd", "m_progress",
                "m_animationState", "m_refreshing",
            }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                activate, typeof(UnityEvent), typeof(UnityEvent), typeof(HLScrollRect), typeof(float), typeof(float),
                typeof(Animation), typeof(string), typeof(string), typeof(string), typeof(float), typeof(AnimationState), typeof(bool),
            }));
            Assert.That(fields.Take(3).All(f => f.IsPublic && f.GetCustomAttributesData().Count == 0), Is.True);
            Assert.That(fields.Skip(3).All(f => f.IsPrivate), Is.True);
            Type[][] attributes =
            {
                new[] { typeof(SerializeField) },
                new[] { typeof(UnityEngine.RangeAttribute), typeof(SerializeField) },
                new[] { typeof(SerializeField), typeof(UnityEngine.RangeAttribute) },
                new[] { typeof(SerializeField) }, new[] { typeof(SerializeField) }, new[] { typeof(SerializeField) },
                new[] { typeof(SerializeField) }, new Type[0], new Type[0], new Type[0],
            };
            for (int i = 0; i < attributes.Length; i++)
                Assert.That(fields[i + 3].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(attributes[i]), fields[i + 3].Name);
            foreach (FieldInfo field in new[] { fields[4], fields[5] })
            {
                UnityEngine.RangeAttribute range = field.GetCustomAttribute<UnityEngine.RangeAttribute>();
                Assert.That(new[] { range.min, range.max }, Is.EqualTo(new[] { 0f, 1f }), field.Name);
            }
            MethodInfo lambda = type.GetMethod("<OnEndDrag>b__19_0", Declared);
            Assert.That(lambda.IsPrivate && lambda.GetParameters().Single().ParameterType == typeof(bool), Is.True);
            Assert.That(type.GetProperty("Refreshing", Declared).SetMethod, Is.Null);

            Rig rig = Build(false);
            Assert.That(Get(rig.Widget, "m_dragThreshold"), Is.EqualTo(0.15f));
            Assert.That(Get(rig.Widget, "m_activationPercentage"), Is.EqualTo(0.6f));
            Assert.That(rig.Widget.OnActivate, Is.InstanceOf<HLScrollRefreshWidget.ActivateEvent>());
            Assert.That(rig.Widget.Refreshing, Is.False);
        }

        // 0x0600007f: progress over the 0.15 threshold, clamped to [0, 1]; without an
        // animation nothing else happens.
        [Test]
        public void OnDragStoresTheClampedThresholdProgress()
        {
            Rig rig = Build(false);
            rig.Widget.OnDrag(0.075f);
            Assert.That((float)Get(rig.Widget, "m_progress"), Is.EqualTo(0.5f).Within(1e-6f));
            rig.Widget.OnDrag(0.3f);
            Assert.That(Get(rig.Widget, "m_progress"), Is.EqualTo(1f));
            rig.Widget.OnDrag(-0.1f);
            Assert.That(Get(rig.Widget, "m_progress"), Is.EqualTo(0f));
            Assert.That(Get(rig.Widget, "m_animationState"), Is.Null);
        }

        // 0x1B3C26F..0x1B3C2A7: with an animation every state is stopped (Animation.Stop at
        // 0x1B3C27A), then the pull state is scrubbed at speed 0 to the progress and becomes
        // the default clip; SetupAnimation re-enables only the pull state.
        [Test]
        public void OnDragScrubsThePullAnimation()
        {
            Rig rig = Build(true);
            AnimationState load = rig.Animation["load"];
            load.enabled = true;
            Assert.That(load.enabled, Is.True);
            rig.Widget.OnDrag(0.075f);
            Assert.That(load.enabled, Is.False, "Stop disables the other states");
            AnimationState pull = rig.Animation["pull"];
            Assert.That(Get(rig.Widget, "m_animationState"), Is.SameAs(pull));
            AssertState(pull, 0f, WrapMode.Default);
            Assert.That(pull.normalizedTime, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(rig.Animation.clip, Is.SameAs(rig.Pull));
            Assert.That(rig.Widget.Refreshing, Is.False);
        }

        // 0x1B3C3DC..0x1B3C43C: below the activation percentage the pull plays backwards,
        // progress resets and OnCancel fires; OnActivate does not.
        [Test]
        public void OnEndDragBelowTheActivationPercentageCancels()
        {
            Rig rig = Build(true);
            Set(rig.Widget, "m_progress", 0.5f);
            rig.Widget.OnEndDrag();
            AssertState(rig.Animation["pull"], -1f, WrapMode.Default);
            Assert.That(rig.Animation.clip, Is.SameAs(rig.Pull));
            Assert.That(Get(rig.Widget, "m_progress"), Is.EqualTo(0f));
            Assert.That(rig.Cancelled, Is.EqualTo(1));
            Assert.That(rig.Activations, Is.Empty);
            Assert.That(rig.Widget.Refreshing, Is.False);
        }

        // 0x1B3C441..0x1B3C4B7 and the lambda 0x1B3C5B0: at the percentage the load loops,
        // the widget refreshes and OnActivate gets a callback that ends the refresh, fires
        // OnComplete and plays the end clip. Progress is not reset.
        [Test]
        public void OnEndDragAtTheActivationPercentageRefreshesUntilTheCallback()
        {
            Rig rig = Build(true);
            Set(rig.Widget, "m_progress", 0.6f);
            rig.Widget.OnEndDrag();
            AssertState(rig.Animation["load"], 1f, WrapMode.Loop);
            Assert.That(rig.Animation.clip, Is.SameAs(rig.Load));
            Assert.That(rig.Widget.Refreshing, Is.True);
            Assert.That(rig.Cancelled, Is.Zero);
            Assert.That(Get(rig.Widget, "m_progress"), Is.EqualTo(0.6f));
            Action<bool> callback = rig.Activations.Single();
            Assert.That(callback.Method.Name, Is.EqualTo("<OnEndDrag>b__19_0"));
            Assert.That(rig.Completed, Is.Zero);

            callback(false);
            Assert.That(rig.Widget.Refreshing, Is.False);
            Assert.That(rig.Completed, Is.EqualTo(1));
            AssertState(rig.Animation["end"], 1f, WrapMode.Default);
            Assert.That(rig.Animation.clip, Is.SameAs(rig.End));
        }

        // jae at 0x1B3C3DA: a NaN progress is not at or above the percentage, so it cancels.
        [Test]
        public void OnEndDragWithNaNProgressCancels()
        {
            Rig rig = Build(true);
            Set(rig.Widget, "m_progress", float.NaN);
            // The cancel branch hands the NaN progress to the pull animation's normalizedTime.
            LogAssert.Expect(LogType.Assert, "Invalid parameter time in AnimationState.SetTime because it was infinity or NaN");
            rig.Widget.OnEndDrag();
            Assert.That(rig.Cancelled, Is.EqualTo(1));
            Assert.That(rig.Activations, Is.Empty);
        }

        // 0x0600007d/0x0600007e: OnEndDrag joins OnDragReleased and OnDrag joins
        // OnRubberDrag, and both leave again; a missing scroll rect is skipped.
        [Test]
        public void AwakeAndOnDestroySubscribeToTheScrollRectDragEvents()
        {
            Rig rig = Build(false);
            HLScrollRect scroll = NewRoot("ProjectLucid.HLScrollRefreshWidget.Scroll", false).AddComponent<HLScrollRect>();
            Set(rig.Widget, "m_scrollRect", scroll);
            Call(rig.Widget, "Awake");
            Delegate released = scroll.OnDragReleased.GetInvocationList().Single();
            Delegate rubber = scroll.OnRubberDrag.GetInvocationList().Single();
            Assert.That(released.Target, Is.SameAs(rig.Widget));
            Assert.That(released.Method.Name, Is.EqualTo("OnEndDrag"));
            Assert.That(rubber.Target, Is.SameAs(rig.Widget));
            Assert.That(rubber.Method.Name, Is.EqualTo("OnDrag"));

            Call(rig.Widget, "OnDestroy");
            Assert.That(scroll.OnDragReleased.GetInvocationListCount(), Is.Zero);
            Assert.That(scroll.OnRubberDrag.GetInvocationListCount(), Is.Zero);

            Rig lone = Build(false);
            Assert.DoesNotThrow(() => Call(lone.Widget, "Awake"));
            Assert.DoesNotThrow(() => Call(lone.Widget, "OnDestroy"));
        }
    }
}
