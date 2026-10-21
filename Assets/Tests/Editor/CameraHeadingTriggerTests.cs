using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CameraHeadingTrigger (TypeDef 0x02000731, x86-64 0x5C86A0..0x5C97DF, with its private
    // SelectionMode enum 0x02000732). The trigger is a real component beside a BoxCollider on an owned inactive host;
    // the CharacterManager, the CinemachineCameraManager and (for continuous updates) a TimeManager are real systems
    // registered before the trigger's field initialisers cache them. The enter, stay and exit callbacks are called by
    // name, as CharacterTrigger's collider callbacks do after their filtering. Release timers and the delayed
    // continuous-update path need a CoroutineUtils host and real time and are not covered.
    public sealed class CameraHeadingTriggerTests
    {
        private static readonly Type SelectionMode = typeof(CameraHeadingTrigger).GetNestedType("SelectionMode", BindingFlags.NonPublic);

        private const int IncomingDirection = 0, CharacterForward = 1, CameraForward = 2, ClosestDistance = 3, CharacterVelocity = 4;

        private sealed class Scene
        {
            internal CameraHeadingTrigger Trigger;
            internal Character Character;
            internal CharacterManager Manager;
            internal Rig Rig;
        }

        private static void WithTrigger(bool timed, Action<Owned, Scene> body) =>
            WithManager((owned, manager, character) =>
            {
                // The camera manager's type initialiser and the trigger's field initialisers take the TimeManager row, so it is
                // registered (or leased) before either is built.
                if (timed) owned.Register(NewTimeManager(owned, TimeCategory.Camera, 1f));
                else owned.LeaseRow<TimeManager>();
                var scene = new Scene { Manager = manager, Character = character, Rig = NewRig(owned, false, true, false) };
                GameObject host = owned.Host("Lucid camera heading trigger", false);
                host.AddComponent<BoxCollider>();
                scene.Trigger = host.AddComponent<CameraHeadingTrigger>();
                Call(scene.Trigger, "Awake");
                SetCurrent(manager, character, true);
                scene.Trigger.SetTriggerWhenInactive(true);
                SetActorProperty(character, "WorldPosition", Vector3.zero);
                SetActorProperty(character, "ForwardDirection", Vector3.forward);
                SetActorProperty(character, "WorldVelocityNormalised", Vector3.right);
                body(owned, scene);
            });

        private static CameraHeadingOverride Heading(Owned owned, string name, Vector3 position, Vector3 forward,
            Action<CameraHeadingOverride.CameraHeadingOverrideSettings> configure = null)
        {
            Transform target = owned.Host(name, false).transform;
            target.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            var heading = new CameraHeadingOverride { Target = target, Settings = new CameraHeadingOverride.CameraHeadingOverrideSettings() };
            configure?.Invoke(heading.Settings);
            heading.Settings.UpdateCachedValues();
            return heading;
        }

        private static void Configure(Scene scene, int mode, params CameraHeadingOverride[] headings)
        {
            Set(scene.Trigger, "m_headingOverrides", headings);
            Set(scene.Trigger, "m_headingSelectionMode", Enum.ToObject(SelectionMode, mode));
        }

        // The index of the override a selection answers, or -1 for null.
        private static int Select(Scene scene, params CameraHeadingOverride[] headings)
        {
            object selected = Call(scene.Trigger, "GetHeadingOverride", scene.Character);
            return selected == null ? -1 : Array.IndexOf(headings, (CameraHeadingOverride)selected);
        }

        private static CameraHeadingOverride Applied(Scene scene) =>
            scene.Rig.Manager.GetCameraSettingOverride<CameraHeadingOverride>(CameraSettingType.HeadingOverride);

        // GetHeadingOverride 0x5C8A30 dispatches on the mode through a five-entry jump table (0x5c8d80); anything else
        // answers null (0x5c8d6a). Every mode answers null for an empty list (the length test at the top of each loop).
        [Test]
        public void NoOverridesOrAnUnknownModeSelectNothing()
        {
            WithTrigger(false, (owned, scene) =>
            {
                for (int mode = 0; mode <= 4; mode++)
                {
                    Configure(scene, mode);
                    Assert.That(Call(scene.Trigger, "GetHeadingOverride", scene.Character), Is.Null, "mode " + mode + " without overrides");
                }
                CameraHeadingOverride one = Heading(owned, "only", new Vector3(0f, 0f, 3f), Vector3.forward);
                Configure(scene, 5, one);
                Assert.That(Call(scene.Trigger, "GetHeadingOverride", scene.Character), Is.Null, "a mode past the table");
            });
        }

        // Mode 0 (SelectBestAlignedIncomingAngle 0x5C8F30) compares each override's forward with the direction from the
        // character to that override (the subtraction at 0x5c8fa5 and the divide by the length at 0x5c901e): the override
        // ahead of the character facing the same way wins, and moving the character moves the answer.
        [Test]
        public void TheIncomingDirectionModeUsesTheDirectionFromTheCharacterToEachOverride()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride[] headings =
                {
                    Heading(owned, "side", new Vector3(10f, 0f, 0f), Vector3.forward),
                    Heading(owned, "ahead", new Vector3(0f, 0f, 10f), Vector3.forward),
                    Heading(owned, "behind", new Vector3(0f, 0f, -10f), Vector3.forward),
                };
                Configure(scene, IncomingDirection, headings);
                // side: dot((0,0,1),(1,0,0)) = 0; ahead: 1; behind: -1.
                Assert.That(Select(scene, headings), Is.EqualTo(1));

                // From (0, 0, 20) the directions are (10,0,-20)/22.36 -> -0.894, (0,0,-1) -> -1, (0,0,-1) -> -1.
                SetActorProperty(scene.Character, "WorldPosition", new Vector3(0f, 0f, 20f));
                Assert.That(Select(scene, headings), Is.EqualTo(0));
            });
        }

        // Modes 1, 4 and 2 are the same loop over a different direction: the character's forward direction
        // (SelectBestAlignedCharacterForward 0x5C9130 reads Actor +0xA4), its normalised velocity (0x5C9200 reads +0xD4)
        // and the camera manager's brain forward (0x5C92D0). The strictly greater test (seta, 0x5c91da) keeps the first of
        // equal overrides.
        [Test]
        public void TheAlignedModesPickTheOverrideFacingTheirDirection()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride[] headings =
                {
                    Heading(owned, "faces x", Vector3.zero, Vector3.right),
                    Heading(owned, "faces z", Vector3.zero, Vector3.forward),
                    Heading(owned, "faces y", Vector3.zero, Vector3.up),
                    Heading(owned, "also faces y", Vector3.zero, Vector3.up),
                };
                // The character faces +z and moves along +x, the camera looks along +y.
                scene.Rig.Brain.transform.rotation = Quaternion.LookRotation(Vector3.up);

                Configure(scene, CharacterForward, headings);
                Assert.That(Select(scene, headings), Is.EqualTo(1), "character forward");
                Configure(scene, CharacterVelocity, headings);
                Assert.That(Select(scene, headings), Is.EqualTo(0), "character velocity");
                Configure(scene, CameraForward, headings);
                Assert.That(Select(scene, headings), Is.EqualTo(2), "camera forward, the first of two equal overrides");
            });
        }

        // IsHeadingValidSelection 0x5C9580 rejects an override only when its minimum-angle rule is on and its limit
        // cosine is above the direction cosine (0x5c9593 ucomiss, ja/jbe). The best aligned override at 60 degrees
        // (cosine 0.5) is dropped by a 45 degree limit (cosine 0.707) and kept by a 70 degree limit (cosine 0.342).
        [Test]
        public void AMinimumAngleRuleRejectsOverridesWhoseCosineIsBelowItsLimit()
        {
            WithTrigger(false, (owned, scene) =>
            {
                Vector3 sixty = new Vector3(0.8660254f, 0f, 0.5f);
                Vector3 eighty = new Vector3(0.9848078f, 0f, 0.1736482f);
                foreach ((bool required, float limit, int expected, string label) in new[]
                         {
                             (true, 45f, 1, "a 45 degree limit drops the 60 degree override"),
                             (true, 70f, 0, "a 70 degree limit keeps it"),
                             (false, 45f, 0, "the limit is ignored while the rule is off"),
                         })
                {
                    CameraHeadingOverride[] headings =
                    {
                        Heading(owned, "sixty", Vector3.zero, sixty, settings =>
                        {
                            settings.RequireMinimumAngle = required;
                            settings.MaximumAngleThreshold = limit;
                        }),
                        Heading(owned, "eighty", Vector3.zero, eighty),
                    };
                    Configure(scene, CharacterForward, headings);
                    Assert.That(Select(scene, headings), Is.EqualTo(expected), label);
                }
            });
        }

        // The rule rejects only when the limit cosine is strictly above the direction cosine (ordered ucomiss, jbe to
        // valid at 0x5c91c7 and ja to skip at 0x5c946f), so an override whose cosine equals its limit is kept. The
        // authored tooltip ("greater than or equal to this angle") would reject it. A limit of 0 degrees has cosine 1
        // and the aligned override's direction cosine is exactly 1.
        [Test]
        public void AMinimumAngleRuleKeepsAnOverrideWhoseCosineEqualsItsLimit()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride[] headings =
                {
                    Heading(owned, "faces x", Vector3.zero, Vector3.right),
                    Heading(owned, "faces z", Vector3.zero, Vector3.forward, settings =>
                    {
                        settings.RequireMinimumAngle = true;
                        settings.MaximumAngleThreshold = 0f;
                    }),
                };
                Configure(scene, CharacterForward, headings);
                Assert.That(Select(scene, headings), Is.EqualTo(1));
            });
        }

        // Mode 3 (SelectClosest 0x5C93D0) answers the nearest override by Vector3.Distance (the strict less test at
        // 0x5c94d9), skipping those the character's forward fails the same minimum-angle rule for (0x5c946f).
        [Test]
        public void TheClosestModeAnswersTheNearestOverrideThatPassesItsAngleRule()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride[] headings =
                {
                    Heading(owned, "five", new Vector3(5f, 0f, 0f), Vector3.forward),
                    Heading(owned, "two", new Vector3(0f, 2f, 0f), Vector3.right),
                    Heading(owned, "nine", new Vector3(0f, 0f, 9f), Vector3.forward),
                };
                Configure(scene, ClosestDistance, headings);
                Assert.That(Select(scene, headings), Is.EqualTo(1), "the override at distance 2");

                // The nearest faces 90 degrees away from the character, whose forward is +z.
                headings[1].Settings.RequireMinimumAngle = true;
                headings[1].Settings.MaximumAngleThreshold = 30f;
                headings[1].Settings.UpdateCachedValues();
                Assert.That(Select(scene, headings), Is.EqualTo(0), "the nearest is skipped by a 30 degree limit");

                headings[1].Settings.MaximumAngleThreshold = 100f;
                headings[1].Settings.UpdateCachedValues();
                Assert.That(Select(scene, headings), Is.EqualTo(1), "a 100 degree limit accepts it");
            });
        }

        // SelectClosest replaces its answer only when the stored distance is strictly greater (ucomiss, jbe skip at
        // 0x5c94d9), so the first of two equidistant overrides wins.
        [Test]
        public void TheClosestModeKeepsTheFirstOfTwoEquidistantOverrides()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride[] headings =
                {
                    Heading(owned, "on z", new Vector3(0f, 0f, 3f), Vector3.forward),
                    Heading(owned, "on x", new Vector3(3f, 0f, 0f), Vector3.forward),
                };
                Configure(scene, ClosestDistance, headings);
                Assert.That(Select(scene, headings), Is.EqualTo(0));
            });
        }

        // Awake 0x5C86A0 calls the base Awake and then refreshes the cached limit cosine of every override.
        [Test]
        public void AwakeCachesTheLimitCosineOfEveryOverride()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride heading = Heading(owned, "limited", Vector3.zero, Vector3.forward,
                    settings => settings.MaximumAngleThreshold = 60f);
                heading.Settings.MaximumAngleThreshold = 90f;
                Configure(scene, CharacterForward, heading);

                Call(scene.Trigger, "Awake");

                Assert.That(heading.Settings.MaximumAngleThresholdCosine, Is.EqualTo(0f).Within(1e-6f));
            });
        }

        // DoTriggerEnter 0x5C8720 applies the selected override to the camera manager under the HeadingOverride setting
        // (key 0xDAD2DA8) and DoTriggerExit 0x5C8DA0 removes it again.
        [Test]
        public void EnteringAppliesTheSelectedOverrideAndExitingRemovesIt()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride[] headings =
                {
                    Heading(owned, "faces x", Vector3.zero, Vector3.right),
                    Heading(owned, "faces z", Vector3.zero, Vector3.forward),
                };
                Configure(scene, CharacterForward, headings);
                Assert.That(Applied(scene), Is.Null);

                Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                Assert.That(Applied(scene), Is.SameAs(headings[1]));

                Call(scene.Trigger, "DoTriggerExit", scene.Character);
                Assert.That(Applied(scene), Is.Null);
            });
        }

        // The released trigger sets m_canTrigger false after its exit when it only triggers once (0x5c8ec0), and enter
        // and exit then return before doing anything (0x5c872e, 0x5c8da6).
        [Test]
        public void ATriggerThatTriggersOnceIgnoresEveryLaterEnterAndExit()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride heading = Heading(owned, "faces z", Vector3.zero, Vector3.forward);
                Configure(scene, CharacterForward, heading);
                Set(scene.Trigger, "m_onlyTriggerOnce", true);

                Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                Assert.That(Applied(scene), Is.SameAs(heading));
                Call(scene.Trigger, "DoTriggerExit", scene.Character);
                Assert.That(Applied(scene), Is.Null);

                Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                Assert.That(Applied(scene), Is.Null, "the second enter does nothing");
            });
        }

        // m_canTrigger is cleared only inside RemoveCameraSettingOverride after a live handle was released: without a
        // handle 0x5c8e32 jumps past the clear (0x5c8ec7; the clear is at 0x5c8ec0). A second enter while live removes
        // first (0x5c87eb), which already clears it, so the following exit returns at 0x5c8dad and keeps the override.
        [Test]
        public void ATriggerThatTriggersOnceOnlyClosesAfterReleasingALiveOverride()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride heading = Heading(owned, "faces z", Vector3.zero, Vector3.forward);
                Configure(scene, CharacterForward, heading);
                Set(scene.Trigger, "m_onlyTriggerOnce", true);

                Call(scene.Trigger, "OnDisable");
                Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                Assert.That(Applied(scene), Is.SameAs(heading), "releasing nothing keeps the trigger open");

                Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                Call(scene.Trigger, "DoTriggerExit", scene.Character);
                Assert.That(Applied(scene), Is.SameAs(heading), "the replacing enter closed the trigger, so the exit is ignored");

                Call(scene.Trigger, "OnDisable");
                Assert.That(Applied(scene), Is.Null, "removal does not check the flag");
            });
        }

        [Test]
        public void ARepeatableTriggerAppliesAgainOnEveryEnter()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride heading = Heading(owned, "faces z", Vector3.zero, Vector3.forward);
                Configure(scene, CharacterForward, heading);

                for (int visit = 0; visit < 2; visit++)
                {
                    Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                    Assert.That(Applied(scene), Is.SameAs(heading), "enter " + visit);
                    Call(scene.Trigger, "DoTriggerExit", scene.Character);
                    Assert.That(Applied(scene), Is.Null, "exit " + visit);
                }
            });
        }

        // The two public actions reach the same bodies. Apply needs a current character (TryGetCharacter, 0x5C8EE0),
        // replaces a live override by first removing it (0x5c87e8), and Remove and OnDisable both clear it.
        [Test]
        public void TheActionsApplyReplaceAndRemoveTheOverride()
        {
            WithTrigger(false, (owned, scene) =>
            {
                CameraHeadingOverride[] headings =
                {
                    Heading(owned, "faces x", Vector3.zero, Vector3.right),
                    Heading(owned, "faces z", Vector3.zero, Vector3.forward),
                };
                Configure(scene, CharacterForward, headings);

                SetCurrent(scene.Manager, scene.Character, false);
                scene.Trigger.Action_ApplySettingOverride();
                Assert.That(Applied(scene), Is.Null, "no valid character");
                SetCurrent(scene.Manager, scene.Character, true);

                scene.Trigger.Action_ApplySettingOverride();
                Assert.That(Applied(scene), Is.SameAs(headings[1]));
                SetActorProperty(scene.Character, "ForwardDirection", Vector3.right);
                scene.Trigger.Action_ApplySettingOverride();
                Assert.That(Applied(scene), Is.SameAs(headings[0]), "the new selection replaces the old one");

                scene.Trigger.Action_RemoveSettingOverride();
                Assert.That(Applied(scene), Is.Null, "the replaced override is gone as well");

                scene.Trigger.Action_ApplySettingOverride();
                Assert.That(Applied(scene), Is.SameAs(headings[0]));
                Call(scene.Trigger, "OnDisable");
                Assert.That(Applied(scene), Is.Null);
            });
        }

        // DoTriggerStay 0x5C88E0 only reconsiders the selection while continuously updating with a live override. A
        // different best override replaces the current one once the accumulated time reaches the switch delay (0x5c89c8,
        // jb), and the accumulated time is cleared when the best override is the current one (0x5c8a1c). The delay is
        // chosen as a value no frame reaches and as one the stored timer already reaches, so the case does not depend on
        // the frame time.
        [Test]
        public void ContinuousUpdatesSwitchTheOverrideOnlyAfterTheDelay()
        {
            WithTrigger(true, (owned, scene) =>
            {
                CameraHeadingOverride[] headings =
                {
                    Heading(owned, "faces x", Vector3.zero, Vector3.right),
                    Heading(owned, "faces z", Vector3.zero, Vector3.forward),
                };
                Configure(scene, CharacterForward, headings);

                Call(scene.Trigger, "DoTriggerStay", scene.Character);
                Assert.That(Applied(scene), Is.Null, "stay does not apply anything by itself");

                Call(scene.Trigger, "DoTriggerEnter", scene.Character);
                SetActorProperty(scene.Character, "ForwardDirection", Vector3.right);
                Call(scene.Trigger, "DoTriggerStay", scene.Character);
                Assert.That(Applied(scene), Is.SameAs(headings[1]), "not continuously updating");

                Set(scene.Trigger, "m_continuouslyUpdate", true);
                Set(scene.Trigger, "m_continuousUpdateSwitchDelaySeconds", 1e9f);
                Call(scene.Trigger, "DoTriggerStay", scene.Character);
                Assert.That(Applied(scene), Is.SameAs(headings[1]), "the delay has not elapsed");

                // While it waits the stored timer is added to, never replaced or cleared (0x5c89b6 GetDeltaTime, addss
                // 0x5c89bb, store 0x5c89c0, then jb 0x5c8a26 past the reset at 0x5c8a1c). The timer is private and only
                // grows by frame time, so a known value is written to it; the frame time is never negative.
                Set(scene.Trigger, "m_continuousUpdateSwitchTimerSeconds", 3f);
                Call(scene.Trigger, "DoTriggerStay", scene.Character);
                Assert.That(Applied(scene), Is.SameAs(headings[1]), "still waiting with a stored timer");
                Assert.That(Get<float>(scene.Trigger, "m_continuousUpdateSwitchTimerSeconds"), Is.GreaterThanOrEqualTo(3f));

                // The wait test is jb after ucomiss (0x5c89c8), which also skips the switch when the delay is NaN.
                Set(scene.Trigger, "m_continuousUpdateSwitchDelaySeconds", float.NaN);
                Call(scene.Trigger, "DoTriggerStay", scene.Character);
                Assert.That(Applied(scene), Is.SameAs(headings[1]), "a NaN delay never elapses");

                // The stored timer plus the frame time reaches a delay equal to the stored timer, which a version that
                // restarts the timer from the frame time does not.
                Set(scene.Trigger, "m_continuousUpdateSwitchTimerSeconds", 5f);
                Set(scene.Trigger, "m_continuousUpdateSwitchDelaySeconds", 5f);
                Call(scene.Trigger, "DoTriggerStay", scene.Character);
                Assert.That(Applied(scene), Is.SameAs(headings[0]), "the delay has elapsed");
                Assert.That(Get<CameraHeadingOverride>(scene.Trigger, "m_currentHeadingOverride"), Is.SameAs(headings[0]));
                Assert.That(Get<float>(scene.Trigger, "m_continuousUpdateSwitchTimerSeconds"), Is.EqualTo(0f));

                // The best override is the current one again: the timer is cleared even from a nonzero value. The timer is
                // private and only grows by frame time, so a known nonzero value is written to it.
                Set(scene.Trigger, "m_continuousUpdateSwitchTimerSeconds", 3f);
                Call(scene.Trigger, "DoTriggerStay", scene.Character);
                Assert.That(Get<float>(scene.Trigger, "m_continuousUpdateSwitchTimerSeconds"), Is.EqualTo(0f));
            });
        }
    }
}
