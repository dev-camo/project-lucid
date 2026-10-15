using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Tests
{
    // Discriminating companions to OriginalMainMenuFSMTests. Owned machines only;
    // no audio, UI, save or App boundary is constructed or entered.
    public class OriginalMainMenuFSMWeakRemediationTests
    {
        private static string Name() => "Lucid-mainmenu-" + Guid.NewGuid().ToString("N");

        // 0x060006fa at x86-64 0x64ef60 parses Clip (args+0x10) at 0x64efde before
        // Source (args+0x18) at 0x64eff4. With both names invalid, or with an invalid
        // clip and no source, only the clip failure can surface. Catches swapping the
        // two EnumUtilities.Parse calls, which the single-field cases cannot see.
        [Test]
        public void AudioClipParsePrecedesSourceParseWhenBothFail()
        {
            var machine = new FiniteStateMachine(Name(), skipAddToManager: true);
            int count = machine.States.Count;
            ArgumentException both = Assert.Throws<ArgumentException>(() => ApplicationStatePlayAudio.ConstructInstance(machine, Name(),
                "{\"Clip\":\"lucid_not_a_real_clip\",\"Source\":\"lucid_not_a_real_source\"}"));
            StringAssert.Contains("lucid_not_a_real_clip", both.Message);
            StringAssert.DoesNotContain("lucid_not_a_real_source", both.Message);
            Assert.That(machine.States.Count, Is.EqualTo(count));
            // An absent Source would raise ArgumentNullException if it were parsed first.
            ArgumentException clipFirst = Assert.Throws<ArgumentException>(() => ApplicationStatePlayAudio.ConstructInstance(machine, Name(),
                "{\"Clip\":\"lucid_not_a_real_clip\"}"));
            StringAssert.Contains("lucid_not_a_real_clip", clipFirst.Message);
            Assert.That(machine.States.Count, Is.EqualTo(count));
        }

        // Complete original metadata v31 CAD row order, including the menu row
        // that the sibling test reads separately: LoadGameSave 0x020000e2,
        // EnableMenuRenderScale 0x02000156, UIMessage 0x02000196 [menu, bounds, null];
        // PlayAudio 0x02000101 [bounds, null, menu]; PublishStateMessage 0x02000113
        // [null, menu, bounds]; CloseUIContainer 0x02000198 [menu, null, bounds].
        [Test]
        public void MainMenuFamiliesKeepOriginalCompleteAttributeRowOrder()
        {
            var expected = new (Type Owner, string[] Rows)[]
            {
                (typeof(ApplicationStateLoadGameSave), new[] { "menu", "bounds", "null" }),
                (typeof(ApplicationStateEnableMenuRenderScale), new[] { "menu", "bounds", "null" }),
                (typeof(ApplicationStatePlayAudio), new[] { "bounds", "null", "menu" }),
                (typeof(ApplicationStatePublishStateMessage), new[] { "null", "menu", "bounds" }),
                (typeof(ApplicationTransitionUIMessage), new[] { "menu", "bounds", "null" }),
                (typeof(ApplicationTransitionUIMessageCloseUIContainer), new[] { "menu", "null", "bounds" }),
            };
            foreach (var (owner, rows) in expected)
            {
                string[] actual = owner.GetCustomAttributesData().Select(Row).ToArray();
                Assert.That(actual, Is.EqualTo(rows), owner.Name);
            }
        }

        private static string Row(CustomAttributeData attribute)
        {
            Assert.That(attribute.NamedArguments, Is.Empty);
            if (attribute.AttributeType == typeof(GraphNodeMenuFormatAttribute))
            {
                Assert.That(attribute.ConstructorArguments.Select(a => a.Value), Is.EqualTo(new object[] { "Application/{0}" }));
                return "menu";
            }
            Assert.That(attribute.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
            Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(2));
            Assert.That(attribute.ConstructorArguments[1].Value, Is.EqualTo(false));
            int option = Convert.ToInt32(attribute.ConstructorArguments[0].Value);
            Assert.That(option, Is.EqualTo((int)Option.NullChecks).Or.EqualTo((int)Option.ArrayBoundsChecks));
            return option == (int)Option.NullChecks ? "null" : "bounds";
        }
    }
}
