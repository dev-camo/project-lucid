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

            // Each target is first loaded as a delegate and then really called,
            // so a filter keyed on target identity instead of opcode would show.
            // The static group is ldnull; ldftn; newobj (or a cached field load).
            public static void DelegateThenCallSameTarget(Fixture fixture)
            {
                Sink = new Action(fixture.InstanceTarget);
                fixture.InstanceTarget();
                Sink = new Action(StaticTarget);
                StaticTarget();
            }

            // constrained. (FE 16, type token) prefixes the callvirt.
            public static void ConstrainedCall<T>(T value) where T : IDisposable
            {
                value.Dispose();
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

            public static void CallsBoth(ContainerProbe container, EventProbe modernEvent)
            {
                Sink = container.FindByName("container");
                Sink = modernEvent.FindByName("event");
            }

            public static void CallsBothReversed(ContainerProbe container, EventProbe modernEvent)
            {
                Sink = modernEvent.FindByName("event");
                Sink = container.FindByName("container");
            }
        }

        private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Declared);

        private static string[] Lookups(string body) =>
            OriginalILCallSequence.Names(OriginalILCallSequence.Calls(Method(typeof(LookupFixture), body)))
                .Where(name => name.EndsWith(".FindByName", StringComparison.Ordinal)).ToArray();

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
            Assert.That(Lookups(nameof(LookupFixture.DelegatesOnly)), Is.Empty);
        }

        // Positive control for the same filter and probe types: real FindByName
        // calls pass it, in IL order, and the reversed body is distinguishable.
        [Test]
        public void RealFindByNameCallsSatisfyTheLookupOrderFilterInIlOrder()
        {
            string[] expected = { "ContainerProbe.FindByName", "EventProbe.FindByName" };
            Assert.That(Lookups(nameof(LookupFixture.CallsBoth)), Is.EqualTo(expected));
            Assert.That(Lookups(nameof(LookupFixture.CallsBothReversed)), Is.EqualTo(new[] { "EventProbe.FindByName", "ContainerProbe.FindByName" }));
            Assert.That(Lookups(nameof(LookupFixture.CallsBothReversed)), Is.Not.EqualTo(expected));
        }

        // A delegate load followed by a real call to the same target yields one
        // record per target, sitting on the call opcode itself: callvirt (0x6F)
        // for the instance target and call (0x28) for the static one, never the
        // two-byte ldftn (FE 06) that precedes each delegate constructor.
        [Test]
        public void DelegateThenRealCallRecordsOnlyTheCallInstruction()
        {
            MethodInfo body = Method(typeof(Fixture), nameof(Fixture.DelegateThenCallSameTarget));
            byte[] il = body.GetMethodBody().GetILAsByteArray();
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(body);
            Assert.That(OriginalILCallSequence.Names(calls), Is.EqualTo(new[]
            {
                "Action..ctor", "Fixture.InstanceTarget", "Action..ctor", "Fixture.StaticTarget",
            }));
            Assert.That(calls[1].Target, Is.EqualTo(Method(typeof(Fixture), nameof(Fixture.InstanceTarget))));
            Assert.That(il[calls[1].Offset], Is.EqualTo(0x6F));
            Assert.That(calls[3].Target, Is.EqualTo(Method(typeof(Fixture), nameof(Fixture.StaticTarget))));
            Assert.That(il[calls[3].Offset], Is.EqualTo(0x28));
            Assert.That(calls[1].Offset, Is.GreaterThan(calls[0].Offset));
            Assert.That(calls[3].Offset, Is.GreaterThan(calls[2].Offset));
        }

        // The constrained. prefix and its type token are skipped, and the
        // following callvirt is recorded against the interface method.
        [Test]
        public void ConstrainedCallvirtIsRecordedAfterItsPrefix()
        {
            MethodInfo body = Method(typeof(Fixture), nameof(Fixture.ConstrainedCall));
            byte[] il = body.GetMethodBody().GetILAsByteArray();
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(body);
            Assert.That(calls.Select(call => call.Target).ToArray(), Is.EqualTo(new MethodBase[] { typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose)) }));
            Assert.That(il[calls[0].Offset], Is.EqualTo(0x6F));
            Assert.That(new[] { il[calls[0].Offset - 6], il[calls[0].Offset - 5] }, Is.EqualTo(new byte[] { 0xFE, 0x16 }));
        }
    }
}
