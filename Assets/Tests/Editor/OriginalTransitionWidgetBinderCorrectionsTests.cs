using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using AddToDateTimeNode = Hardlight.UI.Binding.Nodes.AddToDateTime;
using AnimationCurveNode = Hardlight.UI.Binding.Nodes.AnimationCurve;
using BindingNodes = Hardlight.UI.Binding.Nodes;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // Corrections to the restored HLBinders component-menu owners (including the AddToDateTime and
    // AnimationCurve nodes), the Game.Runtime ActorTransition_AbilityTriggered ctor args and HLModernUI
    // UIWidgetRadioButtonGroup, each grounded in the original 1.10.1 metadata or x86-64 body. Every
    // object is owned and destroyed afterwards; serialized references go in through SerializedObject,
    // and only the private Unity Start message is invoked by reflection.
    public sealed class OriginalTransitionWidgetBinderCorrectionsTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }

        private static void Invoke(object target, string method)
        {
            try { target.GetType().GetMethod(method, Members).Invoke(target, null); }
            catch (TargetInvocationException failure) when (failure.InnerException != null) { throw failure.InnerException; }
        }

        // Exposes the protected nested ctor args type; never constructed itself.
        private sealed class AbilityTriggeredArgsProbe : ActorTransition_AbilityTriggered
        {
            private AbilityTriggeredArgsProbe() : base(null, default, null) { }

            internal static bool NewIsTriggered() => new AbilityTriggeredJSONCtorArgs().IsTriggered;
        }

        // Both nodes' AddComponentMenu CAD blob holds a zero-length menu string, not a null one.
        // Catches [AddComponentMenu(null)], which encodes 0xFF and reads back as null.
        [Test]
        public void BinderNodesHideFromTheComponentMenuWithAnEmptyString()
        {
            foreach (Type node in new[] { typeof(AnimationCurveNode), typeof(AddToDateTimeNode) })
            {
                CustomAttributeData menu = node.GetCustomAttributesData().Single(a => a.AttributeType == typeof(AddComponentMenu));
                Assert.That(menu.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(string)), node.Name);
                Assert.That(menu.ConstructorArguments[0].Value, Is.Not.Null.And.EqualTo(string.Empty), node.Name);
                Assert.That(node.GetCustomAttribute<AddComponentMenu>(false).componentMenu, Is.EqualTo(string.Empty), node.Name);
            }
        }

        // The 45 restored HLBinders.Runtime owners below (tokens 0200000c, 0200000e, 0200001e-0200003d,
        // 02000040, 02000055-0200005c, 0200006d-0200006e) pass AddComponentMenu a zero-length string.
        // For the first 38 the IL2CPP metadata blob of ctor 0xc753 encodes it as 0e 00 (a null string
        // would be 0e 01, a compressed -1); BinderInput's raw blob is
        // 0253c70000645c01000100000e000200000e0a496e7075740800. BinderOutput (0200000e), the TMP_Gradient
        // owners (0200002c, 0200003a, 0200005a), ConstantValue (0200003b) and LogicalToggle/LogicalToggle3
        // (0200005b, 0200005c) carry the DummyDll blob 0100000000 (a null string would be 0100ff0000).
        // Catches [AddComponentMenu(null)].
        [Test]
        public void BinderOwnersHideFromTheComponentMenuWithAnEmptyString()
        {
            Type[] owners =
            {
                typeof(Hardlight.UI.Binding.BinderInput),
                typeof(Hardlight.UI.Binding.BinderOutput),
                typeof(BindingNodes.Conditional<>),
                typeof(BindingNodes.ConditionalAnimation),
                typeof(BindingNodes.ConditionalAudio),
                typeof(BindingNodes.ConditionalBool),
                typeof(BindingNodes.ConditionalColour),
                typeof(BindingNodes.ConditionalDateTime),
                typeof(BindingNodes.ConditionalEnum),
                typeof(BindingNodes.ConditionalFloat),
                typeof(BindingNodes.ConditionalInt),
                typeof(BindingNodes.ConditionalLong),
                typeof(BindingNodes.ConditionalMaterial),
                typeof(BindingNodes.ConditionalSprite),
                typeof(BindingNodes.ConditionalString),
                typeof(BindingNodes.ConditionalTMPGradient),
                typeof(BindingNodes.ConditionalTimeSpan),
                typeof(BindingNodes.ConditionalVector2),
                typeof(BindingNodes.ConditionalVector3),
                typeof(BindingNodes.Constant<>),
                typeof(BindingNodes.ConstantAnimation),
                typeof(BindingNodes.ConstantBool),
                typeof(BindingNodes.ConstantColour),
                typeof(BindingNodes.ConstantFloat),
                typeof(BindingNodes.ConstantInt),
                typeof(BindingNodes.ConstantLong),
                typeof(BindingNodes.ConstantMaterial),
                typeof(BindingNodes.ConstantNull),
                typeof(BindingNodes.ConstantSprite),
                typeof(BindingNodes.ConstantString),
                typeof(BindingNodes.ConstantTMPGradient),
                typeof(BindingNodes.ConstantValue),
                typeof(BindingNodes.ConstantVector2),
                typeof(BindingNodes.ConstantVector3),
                typeof(BindingNodes.DebugLogNode),
                typeof(BindingNodes.LogicalSwitch<>),
                typeof(BindingNodes.LogicalSwitchColour),
                typeof(BindingNodes.LogicalSwitchInt),
                typeof(BindingNodes.LogicalSwitchMaterial),
                typeof(BindingNodes.LogicalSwitchSprite),
                typeof(BindingNodes.LogicalSwitchTMPGradient),
                typeof(BindingNodes.LogicalToggle),
                typeof(BindingNodes.LogicalToggle3),
                typeof(BindingNodes.NumberConditional<>),
                typeof(BindingNodes.NumberConditionalColour),
            };
            foreach (Type owner in owners)
            {
                CustomAttributeData menu = owner.GetCustomAttributesData().Single(a => a.AttributeType == typeof(AddComponentMenu));
                Assert.That(menu.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(string)), owner.Name);
                Assert.That(menu.ConstructorArguments[0].Value, Is.Not.Null.And.EqualTo(string.Empty), owner.Name);
            }
        }

        // AbilityTriggeredJSONCtorArgs..ctor (0x5299B0) stores IsTriggered = 1 before Object..ctor.
        // Catches a missing initializer, which leaves a directly constructed instance false. This covers
        // direct construction only; JsonUtility.FromJson behaviour for omitted IsTriggered is an engine
        // contract still pending native observation, so no test asserts it.
        [Test]
        public void AbilityTriggeredArgsDefaultToTriggered()
        {
            Assert.That(AbilityTriggeredArgsProbe.NewIsTriggered(), Is.True);
        }

        private UIWidgetRadioButton Button(GameObject group, string name, out Image image, out Sprite on, out Sprite off)
        {
            var child = new GameObject("Lucid radio " + name);
            child.transform.SetParent(group.transform, false);
            var button = child.AddComponent<UIWidgetRadioButton>();
            // The image lives outside the button and stays inactive, so it survives the button's destruction.
            var imageObject = Own(new GameObject("Lucid radio " + name + " image", typeof(RectTransform)));
            imageObject.SetActive(false);
            image = imageObject.AddComponent<Image>();
            Texture2D texture = Own(new Texture2D(4, 4, TextureFormat.RGBA32, false));
            on = Own(Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero));
            off = Own(Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.zero));
            using (var serialized = new SerializedObject(button))
            {
                serialized.FindProperty("m_buttonImage").objectReferenceValue = image;
                serialized.FindProperty("m_onSprite").objectReferenceValue = on;
                serialized.FindProperty("m_offSprite").objectReferenceValue = off;
                Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
            }
            return button;
        }

        // DoSingleToggle (0x1A49BD0) compares each child with the incoming button by UnityEngine.Object
        // equality and on a match selects the incoming button (0x1A49C71), never the child; only
        // mismatches deselect the child. The routes differ only when a destroyed child equals a null
        // receiver: the original dereferences the null receiver and leaves the destroyed child's image
        // alone. Catches child.Select(), which instead selects the destroyed child without a fault.
        [Test]
        public void SingleToggleSelectsTheIncomingButtonNotTheEqualChild()
        {
            var root = Own(new GameObject("Lucid radio group"));
            var group = root.AddComponent<UIWidgetRadioButtonGroup>();
            UIWidgetRadioButton first = Button(root, "first", out Image firstImage, out Sprite firstOn, out Sprite firstOff);
            UIWidgetRadioButton second = Button(root, "second", out Image secondImage, out Sprite secondOn, out Sprite secondOff);
            Invoke(group, "Start");

            // Control: a live click selects the clicked button and deselects the other.
            first.Action_OnClick();
            Assert.That(firstImage.sprite, Is.SameAs(firstOn));
            Assert.That(secondImage.sprite, Is.SameAs(secondOff));

            Object.DestroyImmediate(second);
            Assert.That(second == null, Is.True, "a destroyed child equals null");
            Assert.Throws<NullReferenceException>(() => first.OnToggle(null));
            Assert.That(firstImage.sprite, Is.SameAs(firstOff), "the live child was deselected first");
            Assert.That(secondImage.sprite, Is.SameAs(secondOff), "the destroyed child was not selected");
        }
    }
}
