using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.JSON;
using Hardlight.Networking;
using NUnit.Framework;
using UnityEngine.Networking;

namespace ProjectLucid.Tests
{
    // Compiler-generated closures and iterators of HTTPRequestBehaviour (HLNetworking.Runtime),
    // BaseControllerProvider (HLInput.Runtime) and AnalyticsSystem (HLAnalytics.Runtime).
    // Expected flags and fields come from the shipping DummyDll TypeDef rows and dump.cs.
    //
    // Open ordinal gap: the shipping suffixes (d__26, DisplayClass25_0, d__18, d__59,
    // DisplayClass69_0, d__84, d__87`1) count original members that IL2CPP stripped and
    // left no metadata for. The surviving declarations emit lower suffixes, so these tests
    // pin the stem, flags and capture layout and leave the ordinal open instead of
    // asserting a number the source cannot reach without invented members.
    public sealed class OriginalSmallLibsGeneratedMembersTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // NestedPrivate|AutoLayout|AnsiClass|Sealed|BeforeFieldInit on every generated row.
        private const TypeAttributes GeneratedFlags = TypeAttributes.NestedPrivate | TypeAttributes.Sealed |
            TypeAttributes.BeforeFieldInit;

        private static readonly Type[] IteratorInterfaces = { typeof(IEnumerator<object>), typeof(IEnumerator), typeof(IDisposable) };

        // TypeDefs 0x02000008 <>c__DisplayClass25_0 {uri +0x10, request +0x18, callback +0x20}
        // with <post>b__0(UnityWebRequest) 0x06000020 (x86-64 0x1a4b930), and 0x02000009
        // <sendRequestInternal>d__26 {<>1__state, <>2__current, request, callback}.
        [Test]
        public void HTTPRequestBehaviourClosureAndIteratorKeepTheShippingLayout()
        {
            Type[] generated = Generated(typeof(HTTPRequestBehaviour));
            Assert.That(generated.Length, Is.EqualTo(2));

            Type closure = Single(generated, @"^<>c__DisplayClass\d+_0$");
            AssertFields(closure, ("uri", typeof(Uri)), ("request", typeof(UnityWebRequest)),
                ("callback", typeof(HTTPRequestBehaviour.JSONResponseCallback)));
            AssertLambda(closure, "<post>b__0", typeof(void), typeof(UnityWebRequest));

            Type machine = Machine(typeof(HTTPRequestBehaviour), "sendRequestInternal", generated);
            AssertIteratorFields(machine, ("request", typeof(UnityWebRequest)), ("callback", typeof(Action<UnityWebRequest>)));
        }

        // TypeDef 0x0200001a <ControllerConnectionPolling>d__18 {<>1__state +0x10,
        // <>2__current +0x18, <>4__this +0x20}; MoveNext 0x0600005b (x86-64 0x1a1b570)
        // reads the provider only through <>4__this. No closure exists on this owner.
        [Test]
        public void BaseControllerProviderPollingIteratorKeepsTheShippingLayout()
        {
            Type[] generated = Generated(typeof(BaseControllerProvider));
            Assert.That(generated.Length, Is.EqualTo(1));

            Type machine = Machine(typeof(BaseControllerProvider), "ControllerConnectionPolling", generated);
            AssertIteratorFields(machine, ("<>4__this", typeof(BaseControllerProvider)));
        }

