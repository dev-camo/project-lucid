using System;
using System.Reflection;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime EventInvocationInfo (TypeDef 0x02000002, x86-64 0x505580..0x505CF0)
    // against a runtime probe whose public methods record each reflected call. The type has no
    // Il2CppSetOption attributes, so its bodies keep IL2CPP's null and bounds checks.
    public sealed class OriginalEventInvocationInfoTests
    {
        private static MethodInfo Method(string name) => typeof(EventInvocationTargetProbe).GetMethod(name);

        private static void WithProbe(Action<EventInvocationTargetProbe> body) =>
            Owned.Run(owned => body(owned.Host("Lucid event invocation target", false).AddComponent<EventInvocationTargetProbe>()));

        // Metadata of TypeDef 0x02000002 (0x00100001, BeforeFieldInit). The .cctor 0x505CF0 fills
        // a four-element Type[] with string, float, int, bool (0x505dd5, 0x505e23, 0x505e6e,
        // 0x505eb7). The constructor 0x505580 stores the key, method and target.
        [Test]
        public void DeclarationsSupportedTypesAndConstructorStores()
        {
            AssertMembers(typeof(EventInvocationInfo), 0x00100001,
                new[] { ("TargetBehaviour", 0x0006), ("MethodInfo", 0x0006), ("SupportedTypes", 0x0016), ("Key", 0x0006) },
                new[] { (".ctor", 0x1886), ("Invoke", 0x0086), ("InvokeEnum", 0x0086), ("InvokeNoArgs", 0x0086), ("Invoke", 0x0086), (".cctor", 0x1891) });
            Assert.That(AttributeTypes(typeof(EventInvocationInfo)), Is.Empty);
            Assert.That(EventInvocationInfo.SupportedTypes, Is.EqualTo(new[] { typeof(string), typeof(float), typeof(int), typeof(bool) }));

            WithProbe(probe =>
            {
                MethodInfo method = Method(nameof(EventInvocationTargetProbe.NoArguments));
                var info = new EventInvocationInfo("lucid-key", probe, method);
                Assert.That(info.Key, Is.EqualTo("lucid-key"));
                Assert.That(info.TargetBehaviour, Is.SameAs(probe));
                Assert.That(info.MethodInfo, Is.SameAs(method));
            });
        }

        // Invoke(object) 0x5055E0 tests MethodInfo != null (0x505615) and passes a one-element
        // object[]; it has no handler, so the target's exception reaches the caller. InvokeNoArgs
        // 0x505820 passes a null argument array behind the same test.
        [Test]
        public void DirectInvocationsSkipAMissingMethodAndPropagateFailures()
        {
            WithProbe(probe =>
            {
                var argument = new object();
                new EventInvocationInfo("object", probe, Method(nameof(EventInvocationTargetProbe.TakeObject))).Invoke(argument);
                new EventInvocationInfo("none", probe, Method(nameof(EventInvocationTargetProbe.NoArguments))).InvokeNoArgs();
                Assert.That(probe.Calls, Is.EqualTo(new[] { "TakeObject", "NoArguments" }));
                Assert.That(probe.Arguments[0], Is.SameAs(argument));

                var missing = new EventInvocationInfo("missing", probe, null);
                Assert.DoesNotThrow(() => missing.Invoke(argument));
                Assert.DoesNotThrow(missing.InvokeNoArgs);
                Assert.That(probe.Calls.Count, Is.EqualTo(2));

                var failing = new EventInvocationInfo("fail", probe, Method(nameof(EventInvocationTargetProbe.Fail)));
                Assert.Throws<TargetInvocationException>(failing.InvokeNoArgs);
                Assert.That(probe.Calls[2], Is.EqualTo("Fail"));
            });
        }

        // InvokeEnum 0x5056C0 reads GetParameters()[0].ParameterType (slots 16 and 11) before
        // the inlined null test, then Enum.ToObject (0x505769) boxes the parameter's enum type.
        [Test]
        public void EnumInvocationBoxesTheParameterEnumAndFaultsWithoutAMethod()
        {
            WithProbe(probe =>
            {
                new EventInvocationInfo("state", probe, Method(nameof(EventInvocationTargetProbe.TakeState))).InvokeEnum(2);
                Assert.That(probe.Calls, Is.EqualTo(new[] { "TakeState" }));
                Assert.That(probe.Arguments[0], Is.EqualTo(EventInvocationTargetProbe.ProbeState.Two));

                Assert.Throws<NullReferenceException>(() => new EventInvocationInfo("missing", probe, null).InvokeEnum(1));
                Assert.Throws<IndexOutOfRangeException>(() =>
                    new EventInvocationInfo("none", probe, Method(nameof(EventInvocationTargetProbe.NoArguments))).InvokeEnum(1));
                Assert.That(probe.Calls.Count, Is.EqualTo(1));
            });
        }

        // Invoke(bool, string) 0x505870: an enum parameter (Type slot 69, IsEnum) takes
        // ConvertToType<int> boxed as int; otherwise only a SupportedTypes member converts,
        // string passing the text itself and failed parses their default. Every call sits in a
        // catch-all handler (0x505c74), so faults and target exceptions are swallowed.
        [Test]
        public void TextInvocationConvertsSupportedParametersAndSwallowsEveryFault()
        {
            WithProbe(probe =>
            {
                EventInvocationInfo Info(string name) => new EventInvocationInfo(name, probe, name == null ? null : Method(name));
                string text = new string('a', 3);
                Info(nameof(EventInvocationTargetProbe.TakeString)).Invoke(true, text);
                Info(nameof(EventInvocationTargetProbe.TakeInt)).Invoke(true, "42");
                Info(nameof(EventInvocationTargetProbe.TakeInt)).Invoke(true, "not a number");
                Info(nameof(EventInvocationTargetProbe.TakeFloat)).Invoke(true, "2");
                Info(nameof(EventInvocationTargetProbe.TakeBool)).Invoke(true, "True");
                Info(nameof(EventInvocationTargetProbe.TakeState)).Invoke(true, "2");
                Assert.That(probe.Calls, Is.EqualTo(new[] { "TakeString", "TakeInt", "TakeInt", "TakeFloat", "TakeBool", "TakeState" }));
                Assert.That(probe.Arguments[0], Is.SameAs(text), "the text itself is passed");
                Assert.That(probe.Arguments[1], Is.EqualTo(42));
                Assert.That(probe.Arguments[2], Is.EqualTo(0), "a failed parse passes the default");
                Assert.That(probe.Arguments[3], Is.EqualTo(2f));
                Assert.That(probe.Arguments[4], Is.EqualTo(true));
                Assert.That(probe.Arguments[5], Is.EqualTo(EventInvocationTargetProbe.ProbeState.Two));

                probe.Calls.Clear();
                Info(nameof(EventInvocationTargetProbe.TakeDouble)).Invoke(true, "2");
                Info(nameof(EventInvocationTargetProbe.TakeObject)).Invoke(true, "2");
                Assert.That(probe.Calls, Is.Empty, "unsupported parameter types are ignored");

                Info(nameof(EventInvocationTargetProbe.NoArguments)).Invoke(false, "ignored");
                Assert.That(probe.Calls, Is.EqualTo(new[] { "NoArguments" }));
                Assert.DoesNotThrow(() => Info(nameof(EventInvocationTargetProbe.Fail)).Invoke(false, "ignored"), "target exception");
                Assert.DoesNotThrow(() => Info(nameof(EventInvocationTargetProbe.TakeInt)).Invoke(false, "1"), "parameter count");
                Assert.DoesNotThrow(() => Info(nameof(EventInvocationTargetProbe.NoArguments)).Invoke(true, "1"), "missing first parameter");
                Assert.DoesNotThrow(() => Info(null).Invoke(true, "1"), "missing method");
                Assert.DoesNotThrow(() => Info(null).Invoke(false, "1"));
                Assert.That(probe.Calls, Is.EqualTo(new[] { "NoArguments", "Fail" }));
            });
        }
    }
}
