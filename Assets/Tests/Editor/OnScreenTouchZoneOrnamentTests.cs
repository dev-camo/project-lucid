using System;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // OnScreenTouchZoneOrnament (0200097e) against its 1.10.1 x86-64 bodies (0x692F10..0x693060). The zone is a probe on
    // an owned inactive object, driven through the inherited touch flow of a single-touch zone (a serialized flag), so
    // the movement of each frame is the difference of the touch positions. The valid touch area is a RectTransform
    // 160 wide scaled by 2 around world x 500, i.e. 320 wide in world space. Not covered: touches from the player loop.
    public sealed class OnScreenTouchZoneOrnamentTests
    {
        private static Touch TouchAt(float x, float y) =>
            new Touch { fingerId = 1, position = new Vector2(x, y), phase = TouchPhase.Moved };

        // The base field initialiser caches the CharacterManager reference, so its row is leased first.
        private static void WithZone(Action<OnScreenTouchZoneOrnamentProbe> body) => GameInputScope.Run(scope =>
        {
            scope.Row<CharacterManager>();
            var zone = scope.Inactive("Lucid owned ornament zone").AddComponent<OnScreenTouchZoneOrnamentProbe>();
            var area = scope.Inactive("Lucid owned ornament area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.sizeDelta = new Vector2(160f, 40f);
            area.localScale = new Vector3(2f, 1f, 1f);
            area.position = new Vector3(500f, 20f, 0f);
            ModernUITestSupport.Set(zone, "m_validTouchTransform", area); // serialized in the original
            ModernUITestSupport.Set(zone, "m_restrictToSingleTouch", true); // serialized in the original
            body(zone);
        });

        // The constructor 0x693060 stores NaN (0x7FC00000) in m_dragAmount; the release impulse starts at 0, which
        // ConsumeDragReleaseImpulse 0x693030 answers as zero.
        [Test]
        public void AFreshZoneReportsNoDragAndNoImpulse()
        {
            WithZone(zone =>
            {
                Assert.That(float.IsNaN(zone.DragAmount), Is.True);
                Assert.That(zone.ConsumeDragReleaseImpulse(), Is.EqualTo(0f));
            });
        }

        // Initialise 0x692F20 takes corners 0 and 2 of GetWorldCorners (x 340 and 660 here): the span is 320, not the 160
        // of the local rectangle. OnInput 0x692FE0 stores the x movement and its fraction of the span in m_dragAmount.
        [Test]
        public void TheDragAmountIsTheMovementAsAFractionOfTheWorldWidthOfTheTouchArea()
        {
            WithZone(zone =>
            {
                zone.Initialise();
                zone.StartTrackingTouch(TouchAt(80f, 20f));
                zone.SupplyTouchData();
                Assert.That(zone.DragAmount, Is.EqualTo(0.25f), "80 of 320");

                zone.UpdateTouch(TouchAt(144f, 20f));
                zone.SupplyTouchData();
                Assert.That(zone.DragAmount, Is.EqualTo(0.2f), "64 of 320");

                zone.UpdateTouch(TouchAt(100f, 90f));
                zone.SupplyTouchData();
                Assert.That(zone.DragAmount, Is.EqualTo(-0.1375f), "-44 of 320; the y movement is not used");
            });
        }

        // OnInput answers (m_dragAmount, 0) whatever the position and y movement are.
        [Test]
        public void TheInputIsTheDragAmountOnTheXAxisOnly()
        {
            WithZone(zone =>
            {
                zone.Initialise();
                Assert.That(zone.InputNow(new Vector2(9f, 9f), new Vector2(32f, 5f)), Is.EqualTo(new Vector2(0.1f, 0f)));
                Assert.That(zone.DragAmount, Is.EqualTo(0.1f));
            });
        }

        // OnTouchRelease 0x693010 keeps the last x movement as the release impulse and resets the drag amount to NaN;
        // ConsumeDragReleaseImpulse hands the impulse out once (0x693048 clears it).
        [Test]
        public void ReleasingKeepsTheLastMovementAsAnImpulseThatIsConsumedOnce()
        {
            WithZone(zone =>
            {
                zone.Initialise();
                zone.StartTrackingTouch(TouchAt(80f, 20f));
                zone.SupplyTouchData();
                zone.UpdateTouch(TouchAt(100f, 20f));
                zone.SupplyTouchData();
                Assert.That(zone.ConsumeDragReleaseImpulse(), Is.EqualTo(0f), "nothing is released while the touch is down");

                zone.StopTrackingTouch(TouchAt(100f, 20f));
                zone.SupplyTouchData();
                Assert.That(float.IsNaN(zone.DragAmount), Is.True);
                Assert.That(zone.ConsumeDragReleaseImpulse(), Is.EqualTo(20f));
                Assert.That(zone.ConsumeDragReleaseImpulse(), Is.EqualTo(0f), "consumed");
            });
        }

        // ConsumeDragReleaseImpulse returns at 0x693046 only for an impulse equal to zero; a negative impulse and a NaN
        // (unordered, so the jp at 0x693044 does not return) are cleared too.
        [Test]
        public void ANegativeOrNaNImpulseIsClearedWhenConsumed()
        {
            WithZone(zone =>
            {
                zone.Initialise();
                zone.StartTrackingTouch(TouchAt(80f, 20f));
                zone.SupplyTouchData();
                zone.UpdateTouch(TouchAt(30f, 20f));
                zone.SupplyTouchData();
                zone.StopTrackingTouch(TouchAt(30f, 20f));
                zone.SupplyTouchData();
                Assert.That(zone.ConsumeDragReleaseImpulse(), Is.EqualTo(-50f));
                Assert.That(zone.ConsumeDragReleaseImpulse(), Is.EqualTo(0f));

                zone.StartTrackingTouch(TouchAt(float.NaN, 20f));
                zone.SupplyTouchData();
                zone.StopTrackingTouch(TouchAt(0f, 0f));
                zone.SupplyTouchData();
                Assert.That(float.IsNaN(zone.ConsumeDragReleaseImpulse()), Is.True);
                Assert.That(zone.ConsumeDragReleaseImpulse(), Is.EqualTo(0f), "the NaN was cleared");
            });
        }

        // SupplyHeldGameInput 0x692FD0 is empty and OnTouchRelease does not call the base hook that would supply the
        // empty movement, so the zone never reports a held input.
        [Test]
        public void TheZoneSuppliesNoGameInput()
        {
            WithZone(zone =>
            {
                var log = new ButtonCallbackLog();
                log.Attach(zone);
                zone.Initialise();
                zone.StartTrackingTouch(TouchAt(80f, 20f));
                zone.SupplyTouchData();
                zone.SupplyNow(new Vector2(3f, 4f));
                zone.StopTrackingTouch(TouchAt(80f, 20f));
                zone.SupplyTouchData();
                Assert.That(log.Calls, Is.Empty);
            });
        }

        // Initialise runs the base Initialise inline: the tracked touches are cleared (0x692F62..0x692F7E).
        [Test]
        public void InitialiseDropsTheTrackedTouches()
        {
            WithZone(zone =>
            {
                zone.StartTrackingTouch(TouchAt(80f, 20f));
                Assert.That(zone.PointerIsDown, Is.True);
                zone.Initialise();
                Assert.That(zone.PointerIsDown, Is.False);
            });
        }
    }
}
