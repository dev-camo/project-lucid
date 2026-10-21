using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.OnScreenControlTestServices;

namespace ProjectLucid.Tests
{
    // OnScreenTouchZone (0200097c) against its 1.10.1 x86-64 bodies (0x68E1E0..0x692C70). The zone is a probe that
    // runs every inherited body and records the hooks it calls, on an owned inactive object. Serialized fields
    // (restrict to single touch, the ignore list, the minimum pinch distance, the touch area) are set directly,
    // as the inspector would. Not covered: the position and phase of real touches, which need the player loop.
    public sealed class OnScreenTouchZoneTests
    {
        // GetTotalFixedTime (060002c6) returns its cached total unless that is zero, and only then asks the TimeManager
        // reference that an awake character caches; this character never wakes, so the cache is filled directly.
        private static void SetTotalFixedTime(Character character) =>
            ModernUITestSupport.Set(character, "m_totalFixedTime", 10f); // Reflection: the cache is private

        private static readonly Vector2 Zero = Vector2.zero;

        private static Touch TouchAt(int fingerId, float x, float y) =>
            new Touch { fingerId = fingerId, position = new Vector2(x, y), phase = TouchPhase.Moved };

        // The field initialiser caches the CharacterManager reference, so its row is leased first.
        private static void WithZone(Action<GameInputScope, OnScreenTouchZoneProbe> body) => GameInputScope.Run(scope =>
        {
            scope.Row<CharacterManager>();
            body(scope, scope.Inactive("Lucid owned touch zone").AddComponent<OnScreenTouchZoneProbe>());
        });

        private static void SingleTouch(OnScreenTouchZoneProbe zone) => ModernUITestSupport.Set(zone, "m_restrictToSingleTouch", true);

        // ProcessTouch 0x690CC0 calls OnInput (slot 17), then SupplyHeldGameInput (slot 15) with its answer, then
        // sets the pinch flag from `ucomiss 0, pinchDistance; jae`: the distance must exceed zero or be unordered.
        // A pinch also reaches SupplyPinchInput (slot 16) with both distances. The rows separate "> 0" from
        // ">= 0" (zero) and from a NaN-rejecting comparison.
        [Test]
        public void ProcessTouchSuppliesTheInputThenAPinchOnlyForADistanceAboveZero()
        {
            WithZone((scope, zone) =>
            {
                zone.InputResult = new Vector2(4f, -3f);
                foreach ((float pinch, bool pinching) in new[]
                {
                    (50f, true), (0.001f, true), (float.PositiveInfinity, true), (float.NaN, true),
                    (0f, false), (-0f, false), (-1f, false), (float.NegativeInfinity, false),
                })
                {
                    zone.Order.Clear();
                    zone.Pinches.Clear();
                    zone.HeldInputs.Clear();
                    zone.Inputs.Clear();

                    zone.ProcessNow(new Vector2(7f, 8f), new Vector2(1f, 2f), pinch, 25f);

                    Assert.That(zone.Pinching, Is.EqualTo(pinching), "pinch " + pinch);
                    Assert.That(zone.Order, Is.EqualTo(pinching ? new[] { "process", "input", "held", "pinch" } : new[] { "process", "input", "held" }), "pinch " + pinch);
                    Assert.That(zone.Inputs, Is.EqualTo(new[] { (new Vector2(7f, 8f), new Vector2(1f, 2f)) }));
                    Assert.That(zone.HeldInputs, Is.EqualTo(new[] { new Vector2(4f, -3f) }), "the input answer is held");
                    if (!pinching) continue;
                    Assert.That(zone.Pinches.Count, Is.EqualTo(1));
                    if (float.IsNaN(pinch)) Assert.That(float.IsNaN(zone.Pinches[0].pinch), Is.True);
                    else Assert.That(zone.Pinches[0].pinch, Is.EqualTo(pinch));
                    Assert.That(zone.Pinches[0].initial, Is.EqualTo(25f));
                }
            });
        }

