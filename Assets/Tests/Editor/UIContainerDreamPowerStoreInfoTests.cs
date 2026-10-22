using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.CharacterSelectScene;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerDreamPowerStoreInfo (Game.Runtime 0x02000871, x86-64 0x617490..0x6175C0) as a real component on an owned inactive host
    // over the character select scene (a registered App and a SaveManager holding an open save). The inactive coroutine host of that
    // scene refuses the save request with the engine error that is expected below.
    public sealed class UIContainerDreamPowerStoreInfoTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class Rig
        {
            public UIContainerDreamPowerStoreInfo Info;
            public Animation Animation;
            public AnimationClip CloseClip;
            public UIModernEvent CloseEvent;
        }

        private static void Run(System.Action<Scene, Rig> body) => CharacterSelectScene.Run(scene =>
        {
            var rig = new Rig { Info = scene.Owned.Host("Lucid dream power store info", false).AddComponent<UIContainerDreamPowerStoreInfo>() };
            rig.Animation = scene.Owned.Host("Lucid dream power store info animation", true).AddComponent<Animation>();
            rig.Animation.playAutomatically = false;
            rig.CloseClip = AddClip(scene.Owned, rig.Animation, "store info close");
            rig.CloseEvent = scene.Owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
            Set(rig.Info, "m_animation", rig.Animation); // Reflection: serialized reference the editor assigns
            Set(rig.Info, "m_closeClip", rig.CloseClip); // Reflection: authored serialized field
            Set(rig.Info, "m_closeEvent", rig.CloseEvent); // Reflection: authored serialized field
            body(scene, rig);
        });

        // Start 0x617490 takes the App and the SaveManager (0x6174F7, 0x61751D), then compares the store's new state with IsNew
        // (0xb7834d4b at 0x617547): only a new store is set to Seen (0x6d381853, 0x617557) and a save is requested (0x617565).
        [Test]
        public void StartMarksANewStoreAsSeenAndRequestsASave()
        {
            Run((scene, rig) =>
            {
                scene.Save.CurrentSave.DreamPowerStore.IsNewState = DreamPowerStoreIsNewState.IsNew;
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                Call(rig.Info, "Start");

                Assert.That(scene.Save.CurrentSave.DreamPowerStore.IsNewState, Is.EqualTo(DreamPowerStoreIsNewState.Seen));
                Assert.That(Get<App>(rig.Info, "m_app"), Is.SameAs(scene.App));
                Assert.That(Get<SaveManager>(rig.Info, "m_saveManager"), Is.SameAs(scene.Save));
            });
        }

        // Any other state is left alone and no save is requested (the branch at 0x61754E skips both calls); the unexpected-error check
        // of the test run fails the case if a save request is made.
        [TestCase(DreamPowerStoreIsNewState.None, TestName = "StartLeavesAnUnflaggedStoreAlone")]
        [TestCase(DreamPowerStoreIsNewState.Seen, TestName = "StartLeavesASeenStoreAlone")]
        public void StartLeavesAStoreThatIsNotNewAlone(DreamPowerStoreIsNewState state)
        {
            Run((scene, rig) =>
            {
                scene.Save.CurrentSave.DreamPowerStore.IsNewState = state;

                Call(rig.Info, "Start");

                Assert.That(scene.Save.CurrentSave.DreamPowerStore.IsNewState, Is.EqualTo(state));
                Assert.That(Get<App>(rig.Info, "m_app"), Is.SameAs(scene.App));
            });
        }

        // Action_Close 0x617570 plays the close clip by its name.
        [Test]
        public void ClosingPlaysTheCloseClip()
        {
            Run((scene, rig) =>
            {
                Assert.That(rig.Animation.IsPlaying(rig.CloseClip.name), Is.False);

                rig.Info.Action_Close();

                Assert.That(rig.Animation.IsPlaying(rig.CloseClip.name), Is.True);
            });
        }

        // Action_AnimationCloseFinish 0x6175A0 hands the close event to the App (m_app at +0x50) as a unique UI event, once however
        // often the animation finishes.
        [Test]
        public void TheFinishedCloseAnimationQueuesTheCloseEventOnce()
        {
            Run((scene, rig) =>
            {
                Call(rig.Info, "Start");

                rig.Info.Action_AnimationCloseFinish();
                rig.Info.Action_AnimationCloseFinish();

                List<UIModernEvent> events = scene.App.GetUIModernEvents();
                Assert.That(events, Is.EqualTo(new[] { rig.CloseEvent }));
            });
        }
    }
}
