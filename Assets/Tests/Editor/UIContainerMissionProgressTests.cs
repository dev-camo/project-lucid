using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerMissionProgressParameters (Game.Runtime 0x020008e2, ctor 0x6353B0, getter 0x63ECA0) and
    // UIContainerMissionProgress (0x020008e0, 0x63E740..0x63EA9A, Hide state machine MoveNext 0x63EAD0) over a real
    // instanced tracker. Not covered: the HUD fade on an active host (Refresh with m_alwaysDisplay off starts the Hide
    // coroutine, which Edit Mode does not run) and the HUD layout of the serialized flags.
    public sealed class UIContainerMissionProgressTests
    {
        private static readonly Dictionary<Strings, string> OwnedStrings = new Dictionary<Strings, string>
        {
            { (Strings)(-336558325), "{0} of {1}" }, // 0xEBF0870B, the container's default m_localisedStringFormat (0x63EA9A)
        };

        private static Sprite NewSprite(Owned owned) =>
            owned.Track(Sprite.Create(owned.Track(new Texture2D(1, 1)), new Rect(0f, 0f, 1f, 1f), Vector2.zero));

        // An always-displayed container (its Refresh starts no coroutine) on an inactive host, over an initialised tracker
        // of a mission with the given total objective count and no objectives.
        private static void Run(int objectives, System.Action<Owned, UIContainerMissionProgress, MissionInstancedTracker, MissionDefinition> body) =>
            WithWorld(world =>
            {
                Owned owned = world.Owned;
                Mission mission = world.NewMission(objectives, objectives);
                var tracker = world.NewTracker<MissionInstancedTracker>();
                Set(tracker, "m_missionObjectives", new List<MissionObjective>()); // Reflection: authored (empty) objective list
                tracker.Initialise(mission.State, null);
                var container = owned.Host("Lucid mission progress", false).AddComponent<UIContainerMissionProgress>();
                Set(container, "m_alwaysDisplay", true); // Reflection: serialized option
                body(owned, container, tracker, mission.Definition);
            });

        [Test]
        public void TheParametersKeepTheirInstancedTracker()
        {
            WithWorld(world =>
            {
                Assert.That(new UIContainerMissionProgressParameters(null).InstancedTracker, Is.Null);
                var tracker = world.NewTracker<MissionInstancedTracker>();
                Assert.That(new UIContainerMissionProgressParameters(tracker).InstancedTracker, Is.SameAs(tracker));
            });
        }

        // The constructor (0x63E970) stores the string 0xEBF0870B at +0x58 and the doubleword pair at +0x6C from the
        // read-only data at 0x29A7AD0, which holds the floats 2.0 and 1.0.
        [Test]
        public void DeclarationKeepsTheAuthoredDefaults()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid mission progress", false).AddComponent<UIContainerMissionProgress>();
                Assert.That((int)Get<Strings>(container, "m_localisedStringFormat"), Is.EqualTo(-336558325));
                Assert.That(Get<float>(container, "m_displaySeconds"), Is.EqualTo(2f));
                Assert.That(Get<float>(container, "m_fadeTimeSeconds"), Is.EqualTo(1f));
                Assert.That(container.AlwaysDisplay, Is.False);
                Assert.That(container.OnlyShowWhenMissionIslandActive, Is.False);
                Assert.That(container.InstancedTracker, Is.Null);
            });
        }

        [Test]
        public void TheGettersReadTheirSerializedFlags()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid mission progress", false).AddComponent<UIContainerMissionProgress>();
                Set(container, "m_alwaysDisplay", true); // Reflection: serialized flags
                Assert.That(container.AlwaysDisplay, Is.True);
                Assert.That(container.OnlyShowWhenMissionIslandActive, Is.False);
                Set(container, "m_onlyShowWhenMissionIslandActive", true);
                Assert.That(container.OnlyShowWhenMissionIslandActive, Is.True);
            });
        }

        // OnEnable 0x63E790: the group starts invisible unless the container is always displayed.
        [Test]
        public void EnablingHidesTheGroupUnlessTheContainerIsAlwaysDisplayed()
        {
            foreach (bool always in new[] { false, true })
            {
                Owned.Run(owned =>
                {
                    var container = owned.Host("Lucid mission progress", false).AddComponent<UIContainerMissionProgress>();
                    var group = container.gameObject.AddComponent<CanvasGroup>();
                    Set(container, "m_canvasGroup", group); // Reflection: serialized reference
                    Set(container, "m_alwaysDisplay", always);
                    Assert.That(group.alpha, Is.EqualTo(1f));

                    Call(container, "OnEnable");

                    Assert.That(group.alpha, Is.EqualTo(always ? 1f : 0f), "always displayed: " + always);
                });
            }
        }

        // Refresh 0x635600 (inline Setup 0x63E7B0): a mission progress icon replaces the "x of y" text and is the shown icon
        // while complete < target (cmp r15d, r13d; cmovl at 0x635736); without one, or with the objectives complete, the
        // tracker's icon is used (here its mission's objective icon, the tracker having none).
        [Test]
        public void TheProgressIconReplacesTheTextAndTheObjectiveIconOnlyWhileTheObjectivesAreIncomplete()
        {
            // (objectives, has progress icon) -> (shown icon is the progress icon, text)
            foreach ((int objectives, bool hasProgressIcon, bool progressIconShown, string text) in new[]
            {
                (2, true, true, ""),
                (0, true, false, ""),
                (2, false, false, "0 of 2"),
            })
            {
                string row = "objectives " + objectives + ", progress icon " + hasProgressIcon;
                Run(objectives, (owned, container, tracker, definition) => OwnedStringTable.Run(OwnedStrings, () =>
                {
                    Sprite objectiveIcon = NewSprite(owned);
                    Sprite progressIcon = NewSprite(owned);
                    Set(definition, "m_missionObjectiveIcon", objectiveIcon); // Reflection: serialized icons
                    if (hasProgressIcon) Set(definition, "m_missionProgressIcon", progressIcon);
                    container.MissionProgress.Value = "stale";

                    container.Setup(new UIContainerMissionProgressParameters(tracker));

                    Assert.That(container.InstancedTracker, Is.SameAs(tracker), row);
                    Assert.That(container.gameObject.activeSelf, Is.True, row);
                    Assert.That(container.MissionIcon.Value, Is.SameAs(progressIconShown ? progressIcon : objectiveIcon), row);
                    Assert.That(container.MissionProgress.Value, Is.EqualTo(text), row);
                    Assert.That(container.ObjectivesComplete.Value, Is.EqualTo(0), row);
                    Assert.That(container.ObjectivesTarget.Value, Is.EqualTo(objectives), row);
                }));
            }
        }

        // MoveNext 0x63EAD0: the display time is waited out (a null yield per frame, nothing else written); then the group
        // is set to full alpha and yields, and each later step subtracts deltaTime / fadeTime (divss, subss) until the value
        // is no longer above zero (ucomiss, ja at 0x63EB65), when the alpha is zeroed and the container deactivated. A zero
        // fade time makes the step NaN or infinite whatever the frame time is, so the first later step ends the fade.
        [Test]
        public void HideWaitsOutTheDisplayTimeThenFadesTheGroupAndDeactivatesTheContainer()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid mission progress", false).AddComponent<UIContainerMissionProgress>();
                var group = container.gameObject.AddComponent<CanvasGroup>();
                Set(container, "m_canvasGroup", group); // Reflection: serialized reference and timings
                Set(container, "m_displaySeconds", 3600f);
                Set(container, "m_fadeTimeSeconds", 0f);
                group.alpha = 0.5f;
                container.gameObject.SetActive(true);
                group.alpha = 0.5f;

                var hide = (IEnumerator)Call(container, "Hide");
                Assert.That(hide.MoveNext(), Is.True, "waiting out the display time");
                Assert.That(hide.Current, Is.Null);
                Assert.That(hide.MoveNext(), Is.True, "still waiting: an hour has not passed");
                Assert.That(group.alpha, Is.EqualTo(0.5f), "nothing is written while waiting");

                Set(container, "m_displaySeconds", -1f);
                var fade = (IEnumerator)Call(container, "Hide");
                Assert.That(fade.MoveNext(), Is.True, "the display time is already over");
                Assert.That(fade.Current, Is.Null);
                Assert.That(group.alpha, Is.EqualTo(1f), "the fade starts from full alpha");
                Assert.That(container.gameObject.activeSelf, Is.True);

                Assert.That(fade.MoveNext(), Is.False);
                Assert.That(group.alpha, Is.EqualTo(0f));
                Assert.That(container.gameObject.activeSelf, Is.False);
            });
        }

        // MoveNext 0x63EAD0 keeps waiting only while the display deadline is strictly after the current time (ucomiss until, time;
        // jbe at 0x63EBC5 starts the fade when until <= time). Time.time does not advance inside this synchronous test, so with no
        // display time the deadline equals the current time and the first step must already be the fade, at full alpha.
        [Test]
        public void AZeroDisplayTimeStartsTheFadeOnTheFirstStep()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid mission progress", false).AddComponent<UIContainerMissionProgress>();
                var group = container.gameObject.AddComponent<CanvasGroup>();
                Set(container, "m_canvasGroup", group); // Reflection: serialized reference and timings
                Set(container, "m_displaySeconds", 0f);
                Set(container, "m_fadeTimeSeconds", 0f);
                container.gameObject.SetActive(true);
                group.alpha = 0.5f;

                var hide = (IEnumerator)Call(container, "Hide");

                Assert.That(hide.MoveNext(), Is.True);
                Assert.That(group.alpha, Is.EqualTo(1f), "no wait: the fade has begun");
            });
        }

        // Refresh 0x635600 reads the tracker's icon (op_Inequality on +0x118 at 0x6356A4) before the mission's objective icon. The
        // tracker takes the first incomplete objective's icon when an objective reports its status, so completing the second of two
        // objectives gives it the first one's icon, which is neither the mission's objective icon nor the other objective's.
        [Test]
        public void TheTrackersIconIsShownWhenItHasOne()
        {
            WithWorld(world =>
            {
                Owned owned = world.Owned;
                Mission mission = world.NewMission(2, 2);
                var tracker = world.NewTracker<MissionInstancedTracker>();
                var objectives = new List<MissionObjective>
                {
                    world.NewObjective<MissionObjective>(tracker.transform),
                    world.NewObjective<MissionObjective>(tracker.transform),
                };
                Sprite firstIcon = NewSprite(owned);
                Sprite secondIcon = NewSprite(owned);
                Sprite objectiveIcon = NewSprite(owned);
                Set(objectives[0], "m_icon", firstIcon); // Reflection: authored serialized icons
                Set(objectives[1], "m_icon", secondIcon);
                Set(tracker, "m_missionObjectives", objectives); // Reflection: authored list (the editor Reset finds none on inactive hosts)
                Author(tracker, "m_onCompleted"); // Reflection: authored (empty) events
                Author(tracker, "m_onAnyObjectiveCompleted");
                Author(tracker, "m_onLastObjective");
                tracker.Initialise(mission.State, null);
                tracker.StartMission(false);
                objectives[1].Action_ObjectiveComplete();
                Assert.That(tracker.Icon, Is.SameAs(firstIcon), "the first incomplete objective's icon");
                Set(mission.Definition, "m_missionObjectiveIcon", objectiveIcon); // Reflection: serialized icon
                var container = owned.Host("Lucid mission progress", false).AddComponent<UIContainerMissionProgress>();
                Set(container, "m_alwaysDisplay", true); // Reflection: serialized option

                OwnedStringTable.Run(OwnedStrings, () => container.Setup(new UIContainerMissionProgressParameters(tracker)));

                Assert.That(container.MissionIcon.Value, Is.SameAs(firstIcon));
                Assert.That(container.ObjectivesComplete.Value, Is.EqualTo(1));
                Assert.That(container.MissionProgress.Value, Is.EqualTo("1 of 2"));
            });
        }

        // DummyDll TypeDef 0x020008e0 field rows 0x040023a6..0x040023a8 (and dump.cs offsets 0x78, 0x80, 0x88) put the
        // InstancedTracker backing field between the last serialized flag and the coroutine field.
        [Test]
        public void TheInstancedTrackerBackingFieldSitsBetweenTheSerializedFlagAndTheCoroutine()
        {
            var names = new List<string>();
            foreach (System.Reflection.FieldInfo field in typeof(UIContainerMissionProgress).GetFields(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                names.Add(field.Name);
            names.Sort((a, b) => typeof(UIContainerMissionProgress).GetField(a, (System.Reflection.BindingFlags)(-1)).MetadataToken
                .CompareTo(typeof(UIContainerMissionProgress).GetField(b, (System.Reflection.BindingFlags)(-1)).MetadataToken));
            int flag = names.IndexOf("m_onlyShowWhenMissionIslandActive");
            Assert.That(names[flag + 1], Is.EqualTo("<InstancedTracker>k__BackingField"));
            Assert.That(names[flag + 2], Is.EqualTo("m_hideUICoroutine"));
            Assert.That(names[flag + 3], Is.EqualTo("m_missionManager"));
        }
    }
}
