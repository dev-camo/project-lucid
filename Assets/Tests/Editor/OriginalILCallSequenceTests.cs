using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // OriginalILCallSequence must report only call, callvirt and newobj targets.
    // ldftn and ldvirtftn carry the same InlineMethod token but only load a
    // function pointer (ECMA-335 III.3.41, III.4.18), so a body that merely
    // creates method-group delegates must not read as calling those methods.
    // Every fixture below is owned by this file; nothing is invoked.
    public sealed class OriginalILCallSequenceTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // Instance method groups are never cached by the compiler, so each
        // conversion below is ldftn or ldvirtftn followed by newobj Action::.ctor.
        private class Fixture
        {
            public static object Sink;

            public static void StaticTarget() { }

            public void InstanceTarget() { }

            public virtual void VirtualTarget() { }

            public static void DelegatesOnly(Fixture fixture)
            {
                Sink = new Action(fixture.InstanceTarget);
                Sink = new Action(fixture.VirtualTarget);
            }

            public static void Mixed(Fixture fixture)
            {
                StaticTarget();
                Sink = new Action(fixture.InstanceTarget);
                fixture.VirtualTarget();
                Sink = new Fixture();
            }
        }

        private sealed class ContainerProbe
        {
            public object FindByName(string name) => null;
        }

        private sealed class EventProbe
        {
            public object FindByName(string name) => null;
        }

        private static class LookupFixture
        {
            public static object Sink;

            public static void DelegatesOnly(ContainerProbe container, EventProbe modernEvent)
            {
                Sink = new Func<string, object>(container.FindByName);
                Sink = new Func<string, object>(modernEvent.FindByName);
            }
        }

        private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Declared);

        // Before the opcode filter, ldftn InstanceTarget and ldvirtftn
        // VirtualTarget were both reported. Only the two delegate constructors are calls.
        [Test]
        public void DelegateCreationAddsNoCallForTheReferencedMethods()
        {
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(Method(typeof(Fixture), nameof(Fixture.DelegatesOnly)));
            MethodBase[] targets = calls.Select(call => call.Target).ToArray();
            Assert.That(OriginalILCallSequence.Names(calls), Is.EqualTo(new[] { "Action..ctor", "Action..ctor" }));
            Assert.That(targets, Has.None.EqualTo(Method(typeof(Fixture), nameof(Fixture.InstanceTarget))));
            Assert.That(targets, Has.None.EqualTo(Method(typeof(Fixture), nameof(Fixture.VirtualTarget))));
        }

        // call StaticTarget, ldftn InstanceTarget + newobj Action, callvirt
        // VirtualTarget, newobj Fixture: the interleaved ldftn adds nothing and
        // the real targets keep strictly ascending IL offsets.
        [Test]
        public void RealCallsKeepIlOrderAroundAnInterleavedFunctionPointerLoad()
        {
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(Method(typeof(Fixture), nameof(Fixture.Mixed)));
            int[] offsets = calls.Select(call => call.Offset).ToArray();
            Assert.That(OriginalILCallSequence.Names(calls), Is.EqualTo(new[]
            {
                "Fixture.StaticTarget", "Action..ctor", "Fixture.VirtualTarget", "Fixture..ctor",
            }));
            Assert.That(offsets, Is.Ordered.Ascending);
            Assert.That(offsets.Distinct().Count(), Is.EqualTo(offsets.Length));
        }

        // The CloseContainer factory check filters names by ".FindByName". A body
        // that only builds FindByName delegates in the expected order must leave
        // that filter empty, so it cannot satisfy the factory assertion.
        [Test]
        public void FindByNameDelegatesDoNotSatisfyTheLookupOrderFilter()
        {
            MethodInfo body = Method(typeof(LookupFixture), nameof(LookupFixture.DelegatesOnly));
            string[] lookups = OriginalILCallSequence.Names(OriginalILCallSequence.Calls(body))
                .Where(name => name.EndsWith(".FindByName", StringComparison.Ordinal)).ToArray();
            Assert.That(lookups, Is.Empty);
        }
    }
}
