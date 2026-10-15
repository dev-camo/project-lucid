using System.Reflection;
using Hardlight;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // Owned UI components on inactive owners; no Awake/OnEnable, canvas or event system runs.
    public sealed class OriginalUIScrollTextAudioGapTests
    {
        // HLScrollRect.CalculateOffset (0x06000695, x86-64 0x1b70eb0): Unrestricted (+0x2c == 0)
        // returns zero; horizontal tests min.x > view.min.x first (0x1b70f84), vertical tests
        // max.y < view.max.y first (0x1b71006). Content smaller than the view violates both
        // edges on each axis, so the chosen edge is observable. Fails under symmetric ordering
        // (vertical min-first gives -23, horizontal max-first gives 25 for the first case).
        [Test]
        public void OffsetTestsMinFirstHorizontallyAndMaxFirstVertically()
        {
            GameObject owner = null;
            try
            {
                owner = new GameObject("Owned inactive HLScrollRect offset case", typeof(RectTransform));
                owner.SetActive(false);
                var view = (RectTransform)owner.transform;
                view.sizeDelta = new Vector2(100f, 100f);
                HLScrollRect scroll = owner.AddComponent<HLScrollRect>();
                Assert.That(view.rect, Is.EqualTo(new Rect(-50f, -50f, 100f, 100f)));
                typeof(ScrollRect).GetField("m_ContentBounds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(scroll, new Bounds(Vector3.zero, new Vector3(40f, 40f, 0f)));
                MethodInfo offset = typeof(HLScrollRect).GetMethod("CalculateOffset",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Assert.That(offset, Is.Not.Null);

                scroll.horizontal = true;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Elastic;
                Assert.That(offset.Invoke(scroll, new object[] { new Vector2(5f, -7f) }), Is.EqualTo(new Vector2(-35f, 37f)));
                scroll.movementType = ScrollRect.MovementType.Clamped;
                Assert.That(offset.Invoke(scroll, new object[] { Vector2.zero }), Is.EqualTo(new Vector2(-30f, 30f)));
                scroll.horizontal = false;
                Assert.That(offset.Invoke(scroll, new object[] { Vector2.zero }), Is.EqualTo(new Vector2(0f, 30f)));
                scroll.movementType = ScrollRect.MovementType.Unrestricted;
                scroll.horizontal = true;
                Assert.That(offset.Invoke(scroll, new object[] { new Vector2(5f, -7f) }), Is.EqualTo(Vector2.zero));
                Assert.That(owner.activeInHierarchy, Is.False);
            }
            finally
            {
                if (owner != null) Object.DestroyImmediate(owner);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // UIUtils text/gradient forwarding (x86-64): SetText 0x1b6bfe0 Unity-null-guards one
        // virtual text store; SetText(active) 0x1b6c060 stores the text then tail-jumps
        // SetActive(MonoBehaviour) (0x1b6c0dc), even for a null target; SetText(color)
        // 0x1b6c1b0 stores text then color; SetGradient 0x1b6c620 sets StartColour then
        // EndColour (0x1b6c68d, 0x1b6c6a5). Fails if SetText(active) skips SetActive, or if
        // any null target is dereferenced.
        [Test]
        public void TextAndGradientHelpersForwardToOwnedComponentsAndIgnoreNull()
        {
            GameObject root = null;
            try
            {
                root = new GameObject("Owned inactive UIUtils forwarding root", typeof(RectTransform));
                root.SetActive(false);
                var textObject = new GameObject("Owned text", typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
                Hardlight.Gradient gradient = textObject.AddComponent<Hardlight.Gradient>();

                UIUtils.SetText(text, "first");
                Assert.That(text.text, Is.EqualTo("first"));
                UIUtils.SetText(text, "hidden", false);
                Assert.That(text.text, Is.EqualTo("hidden"));
                Assert.That(textObject.activeSelf, Is.False);
                UIUtils.SetText(text, "shown", true);
                Assert.That(text.text, Is.EqualTo("shown"));
                Assert.That(textObject.activeSelf, Is.True);
                Assert.That(textObject.activeInHierarchy, Is.False, "owner stays inactive");
                var tint = new Color(0.25f, 0.5f, 0.75f, 1f);
                UIUtils.SetText(text, "tinted", tint);
                Assert.That(text.text, Is.EqualTo("tinted"));
                Assert.That(text.color, Is.EqualTo(tint));

                var start = new Color(1f, 0f, 0f, 0.5f);
                var end = new Color(0f, 0f, 1f, 0.25f);
                UIUtils.SetGradient(gradient, start, end);
                Assert.That(gradient.StartColour, Is.EqualTo(start));
                Assert.That(gradient.EndColour, Is.EqualTo(end));

                UIUtils.SetText(null, "ignored");
                UIUtils.SetText(null, "ignored", true);
                UIUtils.SetText(null, "ignored", tint);
                UIUtils.SetGradient(null, start, end);
                UIUtils.SetTrigger(null, "ignored");
                UIUtils.SetInteractable((HLSelectable)null, true);
                Assert.That(text.text, Is.EqualTo("tinted"));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private const float OriginalBasePitch = 1.05946f;

        // UISoundEffectManager.PlayAudioClip (x86-64 0x1b391c0): after the source null check
        // it sets pitch (0x1b3922b), then volume (0x1b3923b), then tail-jumps PlayOneShot
        // (0x1b39253) with no clip-null check, so a null clip reaches Unity's own warning.
        // Fails if a clip-null guard is added before or after the property writes.
        [Test]
        public void NullClipStillSetsPitchAndVolumeAndReachesPlayOneShot()
        {
            UISoundEffectManager originalInstance = UISoundEffectManager.Instance;
            GameObject managerObject = null;
            GameObject audioObject = null;
            AudioSource audio = null;
            try
            {
                managerObject = new GameObject("Owned inactive UI sound manager null clip case");
                managerObject.SetActive(false);
                UISoundEffectManager manager = managerObject.AddComponent<UISoundEffectManager>();
                audioObject = new GameObject("Owned muted UI sound source null clip case");
                audio = audioObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.mute = true;
                audio.pitch = 0.5f;
                audio.volume = 0.9f;
                using (var serialized = new SerializedObject(manager))
                {
                    serialized.FindProperty("m_audioSource").objectReferenceValue = audio;
                    Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                }

                LogAssert.Expect(LogType.Warning, "PlayOneShot was called with a null AudioClip.");
                manager.PlayAudioClip(null, 0.25f);
                Assert.That(audio.pitch, Is.EqualTo(OriginalBasePitch).Within(0.00001f));
                Assert.That(audio.volume, Is.EqualTo(0.25f).Within(0.000001f));
                Assert.That(audio.isPlaying, Is.False);
                Assert.That(ReferenceEquals(UISoundEffectManager.Instance, originalInstance), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                try
                {
                    if (audio != null) audio.Stop();
                }
                finally
                {
                    try
                    {
                        if (audioObject != null) Object.DestroyImmediate(audioObject);
                    }
                    finally
                    {
                        if (managerObject != null) Object.DestroyImmediate(managerObject);
                    }
                }
            }
        }
    }
}
