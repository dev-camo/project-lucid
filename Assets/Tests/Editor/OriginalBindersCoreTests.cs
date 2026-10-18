using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight.UI.Binding;
using Hardlight.UI.Binding.Nodes;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored HLBinders.Runtime binder owners, exercised on components of one owned inactive
    // GameObject (no Awake or OnEnable message runs) with real ConstantValue/Constant<T> outputs
    // as binding sources. Protected lifecycle members are invoked by reflection. Expected values
    // come from the 1.10.1 x86-64 bodies cited per case. BindingUtils' genuine member cache may
    // retain the binder types' reflection metadata.
    public sealed class OriginalBindersCoreTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> owned = new List<Object>();
        private GameObject host;

        [SetUp]
        public void CreateInactiveHost()
        {
            Assert.IsFalse(Application.isPlaying, "Owned cases require the Editor binding path");
            host = Own(new GameObject("Owned inactive binder case"));
            host.SetActive(false);
        }

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            host = null;
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }

        private T Add<T>() where T : Component => host.AddComponent<T>();

        private static void Invoke(object target, string method)
        {
            try { target.GetType().GetMethod(method, Hidden).Invoke(target, null); }
            catch (TargetInvocationException failure) when (failure.InnerException != null) { throw failure.InnerException; }
        }

        private static object BindReferences(BinderNode node) =>
            typeof(BinderNode).GetField("bindReferences", Hidden).GetValue(node);

        private ConstantValue IntSource(int value)
        {
            ConstantValue source = Add<ConstantValue>();
            source.IntValue = value;
            return source;
        }

        // .ctor 0x19F4D40 stores 0x0101 over filterTargets/playModeOnly; get_IsSet 0x19F9960 needs
        // a live target and a non-empty name. Cache 0x19F9AC0 types a property by CanWrite, a
        // readonly field as null and a void one-argument method by its parameter; on no member
        // (0x19f9b7c) it clears the three infos and skips the targetType store, so the previous
        // type survives. SetValue 0x19F48D0 turns any value for a string member into its ToString,
        // or String.Empty for null (0x19f49d0..0x19f49fe).
        [Test]
        public void BindTargetTypesItsMemberConvertsStringsAndKeepsTheLastTypeForAMissingMember()
        {
            ConstantValue node = Add<ConstantValue>();
            BindTarget target = new BindTarget();
            Assert.IsTrue(target.PlayModeOnly);
            Assert.That(typeof(BindTarget).GetField("filterTargets", Hidden).GetValue(target), Is.True);
            Assert.IsFalse(target.IsSet);
            Assert.IsNull(target.GetTargetType());

            target.SetTarget(node, "StringValue");
            Assert.IsTrue(target.IsSet);
            Assert.That(target.GetTargetType(), Is.EqualTo(typeof(string)));
            target.SetValue(42);
            Assert.That(node.StringValue, Is.EqualTo("42"));
            target.SetValue(null);
            Assert.That(node.StringValue, Is.Not.Null.And.EqualTo(string.Empty));

            target.SetTarget(node, "IntValue");
            Assert.That(target.GetTargetType(), Is.EqualTo(typeof(int)));
            target.SetValue(7);
            Assert.That(node.IntValue, Is.EqualTo(7));
            Assert.That(node.IntOutput.Value, Is.EqualTo(7), "the property setter refreshes the node");

            target.SetTarget(node, "set_FloatValue");
            Assert.That(target.GetTargetType(), Is.EqualTo(typeof(float)));
            target.SetValue(2.5f);
            Assert.That(node.FloatValue, Is.EqualTo(2.5f));

            Bindable<int> output = node.IntOutput;
            target.SetTarget(node, "IntOutput");
            Assert.IsNull(target.GetTargetType(), "a readonly field has no settable type");
            target.SetValue(new Bindable<int>(9));
            Assert.That(node.IntOutput, Is.SameAs(output));

            target.SetTarget(node, "IntValue");
            target.SetTarget(node, "NoSuchMember");
            Assert.IsTrue(target.IsSet);
            Assert.That(target.GetTargetType(), Is.EqualTo(typeof(int)), "no member leaves the previous type");
            target.SetValue(11);
            Assert.That(node.IntValue, Is.EqualTo(7));

            target.SetTarget(node, string.Empty);
            Assert.IsFalse(target.IsSet);
            Assert.IsNull(target.GetTargetType());
            Assert.That(target.MemberName, Is.EqualTo(string.Empty));
            Assert.That(target.Target, Is.SameAs(node));
            LogAssert.NoUnexpectedReceived();
        }

        // Equals 0x19F9EA0: a non-null other with Object.op_Equality targets and String.op_Equality
        // names; playModeOnly and filterTargets take no part, and two unset targets are equal.
        [Test]
        public void BindTargetEqualityComparesOnlyTargetAndMemberName()
        {
            ConstantValue first = Add<ConstantValue>();
            ConstantValue second = Add<ConstantValue>();
            BindTarget a = new BindTarget();
            BindTarget b = new BindTarget();
            Assert.IsTrue(a.Equals(b), "two unset targets");
            Assert.IsFalse(a.Equals(null));

            a.SetTarget(first, "IntValue");
            b.SetTarget(first, "IntValue");
            typeof(BindTarget).GetField("playModeOnly", Hidden).SetValue(b, false);
            Assert.IsTrue(a.Equals(b));
            b.SetTarget(first, "FloatValue");
            Assert.IsFalse(a.Equals(b));
            b.SetTarget(second, "IntValue");
            Assert.IsFalse(a.Equals(b));
            LogAssert.NoUnexpectedReceived();
        }

        // BinderOutput.Refresh 0x19F4700 forwards Source.GetValue<object>() to Target.SetValue only
        // when Source.IsSet, Target is non-null and the behaviour is enabled.
        // SetUpBindReferenceFields 0x19F4B00 lists Source alone; ShowInNodeEditor stays true.
        [Test]
        public void BinderOutputForwardsOnlyWhenSourceIsSetTargetExistsAndEnabled()
        {
            ConstantValue source = IntSource(5);
            ConstantValue destination = Add<ConstantValue>();
            BinderOutput output = Add<BinderOutput>();
            Assert.IsTrue(output.ShowInNodeEditor);
            Invoke(output, "SetUpBindReferenceFields");
            List<BindReference> references = (List<BindReference>)BindReferences(output);
            Assert.That(references, Has.Count.EqualTo(1));
            Assert.That(references[0], Is.SameAs(output.Source));

            destination.StringValue = "before";
            output.Target.SetTarget(destination, "StringValue");
            output.Refresh();
            Assert.That(destination.StringValue, Is.EqualTo("before"), "an unset source forwards nothing");

            output.Source.SetTarget(source, "IntOutput");
            output.Refresh();
            Assert.That(destination.StringValue, Is.EqualTo("5"));

            source.IntValue = 6;
            output.enabled = false;
            output.Refresh();
            Assert.That(destination.StringValue, Is.EqualTo("5"), "a disabled output forwards nothing");
            output.enabled = true;
            output.Refresh();
            Assert.That(destination.StringValue, Is.EqualTo("6"));

            output.Target = null;
            source.IntValue = 8;
            Assert.DoesNotThrow(output.Refresh);
            Assert.That(destination.StringValue, Is.EqualTo("6"));
            LogAssert.NoUnexpectedReceived();
        }

        // SimpleBinder.Refresh 0x19F4D70 inverts GetValue<bool>() only when invertIfBool is set and
        // Source.ValueType is bool (0x19f4e00..0x19f4e94); otherwise it forwards GetValue<object>().
        // get_ShowInNodeEditor 0x19F4D60 returns false.
        [Test]
        public void SimpleBinderInvertsOnlyBoolSourcesAndHidesFromTheNodeEditor()
        {
            ConstantValue source = Add<ConstantValue>();
            source.BoolValue = true;
            source.IntValue = 5;
            ConstantValue destination = Add<ConstantValue>();
            SimpleBinder binder = Add<SimpleBinder>();
            FieldInfo invert = typeof(SimpleBinder).GetField("invertIfBool", Hidden);
            Assert.IsFalse(binder.ShowInNodeEditor);
            Assert.That(invert.GetValue(binder), Is.False);

            binder.Source.SetTarget(source, "BoolOutput");
            binder.Target.SetTarget(destination, "BoolValue");
            binder.Refresh();
            Assert.IsTrue(destination.BoolValue, "no inversion by default");

            invert.SetValue(binder, true);
            binder.Refresh();
            Assert.IsFalse(destination.BoolValue);

            binder.Source.SetTarget(source, "IntOutput");
            binder.Target.SetTarget(destination, "StringValue");
            binder.Refresh();
            Assert.That(destination.StringValue, Is.EqualTo("5"), "a non-bool source is not inverted");

            binder.Source.SetTarget(source, "BoolOutput");
            binder.Target.SetTarget(destination, "BoolValue");
            destination.BoolValue = true;
            binder.enabled = false;
            binder.Refresh();
            Assert.IsTrue(destination.BoolValue, "a disabled binder forwards nothing");
            LogAssert.NoUnexpectedReceived();
        }

        // Outside play mode OnEnable 0x19F3010 / OnDisable 0x19F33A0 take GetComponents<BinderNode>
        // into a new list, keep the ShowInNodeEditor nodes (<>c 0x19F3790 / 0x19F37C0) and set
        // their enabled flag; only the play-mode path stores cachedNodes (0x19f311d).
        [Test]
        public void AdvancedBinderTogglesOnlyNodeEditorNodesAndNeverCachesInTheEditor()
        {
            AdvancedBinder binder = Add<AdvancedBinder>();
            ConstantValue shown = Add<ConstantValue>();
            SimpleBinder hidden = Add<SimpleBinder>();
            FieldInfo cache = typeof(AdvancedBinder).GetField("cachedNodes", Hidden);
            shown.enabled = false;
            hidden.enabled = false;

            Invoke(binder, "OnEnable");
            Assert.IsTrue(shown.enabled);
            Assert.IsFalse(hidden.enabled, "a node hidden from the node editor keeps its state");
            Assert.IsNull(cache.GetValue(binder));

            hidden.enabled = true;
            Invoke(binder, "OnDisable");
            Assert.IsFalse(shown.enabled);
            Assert.IsTrue(hidden.enabled);
            Assert.IsNull(cache.GetValue(binder));
            LogAssert.NoUnexpectedReceived();
        }

        // SetListValue (shared body 0x8E3870, RGCTX get_Count/EqualityComparer.Default/get_Item):
        // with a current list whose count equals value's (null counts as 0) and whose elements are
        // Default-equal in order, nothing is set; otherwise Value receives new List(value), or a
        // new empty list for null.
        [Test]
        public void SetListValuePublishesACopyOnlyWhenTheElementsDiffer()
        {
            Bindable<List<string>> bindable = new Bindable<List<string>>();
            int changes = 0;
            bindable.AddListener(() => changes++);
            Assert.Throws<NullReferenceException>(() => BindableExtn.SetListValue(null, new List<string>()));

            List<string> supplied = new List<string> { "a", "b" };
            bindable.SetListValue(supplied);
            List<string> first = bindable.Value;
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(first, Is.Not.SameAs(supplied).And.EqualTo(supplied));
            supplied.Add("c");
            Assert.That(first, Is.EqualTo(new[] { "a", "b" }), "the published list is a copy");

            bindable.SetListValue(new List<string> { new string('a', 1), new string('b', 1) });
            Assert.That(changes, Is.EqualTo(1), "equal strings of another identity change nothing");
            Assert.That(bindable.Value, Is.SameAs(first));

            bindable.SetListValue(new List<string> { "a", "x" });
            Assert.That(changes, Is.EqualTo(2));
            bindable.SetListValue(new List<string> { "a", "x", "y" });
            Assert.That(changes, Is.EqualTo(3));

            bindable.SetListValue(null);
            List<string> empty = bindable.Value;
            Assert.That(changes, Is.EqualTo(4));
            Assert.That(empty, Is.Not.Null.And.Empty);
            bindable.SetListValue(null);
            bindable.SetListValue(new List<string>());
            Assert.That(changes, Is.EqualTo(4), "an empty current list equals a null or empty value");
            Assert.That(bindable.Value, Is.SameAs(empty));

            Bindable<List<string>> unset = new Bindable<List<string>>();
            unset.SetListValue(null);
            Assert.That(unset.Value, Is.Not.Null.And.Empty, "a null current list always publishes");
            LogAssert.NoUnexpectedReceived();
        }

        // Each ConstantValue setter (0x19FB080..0x19FB110) stores its field and calls the virtual
        // Refresh (slot 7), which writes Int, Float, Bool, String and then null into the outputs
        // (0x19FB150). SetUpBindReferenceFields 0x19FB210 is empty, so no list is created.
        [Test]
        public void ConstantValueSettersRefreshEveryOutputInDeclarationOrder()
        {
            ConstantValue node = Add<ConstantValue>();
            List<string> fired = new List<string>();
            node.IntOutput.AddListener(() => fired.Add("Int"));
            node.FloatOutput.AddListener(() => fired.Add("Float"));
            node.BoolOutput.AddListener(() => fired.Add("Bool"));
            node.StringOutput.AddListener(() => fired.Add("String"));
            node.nullOutput.AddListener(() => fired.Add("null"));

            node.IntValue = 3;
            Assert.That(fired, Is.EqualTo(new[] { "Int", "Float", "Bool", "String", "null" }));
            Assert.That(node.IntOutput.Value, Is.EqualTo(3));

            fired.Clear();
            node.FloatValue = 1.5f;
            node.BoolValue = true;
            node.StringValue = "text";
            Assert.That(fired, Is.EqualTo(new[] { "Float", "Bool", "String" }));
            Assert.That(node.FloatValue, Is.EqualTo(1.5f));
            Assert.IsTrue(node.BoolValue);
            Assert.That(node.StringValue, Is.EqualTo("text"));
            Assert.That(node.StringOutput.Value, Is.EqualTo("text"));
            Assert.IsNull(node.nullOutput.Value);

            Invoke(node, "SetUpBindReferenceFields");
            Assert.IsNull(BindReferences(node));
            LogAssert.NoUnexpectedReceived();
        }

        // LogicalToggle.Refresh 0x1A01500 scans bindReferences from index 1 with Equality
        // (GetValue<object>, null equals only null, else virtual Object.Equals), stops at the first
        // match, sets AnyMatch to match >= 0 and every bindOutputs[i] (i >= 1) to i == match.
        [Test]
        public void LogicalToggleRaisesOnlyTheFirstEqualInput()
        {
            LogicalToggle toggle = Add<LogicalToggle>();
            ConstantLong longFive = Add<ConstantLong>();
            longFive.Value = 5L;
            longFive.Refresh();
            ConstantValue input = IntSource(5);
            toggle.Input.SetTarget(input, "IntOutput");
            toggle.First.SetTarget(IntSource(4), "IntOutput");
            toggle.Second.SetTarget(longFive, "Output");
            toggle.Third.SetTarget(IntSource(5), "IntOutput");
            toggle.Fifth.SetTarget(IntSource(5), "IntOutput");
            toggle.Sixth.SetTarget(IntSource(6), "IntOutput");
            Invoke(toggle, "SetUpBindReferenceFields");
            Bindable<bool>[] matches = { toggle.MatchFirst, toggle.MatchSecond, toggle.MatchThird, toggle.MatchFourth, toggle.MatchFifth, toggle.MatchSixth };

            toggle.Refresh();
            Assert.IsTrue(toggle.AnyMatch.Value);
            Assert.That(matches.Select(m => m.Value), Is.EqualTo(new[] { false, false, true, false, false, false }),
                "a boxed long 5 differs from an int 5, and the equal Fifth stays false after the Third");

            toggle.Input.SetTarget(null, string.Empty);
            toggle.Refresh();
            Assert.IsTrue(toggle.AnyMatch.Value, "an unset Input equals the unset Fourth");
            Assert.That(matches.Select(m => m.Value), Is.EqualTo(new[] { false, false, false, true, false, false }));

            toggle.Input.SetTarget(input, "IntOutput");
            toggle.Fourth.SetTarget(IntSource(7), "IntOutput");
            input.IntValue = 9;
            toggle.Refresh();
            Assert.IsFalse(toggle.AnyMatch.Value);
            Assert.That(matches.Select(m => m.Value), Has.All.False);
            LogAssert.NoUnexpectedReceived();
        }

        // LogicalToggle3.Refresh 0x1A02290 is the same first-match scan over Input, First, Second
        // and Third (SetUpBindReferenceFields 0x1A02700, SetupBinderOutputs 0x1A02460).
        [Test]
        public void LogicalToggle3RaisesOnlyTheFirstEqualInput()
        {
            LogicalToggle3 toggle = Add<LogicalToggle3>();
            ConstantValue input = IntSource(2);
            toggle.Input.SetTarget(input, "IntOutput");
            toggle.First.SetTarget(IntSource(1), "IntOutput");
            toggle.Second.SetTarget(IntSource(2), "IntOutput");
            toggle.Third.SetTarget(IntSource(2), "IntOutput");
            Invoke(toggle, "SetUpBindReferenceFields");
            Bindable<bool>[] matches = { toggle.MatchFirst, toggle.MatchSecond, toggle.MatchThird };

            toggle.Refresh();
            Assert.IsTrue(toggle.AnyMatch.Value);
            Assert.That(matches.Select(m => m.Value), Is.EqualTo(new[] { false, true, false }));

            input.IntValue = 1;
            toggle.Refresh();
            Assert.That(matches.Select(m => m.Value), Is.EqualTo(new[] { true, false, false }));

            input.IntValue = 3;
            toggle.Refresh();
            Assert.IsFalse(toggle.AnyMatch.Value);
            Assert.That(matches.Select(m => m.Value), Has.All.False);
            LogAssert.NoUnexpectedReceived();
        }

        // The TMP_Gradient owners close Constant<T>, Conditional<T> and LogicalSwitch<T> over
        // TMP_ColorGradient (TypeDefs 0200003a, 0200002c, 0200005a); their ctors only chain.
        // LogicalSwitch`1.Refresh (shared body 0x17B1D70) scans switchInputs with Equality against
        // Input and on the first match stores AnyMatch into AnyMatchOutput (0x17b1e7b..0x17b1ea7),
        // then switchOutputs[i] into Output (0x17b1ead..); with no match it stores nothing.
        [Test]
        public void TmpGradientNodesSelectAndPublishTheGradientObject()
        {
            TMP_ColorGradient bright = Own(ScriptableObject.CreateInstance<TMP_ColorGradient>());
            TMP_ColorGradient dark = Own(ScriptableObject.CreateInstance<TMP_ColorGradient>());
            TMP_ColorGradient any = Own(ScriptableObject.CreateInstance<TMP_ColorGradient>());
            ConstantTMPGradient brightNode = Add<ConstantTMPGradient>();
            brightNode.Value = bright;
            brightNode.Refresh();
            ConstantTMPGradient darkNode = Add<ConstantTMPGradient>();
            darkNode.Value = dark;
            darkNode.Refresh();
            Assert.That(brightNode.Output.Value, Is.SameAs(bright));

            ConstantValue condition = Add<ConstantValue>();
            condition.BoolValue = true;
            ConditionalTMPGradient conditional = Add<ConditionalTMPGradient>();
            conditional.Input.SetTarget(condition, "BoolOutput");
            conditional.IfTrue.SetTarget(brightNode, "Output");
            conditional.IfFalse.SetTarget(darkNode, "Output");
            conditional.Refresh();
            Assert.That(conditional.Output.Value, Is.SameAs(bright));
            condition.BoolValue = false;
            conditional.Refresh();
            Assert.That(conditional.Output.Value, Is.SameAs(dark));

            LogicalSwitchTMPGradient selector = Add<LogicalSwitchTMPGradient>();
            Type closed = typeof(LogicalSwitch<TMP_ColorGradient>);
            closed.GetField("AnyMatch", Hidden).SetValue(selector, any);
            closed.GetField("MatchFirst", Hidden).SetValue(selector, dark);
            closed.GetField("MatchSecond", Hidden).SetValue(selector, bright);
            ConstantValue choice = IntSource(2);
            selector.Input.SetTarget(choice, "IntOutput");
            selector.First.SetTarget(IntSource(1), "IntOutput");
            selector.Second.SetTarget(IntSource(2), "IntOutput");
            Invoke(selector, "SetUpBindReferenceFields");
            selector.Refresh();
            Assert.That(selector.Output.Value, Is.SameAs(bright));
            Assert.That(selector.AnyMatchOutput.Value, Is.SameAs(any));
            choice.IntValue = 1;
            selector.Refresh();
            Assert.That(selector.Output.Value, Is.SameAs(dark));
            choice.IntValue = 9;
            selector.Refresh();
            Assert.That(selector.Output.Value, Is.SameAs(dark), "no match leaves the last selection");

            Assert.That(typeof(LogicalSwitchTMPGradient).BaseType, Is.EqualTo(typeof(LogicalSwitch<TMP_ColorGradient>)));
            Assert.IsTrue(typeof(LogicalSwitchTMPGradient).IsSealed);
            Assert.IsFalse(typeof(ConstantTMPGradient).IsSealed);
            Assert.IsFalse(typeof(ConditionalTMPGradient).IsSealed);
            LogAssert.NoUnexpectedReceived();
        }

        // Original CustomAttribute rows (DummyDll HLBinders.Runtime): AddNodeMenu path/priority and the
        // two binders' AddComponentMenu strings. The empty AddComponentMenu blobs of the node owners are
        // checked in OriginalTransitionWidgetBinderCorrectionsTests.
        [Test]
        public void RestoredOwnersKeepTheirOriginalMenuAttributes()
        {
            var nodeMenus = new Dictionary<Type, (string, int)>
            {
                { typeof(BinderOutput), ("Output", 1) },
                { typeof(ConstantValue), ("Constants/Constant Value (Legacy)", int.MaxValue) },
                { typeof(LogicalToggle), ("Logic/Switch-Toggle", int.MaxValue) },
                { typeof(LogicalToggle3), ("Logic/Switch-Toggle3", int.MaxValue) },
                { typeof(ConditionalTMPGradient), ("Conditional/TMP_Gradient", int.MaxValue) },
                { typeof(ConstantTMPGradient), ("Constants/TMP_Gradient", int.MaxValue) },
                { typeof(LogicalSwitchTMPGradient), ("Logic/Switch-TMP_Gradient", int.MaxValue) },
            };
            foreach (KeyValuePair<Type, (string, int)> entry in nodeMenus)
            {
                AddNodeMenu menu = entry.Key.GetCustomAttribute<AddNodeMenu>(false);
                Assert.That((menu.MenuPath, menu.Priority), Is.EqualTo(entry.Value), entry.Key.Name);
            }
            Assert.IsNull(typeof(SimpleBinder).GetCustomAttribute<AddNodeMenu>(false));
            Assert.That(typeof(SimpleBinder).GetCustomAttribute<AddComponentMenu>(false).componentMenu, Is.EqualTo("Hardlight UI/Binding/Binder (Simple)"));
            Assert.That(typeof(AdvancedBinder).GetCustomAttribute<AddComponentMenu>(false).componentMenu, Is.EqualTo("Hardlight UI/Binding/Binder (Advanced)"));
            Assert.IsTrue(typeof(AdvancedBinder).IsDefined(typeof(ExecuteInEditMode), false));
            Assert.IsTrue(typeof(AdvancedBinder).IsDefined(typeof(DisallowMultipleComponent), false));
            Assert.IsTrue(typeof(BindTarget).IsSerializable);
        }

        // DummyDll HLBinders.Runtime rows: BinderOutput fields 0x04000016 Source [FormerlySerializedAs("source"),
        // NodeInput] and 0x04000017 Target [FormerlySerializedAs("target")]; SimpleBinder 0x04000019
        // invertIfBool [SerializeField]; BindTarget fields 0x04000042-0x0400004a in the order below with
        // [SerializeField] on the first four only; AdvancedBinder OnEnable 0x06000009 and OnDisable
        // 0x0600000a flags 0x01c4 Family|Virtual|HideBySig|NewSlot.
        [Test]
        public void RestoredOwnersKeepTheirOriginalSerializedFieldsAndLifecycleFlags()
        {
            FieldInfo source = typeof(BinderOutput).GetField("Source");
            FieldInfo target = typeof(BinderOutput).GetField("Target");
            Assert.That(FormerName(source), Is.EqualTo("source"));
            Assert.That(FormerName(target), Is.EqualTo("target"));
            Assert.IsTrue(source.IsDefined(typeof(NodeInput), false));
            Assert.IsFalse(target.IsDefined(typeof(NodeInput), false));
            Assert.IsTrue(typeof(SimpleBinder).GetField("invertIfBool", Hidden).IsDefined(typeof(SerializeField), false));

            FieldInfo[] fields = typeof(BindTarget).GetFields(Hidden | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(new[]
            {
                "target", "memberName", "filterTargets", "playModeOnly",
                "fieldInfo", "propertyInfo", "methodInfo", "targetType", "cached",
            }));
            Assert.That(fields.Select(f => f.IsDefined(typeof(SerializeField), false)),
                Is.EqualTo(new[] { true, true, true, true, false, false, false, false, false }));
            Assert.That(fields.Select(f => f.IsPrivate), Has.All.True);

            const MethodAttributes layout = MethodAttributes.MemberAccessMask | MethodAttributes.Static | MethodAttributes.Final
                | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask | MethodAttributes.Abstract;
            foreach (string lifecycle in new[] { "OnEnable", "OnDisable" })
            {
                MethodInfo method = typeof(AdvancedBinder).GetMethod(lifecycle, Hidden | BindingFlags.DeclaredOnly);
                Assert.That((int)(method.Attributes & layout), Is.EqualTo(0x01c4), lifecycle);
            }
        }

        private static string FormerName(FieldInfo field) =>
            (string)field.GetCustomAttributesData().Single(a => a.AttributeType == typeof(FormerlySerializedAsAttribute))
                .ConstructorArguments[0].Value;
    }
}
