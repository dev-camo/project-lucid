using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSaveDataViewParameters (Game.Runtime 0x020009dc, x86-64 0x6A57B0..0x6A5818): the constructor 0x6A57D0 calls
    // the UIWidgetToggleParameters constructor (0x1A49F90) with the label and callback, then stores the save at +0x20 and
    // the progress at +0x28. A ProgressionCollectableSave takes the challenge and dream power store managers when it is built
    // (OwnedChallengeServices registers genuine ones).
    public sealed class UIContainerSaveDataViewParametersTests
    {
        [Test]
        public void TheParametersKeepTheSaveTheProgressTheLabelAndTheCallback()
        {
            Owned.Run(owned =>
            {
                OwnedChallengeServices.Register(owned);
                var save = new SaveDataGame();
                var progress = new ProgressionCollectableSave();
                var received = new List<bool>();

                var parameters = new UIContainerSaveDataViewParameters(save, progress, "Slot label", received.Add);

                Assert.That(parameters.SaveData, Is.SameAs(save));
                Assert.That(parameters.Progress, Is.SameAs(progress));
                Assert.That(parameters.Label, Is.EqualTo("Slot label"));
                parameters.Callback(true);
                Assert.That(received, Is.EqualTo(new[] { true }), "the callback is the one the base constructor stored");
            });
        }

        // Nothing is checked or replaced: an empty slot passes no save, and the label and callback may be absent.
        [Test]
        public void NothingIsValidatedOrReplaced()
        {
            var parameters = new UIContainerSaveDataViewParameters(null, null, null, null);

            Assert.That(parameters.SaveData, Is.Null);
            Assert.That(parameters.Progress, Is.Null);
            Assert.That(parameters.Label, Is.Null);
            Assert.That(parameters.Callback, Is.Null);
        }
    }
}