        // OnTouchRelease 0x690A90 supplies the held input with Vector2.zero (the static at +0xB8 of Vector2's statics).
        [Test]
        public void ReleasingSuppliesZeroMovement()
        {
            WithZone((scope, zone) =>
            {
                zone.InputResult = new Vector2(9f, 9f);
                zone.ReleaseNow();
                Assert.That(zone.HeldInputs, Is.EqualTo(new[] { Zero }));
                Assert.That(zone.Order, Is.EqualTo(new[] { "release", "held" }), "the answer of OnInput is not used");
            });
        }

        // TouchPositionValid 0x68E7B0 writes float.MaxValue (0x7F7FFFFF) to the score, then asks
        // RectTransformUtility.RectangleContainsScreenPoint (2D rectangle of 100 by 100 around the origin here); only
        // a contained position asks GetScoreForTouchPosition (slot 14), whose base answer is 0 (0x6929B0).
        [Test]
        public void ScoreIsTheWorstValueUnlessThePositionIsInsideTheTouchArea()
        {
            WithZone((scope, zone) =>
            {
                var area = scope.Inactive("Lucid owned touch area", typeof(RectTransform)).GetComponent<RectTransform>();
                area.sizeDelta = new Vector2(100f, 100f);
                ModernUITestSupport.Set(zone, "m_validTouchTransform", area);

                Assert.That(zone.TouchPositionValid(new Vector2(10f, 10f), out float score), Is.True);
                Assert.That(score, Is.EqualTo(0f), "the base score");
                zone.ScoreOverride = 7.5f;
                Assert.That(zone.TouchPositionValid(new Vector2(-49f, 49f), out score), Is.True);
                Assert.That(score, Is.EqualTo(7.5f));
                Assert.That(zone.TouchPositionValid(new Vector2(80f, 0f), out score), Is.False);
                Assert.That(score, Is.EqualTo(3.4028235E+38f), "the score hook is not asked");
            });
        }

        // IsActive 0x6929D0 is the game object's activeInHierarchy.
        [Test]
        public void ZoneIsActiveWhileItsObjectIsActiveInTheHierarchy()
        {
            WithZone((scope, zone) =>
            {
                Assert.That(zone.IsActive(), Is.False);
                zone.gameObject.SetActive(true);
                Assert.That(zone.IsActive(), Is.True);
                zone.gameObject.SetActive(false);
                Assert.That(zone.IsActive(), Is.False);
            });
        }

        // StartTrackingTouch 0x68E850 adds only while the peak count (+0x88) equals the tracked count, and
        // StopTrackingTouch 0x68E3B0 resets the peak when the last touch goes. So after one of two touches leaves,
        // no new touch is taken until the other leaves too.
        [Test]
        public void NewTouchesAreTrackedByFingerAndOnlyWhileTheZoneIsAtItsPeak()
        {
            WithZone((scope, zone) =>
            {
                Assert.That(zone.PointerIsDown, Is.False);
                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                Assert.That((zone.IsTrackingTouch(TouchAt(1, 0f, 0f)), zone.IsTrackingTouch(TouchAt(2, 10f, 10f))), Is.EqualTo((true, false)), "tracked by finger id");
                zone.StartTrackingTouch(TouchAt(2, 20f, 20f));
                zone.StopTrackingTouch(TouchAt(1, 0f, 0f));
                Assert.That((zone.IsTrackingTouch(TouchAt(1, 0f, 0f)), zone.IsTrackingTouch(TouchAt(2, 0f, 0f))), Is.EqualTo((false, true)));

                zone.StartTrackingTouch(TouchAt(3, 30f, 30f));
                Assert.That(zone.IsTrackingTouch(TouchAt(3, 0f, 0f)), Is.False, "below the peak of two touches");

                zone.StopTrackingTouch(TouchAt(9, 0f, 0f));
                Assert.That(zone.IsTrackingTouch(TouchAt(2, 0f, 0f)), Is.True, "stopping an unknown finger changes nothing");
                zone.StopTrackingTouch(TouchAt(2, 0f, 0f));
                Assert.That(zone.PointerIsDown, Is.False);
                zone.StartTrackingTouch(TouchAt(3, 30f, 30f));
                Assert.That(zone.IsTrackingTouch(TouchAt(3, 0f, 0f)), Is.True, "the peak was reset");
            });
        }