        // TypeDefs 0x02000006 <>c__DisplayClass69_0 {<>4__this +0x10, eventDataHolderList +0x18,
        // shouldWriteFailedEvents +0x20, isPostInProgress +0x21} with
        // <AnalyticsServerCallback>b__0(Status, JSONHashtable); 0x02000008 d__59 and
        // 0x02000007 d__84 {<>1__state, <>2__current, <>4__this}; 0x02000009 d__87`1
        // {<>1__state, <>2__current, analyticsEvent T, <>4__this, category, arrayIndex}.
        [Test]
        public void AnalyticsSystemClosureAndIteratorsKeepTheShippingLayout()
        {
            Type[] generated = Generated(typeof(AnalyticsSystem));
            Assert.That(generated.Length, Is.EqualTo(4));

            Type closure = Single(generated, @"^<>c__DisplayClass\d+_0$");
            AssertFields(closure, ("<>4__this", typeof(AnalyticsSystem)),
                ("eventDataHolderList", typeof(List<AnalyticsEventDataHolder>)),
                ("shouldWriteFailedEvents", typeof(bool)), ("isPostInProgress", typeof(bool)));
            AssertLambda(closure, "<AnalyticsServerCallback>b__0", typeof(void),
                typeof(HTTPRequestBehaviour.Status), typeof(JSONHashtable));

            Type sending = Machine(typeof(AnalyticsSystem), "WaitAnalyticsEventsSendingAllowance", generated);
            AssertIteratorFields(sending, ("<>4__this", typeof(AnalyticsSystem)));

            Type collection = Machine(typeof(AnalyticsSystem), "WaitAnalyticsEventsCollectionNotAllowed", generated);
            AssertIteratorFields(collection, ("<>4__this", typeof(AnalyticsSystem)));

            Type late = Machine(typeof(AnalyticsSystem), "WaitLateDataBeforePushingAnalyticsEventToManualQueue", generated);
            Assert.That(late.IsGenericTypeDefinition, Is.True);
            Assert.That(late.Name, Does.EndWith("`1"));
            Type t = late.GetGenericArguments().Single();
            AssertIteratorFields(late, ("analyticsEvent", t), ("<>4__this", typeof(AnalyticsSystem)),
                ("category", typeof(int)), ("arrayIndex", typeof(int)));
        }

        private static Type[] Generated(Type owner)
        {
            return owner.GetNestedTypes(Declared)
                .Where(nested => nested.IsDefined(typeof(CompilerGeneratedAttribute), false))
                .ToArray();
        }

        private static Type Single(Type[] generated, string pattern)
        {
            Type[] matches = generated.Where(type => Regex.IsMatch(type.Name, pattern)).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), pattern);
            Assert.That(matches[0].Attributes, Is.EqualTo(GeneratedFlags), matches[0].Name);
            return matches[0];
        }

        // The kickoff's [IteratorStateMachine] names the machine; the stem must be the
        // kickoff's own name, so a renamed or hand-written machine fails here.
        private static Type Machine(Type owner, string kickoff, Type[] generated)
        {
            MethodInfo method = owner.GetMethod(kickoff, Declared);
            Assert.That(method, Is.Not.Null, kickoff);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(IEnumerator)), kickoff);
            Type machine = method.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
            Assert.That(machine, Is.Not.Null, kickoff);
            Assert.That(generated, Does.Contain(machine), kickoff);
            Assert.That(machine.Name, Does.Match("^<" + kickoff + @">d__\d+(`1)?$"));
            Assert.That(machine.Attributes, Is.EqualTo(GeneratedFlags), machine.Name);
            Assert.That(machine.GetInterfaces(), Is.EquivalentTo(IteratorInterfaces), machine.Name);
            return machine;
        }

        private static void AssertIteratorFields(Type machine, params (string name, Type type)[] captured)
        {
            var expected = new List<(string, Type)> { ("<>1__state", typeof(int)), ("<>2__current", typeof(object)) };
            expected.AddRange(captured);
            AssertFields(machine, expected.ToArray());
        }

        private static void AssertFields(Type type, params (string name, Type type)[] expected)
        {
            FieldInfo[] fields = type.GetFields(Declared).OrderBy(field => field.MetadataToken).ToArray();
            Assert.That(fields.Select(field => field.Name), Is.EqualTo(expected.Select(field => field.name)), type.Name);
            Assert.That(fields.Select(field => field.FieldType), Is.EqualTo(expected.Select(field => field.type)), type.Name);
        }

        private static void AssertLambda(Type closure, string name, Type returnType, params Type[] parameters)
        {
            MethodInfo[] methods = closure.GetMethods(Declared);
            Assert.That(methods.Select(method => method.Name), Is.EqualTo(new[] { name }), closure.Name);
            Assert.That(methods[0].IsAssembly, Is.True, name);
            Assert.That(methods[0].ReturnType, Is.EqualTo(returnType), name);
            Assert.That(methods[0].GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(parameters), name);
        }
    }
}
