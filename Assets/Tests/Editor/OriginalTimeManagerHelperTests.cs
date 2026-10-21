using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.TimeScaledTestHarness;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TimeManagerDebugUtilities (TypeDef 0x020007f2, x86-64 0x5F7A20..0x5F7F8F) and
    // TimeManagerSaveIntegration (0x020007f3, 0x5F8010..0x5F844F). Components are real, on owned inactive hosts
    // that never wake; the TimeManager is a real component that only holds its settings. The debug utilities
    // read and the cases set Unity's global time scale, which each case puts back. OnSaveGameLoaded and the debug
    // input registration are not covered: they need the runtime time configuration and the shared input tables.
    public sealed class OriginalTimeManagerHelperTests
    {
        private static void WithDebugUtilities(Action<Scene, TimeManagerDebugUtilities> body) => WithManager(scene =>
        {
            float priorTimeScale = Time.timeScale;
            scene.Owned.Lease(new ReleaseAction(() => Time.timeScale = priorTimeScale));
            var utilities = scene.Owned.Host("Lucid owned time debug utilities", false).AddComponent<TimeManagerDebugUtilities>();
            Set(utilities, "m_timeManager", scene.Manager); // serialized in the original
            body(scene, utilities);
        });

        // 0x5f7ac0: the first of the 15 settings (5, 2, 1, .95, .9, .85, .8, .75, .7, .65, .6, .5, .25, .1, 0) that
        // the time scale exceeds with 0.001 of slack below the setting (0x5f7af6 addss -0.001), else the last.
        [Test]
        public void TheSpeedSettingIndexIsTheFirstSettingTheTimeScaleExceeds()
        {
            WithDebugUtilities((scene, utilities) =>
            {
                foreach ((float timeScale, int index) in new[]
                {
                    (10f, 0), (5f, 0), (2f, 1), (1.5f, 2), (1f, 2), (0.9991f, 2), (0.999f, 3), (0.95f, 3),
                    (0.9f, 4), (0.5f, 11), (0.3f, 12), (0.25f, 12), (0.1f, 13), (0.05f, 14), (0f, 14),
                })
                {
                    Time.timeScale = timeScale;
                    Assert.That(Call(utilities, "GetDebugSpeedSettingIndex"), Is.EqualTo(index), "time scale " + timeScale);
                }
            });
        }

        // 0x5f7b20: one setting faster (index - 1), never before the first.
        [Test]
        public void SpeedingUpSelectsTheNextFasterSettingAndStopsAtTheFirst()
        {
            WithDebugUtilities((scene, utilities) =>
            {
                foreach ((float timeScale, float expected) in new[] { (1f, 2f), (10f, 5f), (0f, 0.1f), (0.95f, 1f) })
                {
                    Time.timeScale = timeScale;
                    Call(utilities, "OnDebugGameSpeedInc", 0f);
                    Assert.That(scene.Manager.GetUnityTimescale(), Is.EqualTo(expected), "from " + timeScale);
                    Assert.That(scene.Manager.UnityTimescaleOverride, Is.True);
                }
            });
        }

        // 0x5f7ba0: one setting slower (index + 1), never past the last.
        [Test]
        public void SlowingDownSelectsTheNextSlowerSettingAndStopsAtTheLast()
        {
            WithDebugUtilities((scene, utilities) =>
            {
                foreach ((float timeScale, float expected) in new[] { (1f, 0.95f), (10f, 2f), (0f, 0f), (0.1f, 0f) })
                {
                    Time.timeScale = timeScale;
                    Call(utilities, "OnDebugGameSpeedDec", 0f);
                    Assert.That(scene.Manager.GetUnityTimescale(), Is.EqualTo(expected), "from " + timeScale);
                }
            });
        }

        // 0x5f7a40: at the last (zero) setting the pause toggle resumes at 1, anywhere else it pauses at 0; the
        // override is the debug kind, so zero is accepted.
        [Test]
        public void TheGamePauseToggleResumesFromZeroAndPausesOtherwise()
        {
            WithDebugUtilities((scene, utilities) =>
            {
                Time.timeScale = 1f;
                Call(utilities, "OnDebugGamePause", 0f);
                Assert.That(scene.Manager.GetUnityTimescale(), Is.EqualTo(0f));
                Assert.That(scene.Manager.UnityTimescaleOverride, Is.True);
                Time.timeScale = 0f;
                Call(utilities, "OnDebugGamePause", 0f);
                Assert.That(scene.Manager.GetUnityTimescale(), Is.EqualTo(1f));
            });
        }

        // 0x5f7eb0: the label is bold only while Unity's time scale is overridden and within 0.001 of the setting.
        [Test]
        public void TheButtonLabelIsBoldOnlyForTheOverriddenSetting()
        {
            WithDebugUtilities((scene, utilities) =>
            {
                Time.timeScale = 2f;
                Assert.That(Call(utilities, "TimescaleButtonText", 2f), Is.EqualTo("2x"), "no override");
                scene.Manager.OverrideUnityTimescale(2f);
                Assert.That(Call(utilities, "TimescaleButtonText", 2f), Is.EqualTo("**2x**"));
                Assert.That(Call(utilities, "TimescaleButtonText", 5f), Is.EqualTo("5x"), "another setting");
                Time.timeScale = 2.0005f;
                Assert.That(Call(utilities, "TimescaleButtonText", 2f), Is.EqualTo("**2x**"), "within 0.001");
                Time.timeScale = 2.002f;
                Assert.That(Call(utilities, "TimescaleButtonText", 2f), Is.EqualTo("2x"), "beyond 0.001");
            });
        }

        // 0x5f8010 hands an observer to the save manager's reference for each of its two events; the reference is
        // valid, so both run at once and append the handlers (06002e08, 06002e09).
        [Test]
        public void StartSubscribesToTheSaveLoadedAndSavedEvents()
        {
            Owned.Run(owned =>
            {
                owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
                SaveManager save = owned.Register(new SaveManager());
                var integration = owned.Host("Lucid owned time save integration", false).AddComponent<TimeManagerSaveIntegration>();
                CollectionAssert.DoesNotContain(HandlerNames(save.OnLoadCompleted), "OnSaveGameLoaded");
                CollectionAssert.DoesNotContain(HandlerNames(save.OnSaveCompleted), "OnGameSaved");
                Call(integration, "Start");
                CollectionAssert.Contains(HandlerNames(save.OnLoadCompleted), "OnSaveGameLoaded");
                CollectionAssert.Contains(HandlerNames(save.OnSaveCompleted), "OnGameSaved");
            });
        }

        // 0x5f81f0: every save pushes the saved settings' game speed to the time manager, which only stores it.
        [Test]
        public void ASaveStoresTheSavedGameSpeedInTheTimeManager()
        {
            Owned.Run(owned =>
            {
                owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
                owned.LeaseRow<TimeManager>();
                SaveManager save = owned.Register(new SaveManager());
                TimeManager manager = NewTimeManager(owned, TimeCategory.Environment, 1f);
                var integration = owned.Host("Lucid owned time save integration", false).AddComponent<TimeManagerSaveIntegration>();
                Set(integration, "m_timeManager", manager); // serialized in the original
                var settings = new SaveDataSettings { GameSpeed = 1.5f };
                Set(save, "m_saveSettings", settings); // loaded by the save flow in the original
                Call(integration, "OnGameSaved");
                Assert.That(Get<float>(manager, "m_overrideSpeed"), Is.EqualTo(1.5f));
                settings.GameSpeed = 0.5f;
                Call(integration, "OnGameSaved");
                Assert.That(Get<float>(manager, "m_overrideSpeed"), Is.EqualTo(0.5f));
            });
        }
    }
}
