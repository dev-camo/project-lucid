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
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // Corrections to the restored HLBinders AddToDateTime and AnimationCurve nodes, the Game.Runtime
    // ActorTransition_AbilityTriggered ctor args and HLModernUI UIWidgetRadioButtonGroup, each grounded
    // in the original 1.10.1 metadata or x86-64 body. Every object is owned and destroyed afterwards;
    // serialized references go in through SerializedObject, and only the private Unity Start message is
    // invoked by reflection.
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

            // The ConstructInstance (0x529800) parse, without the DataManager lookup the ctor needs.
            internal static bool JsonIsTriggered(string json) => JsonUtility.FromJson<AbilityTriggeredJSONCtorArgs>(json).IsTriggered;
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

        // AbilityTriggeredJSONCtorArgs..ctor (0x5299B0) stores IsTriggered = 1 before Object..ctor.
        // Catches a missing initializer, which leaves a fresh instance false.
        [Test]
        public void AbilityTriggeredArgsDefaultToTriggered()
        {
            Assert.That(AbilityTriggeredArgsProbe.NewIsTriggered(), Is.True);
        }

        // ConstructInstance parses its args with JsonUtility.FromJson, so JSON that omits IsTriggered
        // keeps the constructor's true and an explicit false still reads false.
        [Test]
        public void AbilityTriggeredJsonWithoutIsTriggeredKeepsTheTrueDefault()
        {
            Assert.That(AbilityTriggeredArgsProbe.JsonIsTriggered("{\"AbilityDefinitionName\":\"Probe\"}"), Is.True);
            Assert.That(AbilityTriggeredArgsProbe.JsonIsTriggered("{\"AbilityDefinitionName\":\"Probe\",\"IsTriggered\":false}"), Is.False);
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
