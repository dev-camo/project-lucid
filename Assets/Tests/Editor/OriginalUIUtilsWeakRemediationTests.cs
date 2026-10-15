using System;
using System.Collections.Generic;
using Hardlight;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // Non-null forwarding cases for UIUtils helpers that OriginalUIUtilsTests
    // reaches only with null targets or an already inactive root. Owned objects
    // stay inactive except the one root whose activation is under test.
    public sealed class OriginalUIUtilsWeakRemediationTests
    {
        // 0x060005e3 (x86-64 0x1b6c0f0) reads Component.gameObject and forwards the
        // flag to GameObject.SetActive. Starting inactive and toggling both ways
        // catches a dropped or inverted forward that an inactive root hides.
        [Test]
        public void OriginalMonoBehaviourActivationForwardsBothFlagsToItsGameObject()
        {
            using (var owned = new OwnedObjects())
            {
                GameObject root = owned.Add(new GameObject("Owned original activation target", typeof(RectTransform)));
                root.SetActive(false);
                Image image = root.AddComponent<Image>();
                UIUtils.SetActive((MonoBehaviour)image, true);
                Assert.That(root.activeSelf, Is.True);
                UIUtils.SetActive((MonoBehaviour)image, false);
                Assert.That(root.activeSelf, Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // 0x060005e8 (x86-64 0x1b6ca50) tail-calls Selectable.set_interactable;
        // 0x060005e1 (0x1b6c620) calls Gradient.set_StartColour then set_EndColour.
        // Catches a constant or ignored flag and swapped gradient colours.
        [Test]
        public void OriginalInteractableAndGradientForwardEachArgumentToItsOwnSetter()
        {
            using (var owned = new OwnedObjects())
            {
                GameObject buttonRoot = owned.Add(new GameObject("Owned original interactable target", typeof(RectTransform)));
                buttonRoot.SetActive(false);
                Button button = buttonRoot.AddComponent<Button>();
                Assert.That(button.interactable, Is.True);
                UIUtils.SetInteractable((Selectable)button, false);
                Assert.That(button.interactable, Is.False);
                UIUtils.SetInteractable((Selectable)button, true);
                Assert.That(button.interactable, Is.True);

                GameObject gradientRoot = owned.Add(new GameObject("Owned original gradient target", typeof(RectTransform)));
                gradientRoot.SetActive(false);
                gradientRoot.AddComponent<Image>();
                Hardlight.Gradient gradient = gradientRoot.AddComponent<Hardlight.Gradient>();
                Color start = new Color(0.25f, 0.5f, 0.75f, 1f), end = new Color(0.125f, 0.375f, 0.625f, 0.5f);
                UIUtils.SetGradient(gradient, start, end);
                Assert.That(gradient.StartColour, Is.EqualTo(start));
                Assert.That(gradient.EndColour, Is.EqualTo(end));
                UIUtils.SetGradient(null, end, start);
                Assert.That(gradient.StartColour, Is.EqualTo(start));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // 0x060005d8 (x86-64 0x1b6bfe0) dispatches the virtual TMP_Text.text setter;
        // 0x060005da (0x1b6c1b0) does the same, then the virtual Graphic.color setter.
        // The text object stays inactive so no font or layout work runs.
        [Test]
        public void OriginalTextHelpersForwardTextThenColourToTheOwnedTextComponent()
        {
            using (var owned = new OwnedObjects())
            {
                GameObject root = owned.Add(new GameObject("Owned original text target", typeof(RectTransform)));
                root.SetActive(false);
                TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
                UIUtils.SetText(text, "owned first text");
                Assert.That(text.text, Is.EqualTo("owned first text"));
                Color color = new Color(0.5f, 0.25f, 0.125f, 0.75f);
                UIUtils.SetText(text, "owned second text", color);
                Assert.That(text.text, Is.EqualTo("owned second text"));
                Assert.That(text.color, Is.EqualTo(color));
                UIUtils.SetText(null, "unused", color);
                Assert.That(root.activeSelf, Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private sealed class OwnedObjects : IDisposable
        {
            private readonly List<Object> objects = new List<Object>();
            public T Add<T>(T value) where T : Object { objects.Add(value); return value; }
            public void Dispose()
            {
                for (int i = objects.Count - 1; i >= 0; --i)
                    if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
