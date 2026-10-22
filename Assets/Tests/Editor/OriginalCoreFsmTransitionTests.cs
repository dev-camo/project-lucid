using System;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Game.Runtime core FSM transitions built through their genuine factories on
    // an owned machine (skipAddToManager); no registry row or service is touched.
    public sealed class OriginalCoreFsmTransitionTests : OwnedGraphNamesFixture
    {
        public OriginalCoreFsmTransitionTests() : base("Lucid-core-fsm-") { }

        private static readonly FSMUpdateContext s_context = new FSMUpdateContext(0.25f, FSMUpdateType.Manual);

        private static string Name() => "Lucid-core-fsm-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        private static FSMUser User() => new FSMUser(new FSMStorage(0));

        // FSMTransitionIsTvOs.ConstructInstance 0x0600002e (x86-64 0x507b60) reads
        // TrueCausesTransition into +0x1C; DoUpdate 0x0600002f (0x507c00) returns
        // its negation because the platform test is folded to false. Returning the
        // flag itself, or ignoring it, fails one row of this table.
        [Test]
        public void IsTvOsFiresOnlyWhenFalseCausesTheTransition()
        {
            FiniteStateMachine machine = Machine();
            FSMUser user = User();
            IFSMTransition trueCauses = FSMTransitionIsTvOs.ConstructInstance(machine, Name(), "{\"TrueCausesTransition\":true}");
            IFSMTransition falseCauses = FSMTransitionIsTvOs.ConstructInstance(machine, Name(), "{\"TrueCausesTransition\":false}");
            Assert.That(trueCauses, Is.TypeOf<FSMTransitionIsTvOs>());
            Assert.That(trueCauses.Update(user, s_context), Is.False, "not tvOS, so true-causes never fires");
            Assert.That(falseCauses.Update(user, s_context), Is.True, "not tvOS, so false-causes fires");
            Assert.That(falseCauses.Update(user, s_context), Is.True, "the result does not change between updates");
        }

        // FSMTransitionDevelopmentOnly/EditorOnly DoUpdate 0x06000029 (0x507ab0) and
        // 0x0600002c (0x507b30) are xor eax,eax in this player build, and their
        // ConstructInstance 0x06000028/0x0600002b ignore the JSON arguments.
        [Test]
        public void DevelopmentAndEditorOnlyNeverFireAndIgnoreTheirJson()
        {
            FiniteStateMachine machine = Machine();
            FSMUser user = User();
            foreach (string json in new[] { string.Empty, "{}", "{\"TrueCausesTransition\":true}" })
            {
                IFSMTransition development = FSMTransitionDevelopmentOnly.ConstructInstance(machine, Name(), json);
                IFSMTransition editor = FSMTransitionEditorOnly.ConstructInstance(machine, Name(), json);
                Assert.That(development, Is.TypeOf<FSMTransitionDevelopmentOnly>());
                Assert.That(editor, Is.TypeOf<FSMTransitionEditorOnly>());
                Assert.That(development.Update(user, s_context), Is.False, json);
                Assert.That(editor.Update(user, s_context), Is.False, json);
            }
        }
    }
}
