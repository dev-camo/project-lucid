using System;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // Compiled own-field order must match the original 1.10.1 Game.Runtime field rows.
    // This covers metadata order only, not native object layout or ability behavior.
    public sealed class OriginalAbilityFieldOrderTests
    {
        [Test]
        public void IdleRetainsOriginalFieldOrder() // type 0x020002ce, fields 0x04000ac2..ac3
        {
            AssertFieldOrder(typeof(CharacterAbility_Idle), "<IsTriggered>k__BackingField", "m_enterIdleElapsedTime");
        }

        [Test]
        public void GroundBoostBaseRetainsOriginalFieldOrder() // type 0x020002da, fields 0x04000ad8..ada
        {
            AssertFieldOrder(typeof(CharacterAbility_MovementGroundBoostBase<>), "<IsTriggered>k__BackingField", "m_queuedBoost", "m_timeActivated");
        }

        [Test]
        public void JumpBaseRetainsOriginalFieldOrder() // type 0x020002de, fields 0x04000adf..ae2
        {
            AssertFieldOrder(typeof(CharacterAbility_MovementJumpBase<>), "<Jump>k__BackingField", "m_queuedJump", "m_endJump", "m_timeActivated");
        }

        [Test]
        public void RailRetainsOriginalFieldOrder() // type 0x020002e0, fields 0x04000ae3..ae9
        {
            AssertFieldOrder(typeof(CharacterAbility_MovementRail<>), "<IsTriggered>k__BackingField", "<JumpOnRailTracker>k__BackingField",
                "m_inputDebounce", "m_queuedJump", "m_isAutoRail", "m_controlsEnabledHandle", "m_gravityEnabledHandle");
        }

        private static void AssertFieldOrder(Type type, params string[] names)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .OrderBy(field => field.MetadataToken).ToArray();
            Assert.That(fields.Select(field => field.Name), Is.EqualTo(names));
        }
    }
}
