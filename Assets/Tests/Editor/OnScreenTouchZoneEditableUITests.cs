using System;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // OnScreenTouchZoneEditableUI (0200097d) against its 1.10.1 x86-64 bodies (0x692C70..0x692F00). The zone is a probe on
    // an owned inactive object; Awake is called directly because Edit Mode sends no messages. The editable component
    // and the valid touch area are RectTransforms on owned inactive objects. Not covered: touches from the player loop,
    // and the empty TransformUpdated.
    public sealed class OnScreenTouchZoneEditableUITests
    {
        private static Touch TouchAt(int fingerId, float x, float y) =>
            new Touch { fingerId = fingerId, position = new Vector2(x, y), phase = TouchPhase.Moved };

        // The base field initialiser caches the CharacterManager reference, so its row is leased first.
        private static void WithZone(Action<GameInputScope, OnScreenTouchZoneEditableUIProbe, RectTransform> body) => GameInputScope.Run(scope =>
        {
            scope.Row<CharacterManager>();
            var zone = scope.Inactive("Lucid owned editable ui zone").AddComponent<OnScreenTouchZoneEditableUIProbe>();
            var component = scope.Inactive("Lucid owned editable component", typeof(RectTransform)).GetComponent<RectTransform>();
            ModernUITestSupport.Set(zone, "m_editableComponentTransform", component); // serialized in the original
            body(scope, zone, component);
        });

        // Awake 0x692C70 copies the serialized initial flag into the active flag; IsActive 0x692DC0 is the object's
        // activeInHierarchy and that flag, both needed.
        [Test]
        public void AwakeStartsActiveOnlyWhenInitiallyActiveAndTheZoneNeedsTheObjectActiveToo()
        {
            WithZone((scope, zone, component) =>
            {
                Assert.That(zone.IsActive(), Is.False, "the object is inactive");
                zone.gameObject.SetActive(true);
                ModernUITestSupport.Call(typeof(OnScreenTouchZoneEditableUI), zone, "Awake");
                Assert.That(zone.IsActive(), Is.False, "not initially active");

                ModernUITestSupport.Set(zone, "m_initiallyActive", true); // serialized in the original
                ModernUITestSupport.Call(typeof(OnScreenTouchZoneEditableUI), zone, "Awake");
                Assert.That(zone.IsActive(), Is.True);

                zone.gameObject.SetActive(false);
                Assert.That(zone.IsActive(), Is.False, "the flag alone is not enough");
            });
        }

        // SetActive 0x692D00 stores the flag; with the object active that decides IsActive.
        [Test]
        public void SetActiveDecidesTheFlagOfAnActiveObject()
        {
            WithZone((scope, zone, component) =>
            {
                zone.gameObject.SetActive(true);
                zone.SetActive(true);
                Assert.That(zone.IsActive(), Is.True);
                zone.SetActive(false);
                Assert.That(zone.IsActive(), Is.False);
            });
        }

        // SetEditing 0x692DA0 stores the argument in the active flag and turns multi-touch on (+0x60) without clearing
        // the tracked touches. CanAcceptNewTouch (0x68E740) takes a second touch only with multi-touch on.
        [Test]
        public void EditingSetsTheActiveFlagAndTurnsMultiTouchOnWithoutDroppingTouches()
        {
            WithZone((scope, zone, component) =>
            {
                zone.gameObject.SetActive(true);
                zone.SetActive(true);
                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                Assert.That(zone.CanAcceptNewTouch(), Is.False, "one touch, multi-touch still off");

                zone.SetEditing(false);
                Assert.That(zone.IsActive(), Is.False, "the argument is the active flag");
                Assert.That(zone.PointerIsDown, Is.True, "the touch is kept");
                zone.SetActive(true);
                Assert.That(zone.CanAcceptNewTouch(), Is.True, "multi-touch stayed on");
            });
        }

        // GetRectTransform 0x692CF0 answers the serialized editable component.
        [Test]
        public void TheRectTransformIsTheEditableComponent()
        {
            WithZone((scope, zone, component) => Assert.That(zone.GetRectTransform(), Is.SameAs(component)));
        }

        // OnInput 0x692CA0 stores the touch position and answers Vector2.zero; GetTouchPosition 0x692D10 copies the stored
        // position out whatever the touch state and answers whether any touch is tracked.
        [Test]
        public void TheTouchPositionIsKeptAndReportedWithWhetherATouchIsTracked()
        {
            WithZone((scope, zone, component) =>
            {
                Assert.That(zone.GetTouchPosition(out Vector2 touch), Is.False);
                Assert.That(touch, Is.EqualTo(Vector2.zero));

                Assert.That(zone.InputNow(new Vector2(12f, 34f), new Vector2(5f, 6f)), Is.EqualTo(Vector2.zero));
                Assert.That(zone.GetTouchPosition(out touch), Is.False, "no touch is tracked");
                Assert.That(touch, Is.EqualTo(new Vector2(12f, 34f)));

                ModernUITestSupport.Set(zone, "m_restrictToSingleTouch", true); // serialized in the original
                zone.StartTrackingTouch(TouchAt(1, 70f, 80f));
                zone.SupplyTouchData();
                Assert.That(zone.GetTouchPosition(out touch), Is.True);
                Assert.That(touch, Is.EqualTo(new Vector2(70f, 80f)), "the zone stored the touch it was supplied");
            });
        }

        // GetPinchInfo 0x692D60 answers whether a pinch is in progress and gives both distances, which read 0 otherwise.
        // Two touches 30 and 40 apart span a diagonal of 50, the minimum pinch distance; moving one to (60, 80) makes the
        // diagonal 100 while the initial distance of the first pass stays 50.
        [Test]
        public void PinchInfoIsTheCurrentAndInitialDistanceOnlyWhileAPinchIsInProgress()
        {
            WithZone((scope, zone, component) =>
            {
                Assert.That(zone.GetPinchInfo(out float pinch, out float initial), Is.False);
                Assert.That((pinch, initial), Is.EqualTo((0f, 0f)));

                zone.StartTrackingTouch(TouchAt(1, 0f, 0f));
                zone.StartTrackingTouch(TouchAt(2, 30f, 40f));
                zone.SupplyTouchData();
                Assert.That(zone.GetPinchInfo(out pinch, out initial), Is.True);
                Assert.That((pinch, initial), Is.EqualTo((50f, 50f)));

                zone.UpdateTouch(TouchAt(2, 60f, 80f));
                zone.SupplyTouchData();
                Assert.That(zone.GetPinchInfo(out pinch, out initial), Is.True);
                Assert.That((pinch, initial), Is.EqualTo((100f, 50f)));

                zone.StopTrackingTouch(TouchAt(2, 0f, 0f));
                zone.SupplyTouchData();
                Assert.That(zone.GetPinchInfo(out pinch, out initial), Is.False, "a single touch ends the pinch");
                Assert.That((pinch, initial), Is.EqualTo((0f, 0f)));
            });
        }

        // GetScoreForTouchPosition 0x692E00: 0 for a position on the component or while a touch is tracked, otherwise the
        // squared distance from the touch to the centre of the component's rectangle, in the component's local space. The
        // component here is 100 by 50 with its pivot at the lower left, placed at world (200, 0): its rectangle centre is
        // local (50, 25), and the position (100, 100) is local (-100, 100), so the squared distance is 150^2 + 75^2.
        [Test]
        public void TheScoreIsZeroOnTheComponentOrWithATouchAndOtherwiseTheSquaredDistanceToItsCentre()
        {
            WithZone((scope, zone, component) =>
            {
                component.sizeDelta = new Vector2(100f, 50f);
                component.pivot = Vector2.zero;
                component.position = new Vector3(200f, 0f, 0f);

                Assert.That(zone.ScoreNow(new Vector2(250f, 20f)), Is.EqualTo(0f), "on the component");
                Assert.That(zone.ScoreNow(new Vector2(100f, 100f)), Is.EqualTo(28125f).Within(0.01f));

                zone.StartTrackingTouch(TouchAt(1, 5f, 5f));
                Assert.That(zone.ScoreNow(new Vector2(100f, 100f)), Is.EqualTo(0f), "a tracked touch");
            });
        }

        // The zone supplies no game input (SupplyHeldGameInput 0x692C90 is empty).
        [Test]
        public void TheZoneSuppliesNoGameInput()
        {
            WithZone((scope, zone, component) =>
            {
                var log = new ButtonCallbackLog();
                log.Attach(zone);
                ModernUITestSupport.Set(zone, "m_restrictToSingleTouch", true); // serialized in the original
                zone.StartTrackingTouch(TouchAt(1, 70f, 80f));
                zone.SupplyTouchData();
                zone.SupplyNow(new Vector2(1f, 2f));
                zone.StopTrackingTouch(TouchAt(1, 0f, 0f));
                zone.SupplyTouchData();
                Assert.That(log.Calls, Is.Empty);
            });
        }
    }
}