        // The peak is not consulted when the zone is for a single touch (IsSingleTouch, slot 19, tested first).
        [Test]
        public void SingleTouchZonesTakeNewTouchesBelowTheirPeakToo()
        {
            foreach (bool singleTouch in new[] { false, true })
            {
                WithZone((scope, zone) =>
                {
                    if (singleTouch) SingleTouch(zone);
                    zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                    zone.StartTrackingTouch(TouchAt(2, 20f, 20f));
                    zone.StopTrackingTouch(TouchAt(1, 0f, 0f));
                    zone.StartTrackingTouch(TouchAt(3, 30f, 30f));
                    Assert.That(zone.IsTrackingTouch(TouchAt(3, 0f, 0f)), Is.EqualTo(singleTouch), "single touch " + singleTouch);
                });
            }
        }

        // UpdateTouch 0x68E550 replaces the tracked touch of the same finger under the same peak rule, and does
        // nothing for a finger that is not tracked.
        [Test]
        public void UpdatingATouchReplacesOnlyATrackedFingersPosition()
        {
            WithZone((scope, zone) =>
            {
                SingleTouch(zone);
                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                zone.UpdateTouch(TouchAt(1, 20f, 30f));
                zone.UpdateTouch(TouchAt(2, 99f, 99f));
                Assert.That(zone.IsTrackingTouch(TouchAt(2, 0f, 0f)), Is.False, "an untracked finger is not added");

                zone.SupplyTouchData();
                Assert.That(zone.Processed[0].position, Is.EqualTo(new Vector2(20f, 30f)));
            });
        }

        // CanAcceptNewTouch 0x68E740: an inactive zone never accepts; an idle one always does; one that tracks
        // a touch accepts only with multi-touch enabled (+0x60, off by default).
        [Test]
        public void ANewTouchNeedsAnActiveZoneAndMultiTouchOnceOneIsTracked()
        {
            foreach ((bool active, bool tracking, bool multi, bool accepts) in new[]
            {
                (false, false, false, false), (false, false, true, false), (false, true, false, false), (false, true, true, false),
                (true, false, false, true), (true, false, true, true), (true, true, false, false), (true, true, true, true),
            })
            {
                WithZone((scope, zone) =>
                {
                    Assert.That(zone.MultiTouch, Is.False, "off by default");
                    zone.ActiveOverride = active;
                    zone.MultiTouch = multi;
                    if (tracking) zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                    Assert.That(zone.CanAcceptNewTouch(), Is.EqualTo(accepts), "active " + active + " tracking " + tracking + " multi " + multi);
                });
            }
        }

        // SetMultiTouchEnabled 0x692A00 does nothing for the current value; only turning it off drops the touches.
        [Test]
        public void TurningMultiTouchOffDropsEveryTouchButTurningItOnKeepsThem()
        {
            WithZone((scope, zone) =>
            {
                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                zone.SetMultiTouchEnabled(false);
                Assert.That(zone.IsTrackingTouch(TouchAt(1, 0f, 0f)), Is.True, "already off");
                zone.SetMultiTouchEnabled(true);
                Assert.That((zone.MultiTouch, zone.IsTrackingTouch(TouchAt(1, 0f, 0f))), Is.EqualTo((true, true)));
                zone.SetMultiTouchEnabled(true);
                Assert.That(zone.IsTrackingTouch(TouchAt(1, 0f, 0f)), Is.True, "already on");
                zone.SetMultiTouchEnabled(false);
                Assert.That((zone.MultiTouch, zone.PointerIsDown), Is.EqualTo((false, false)));
            });
        }

        // Initialise 0x690920 and ClearAllTouches 0x68E1E0 drop the touches. Shutdown 0x690A30 does the same and
        // reports the empty state through SupplyTouchData (which releases a pointer that was down) only for a valid
        // input type.
        [Test]
        public void InitialiseAndShutdownClearTheTouchesAndShutdownReportsOnlyForAValidInputType()
        {
            WithZone((scope, zone) =>
            {
                SingleTouch(zone);
                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                zone.Initialise();
                Assert.That(zone.PointerIsDown, Is.False);

                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                zone.ClearAllTouches();
                Assert.That(zone.PointerIsDown, Is.False);

                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                zone.SupplyTouchData();
                zone.Order.Clear();
                zone.Shutdown(false);
                Assert.That(zone.PointerIsDown, Is.False);
                Assert.That(zone.Order, Is.Empty, "an invalid input type reports nothing");
                Assert.That(zone.WasDown, Is.True);

                zone.Shutdown(true);
                Assert.That(zone.Order, Is.EqualTo(new[] { "release", "held" }), "the pointer that was down is released");
                Assert.That(zone.WasDown, Is.False);
            });
        }

