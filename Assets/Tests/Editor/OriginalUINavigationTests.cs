using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Tests.UISelectionShared;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.UINavigation (HLUnityUI.Runtime 0x0600037f..8d)
    // against the shipped x86-64 bodies. The struct cases allocate nothing; the
    // equality cases own inactive RecordingUISelectable targets whose lifecycle
    // callbacks stay off, so no static selectable table or system is touched.
    public sealed class OriginalUINavigationTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        static UINavigation WithFlag(UINavigation navigation, string field, bool value)
        {
            object boxed = navigation;
            typeof(UINavigation).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boxed, value);
            return (UINavigation)boxed;
        }

        static RecordingUISelectable NewTarget(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            return go.AddComponent<RecordingUISelectable>();
        }

        [Test]
        public void DeclarationKeepsOriginalFieldsFlagsAndAccessors()
        {
            Type type = typeof(UINavigation);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("Hardlight.UINavigation"));
            Assert.That(type.IsValueType && type.IsSerializable, Is.True);
            Assert.That(type.GetInterfaces(), Is.EqualTo(new[] { typeof(IEquatable<UINavigation>) }));

            Type mode = typeof(UINavigation.NavMode);
            Assert.That(type.GetNestedTypes(Declared), Is.EqualTo(new[] { mode }));
            Assert.That(mode.IsDefined(typeof(FlagsAttribute), false), Is.True);
            Assert.That(Enum.GetNames(mode), Is.EqualTo(new[] { "None", "Horizontal", "Vertical", "Automatic", "Explicit" }));
            Assert.That(Enum.GetValues(mode).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));

            // dump.cs TypeDefIndex 11945, 0x0..0x2A.
            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] {
                "m_mode", "m_selectOnUp", "m_selectOnDown", "m_selectOnLeft", "m_selectOnRight",
                "m_autoNavigateIfExplicitSelectionIsNotValid", "m_pointerEnterShouldTriggerNavigationEvent", "m_pointerExitShouldTriggerNavigationEvent" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] {
                mode, typeof(UISelectable), typeof(UISelectable), typeof(UISelectable), typeof(UISelectable), typeof(bool), typeof(bool), typeof(bool) }));
            Assert.That(fields.All(f => f.IsPrivate), Is.True);
            for (int i = 0; i < fields.Length; i++)
            {
                Type[] expected = i == 5 ? new[] { typeof(SerializeField), typeof(TooltipAttribute) } : new[] { typeof(SerializeField) };
                Assert.That(fields[i].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(expected), fields[i].Name);
            }
            Assert.That(fields[5].GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("If you explicitly define navigation, but the object is off or null this will automatically find another."));

            PropertyInfo[] properties = type.GetProperties(Declared);
            Assert.That(properties.Select(p => p.Name).ToArray(), Is.EqualTo(new[] {
                "Mode", "AutoNavigateIfExplicitSelectionIsNotValid", "PointerEnterShouldTriggerNavigationEvent", "PointerExitShouldTriggerNavigationEvent",
                "SelectOnUp", "SelectOnDown", "SelectOnLeft", "SelectOnRight", "DefaultNavigation" }));
            Assert.That(properties.Select(p => p.CanWrite).ToArray(), Is.EqualTo(new[] { true, false, false, false, true, true, true, true, false }));
            Assert.That(properties.Last().GetMethod.IsStatic, Is.True);
        }

        [Test]
        public void DefaultNavigationIsAutomaticWithNoTargetsAndNoFlags()
        {
            // 0x1B54230 zeroes the struct and stores mode 3.
            UINavigation navigation = UINavigation.DefaultNavigation;
            Assert.That(navigation.Mode, Is.EqualTo(UINavigation.NavMode.Automatic));
            Assert.That((object)navigation.SelectOnUp ?? navigation.SelectOnDown ?? navigation.SelectOnLeft ?? (object)navigation.SelectOnRight, Is.Null);
            Assert.That(navigation.AutoNavigateIfExplicitSelectionIsNotValid, Is.False);
            Assert.That(navigation.PointerEnterShouldTriggerNavigationEvent, Is.False);
            Assert.That(navigation.PointerExitShouldTriggerNavigationEvent, Is.False);
            Assert.That(default(UINavigation).Mode, Is.EqualTo(UINavigation.NavMode.None));
        }

        [Test]
        public void SettersWriteOnlyTheirOwnField()
        {
            RecordingUISelectable up = NewTarget("Up"), down = NewTarget("Down"), left = NewTarget("Left"), right = NewTarget("Right");
            try
            {
                var navigation = new UINavigation { Mode = UINavigation.NavMode.Explicit, SelectOnUp = up, SelectOnDown = down, SelectOnLeft = left, SelectOnRight = right };
                Assert.That(navigation.Mode, Is.EqualTo(UINavigation.NavMode.Explicit));
                Assert.That(new object[] { navigation.SelectOnUp, navigation.SelectOnDown, navigation.SelectOnLeft, navigation.SelectOnRight },
                    Is.EqualTo(new object[] { up, down, left, right }));
            }
            finally
            {
                foreach (var target in new[] { up, down, left, right }) Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void EqualsAlsoComparesEachOfTheThreeFlags()
        {
            // 0x1B54365..0x1B543B3: after mode and the four targets, each flag
            // must match. HLNavigation has no such flags, so a copy of its
            // Equals would report these pairs equal.
            UINavigation baseline = UINavigation.DefaultNavigation;
            Assert.That(baseline.Equals(UINavigation.DefaultNavigation), Is.True);
            foreach (string flag in new[] { "m_autoNavigateIfExplicitSelectionIsNotValid", "m_pointerEnterShouldTriggerNavigationEvent", "m_pointerExitShouldTriggerNavigationEvent" })
            {
                UINavigation changed = WithFlag(baseline, flag, true);
                Assert.That(baseline.Equals(changed), Is.False, flag);
                Assert.That(changed.Equals(baseline), Is.False, flag);
                Assert.That(changed.Equals(WithFlag(baseline, flag, true)), Is.True, flag);
            }

            UINavigation otherMode = baseline;
            otherMode.Mode = UINavigation.NavMode.Vertical;
            Assert.That(baseline.Equals(otherMode), Is.False);
        }

        [Test]
        public void EqualsUsesEngineEqualityForTargets()
        {
            RecordingUISelectable live = NewTarget("Live"), destroyed = NewTarget("Destroyed");
            try
            {
                Object.DestroyImmediate(destroyed.gameObject);
                var withDestroyed = new UINavigation { SelectOnLeft = destroyed };
                var withLive = new UINavigation { SelectOnLeft = live };
                // op_Equality at 0x1B54328 treats the destroyed target as null.
                Assert.That(withDestroyed.Equals(default(UINavigation)), Is.True);
                Assert.That(withLive.Equals(default(UINavigation)), Is.False);
                Assert.That(withLive.Equals(new UINavigation { SelectOnRight = live }), Is.False);
                Assert.That(withLive.Equals(new UINavigation { SelectOnLeft = live }), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(live.gameObject);
            }
        }
    }
}
