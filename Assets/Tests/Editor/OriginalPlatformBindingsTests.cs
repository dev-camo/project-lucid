using Hardlight.UI.Binding;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.PlatformBindings (TypeDef 0x0200083a, x86-64 0x608C90..0x608DB2).
    public sealed class OriginalPlatformBindingsTests
    {
        [Test]
        public void DeclarationsKeepTheOriginalFieldsAndAttributes()
        {
            AssertMembers(typeof(PlatformBindings), 0x00100001,
                new[] { ("m_debugEnablePlatforms", 0x0001), ("m_debugIOS", 0x0001), ("m_debugTouchDevice", 0x0001), ("m_debugTvOS", 0x0001),
                        ("m_debugMacOs", 0x0001), ("IsIOS", 0x0026), ("IsTouchDevice", 0x0026), ("IsTvOS", 0x0026), ("IsMacOS", 0x0026) },
                new[] { ("Awake", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(PlatformBindings), 1, 2);
            foreach (var (field, row) in new[]
            {
                ("m_debugEnablePlatforms", "SerializeField()"),
                ("m_debugIOS", "ShowIfAttribute(m_debugEnablePlatforms,null) SerializeField()"),
                ("m_debugTouchDevice", "SerializeField() ShowIfAttribute(m_debugEnablePlatforms,null)"),
                ("m_debugTvOS", "ShowIfAttribute(m_debugEnablePlatforms,null) SerializeField()"),
                ("m_debugMacOs", "ShowIfAttribute(m_debugEnablePlatforms,null) SerializeField()"),
            })
                Assert.That(WorldMemberAccess.AttributeRow(typeof(PlatformBindings).GetField(field, Declared)), Is.EqualTo(row), field);
        }

        // .ctor 0x608CC0 allocates four distinct Bindable<bool>() in field order. Awake 0x608C90 makes
        // one virtual call, slot 9 (0x1C8, set_Value) on IsMacOS (+0x40) with true; nothing else is
        // set, and the debug overrides are not read.
        [Test]
        public void AwakeSetsOnlyTheMacOSFlag()
        {
            Owned.Run(owned =>
            {
                var bindings = owned.Host("Lucid platform bindings").AddComponent<PlatformBindings>();
                Bindable<bool>[] flags = { bindings.IsIOS, bindings.IsTouchDevice, bindings.IsTvOS, bindings.IsMacOS };
                Assert.That(flags, Is.Unique);
                Assert.That(flags, Has.None.Null);
                Assert.That(Get<bool>(bindings.IsMacOS, "initialised"), Is.False);
                int changes = 0;
                bindings.IsMacOS.AddListener(() => changes++);
                foreach (string field in new[] { "m_debugEnablePlatforms", "m_debugIOS", "m_debugTouchDevice", "m_debugTvOS" })
                    Set(bindings, field, true);

                Call(bindings, "Awake");
                Assert.That((bindings.IsIOS.Value, bindings.IsTouchDevice.Value, bindings.IsTvOS.Value, bindings.IsMacOS.Value), Is.EqualTo((false, false, false, true)));
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(Get<bool>(bindings.IsIOS, "initialised"), Is.False, "IsIOS is never written");
            });
        }
    }
}
