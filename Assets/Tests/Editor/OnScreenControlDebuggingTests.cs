using System;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // OnScreenControlDebugging (0200096f) against its 1.10.1 x86-64 bodies (0x68CBB0..0x68CFA0). The controls it
    // lists are real UserEditableUIComponentControllers on owned inactive hosts, left in their constructed state
    // (position 0, 0 and scale 0, which print identically in every culture). Not covered: the GUI drawing of
    // OnGUI (0x68CBD0) while the debug text is on, which needs an IMGUI event.
    public sealed class OnScreenControlDebuggingTests
    {
        private static OnScreenControlDebugging NewDebugging(Owned owned, params string[] saveIds)
        {
            var controls = new UserEditableUIComponentController[saveIds.Length];
            for (int i = 0; i < saveIds.Length; i++)
            {
                controls[i] = owned.Host("Lucid owned touch control " + saveIds[i], false).AddComponent<UserEditableUIComponentController>();
                Set(controls[i], "m_saveId", saveIds[i]); // serialized in the original
            }
            var debugging = owned.Host("Lucid owned control debugging", false).AddComponent<OnScreenControlDebugging>();
            Set(debugging, "m_touchControls", controls); // serialized in the original
            return debugging;
        }

        private static string Line(string saveId) => saveId + " pos: {0, 0} scale 0" + Environment.NewLine;

        // 0x68CD60 clears the builder (Length = 0), appends every control's line in array order through
        // GetPositionInfo (0x68CDE0, "{0} pos: {{{1}, {2}}} scale {3}" with AppendLine) and returns the text.
        [Test]
        public void TouchPositionInfoListsEveryControlInOrderAndRestartsEachTime()
        {
            Owned.Run(owned =>
            {
                OnScreenControlDebugging debugging = NewDebugging(owned, "jump", "attack");
                Assert.That(Call(debugging, "ShowCurrentTouchPositionInfo"), Is.EqualTo(Line("jump") + Line("attack")));
                Assert.That(Call(debugging, "ShowCurrentTouchPositionInfo"), Is.EqualTo(Line("jump") + Line("attack")), "the builder is cleared, not appended to");
            });
        }

        [Test]
        public void NoControlsGiveEmptyInfo()
        {
            Owned.Run(owned =>
            {
                OnScreenControlDebugging debugging = NewDebugging(owned);
                Assert.That(Call(debugging, "ShowCurrentTouchPositionInfo"), Is.EqualTo(string.Empty));
            });
        }

        // 0x68CDD0 xors the flag (m_debugActive, +0x38) with 1.
        [Test]
        public void ToggleFlipsTheDebugFlagEachTime()
        {
            Owned.Run(owned =>
            {
                OnScreenControlDebugging debugging = NewDebugging(owned, "jump");
                Assert.That(Get<bool>(debugging, "m_debugActive"), Is.False);
                Call(debugging, "ToggleDebugText");
                Assert.That(Get<bool>(debugging, "m_debugActive"), Is.True);
                Call(debugging, "ToggleDebugText");
                Assert.That(Get<bool>(debugging, "m_debugActive"), Is.False);
            });
        }

        // OnGUI 0x68CBD0 returns at once while the flag is off (0x68CC06), before touching the GUI skin or the
        // string builder, so it is safe outside an IMGUI event.
        [Test]
        public void OnGuiDoesNothingWhileTheDebugTextIsOff()
        {
            Owned.Run(owned =>
            {
                OnScreenControlDebugging debugging = NewDebugging(owned, "jump");
                Assert.DoesNotThrow(() => Call(debugging, "OnGUI"));
                Assert.That(Get<System.Text.StringBuilder>(debugging, "m_stringBuilder").Length, Is.EqualTo(0));
            });
        }

        // Awake 0x68CBB0 and OnDestroy 0x68CBC0 are bare returns: nothing they could touch changes.
        [Test]
        public void AwakeAndOnDestroyChangeNothing()
        {
            Owned.Run(owned =>
            {
                OnScreenControlDebugging debugging = NewDebugging(owned, "jump");
                Call(debugging, "Awake");
                Call(debugging, "OnDestroy");
                Assert.That(Get<bool>(debugging, "m_debugActive"), Is.False);
                Assert.That(Get<UserEditableUIComponentController[]>(debugging, "m_touchControls").Length, Is.EqualTo(1));
            });
        }
    }
}
