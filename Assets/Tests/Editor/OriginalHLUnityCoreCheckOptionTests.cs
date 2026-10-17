using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Pooling;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Own type attributes of HLUnityCore.Runtime owners that the original
    // metadata annotates with both Il2CppSetOption check options. Expected
    // lists are dump.cs (schemas-9bf182c62426) in metadata order, by
    // TypeDefIndex. Il2CppSetOption is Inherited = false (TypeDefIndex 8762),
    // so a base or containing type never stands in for a missing own entry.
    // Serializable is a TypeDef flag projected as a pseudo-attribute; it is
    // checked separately from the ordered custom attribute rows.
    [TestFixture]
    public sealed class OriginalHLUnityCoreCheckOptionTests
    {
        const string Null = "Il2CppSetOption(Option 1, Boolean False)";
        const string Bounds = "Il2CppSetOption(Option 2, Boolean False)";
        const string Extension = "Extension()";
        const string MenuFormat = "GraphNodeMenuFormat(String \"Core/{0}\")";

        static Type FactoryNested(string name) => typeof(FSMClassFactory).GetNestedType(name, BindingFlags.Public);
        static Type CtorArgs(Type owner) => owner.GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);

        static IEnumerable<(int TypeDefIndex, Type Type, bool Serializable, string[] Attributes)> Expected()
        {
            yield return (8894, typeof(FiniteStateMachine), false, new[] { Null, Bounds });
            yield return (8902, FactoryNested("NodeMetadataWithRouting"), true, new[] { Bounds, Null });
            yield return (8903, FactoryNested("JSONStateTransition"), true, new[] { Bounds, Null });
            yield return (8904, FactoryNested("JSONTransitionClass"), true, new[] { Null, Bounds });
            yield return (8905, FactoryNested("JSONStateClass"), true, new[] { Bounds, Null });
            yield return (8906, FactoryNested("JSONFiniteStateMachineClass"), true, new[] { Bounds, Null });
            yield return (8907, FactoryNested("JSONMultipleFSMs"), true, new[] { Bounds, Null });
            // FSMClassFactory is partial; one part carries the pair, so it is not duplicated.
            yield return (8919, typeof(FSMClassFactory), false, new[] { Bounds, Null });
            yield return (8922, typeof(FSMManager), false, new[] { Null, Bounds });
            yield return (8923, typeof(FSMState), false, new[] { MenuFormat, Bounds, Null });
            yield return (8924, typeof(FSMStateChangeAction), false, new[] { Bounds, Null });
            yield return (8925, CtorArgs(typeof(FSMStateSubFSM)), true,
                new[] { Null, "GraphNodeColour(Single 0, Single 1, Single 0)", Bounds });
            yield return (8932, CtorArgs(typeof(FSMStateGroup)), true,
                new[] { "GraphNodeDefaultName(String \"StateGroup\")", "GraphNodeColour(Single 0, Single 0.75, Single 1)", Bounds, Null });
            yield return (8970, CtorArgs(typeof(FSMTransitionLogicOp)), true,
                new[] { "GraphNodeDefaultName(String \"LogicOp\")", Bounds, Null });
            yield return (8976, CtorArgs(typeof(FSMTransitionWithChildren)), true,
                new[] { "GraphNodeDefaultName(String \"TransitionWithChildren\")", Bounds, Null });
            yield return (8978, typeof(FSMStorage), false, new[] { Bounds, Null });
            yield return (8979, typeof(FSMTransition), false, new[] { Bounds, Null, MenuFormat });
            yield return (8982, typeof(FSMUser), false, new[] { Null, Bounds });
            yield return (9067, typeof(GraphNameLookup), false, new[] { Null, Bounds });
            yield return (9069, typeof(IGraphStorage.Var<>), false, new[] { Bounds, Null });
            yield return (9079, typeof(JSONClassFactory), false, new[] { Extension, Null, Bounds });
            yield return (9080, typeof(JSONCtorArgsAttribute), false, new[] { "AttributeUsage(AttributeTargets 4)", Null, Bounds });
            yield return (9081, typeof(JSONFactoryClass), false, new[] { Bounds, Null });
            yield return (9221, typeof(GraphAttributeBase), false,
                new[] { Bounds, Null, "AttributeUsage(AttributeTargets 256, AllowMultiple=Boolean True)" });
            yield return (9222, typeof(GraphDisplayNameAttribute), false, new[] { Null, Bounds, "AttributeUsage(AttributeTargets 388)" });
            yield return (9230, typeof(BackgroundMetadata), true, new[] { Null, Bounds });
            yield return (9231, typeof(NodeMetadata), true, new[] { Bounds, Null });
            yield return (9232, typeof(NodeMetadataBasic), true, new[] { Null, Bounds });
            yield return (9233, typeof(ViewMetadata), true, new[] { Bounds, Null });
            yield return (9255, typeof(HLPropertyList.Property), false, new[] { Null, Bounds });
            yield return (9378, typeof(HLCRC32), false, new[] { Bounds, Null });
            yield return (9403, typeof(TimeUtils), false, new[] { Extension, Null, Bounds });
            yield return (9408, typeof(ObjectPool<>), false, new[] { Bounds, Null });
        }

        // Fails per owner with the full actual list, so one run reports every gap.
        [Test]
        public void CoreTypesKeepTheShippedOwnCheckOptionAnnotationsInOrder()
        {
            var owners = Expected().ToArray();
            Assert.AreEqual(33, owners.Length);
            Assert.AreEqual(33, owners.Select(o => o.Type).Distinct().Count());

            var failures = new List<string>();
            foreach (var (index, type, serializable, attributes) in owners)
            {
                Assert.IsNotNull(type, "TypeDefIndex " + index + " owner not found");
                Assert.AreEqual("HLUnityCore.Runtime", type.Assembly.GetName().Name, type.FullName);
                IList<CustomAttributeData> data = type.GetCustomAttributesData();
                string[] actual = data.Where(a => a.AttributeType != typeof(SerializableAttribute)).Select(Describe).ToArray();
                if (!actual.SequenceEqual(attributes))
                    failures.Add(index + " " + type.FullName + ": [" + string.Join("; ", actual) + "]");
                if (type.IsSerializable != serializable)
                    failures.Add(index + " " + type.FullName + ": IsSerializable " + type.IsSerializable);
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // Name without the Attribute suffix, then each constructor argument as
        // its declared metadata type and raw value, then named arguments.
        static string Describe(CustomAttributeData attribute)
        {
            string name = attribute.AttributeType.Name;
            if (name.EndsWith("Attribute", StringComparison.Ordinal)) name = name.Substring(0, name.Length - "Attribute".Length);
            IEnumerable<string> arguments = attribute.ConstructorArguments.Select(Describe)
                .Concat(attribute.NamedArguments.Select(a => a.MemberName + "=" + Describe(a.TypedValue)));
            return name + "(" + string.Join(", ", arguments) + ")";
        }

        static string Describe(CustomAttributeTypedArgument argument)
        {
            object value = argument.Value;
            string text = value is string s ? "\"" + s + "\""
                : argument.ArgumentType.IsEnum ? Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
                : Convert.ToString(value, CultureInfo.InvariantCulture);
            return argument.ArgumentType.Name + " " + text;
        }
    }
}
