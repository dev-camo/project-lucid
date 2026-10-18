using HardlightProject;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.HoverFan (TypeDef 0x02000ac7, x86-64 0x6E7A20..0x6E7CC0). The fan sits on an
    // owned GameObject with a BoxCollider, so its abstract RequireComponent(Collider) is met, and
    // the Character comes from its real constructor through AddComponent; EditMode runs neither
    // Awake. Character's field initializer creates the empty GameplayIslandManager row, which the
    // lifecycle-row lease admits and removes again once it is still empty.
    public sealed class OriginalHoverFanTests
    {
        private const int HoverDataModifier = 0x6b60803d;

        private static void WithFan(System.Action<HoverFan, Character, HoverData> body)
        {
            using (var islandRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<GameplayIslandManager>()))
            using (var objects = new OwnedWorldObjects("lucid-hover-fan"))
            {
                GameObject fanObject = objects.Create("fan");
                fanObject.AddComponent<BoxCollider>();
                HoverFan fan = fanObject.AddComponent<HoverFan>();
                var hoverData = new HoverData { TargetHeight = 2f };
                Set(fan, "m_hoverData", hoverData);
                Character character = objects.Create("character").AddComponent<Character>();
                body(fan, character, hoverData);
            }
        }

        private static StackableDataHandle Handle(HoverFan fan) => (StackableDataHandle)Get(fan, "m_characterHoverFanHandle");

        [Test]
        public void DeclarationsKeepTheOriginalFieldsAndMethods()
        {
            Assert.That((int)typeof(HoverFan).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(HoverFan).BaseType, Is.EqualTo(typeof(CharacterTrigger)));
            Assert.That(Fields(typeof(HoverFan)), Is.EqualTo(new[]
            {
                "m_hoverData:HoverData:0001", "m_characterHoverFanHandle:StackableDataHandle:0001"
            }));
            Assert.That(Methods(typeof(HoverFan)), Is.EqualTo(new[]
            {
                "DoTriggerEnter:00c4", "DoTriggerExit:00c4", "OnDrawGizmosSelected:0081", ".ctor:1886"
            }));
            Assert.That(AttributeRow(typeof(HoverFan)), Is.EqualTo(
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) RequireComponent(Collider)"));
            Assert.That((int)GameplayModifierType.HoverData, Is.EqualTo(HoverDataModifier));
        }

        // DoTriggerEnter 0x6E7A20 stores AddModifierOverride<HoverData>(0x6b60803d, m_hoverData);
        // DoTriggerExit 0x6E7A90 removes that handle and clears it.
        [Test]
        public void EnterLendsTheHoverDataAndExitRemovesIt()
        {
            WithFan((fan, character, hoverData) =>
            {
                Assert.That(character.HasHoverData(), Is.False);
                Call(fan, "DoTriggerEnter", character);
                StackableDataHandle handle = Handle(fan);
                Assert.That(handle, Is.Not.Null);
                Assert.That(character.GetModifierOverride<HoverData>(handle, HoverDataModifier), Is.SameAs(hoverData));
                Assert.That(character.HasHoverData(), Is.True);

                Call(fan, "DoTriggerExit", character);
                Assert.That(Handle(fan), Is.Null);
                Assert.That(character.HasHoverData(), Is.False);
            });
        }

        // A second entry overwrites the stored handle without removing the first override, so an
        // exit removes only the newer one and the character keeps the fan's hover data.
        [Test]
        public void ASecondEntryReplacesTheHandleWithoutRemovingTheFirst()
        {
            WithFan((fan, character, hoverData) =>
            {
                Call(fan, "DoTriggerEnter", character);
                StackableDataHandle first = Handle(fan);
                Call(fan, "DoTriggerEnter", character);
                Assert.That(Handle(fan), Is.Not.SameAs(first));
                Call(fan, "DoTriggerExit", character);
                Assert.That(character.HasHoverData(), Is.True);
                Assert.That(character.GetModifierOverride<HoverData>(first, HoverDataModifier), Is.SameAs(hoverData));
                character.RemoveModifierOverrides(first);
                Assert.That(character.HasHoverData(), Is.False);
            });
        }
    }
}