        // SupplyTouchData 0x68E9B0 / SupplySingleTouch 0x692B70: the touch position goes to ProcessTouch with the
        // movement since the last supply (the previous position starts at zero) and no pinch; the initial pinch
        // distance stays 0. Releasing supplies a release once and then nothing.
        [Test]
        public void ASingleTouchSuppliesItsPositionAndMovementThenReleasesOnce()
        {
            WithZone((scope, zone) =>
            {
                SingleTouch(zone);
                zone.InputResult = new Vector2(5f, 6f);
                zone.StartTrackingTouch(TouchAt(7, 120f, 340f));
                zone.SupplyTouchData();
                Assert.That(zone.Processed, Is.EqualTo(new[] { (new Vector2(120f, 340f), new Vector2(120f, 340f), 0f, 0f) }));
                Assert.That(zone.Order, Is.EqualTo(new[] { "process", "input", "held" }));
                Assert.That(zone.HeldInputs, Is.EqualTo(new[] { new Vector2(5f, 6f) }));
                Assert.That((zone.Pinching, zone.WasDown), Is.EqualTo((false, true)));

                zone.UpdateTouch(TouchAt(7, 130f, 360f));
                zone.SupplyTouchData();
                Assert.That(zone.Processed[1], Is.EqualTo((new Vector2(130f, 360f), new Vector2(10f, 20f), 0f, 0f)));

                zone.StopTrackingTouch(TouchAt(7, 0f, 0f));
                zone.Order.Clear();
                zone.SupplyTouchData();
                Assert.That(zone.Order, Is.EqualTo(new[] { "release", "held" }));
                Assert.That(zone.HeldInputs[zone.HeldInputs.Count - 1], Is.EqualTo(Zero));
                Assert.That(zone.WasDown, Is.False);

                zone.Order.Clear();
                zone.SupplyTouchData();
                Assert.That(zone.Order, Is.Empty, "nothing more to release");
            });
        }

        // A zone that is not single-touch takes the bounds path even for one touch (0x68EE39/0x68EE58: fewer than two
        // touches leave the pinch distance at 0): the first supply after the change has no movement, unlike the
        // single-touch path (0x692B70), which measures from the zero previous position. No pinch is supplied.
        [Test]
        public void OneTouchOnAMultiTouchZoneSuppliesNoMovementAtFirstAndNoPinch()
        {
            WithZone((scope, zone) =>
            {
                zone.StartTrackingTouch(TouchAt(1, 100f, 100f));
                zone.SupplyTouchData();
                Assert.That(zone.Processed[0], Is.EqualTo((new Vector2(100f, 100f), Zero, 0f, 0f)));

                zone.UpdateTouch(TouchAt(1, 110f, 120f));
                zone.SupplyTouchData();
                Assert.That(zone.Processed[1], Is.EqualTo((new Vector2(110f, 120f), new Vector2(10f, 20f), 0f, 0f)));
                Assert.That(zone.Order, Is.EqualTo(new[] { "process", "input", "held", "process", "input", "held" }), "no pinch row");
            });
        }

        // SupplySingleTouch 0x692B70 passes the stored initial pinch distance (+0x80, 0x692C18) to ProcessTouch and
        // measures from the stored previous position; ClearAllTouches resets neither. A pinch of two touches stores
        // distance 100 and centre (130, 140); the same zone restricted to a single touch then moves from that centre.
        [Test]
        public void ASingleTouchKeepsTheInitialPinchDistanceAndPreviousPositionOfEarlierTouches()
        {
            WithZone((scope, zone) =>
            {
                zone.StartTrackingTouch(TouchAt(1, 100f, 100f));
                zone.StartTrackingTouch(TouchAt(2, 160f, 180f));
                zone.SupplyTouchData();

                zone.ClearAllTouches();
                SingleTouch(zone);
                zone.StartTrackingTouch(TouchAt(3, 10f, 10f));
                zone.SupplyTouchData();
                Assert.That(zone.Processed[1], Is.EqualTo((new Vector2(10f, 10f), new Vector2(-120f, -130f), 0f, 100f)));
                Assert.That(zone.Pinching, Is.False);
            });
        }

