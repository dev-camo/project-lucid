using System;
using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ProjectLucid.Tests
{
    // SplashScreen.BeginSplashScreen 0x0600062b, x86-64 0x53b690, on an owned
    // inactive component (Awake never runs, so no registry or director access).
    // Entries are built from the live Screen size so only their widths differ.
    public sealed class OriginalSplashSelectionTests
    {
        // 0x53b723 preselects entry 0; 0x53b76c seeds the previous difference with
        // float.MinValue. A matching entry (WithinTolerance 0x53b7b9) is taken when
        // (prev < diff && prev < -tol) 0x53b7e3..0x53b7fb or (tol < prev && diff < prev)
        // 0x53b7fd..0x53b811, diff = Screen.width - Width (0x53b7c7). The third
        // sequence ends on an entry wider than the screen. Catches a nearest
        // absolute-width sort (picks diff -60 in the first sequence) and a
        // largest-fitting-width rule (keeps diff 50 in the third).
        [Test]
        public void SelectionFollowsSignedWidthDifferenceSequenceNotNearestWidth()
        {
            RequireScreen(out int width, out int height);
            using (var splash = new OwnedSplash())
            {
                splash.Select(
                    Entry(splash, width, height, width - 100, "diff 100"),
                    Entry(splash, width, height, width + 60, "diff -60"),
                    Entry(splash, width, height, width - 80, "diff 80"),
                    Entry(splash, width, height * 3, width, "aspect mismatch"));
                Assert.AreEqual("diff 80", splash.Target.sprite.name);

                splash.Select(
                    Entry(splash, width, height, width - 50, "diff 50"),
                    Entry(splash, width, height, width + 10, "diff -10"),
                    Entry(splash, width, height, width - 30, "diff 30 after -10"));
                Assert.AreEqual("diff 30 after -10", splash.Target.sprite.name);

                splash.Select(
                    Entry(splash, width, height, width - 50, "diff 50"),
                    Entry(splash, width, height, width + 10, "diff -10"));
                Assert.AreEqual("diff -10", splash.Target.sprite.name);

                // A difference within tolerance stops later replacement.
                splash.Select(
                    Entry(splash, width, height, width, "diff 0"),
                    Entry(splash, width, height, width - 1, "diff 1"),
                    Entry(splash, width, height, width + 1, "diff -1"));
                Assert.AreEqual("diff 0", splash.Target.sprite.name);
            }
        }

        // With no aspect match the loop never assigns, so entry 0 is used even
        // though it does not match either. Catches a "no selection, no change" or
        // "first matching entry" fallback.
        [Test]
        public void UnmatchedAspectFallsBackToTheFirstEntry()
        {
            RequireScreen(out int width, out int height);
            using (var splash = new OwnedSplash())
            {
                splash.Select(
                    Entry(splash, width, height * 3, width, "first unmatched"),
                    Entry(splash, width * 3, height, width, "second unmatched"));
                Assert.AreEqual("first unmatched", splash.Target.sprite.name);
            }
        }

        private static void RequireScreen(out int width, out int height)
        {
            width = Screen.width;
            height = Screen.height;
            Assert.Greater(width, 1, "Selection needs a positive Screen size");
            Assert.Greater(height, 0, "Selection needs a positive Screen size");
        }

        private static SplashScreen.AspectRatioSplashScreenData Entry(OwnedSplash splash, int aspectX, int aspectY,
            int minimumWidth, string name)
        {
            return new SplashScreen.AspectRatioSplashScreenData
            {
                AspectRatio = new Vector2(aspectX, aspectY),
                Width = minimumWidth,
                Videos = new List<VideoClip>(),
                Sprites = new List<Sprite> { splash.Sprite(name) },
            };
        }

        private sealed class OwnedSplash : IDisposable
        {
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly SplashScreen splash;
            public readonly Image Target;

            public OwnedSplash()
            {
                var root = new GameObject("Owned inactive splash selection");
                owned.Add(root);
                root.SetActive(false);
                splash = root.AddComponent<SplashScreen>();
                var image = new GameObject("Owned splash target", typeof(RectTransform));
                owned.Add(image);
                image.SetActive(false);
                image.transform.SetParent(root.transform, false);
                Target = image.AddComponent<Image>();
                Set("m_targetVideoPlayers", new List<VideoPlayer>());
                Set("m_targetSplashScreens", new List<Image> { Target });
                Assert.AreEqual(0.05f, (float)typeof(SplashScreen).GetField("m_tolerance",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(splash));
            }

            public Sprite Sprite(string name)
            {
                var texture = new Texture2D(2, 2);
                owned.Add(texture);
                Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
                sprite.name = name;
                owned.Add(sprite);
                return sprite;
            }

            public void Select(params SplashScreen.AspectRatioSplashScreenData[] entries)
            {
                Target.sprite = null;
                Set("m_aspectRatioSplashScreenData", entries);
                splash.BeginSplashScreen();
                Assert.IsNotNull(Target.sprite);
                Assert.IsFalse(splash.gameObject.activeInHierarchy);
            }

            private void Set(string name, object value)
            {
                typeof(SplashScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .SetValue(splash, value);
            }

            public void Dispose()
            {
                for (int i = owned.Count - 1; i >= 0; i--)
                    if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            }
        }
    }
}
