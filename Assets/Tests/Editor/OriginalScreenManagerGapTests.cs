using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Original ScreenManager orientation-lock flags on an owned inactive
    // component. Only the four Screen autorotate flags are written; they are
    // captured first and restored. No Awake, registry or property store runs.
    public sealed class OriginalScreenManagerGapTests
    {
        // ToggleOrientationLock 0x06000dc7 (x86-64 0x1b12f50) returns when the
        // user lock (+0x30) is set, otherwise inlines ControlOrientationLock
        // 0x06000dc9 (x86-64 0x1b12860): IsLandscape(DeviceOrientation +0x54)
        // is orientation 3 or 4 (0x1ab3990); both landscape flags take
        // (!setLock | landscape), both portrait flags take !(landscape & setLock).
        // Fails if the landscape branch is inverted, if setLock is ignored, if
        // a flag pair is left unwritten, or if Toggle sets the user lock.
        [Test]
        public void UnlockedToggleWritesLockFlagsFromTheDeviceOrientation()
        {
            var rows = new[]
            {
                // orientation, setLock, landscape flags, portrait flags
                new object[] { ScreenOrientation.Portrait, true, false, true },
                new object[] { ScreenOrientation.Portrait, false, true, true },
                new object[] { ScreenOrientation.PortraitUpsideDown, true, false, true },
                new object[] { ScreenOrientation.LandscapeLeft, true, true, false },
                new object[] { ScreenOrientation.LandscapeLeft, false, true, true },
                new object[] { ScreenOrientation.LandscapeRight, true, true, false },
                new object[] { ScreenOrientation.LandscapeRight, false, true, true },
                new object[] { ScreenOrientation.AutoRotation, true, false, true },
            };
            bool[] saved = Flags();
            Assert.That(ScreenManager.Instance, Is.Null, "A foreign singleton cannot be replaced.");
            var host = new GameObject("Owned original ScreenManager lock flags");
            host.SetActive(false);
            try
            {
                ScreenManager manager = host.AddComponent<ScreenManager>();
                MethodInfo setDevice = typeof(ScreenManager).GetProperty("DeviceOrientation").GetSetMethod(true);
                foreach (object[] row in rows)
                {
                    var orientation = (ScreenOrientation)row[0];
                    bool setLock = (bool)row[1], landscape = (bool)row[2], portrait = (bool)row[3];
                    // Original private setter 0x06000db9.
                    setDevice.Invoke(manager, new object[] { orientation });
                    SetFlags(!landscape, !landscape, !portrait, !portrait);
                    manager.ToggleOrientationLock(setLock);
                    string label = orientation + " lock=" + setLock;
                    Assert.That(Flags(), Is.EqualTo(new[] { landscape, landscape, portrait, portrait }), label);
                    Assert.That(manager.IsOrientationLocked(), Is.False, label);
                }
                Assert.That(ScreenManager.Instance, Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(host);
                SetFlags(saved[0], saved[1], saved[2], saved[3]);
            }
        }

        private static bool[] Flags() => new[]
        {
            Screen.autorotateToLandscapeLeft, Screen.autorotateToLandscapeRight,
            Screen.autorotateToPortrait, Screen.autorotateToPortraitUpsideDown
        };

        private static void SetFlags(bool landscapeLeft, bool landscapeRight, bool portrait, bool portraitUpsideDown)
        {
            Screen.autorotateToLandscapeLeft = landscapeLeft;
            Screen.autorotateToLandscapeRight = landscapeRight;
            Screen.autorotateToPortrait = portrait;
            Screen.autorotateToPortraitUpsideDown = portraitUpsideDown;
        }
    }
}