        // With several touches the zone works from the bounding box of all of them (0x68EBA8..0x68ED90): its centre
        // is the position, its diagonal the pinch distance, clamped to at least the minimum (50, constructor 0x6910E0
        // stores 0x42480000). The first supply after a change has no movement and takes the distance as the
        // initial one; later supplies move by the change of the centre and keep the initial distance. Distances
        // are single precision: sqrt(59600) is 244.13112.
        [Test]
        public void SeveralTouchesSupplyTheCentreOfTheirBoundsAndAPinchDistance()
        {
            WithZone((scope, zone) =>
            {
                zone.StartTrackingTouch(TouchAt(1, 100f, 100f));
                zone.StartTrackingTouch(TouchAt(2, 160f, 180f));
                zone.SupplyTouchData();
                Assert.That(zone.Processed[0], Is.EqualTo((new Vector2(130f, 140f), Zero, 100f, 100f)), "bounds 60 by 80: diagonal 100");
                Assert.That(zone.Order, Is.EqualTo(new[] { "process", "input", "held", "pinch" }));
                Assert.That(zone.Pinches[0], Is.EqualTo((100f, 100f, 100f, 100f)), "the properties read the live distances while pinching");
                Assert.That((zone.Pinching, zone.PinchNow, zone.InitialPinchNow), Is.EqualTo((true, 100f, 100f)));

                zone.UpdateTouch(TouchAt(2, 190f, 220f));
                zone.SupplyTouchData();
                Assert.That(zone.Processed[1], Is.EqualTo((new Vector2(145f, 160f), new Vector2(15f, 20f), 150f, 100f)), "bounds 90 by 120: diagonal 150");

                zone.StartTrackingTouch(TouchAt(3, 50f, 300f));
                zone.SupplyTouchData();
                Assert.That(zone.Processed[2], Is.EqualTo((new Vector2(120f, 200f), Zero, 244.13112f, 244.13112f)), "bounds 140 by 200 over three touches");
            });
        }

        // A diagonal below the minimum pinch distance is raised to it.
        [Test]
        public void ATightPinchIsRaisedToTheMinimumDistance()
        {
            WithZone((scope, zone) =>
            {
                zone.StartTrackingTouch(TouchAt(4, 100f, 100f));
                zone.StartTrackingTouch(TouchAt(5, 110f, 100f));
                zone.SupplyTouchData();
                Assert.That(zone.Processed[0], Is.EqualTo((new Vector2(105f, 100f), Zero, 50f, 50f)));

                ModernUITestSupport.Set(zone, "m_minimumPinchDistance", 4f); // serialized in the original
                zone.SupplyTouchData();
                Assert.That(zone.Processed[1], Is.EqualTo((new Vector2(105f, 100f), Zero, 10f, 50f)),
                    "the diagonal now exceeds the minimum; nothing changed, so the initial distance is kept");
            });
        }

        // After a touch is lost below the peak (0x68EDE3) the zone keeps its last position with no movement and no
        // pinch, and stays down; once every touch is gone it releases.
        [Test]
        public void ALostTouchHoldsTheLastPositionUntilEveryTouchIsGone()
        {
            WithZone((scope, zone) =>
            {
                zone.StartTrackingTouch(TouchAt(1, 100f, 100f));
                zone.StartTrackingTouch(TouchAt(2, 160f, 180f));
                zone.SupplyTouchData();
                zone.StopTrackingTouch(TouchAt(2, 0f, 0f));

                zone.Order.Clear();
                zone.SupplyTouchData();
                Assert.That(zone.Processed[1], Is.EqualTo((new Vector2(130f, 140f), Zero, 0f, 0f)));
                Assert.That(zone.Order, Is.EqualTo(new[] { "process", "input", "held" }));
                Assert.That((zone.Pinching, zone.WasDown), Is.EqualTo((false, true)));

                zone.StopTrackingTouch(TouchAt(1, 0f, 0f));
                zone.Order.Clear();
                zone.SupplyTouchData();
                Assert.That(zone.Order, Is.EqualTo(new[] { "release", "held" }));
            });
        }

