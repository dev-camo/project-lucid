using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerMissionTimer (Game.Runtime 0x020008ea; OnEnable 0x668A20, Update 0x668AB0, SetPauseState 0x669380, ClearValues
    // 0x668A60, TryInitialise 0x668F60, OnBonusTimeAdded 0x669400, OnDestroy 0x669480, constructor 0x6695B0) over a real mission
    // manager with an active mission whose state holds a real instanced tracker and a real MissionTimer (driven through its own
    // start and update step). The time format is "{0}|{1}|{2}". Not covered: the bonus text's fade shape between its two ends,
    // because the frame time is engine behaviour (the fade is checked at its two ends only, with the display countdown written
    // directly). The bonus animation plays an empty clip.
    public sealed class UIContainerMissionTimerTests
    {
        private static readonly Dictionary<Strings, string> OwnedStrings = new Dictionary<Strings, string>
        {
            { Strings.MISSION_TIMER_FORMAT_DESCRIPTION, "{0}|{1}|{2}" },
            { Strings.CHALLENGES_LABEL_XP, "+{0}s" },
        };

        private static readonly Color Normal = new Color(0.25f, 0.5f, 0.75f, 1f);
        private static readonly Color Alternate = new Color(1f, 0.25f, 0.5f, 1f);

        private sealed class Rig
        {
            internal World World;
            internal Owned Owned;
            internal UIContainerMissionTimer Container;
            internal MissionTimer Timer;
            internal MissionInstancedTracker Tracker;
            internal MissionState State;
            internal TextMeshProUGUI TimerText;
            internal TextMeshProUGUI BonusText;
            internal GameObject EnableRoot;
            internal GameObject PauseGroupRoot;
            internal GameObject PauseEnabledRoot;
            internal GameObject PauseDisabledRoot;
        }

        private static TextMeshProUGUI NewText(Owned owned, string name) => owned.Host(name, false).AddComponent<TextMeshProUGUI>();

        // The tracker's timer limit is 60 seconds. timed: the tracker has the timer. countsDown: the tracker has a fail state.
        private static void Run(Action<Rig> body, bool timed = true, bool countsDown = true, bool canBePaused = false, bool timeTrial = false) => WithWorld(world =>
        {
            Owned owned = world.Owned;
            owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
            SaveManager save = owned.Register(new SaveManager());
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(save)));
            var game = new SaveDataGame();
            game.Initialise();
            Set(save, "m_currentSave", game); // Reflection: the open save the manager keeps
            var level = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
            Set(level, "m_guid", "lucid-timer-level"); // Reflection: authored GUID of a serialized definition
            Mission mission = world.NewMission();
            Set(mission.Definition, "m_subMissions", new List<MissionDefinition>()); // Reflection: authored (empty) list
            world.Missions.SetActiveMissionContext(new MissionContextRegular(mission.Definition, level, false));
            if (timeTrial)
            {
                Set(mission.Definition, "m_timeLimitSeconds", 60); // Reflection: authored mission data
                Set(mission.Definition, "m_isBeingOverridden", true); // Reflection: set by UpdateOverrides for a completed mission with overrides, which loading the mission has run
            }

            var rig = new Rig { World = world, Owned = owned };
            rig.State = world.Missions.MissionStates[mission.Definition.GetGUID()];
            rig.Tracker = world.NewTracker<MissionInstancedTracker>();
            Set(rig.Tracker, "m_missionObjectives", new List<MissionObjective>()); // Reflection: authored (empty) objective list
            Set(rig.Tracker, "m_failStateBehaviour", countsDown ? MissionTracker.FailStateBehaviourType.ShowResultsScreen : MissionTracker.FailStateBehaviourType.None);
            rig.Timer = EnemyAbilityTestSupport.AddTimeScaled<MissionTimer>(owned.Host("Lucid mission timer", false));
            Set(rig.Timer, "m_canBePaused", canBePaused); // Reflection: serialized options
            Set(rig.Timer, "m_onTimeElapseEvents", new List<UnityEvent>()); // Reflection: authored (empty) serialized list, which a component added in code does not have
            if (timed) Set(rig.Tracker, "m_optionalMissionTimer", rig.Timer);
            Call(rig.State, "OnTrackerSetup", rig.Tracker, null, null, true); // the callback MissionState.SetUpTracker passes the loaded tracker

            rig.Container = owned.Host("Lucid mission timer container", false).AddComponent<UIContainerMissionTimer>();
            rig.TimerText = NewText(owned, "Lucid timer text");
            rig.BonusText = NewText(owned, "Lucid bonus text");
            rig.EnableRoot = owned.Host("Lucid enable root", true);
            rig.PauseGroupRoot = owned.Host("Lucid pause group", true);
            rig.PauseEnabledRoot = owned.Host("Lucid pause enabled", true);
            rig.PauseDisabledRoot = owned.Host("Lucid pause disabled", true);
            Set(rig.Container, "m_timeDisplayFormat", Strings.MISSION_TIMER_FORMAT_DESCRIPTION); // Reflection: serialized fields
            Set(rig.Container, "m_bonusTimeDisplayFormat", Strings.CHALLENGES_LABEL_XP);
            Set(rig.Container, "m_bonusTimeText", rig.BonusText);
            Set(rig.Container, "m_bonusTimeAnimation", rig.Container.gameObject.AddComponent<Animation>());
            Set(rig.Container, "m_bonusTimeDisplayDurationSeconds", 2f);
            Set(rig.Container, "m_bonusTimeOpacityCurve", new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 0.75f)));
            Set(rig.Container, "m_lowTimeRemainingThresholdSeconds", 10f);
            Set(rig.Container, "m_textColourNormal", Normal);
            Set(rig.Container, "m_textColourAlternate", Alternate);
            Set(rig.Container, "m_timerText", rig.TimerText);
            Set(rig.Container, "m_enableRoot", rig.EnableRoot);
            Set(rig.Container, "m_pauseGroupRoot", rig.PauseGroupRoot);
            Set(rig.Container, "m_pauseEnabledRoot", rig.PauseEnabledRoot);
            Set(rig.Container, "m_pauseDisabledRoot", rig.PauseDisabledRoot);
            OwnedStringTable.Run(OwnedStrings, () => body(rig));
        });

        private static int Hooks(Delegate handlers, object target) =>
            handlers == null ? 0 : handlers.GetInvocationList().Count(handler => ReferenceEquals(handler.Target, target));

        // Starts the timer (remaining = its 60 second limit) and then runs it for some seconds.
        private static void StartAndRun(Rig rig, float seconds)
        {
            rig.Timer.Action_StartTimer();
            Call(rig.Timer, "InternalUpdate", seconds);
        }

        // dump.cs TypeDefIndex 2281 and the DummyDll rows (fields 0x0400240f..0x04002427).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerMissionTimer), 0x00100001,
                new (string, int)[]
                {
                    ("m_timeDisplayFormat", 0x0001), ("m_bonusTimeDisplayFormat", 0x0001), ("m_bonusTimeText", 0x0001), ("m_bonusTimeAnimation", 0x0001),
                    ("m_bonusTimeDisplayDurationSeconds", 0x0001), ("m_bonusTimeOpacityCurve", 0x0001), ("m_lowTimeRemainingThresholdSeconds", 0x0001),
                    ("m_textColourNormal", 0x0001), ("m_textColourAlternate", 0x0001), ("m_timerText", 0x0001), ("m_enableRoot", 0x0001),
                    ("m_pauseGroupRoot", 0x0001), ("m_pauseEnabledRoot", 0x0001), ("m_pauseDisabledRoot", 0x0001), ("m_missionTimer", 0x0001),
                    ("m_missionManagerRef", 0x0021), ("m_initialised", 0x0001), ("m_countsDown", 0x0001), ("m_bonusTimeDisplayTimeRemainingSeconds", 0x0001),
                    ("m_timeDisplayFormatString", 0x0001), ("m_bonusTimeDisplayFormatString", 0x0001), ("m_hasMissionTimer", 0x0001),
                    ("m_missionTimerCanBePaused", 0x0001), ("m_bonusTimeOpacityCurveTotalTime", 0x0001), ("m_isTimeTrial", 0x0001),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081), ("Update", 0x0081), ("SetPauseState", 0x0081), ("ClearValues", 0x0081), ("TryInitialise", 0x0081),
                    ("OnBonusTimeAdded", 0x0081), ("OnDestroy", 0x0081), (".ctor", 0x1886),
                });
        }

        // OnEnable 0x668A20: not initialised, the bonus text off and its countdown zero, the timer object hidden.
        [Test]
        public void EnablingResetsTheBonusTextAndHidesTheTimer()
        {
            Run(rig =>
            {
                Call(rig.Container, "TryInitialise");
                rig.BonusText.enabled = true;
                Set(rig.Container, "m_bonusTimeDisplayTimeRemainingSeconds", 3f); // Reflection: counted down by Update, written by OnBonusTimeAdded
                rig.EnableRoot.SetActive(true);

                Call(rig.Container, "OnEnable");

                Assert.That(Get<bool>(rig.Container, "m_initialised"), Is.False);
                Assert.That(rig.BonusText.enabled, Is.False);
                Assert.That(Get<float>(rig.Container, "m_bonusTimeDisplayTimeRemainingSeconds"), Is.EqualTo(0f));
                Assert.That(rig.EnableRoot.activeSelf, Is.False);
            });
        }

        // SetPauseState 0x669380: a missing root is ignored; a root is switched only when its state differs.
        [Test]
        public void ARootIsSwitchedToTheRequestedState()
        {
            Run(rig =>
            {
                Call(rig.Container, "SetPauseState", new object[] { null, true });
                var root = rig.Owned.Host("Lucid pause root", true);

                Call(rig.Container, "SetPauseState", root, false);
                Assert.That(root.activeSelf, Is.False);
                Call(rig.Container, "SetPauseState", root, false);
                Assert.That(root.activeSelf, Is.False);
                Call(rig.Container, "SetPauseState", root, true);
                Assert.That(root.activeSelf, Is.True);
            });
        }

        // ClearValues 0x668A60: the bonus text off and its countdown zero; the timer object is hidden unless the timer is visible.
        [Test]
        public void ClearingValuesHidesTheTimerUnlessItIsVisible()
        {
            Run(rig =>
            {
                foreach (bool visible in new[] { false, true })
                {
                    rig.BonusText.enabled = true;
                    rig.EnableRoot.SetActive(true);
                    Set(rig.Container, "m_bonusTimeDisplayTimeRemainingSeconds", 3f); // Reflection: written by OnBonusTimeAdded

                    Call(rig.Container, "ClearValues", visible);

                    Assert.That(rig.BonusText.enabled, Is.False, "visible " + visible);
                    Assert.That(Get<float>(rig.Container, "m_bonusTimeDisplayTimeRemainingSeconds"), Is.EqualTo(0f), "visible " + visible);
                    Assert.That(rig.EnableRoot.activeSelf, Is.EqualTo(visible), "visible " + visible);
                }
            });
        }

        // TryInitialise 0x668F60: with the active mission set up it hooks the tracker's timer for bonus time, remembers whether the
        // timer can be paused, whether the mission is a time trial and whether it counts down (a fail state, 0x669316), takes the
        // curve's last key time, formats the strings and sets the pause group (0x66916E); and it returns true from then on.
        [Test]
        public void InitialisingHooksTheTimerAndCachesTheMissionSettings()
        {
            Run(rig =>
            {
                Assert.That(Call(rig.Container, "TryInitialise"), Is.EqualTo(true));

                Assert.That(Hooks(rig.Timer.OnBonusTimeAdded, rig.Container), Is.EqualTo(1));
                Assert.That(Get<MissionTimer>(rig.Container, "m_missionTimer"), Is.SameAs(rig.Timer));
                Assert.That(Get<bool>(rig.Container, "m_hasMissionTimer"), Is.True);
                Assert.That(Get<bool>(rig.Container, "m_countsDown"), Is.True);
                Assert.That(Get<bool>(rig.Container, "m_isTimeTrial"), Is.False);
                Assert.That(Get<bool>(rig.Container, "m_missionTimerCanBePaused"), Is.False);
                Assert.That(Get<float>(rig.Container, "m_bonusTimeOpacityCurveTotalTime"), Is.EqualTo(1f));
                Assert.That(Get<string>(rig.Container, "m_timeDisplayFormatString"), Is.EqualTo("{0}|{1}|{2}"));
                Assert.That(Get<string>(rig.Container, "m_bonusTimeDisplayFormatString"), Is.EqualTo("+{0}s"));
                Assert.That(rig.PauseGroupRoot.activeSelf, Is.False, "the timer cannot be paused");
                Assert.That(Get<bool>(rig.Container, "m_initialised"), Is.True);

                Assert.That(Call(rig.Container, "TryInitialise"), Is.EqualTo(true));
                Assert.That(Hooks(rig.Timer.OnBonusTimeAdded, rig.Container), Is.EqualTo(1), "initialised once");
            });
            Run(rig =>
            {
                rig.PauseGroupRoot.SetActive(false);

                Call(rig.Container, "TryInitialise");

                Assert.That(Get<bool>(rig.Container, "m_countsDown"), Is.False);
                Assert.That(Get<bool>(rig.Container, "m_isTimeTrial"), Is.True);
                Assert.That(Get<bool>(rig.Container, "m_missionTimerCanBePaused"), Is.True);
                Assert.That(rig.PauseGroupRoot.activeSelf, Is.True, "the timer can be paused");
            }, countsDown: false, canBePaused: true, timeTrial: true);
        }

        // A mission whose tracker has no timer initialises without one: no hook, and the pause group follows the (unset) pause flag.
        [Test]
        public void ATrackerWithoutATimerInitialisesWithoutHookingOne()
        {
            Run(rig =>
            {
                Assert.That(Call(rig.Container, "TryInitialise"), Is.EqualTo(true));

                Assert.That(Get<bool>(rig.Container, "m_hasMissionTimer"), Is.False);
                Assert.That(Get<MissionTimer>(rig.Container, "m_missionTimer"), Is.Null);
                Assert.That(Hooks(rig.Timer.OnBonusTimeAdded, rig.Container), Is.EqualTo(0));
                Assert.That(rig.PauseGroupRoot.activeSelf, Is.False);
            }, timed: false);
        }

        // Without an active mission there is nothing to initialise: TryInitialise gives false and caches nothing.
        [Test]
        public void WithoutAnActiveMissionNothingIsInitialised()
        {
            WithWorld(world =>
            {
                var container = world.Owned.Host("Lucid mission timer container", false).AddComponent<UIContainerMissionTimer>();

                Assert.That(Call(container, "TryInitialise"), Is.EqualTo(false));
                Assert.That(Get<bool>(container, "m_initialised"), Is.False);
                Assert.That(Get<string>(container, "m_timeDisplayFormatString"), Is.Null);
            });
        }

        // Update 0x668AB0 counting down: the time remaining is shown as minutes, seconds and hundredths in two digits each
        // (cvttsd2si of TotalMinutes, "D2", and the division of the milliseconds by ten at 0x668D63): 47.75 s is 00|47|75. Above
        // the low-time threshold the normal colour is used, below it the alternate one (ucomiss at 0x668CCD).
        [Test]
        public void ACountdownShowsTheTimeRemainingAndTurnsAlternateWhenLow()
        {
            Run(rig =>
            {
                StartAndRun(rig, 12.25f);

                Call(rig.Container, "Update");

                Assert.That(rig.TimerText.text, Is.EqualTo("00|47|75"));
                Assert.That(rig.TimerText.color, Is.EqualTo(Normal));
                Assert.That(rig.EnableRoot.activeSelf, Is.True);

                Call(rig.Timer, "InternalUpdate", 40f); // 7.75 s remain, under the 10 s threshold

                Call(rig.Container, "Update");

                Assert.That(rig.TimerText.text, Is.EqualTo("00|07|75"));
                Assert.That(rig.TimerText.color, Is.EqualTo(Alternate));
            });
        }

        // The minutes are the whole minutes of the total (get_TotalMinutes at 0x668D13, cvttsd2si at 0x668D18), not the minutes
        // component of a time span: 3700 s remaining is 61 minutes 40 seconds, where an hours-aware minutes field would read 01.
        [Test]
        public void MinutesPastAnHourAreTheWholeMinutesOfTheTotal()
        {
            Run(rig =>
            {
                Set(rig.Timer, "m_timeLimit", 4000f); // Reflection: serialized option
                StartAndRun(rig, 300f);

                Call(rig.Container, "Update");

                Assert.That(rig.TimerText.text, Is.EqualTo("61|40|00"));
            });
        }

        // The low-time test is strict (ucomiss then seta at 0x668CCD, and 0x668F17 for the count-up case): a time remaining exactly
        // on the threshold is not low.
        [Test]
        public void ATimeRemainingOnTheThresholdIsNotLow()
        {
            Run(rig =>
            {
                Set(rig.Container, "m_lowTimeRemainingThresholdSeconds", 7.75f); // Reflection: serialized option
                StartAndRun(rig, 52.25f); // exactly 7.75 s remain

                Call(rig.Container, "Update");

                Assert.That(rig.TimerText.color, Is.EqualTo(Normal));
            });
            Run(rig =>
            {
                Set(rig.Container, "m_lowTimeRemainingThresholdSeconds", 7.75f); // Reflection: serialized option
                StartAndRun(rig, 52.25f);

                Call(rig.Container, "Update");

                Assert.That(rig.TimerText.color, Is.EqualTo(Normal), "counting up in a time trial");
            }, countsDown: false, timeTrial: true);
        }

        // OnBonusTimeAdded 0x669400: the bonus text is switched on and shows the amount in the bonus format, and its display
        // countdown starts from the full duration.
        [Test]
        public void BonusTimeShowsTheAmountAndRestartsTheDisplayCountdown()
        {
            Run(rig =>
            {
                Call(rig.Container, "TryInitialise");
                var animation = Get<Animation>(rig.Container, "m_bonusTimeAnimation");
                animation.clip = rig.Owned.Track(new AnimationClip { legacy = true, name = "Lucid bonus" }); // a default clip for Play
                rig.BonusText.enabled = false;

                Call(rig.Container, "OnBonusTimeAdded", 3f);

                Assert.That(rig.BonusText.enabled, Is.True);
                Assert.That(rig.BonusText.text, Is.EqualTo("+3s"));
                Assert.That(Get<float>(rig.Container, "m_bonusTimeDisplayTimeRemainingSeconds"), Is.EqualTo(2f));
            });
        }

        // Without a fail state the elapsed time is shown (the time limit minus the time remaining, 0x668F29..0x668F35): 52.25 s is
        // 00|52|25. The low-time colour only applies to a time trial (0x668F09).
        [Test]
        public void WithoutAFailStateTheElapsedTimeIsShownAndOnlyATimeTrialGoesLow()
        {
            Run(rig =>
            {
                StartAndRun(rig, 52.25f); // 7.75 s remain; 52.25 s elapsed

                Call(rig.Container, "Update");

                Assert.That(rig.TimerText.text, Is.EqualTo("00|52|25"));
                Assert.That(rig.TimerText.color, Is.EqualTo(Normal), "not a time trial: no low-time colour");
            }, countsDown: false);
            Run(rig =>
            {
                StartAndRun(rig, 52.25f);

                Call(rig.Container, "Update");

                Assert.That(rig.TimerText.color, Is.EqualTo(Alternate), "a time trial goes low");
            }, countsDown: false, timeTrial: true);
        }

        // A negative time remaining (bonus time taken off) is alternate whatever the mission (the ucomiss of zero against the time
        // remaining at 0x668DC1, or-ed into the colour index). The bonus is taken off before the container hooks the timer.
        [Test]
        public void ANegativeTimeRemainingIsAlwaysAlternate()
        {
            Run(rig =>
            {
                StartAndRun(rig, 12.25f);
                rig.Timer.AddBonusTime(-100f); // -52.25 s remain

                Call(rig.Container, "Update");

                Assert.That(rig.TimerText.color, Is.EqualTo(Alternate));
            }, countsDown: false);
        }

        // Update 0x668AB0 with a timer that is not running: the timer object is hidden (ClearValues with the timer's own visibility
        // flag 0x668ECA), and a hidden timer (m_isHidden, +0x78) keeps the object off while it runs (sete at 0x668C9C).
        [Test]
        public void AStoppedTimerHidesTheObjectAndAHiddenTimerKeepsItOff()
        {
            Run(rig =>
            {
                rig.EnableRoot.SetActive(true);

                Call(rig.Container, "Update");

                Assert.That(rig.EnableRoot.activeSelf, Is.False, "the timer has not been started");

                rig.Timer.Action_StartTimer();
                Call(rig.Container, "Update");

                Assert.That(rig.EnableRoot.activeSelf, Is.True, "running");

                Set(rig.Timer, "m_isHidden", true); // Reflection: serialized option
                Call(rig.Container, "Update");

                Assert.That(rig.EnableRoot.activeSelf, Is.False, "hidden while it runs");
            });
            Run(rig =>
            {
                Set(rig.Timer, "m_alwaysVisible", true); // Reflection: serialized option
                rig.EnableRoot.SetActive(true);

                Call(rig.Container, "Update");

                Assert.That(rig.EnableRoot.activeSelf, Is.True, "an always visible timer stays shown while stopped");
            });
        }

        // Update with a tracker that has no timer: the timer object is hidden.
        [Test]
        public void WithoutATimerTheObjectIsHidden()
        {
            Run(rig =>
            {
                rig.EnableRoot.SetActive(true);

                Call(rig.Container, "Update");

                Assert.That(rig.EnableRoot.activeSelf, Is.False);
            }, timed: false);
        }

        // Update 0x668AB0 for a timer that can be paused: the paused-state object is shown while the timer is enabled but not
        // running, the running-state object while it is enabled and running, and neither while the timer component is disabled.
        [Test]
        public void ThePauseObjectsFollowTheTimersEnabledAndRunningState()
        {
            Run(rig =>
            {
                Func<bool[]> shown = () => new[] { rig.PauseEnabledRoot.activeSelf, rig.PauseDisabledRoot.activeSelf };

                Call(rig.Container, "Update");
                Assert.That(shown(), Is.EqualTo(new[] { true, false }), "enabled and not running");

                rig.Timer.Action_StartTimer();
                Call(rig.Container, "Update");
                Assert.That(shown(), Is.EqualTo(new[] { false, true }), "enabled and running");

                rig.Timer.enabled = false;
                Call(rig.Container, "Update");
                Assert.That(shown(), Is.EqualTo(new[] { false, false }), "disabled");
            }, canBePaused: true);
        }

        // The bonus text fades along the curve (x scaled to the duration, 0x668E27..0x668E6B) and is switched off once its countdown
        // has run out (0x668EED). Only the ends are checked, which do not depend on the frame time: a duration so long that the
        // countdown stays at its full value (the curve's first key, 0.25) and a countdown that is already over.
        [Test]
        public void TheBonusTextShowsTheCurveStartWhileCountingDownAndSwitchesOffWhenOver()
        {
            Run(rig =>
            {
                rig.Timer.Action_StartTimer();
                Set(rig.Container, "m_bonusTimeDisplayDurationSeconds", 1e9f); // Reflection: serialized option
                Call(rig.Container, "TryInitialise");
                Set(rig.Container, "m_bonusTimeDisplayTimeRemainingSeconds", 1e9f); // Reflection: written by OnBonusTimeAdded
                rig.BonusText.enabled = true;

                Call(rig.Container, "Update");

                Assert.That(rig.BonusText.alpha, Is.EqualTo(0.25f));
                Assert.That(rig.BonusText.enabled, Is.True);

                Set(rig.Container, "m_bonusTimeDisplayTimeRemainingSeconds", 0f);

                Call(rig.Container, "Update");

                Assert.That(rig.BonusText.enabled, Is.False);
            });
        }

        // OnDestroy 0x669480 unhooks the timer; a container that never initialised has nothing to unhook.
        [Test]
        public void DestroyingUnhooksTheTimer()
        {
            Run(rig =>
            {
                Call(rig.Container, "OnDestroy");
                Call(rig.Container, "TryInitialise");
                Assert.That(Hooks(rig.Timer.OnBonusTimeAdded, rig.Container), Is.EqualTo(1));

                Call(rig.Container, "OnDestroy");

                Assert.That(Hooks(rig.Timer.OnBonusTimeAdded, rig.Container), Is.EqualTo(0));
            });
        }

        // Initialising again after the container was re-enabled drops the old hook before adding the new one (0x6691E9..0x669251), so
        // there is one subscription.
        [Test]
        public void ReinitialisingKeepsASingleHook()
        {
            Run(rig =>
            {
                Call(rig.Container, "TryInitialise");
                Call(rig.Container, "OnEnable");

                Call(rig.Container, "TryInitialise");

                Assert.That(Hooks(rig.Timer.OnBonusTimeAdded, rig.Container), Is.EqualTo(1));
            });
        }
    }
}
