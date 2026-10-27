using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime RigidbodyForceTrigger constructor (060029d3 @0x5D44C0): the field initialisers of a newly
    // added component, as the shipped constructor stores them.
    public sealed class RigidbodyForceTriggerDefaultsTests
    {
        [Test]
        public void NewTriggerKeepsTheOriginalConstructorDefaults()
        {
            Owned.Run(owned =>
            {
                var trigger = owned.Host("Lucid rigidbody force trigger", false).AddComponent<RigidbodyForceTrigger>();
                // 0x5d451c stores 2 at +0x58: ForceMode.VelocityChange, a mass-independent change.
                Assert.That(Get<ForceMode>(trigger, "m_forceMode"), Is.EqualTo(ForceMode.VelocityChange));
                Assert.That(Get<TimeCategory>(trigger, "m_timeCategory"), Is.EqualTo(TimeCategory.PlayerPhysics));
                Assert.That(Get<string>(trigger, "m_colliderTagToCheck"), Is.EqualTo("Player"));
                Assert.That(Get<float>(trigger, "m_resultantForceMultiplier"), Is.EqualTo(1f));
                Assert.That(Get<bool>(trigger, "m_gripReset"), Is.True);
            });
        }
    }
}