        // TouchValidForCharacter 0x691640 is true without touching the character unless the list has abilities and
        // the current character has one of them in use within the grace period 0.2 (0x29A6C00).
        [Test]
        public void ATouchIsIgnoredWhileAListedAbilityIsInUseOnTheCurrentCharacter()
        {
            With(services =>
            {
                var zone = services.Owned.Host("Lucid owned touch zone", false).AddComponent<OnScreenTouchZoneProbe>();
                var listed = new List<ActorAbilityType> { ActorAbilityType.Character_AirStompAttack };
                var other = new List<ActorAbilityType> { ActorAbilityType.Character_SpinDashCharge };
                Action<ActorAbilityType, bool, float> storeUse = (ability, inUse, usedAt) =>
                {
                    var use = new Dictionary<ActorAbilityType, ActorAbilityInUseData>(HardlightProject.HardlightEnumComparers.ActorAbilityTypeComparer);
                    use[ability] = new ActorAbilityInUseData { InUse = inUse, TimeLastUsedSeconds = usedAt };
                    services.Character.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, use);
                };

                Assert.That(zone.ValidFor(new List<ActorAbilityType>()), Is.True, "no abilities listed");
                Assert.That(zone.ValidFor(listed), Is.True, "no current character");
                SetCurrent(services.Characters, services.Character, true);
                SetTotalFixedTime(services.Character);
                Assert.That(zone.ValidFor(listed), Is.True, "nothing in use");

                storeUse(ActorAbilityType.Character_AirStompAttack, true, 0f);
                Assert.That(zone.ValidFor(listed), Is.False, "in use");
                Assert.That(zone.ValidFor(other), Is.True, "another ability");
                Assert.That(zone.ValidFor(new List<ActorAbilityType>()), Is.True, "an empty list ignores nothing");

                float now = services.Character.GetTotalFixedTime();
                storeUse(ActorAbilityType.Character_AirStompAttack, false, now - 0.1f);
                Assert.That(zone.ValidFor(listed), Is.False, "used within the grace period");
                storeUse(ActorAbilityType.Character_AirStompAttack, false, now - 0.5f);
                Assert.That(zone.ValidFor(listed), Is.True, "used before the grace period");
            });
        }

        // SupplyTouchData asks TouchValidForCharacter with the serialized ignore list (+0x48): while a listed
        // ability is in use the pointer counts as up, so tracked touches supply nothing and nothing is released.
        [Test]
        public void TrackedTouchesSupplyNothingWhileAnIgnoredAbilityIsInUse()
        {
            With(services =>
            {
                var zone = services.Owned.Host("Lucid owned touch zone", false).AddComponent<OnScreenTouchZoneProbe>();
                SingleTouch(zone);
                ModernUITestSupport.Set(zone, "m_touchIgnoreAbilities", new List<ActorAbilityType> { ActorAbilityType.Character_AirStompAttack }); // serialized in the original
                SetCurrent(services.Characters, services.Character, true);
                SetTotalFixedTime(services.Character);
                var use = new Dictionary<ActorAbilityType, ActorAbilityInUseData>(HardlightProject.HardlightEnumComparers.ActorAbilityTypeComparer);
                use[ActorAbilityType.Character_AirStompAttack] = new ActorAbilityInUseData { InUse = true };
                services.Character.Storage.SetValue(ActorFSMKeys.AbilitiesInUse, use);

                zone.StartTrackingTouch(TouchAt(1, 10f, 10f));
                zone.SupplyTouchData();
                Assert.That(zone.Order, Is.Empty);

                use[ActorAbilityType.Character_AirStompAttack] = new ActorAbilityInUseData { InUse = false, TimeLastUsedSeconds = -100f };
                zone.SupplyTouchData();
                Assert.That(zone.Order, Is.EqualTo(new[] { "process", "input", "held" }));
            });
        }
    }
}
