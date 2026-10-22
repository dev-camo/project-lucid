using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSettings (Game.Runtime 0x02000922, OnEnable 0x6794C0, constructor 0x6795D0) as a real component on an
    // owned inactive host (no OnEnable runs, so each case calls it itself). The services it reads are real: the App
    // and DataManager of OwnedCharacterManagers, a ScalablePerformance holding an empty profile, and a
    // VisualQualityManager_SDT whose "graphics switching" requirement is an authored quality level.
    public sealed class UIContainerSettingsTests
    {
        private static void WithServices(PerformanceProfile.QualityLevel requiredLevel, System.Action<Owned, App, UIContainerSettings, GameObject> body) =>
            Owned.Run(owned =>
            {
                // Taken first, so it is released last: the rows of the App, DataManager and SaveManager.
                owned.Lease(new OwnedCharacterManagers());
                var performance = owned.Register(owned.Host("Lucid scalable performance", false).AddComponent<ScalablePerformance>());
                performance.SetPerformanceProfile(owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>()));
                var quality = owned.Register(owned.Host("Lucid visual quality manager", false).AddComponent<VisualQualityManager_SDT>());
                var feature = owned.Track(ScriptableObject.CreateInstance<ScalableFeature>());
                feature.ID = "lucid-graphics-switching";
                // Serialized in the original. The profile holds no attribute for the feature, so IsSupported answers
                // whether the required level is above Medium.
                Set(quality, "m_allowGraphicsQualitySwitching", new PerformanceAttribute { Feature = feature, Level = requiredLevel });
                var container = owned.Host("Lucid settings screen", false).AddComponent<UIContainerSettings>();
                GameObject debug = owned.Host("Lucid debug settings");
                Set(container, "m_debugSettings", debug); // serialized in the original
                body(owned, ProcessManager.GetSystem<App>(), container, debug);
            });

        // OnEnable 0x6794C0: the three binders take the App's metagame and gameplay flags and the quality manager's
        // answer, in that order, and the debug page is switched off.
        [Test]
        public void EnablingPublishesTheMetaGameFlagAndTheGraphicsAvailability()
        {
            WithServices(PerformanceProfile.QualityLevel.High, (owned, app, container, debug) =>
            {
                app.Storage.SetValue(AppFSMKeys.MetaGameActive, true);
                Assert.That(debug.activeSelf, Is.True);

                Call(container, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(container.IsInMetaGame.Value, Is.True);
                Assert.That(container.IsInGameplay.Value, Is.False);
                Assert.That(container.GraphicsSettingsActive.Value, Is.True);
                Assert.That(debug.activeSelf, Is.False);
            });
        }

        [Test]
        public void EnablingPublishesTheGameplayFlagAndAnUnavailableGraphicsSetting()
        {
            WithServices(PerformanceProfile.QualityLevel.Medium, (owned, app, container, debug) =>
            {
                app.Storage.SetValue(AppFSMKeys.GameplayActive, true);

                Call(container, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(container.IsInMetaGame.Value, Is.False);
                Assert.That(container.IsInGameplay.Value, Is.True);
                Assert.That(container.GraphicsSettingsActive.Value, Is.False);
                Assert.That(debug.activeSelf, Is.False);
            });
        }

        // The binders are built by the constructor (0x6795D0), each its own bindable and still empty.
        [Test]
        public void EachFlagHasItsOwnBinder()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid settings screen", false).AddComponent<UIContainerSettings>();
                Assert.That(container.IsInMetaGame, Is.Not.Null);
                Assert.That(container.IsInGameplay, Is.Not.SameAs(container.IsInMetaGame));
                Assert.That(container.GraphicsSettingsActive, Is.Not.SameAs(container.IsInGameplay));
                Assert.That(container.GraphicsSettingsActive, Is.Not.SameAs(container.IsInMetaGame));
                Assert.That(container.IsInMetaGame.Value, Is.False);
            });
        }
    }
}
