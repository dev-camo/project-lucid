using System;
using System.Linq;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucid.Tests
{
    // OnScreenJoystickDebug (02000978) against its 1.10.1 x86-64 bodies (0x691E60..0x692160). Its development
    // members carry Conditional("BUILD_DEVELOPMENT"), so a direct call in a build without that symbol is removed
    // by the compiler; the cases reach them by reflection, as Unity does for the messages. DebugMenu's static
    // character-info action is taken over for the case and restored.
    public sealed class OnScreenJoystickDebugTests
    {
        private static object Call(OnScreenJoystickDebug target, string name, params object[] arguments) =>
            ModernUITestSupport.Call(typeof(OnScreenJoystickDebug), target, name, arguments);

        private static void WithDebug(Action<OnScreenJoystickDebug, Image[]> body) => GameInputScope.Run(scope =>
        {
            Action<bool> prior = DebugMenu.OnCharacterInfoToggle;
            DebugMenu.OnCharacterInfoToggle = null;
            scope.Defer(() => DebugMenu.OnCharacterInfoToggle = prior);
            var images = new Image[3];
            for (int i = 0; i < images.Length; i++)
                images[i] = scope.Inactive("Lucid owned dead zone image " + i, typeof(Image)).GetComponent<Image>();
            var debug = scope.Inactive("Lucid owned joystick debug").AddComponent<OnScreenJoystickDebug>();
            ModernUITestSupport.Set(debug, "m_verticalDeadZoneInner", images[0]); // serialized in the original
            ModernUITestSupport.Set(debug, "m_horizontalDeadZoneInner", images[1]); // serialized in the original
            ModernUITestSupport.Set(debug, "m_liveZone", images[2]); // serialized in the original
            body(debug, images);
        });

        // OnEnable 0x691E60 adds ToggleCharacterInfoShowing to DebugMenu.OnCharacterInfoToggle (Delegate.Combine into the
        // static); OnDisable 0x692010 removes it again (Delegate.Remove), leaving the action null when it was the only one.
        [Test]
        public void EnablingListensToTheCharacterInfoToggleAndDisablingStops()
        {
            WithDebug((debug, images) =>
            {
                Call(debug, "OnEnable");
                Assert.That(DebugMenu.OnCharacterInfoToggle, Is.Not.Null);
                Assert.That(DebugMenu.OnCharacterInfoToggle.GetInvocationList().Length, Is.EqualTo(1));
                Assert.That(DebugMenu.OnCharacterInfoToggle.Target, Is.SameAs(debug));

                Call(debug, "OnDisable");
                Assert.That(DebugMenu.OnCharacterInfoToggle, Is.Null);
            });
        }

        // ToggleCharacterInfoShowing 0x691FA0 and ToggleIsInputTouch 0x691FB0 only store a flag; ToggleDeadZones
        // 0x691FC0 then enables all three images exactly when both flags are set (touch tested first, 0x691FCA).
        [Test]
        public void TheDeadZoneImagesAreEnabledOnlyForTouchInputWhileTheCharacterInfoIsShowing()
        {
            WithDebug((debug, images) =>
            {
                Call(debug, "OnEnable");
                try
                {
                    foreach (Image image in images) Assert.That(image.enabled, Is.True, "enabled by default");
                    foreach ((bool touch, bool info) in new[] { (false, false), (true, false), (false, true), (true, true), (false, true), (true, false) })
                    {
                        bool[] before = images.Select(image => image.enabled).ToArray();
                        Call(debug, "ToggleIsInputTouch", touch);
                        DebugMenu.OnCharacterInfoToggle(info);
                        Assert.That(images.Select(image => image.enabled).ToArray(), Is.EqualTo(before), "the flags alone do not touch the images");
                        Call(debug, "ToggleDeadZones");
                        foreach (Image image in images) Assert.That(image.enabled, Is.EqualTo(touch && info), "touch " + touch + " info " + info);
                    }
                }
                finally
                {
                    Call(debug, "OnDisable");
                }
            });
        }
    }
}
