using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.SettingsPageTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // InvertCameraSetting (Game.Runtime 0x02000835; Awake 0x607E80, OnSaveManagerValid 0x607F60, Action_InvertCameraSetting
    // 0x607F80) as a real component on an owned inactive host over the SaveManager of SettingsPageTestSupport, which holds
    // live settings marked saved. Awake is called by the case, as Unity does not run it on an inactive host.
    public sealed class InvertCameraSettingTests
    {
        // Awake takes the SaveManager through its reference (0x607F2E, InvokeOnValid 0x607F4D), which is valid here, so the
        // callback stores it at once (+0x20).
        [Test]
        public void AwakeTakesTheSaveManager()
        {
            With(page =>
            {
                var setting = Add<InvertCameraSetting>(page);
                Assert.That(Get<SaveManager>(setting, "m_saveManager"), Is.Null);

                Call(setting, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Assert.That(Get<SaveManager>(setting, "m_saveManager"), Is.SameAs(page.Save));
            });
        }

        // The action 0x607F80 writes the opposite of the stored flag (sete at 0x607FA4) and requests a settings save
        // (0x607FBB), which serialises the settings and marks them saved.
        [Test]
        public void TheActionFlipsTheStoredFlagBothWaysAndSavesEachTime()
        {
            With(page =>
            {
                var setting = Add<InvertCameraSetting>(page);
                Call(setting, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(page.Settings.CameraInvertedControls, Is.False);
                Assert.That(SavedJson(page), Is.Null);

                setting.Action_InvertCameraSetting();

                Assert.That(page.Settings.CameraInvertedControls, Is.True);
                StringAssert.Contains("\"m_cameraInvertedControls\":true", SavedJson(page));
                Assert.That(page.Settings.HasChangesToSave(), Is.False, "the save request marks the settings saved");

                setting.Action_InvertCameraSetting();

                Assert.That(page.Settings.CameraInvertedControls, Is.False);
                StringAssert.Contains("\"m_cameraInvertedControls\":false", SavedJson(page));
            });
        }

        // Before Awake there is no SaveManager (0x607F8A tests +0x20): nothing is changed or saved.
        [Test]
        public void TheActionBeforeAwakeDoesNothing()
        {
            With(page =>
            {
                var setting = Add<InvertCameraSetting>(page);

                setting.Action_InvertCameraSetting();

                Assert.That(page.Settings.CameraInvertedControls, Is.False);
                Assert.That(SavedJson(page), Is.Null);
            });
        }

        // A SaveManager without loaded settings (the test at 0x607F99) is neither written to nor asked to save.
        [Test]
        public void TheActionWithoutLoadedSettingsDoesNothing()
        {
            With(page =>
            {
                // Reflection: the manager's settings have no setter; the state before the save flow has loaded them is the case.
                Set(page.Save, "m_saveSettings", null);
                var setting = Add<InvertCameraSetting>(page);
                Call(setting, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                setting.Action_InvertCameraSetting();

                Assert.That(SavedJson(page), Is.Null);
            });
        }
    }
}
