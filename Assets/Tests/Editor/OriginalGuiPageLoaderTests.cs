using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalGuiPageLoaderTests
    {
        private const int Information = 1753178996;
        private const int TextEntry = 2001913270;
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        [Test]
        public void NormalInitialisationKeepsExactlyTheTwoShippedOverlayMappings()
        {
            Snapshot before = SuitableState();
            GuiPageLoader.InitialiseGUIPageLoader();
            RequireMappings();
            before.RequireInitialisationResult();
            Snapshot initialised = SuitableState();
            GuiPageLoader.InitialiseGUIPageLoader();
            initialised.RequireUnchanged();
            Assert.That(GuiPageLoader.GetSceneFromID((int)OverlayPageIdentifier.general_information),
                Is.EqualTo("s_overlay_general_information"));
            Assert.That(GuiPageLoader.GetSceneFromID((int)OverlayPageIdentifier.general_text_entry),
                Is.EqualTo("s_overlay_general_text_entry"));
        }

        [Test]
        public void MissingNumericRoutesReturnBeforeAnySceneRequest()
        {
            InitialiseSuitableState();
            Snapshot before = SuitableState();
            Assert.That(GuiPageLoader.GetSceneFromID(0), Is.EqualTo(string.Empty));
            GuiPageLoader.LoadSceneFromValue(0);
            GuiPageLoader.LoadSceneFromValueAsync(0);
            Assert.That(GuiPageLoader.UnloadSceneFromValue(0), Is.False);
            Assert.That(GuiPageLoader.UnloadSceneFromValue(0, true), Is.False);
            before.RequireUnchanged();
        }

        [Test]
        public void UnconstrainedGenericConversionFaultsPrecedeSceneLookup()
        {
            InitialiseSuitableState();
            Snapshot before = SuitableState();
            Assert.Throws<FormatException>(() => GuiPageLoader.LoadSceneFromEnum<string>("lucid_not_a_number"));
            Assert.Throws<OverflowException>(() => GuiPageLoader.LoadSceneFromEnum<long>(long.MaxValue));
            object ownedArgument = new object();
            Assert.Throws<InvalidCastException>(() => GuiPageLoader.LoadSceneFromEnum<object>(ownedArgument));
            // Convert.ToInt32(null) is zero; the real loader's lookup still misses.
            GuiPageLoader.LoadSceneFromEnum<object>(null);
            before.RequireUnchanged();
        }

        [Test]
        public void PermanentUnmappedIdentifierAppendsOnceAndRetainsItsOriginalRecord()
        {
            InitialiseSuitableState();
            Snapshot before = SuitableState();
            int identifier = int.MinValue;
            // There are at most Count occupied candidates. The selected value is
            // absent from the actual list and the exact two-entry scene map.
            int attempts = 0;
            while (before.Permanent.Contains(identifier) || before.Lookup.ContainsKey(identifier))
            {
                Assert.That(attempts++, Is.LessThan(before.Permanent.Count + 2));
                identifier = checked(identifier + 1);
            }
            Assert.That(GuiPageLoader.IsPermanentScene(identifier), Is.False);
            Assert.That(GuiPageLoader.GetSceneFromID(identifier), Is.EqualTo(string.Empty));
            GuiPageLoader.LoadPermanently(identifier);
            before.RequireIdentitiesAndPermanentPrefix(true);
            before.RequireLookupUnchanged();
            Assert.That(before.Permanent[before.PermanentValues.Length], Is.EqualTo(identifier));
            Assert.That(GuiPageLoader.IsPermanentScene(identifier), Is.True);
            Snapshot appended = SuitableState();
            GuiPageLoader.LoadPermanently(identifier);
            Assert.That(GuiPageLoader.UnloadSceneFromValue(identifier), Is.False);
            Assert.That(GuiPageLoader.UnloadSceneFromValue(identifier, true), Is.False);
            appended.RequireUnchanged();
            // This intentionally retains the original public method's one static
            // append. No private removal, reset, or simulated restoration occurs.
        }

        [Test]
        public void WholeOriginalDeclarationRetainsAllFifteenMethodsAndRealEnumProviders()
        {
            Type owner = typeof(GuiPageLoader);
            Assert.That(owner.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(owner.FullName, Is.EqualTo("Hardlight.GuiPageLoader"));
            Assert.That((int)owner.Attributes, Is.EqualTo(1048577));
            Assert.That(owner.BaseType, Is.SameAs(typeof(object)));
            Assert.That(owner.GetNestedTypes(Declared).Length, Is.EqualTo(0));
            Assert.That(owner.GetInterfaces().Length, Is.EqualTo(0));
            Assert.That(owner.GetProperties(Declared).Length, Is.EqualTo(0));
            Assert.That(owner.GetEvents(Declared).Length, Is.EqualTo(0));
            FieldInfo[] fields = owner.GetFields(Declared);
            Assert.That(fields.Length, Is.EqualTo(3));
            CheckField(fields[0], "m_sceneNameLookup", typeof(Dictionary<int, string>));
            CheckField(fields[1], "m_isInitialised", typeof(bool));
            CheckField(fields[2], "m_permanentScenes", typeof(List<int>));
            MethodInfo[] methods = owner.GetMethods(Declared);
            Assert.That(methods.Length, Is.EqualTo(13));
            CheckMethod("InitialiseGUIPageLoader", typeof(void), 150, 0, new Type[0], new string[0]);
            CheckMethod("InitaliseGUIPageLoaderFromEnum", typeof(void), 145, 1,
                new[] { typeof(string) }, new[] { "prefix" });
            CheckMethod("LoadSceneFromEnum", typeof(void), 150, 1, null, new[] { "identifier" });
            CheckMethod("LoadSceneFromValue", typeof(void), 150, 0, new[] { typeof(int) }, new[] { "val" });
            CheckMethod("LoadSceneFromValueAsync", typeof(void), 150, 0, new[] { typeof(int) }, new[] { "val" });
            CheckMethod("LoadSceneFromString", typeof(void), 150, 0, new[] { typeof(string) }, new[] { "name" });
            CheckMethod("LoadSceneFromStringAsync", typeof(void), 150, 0, new[] { typeof(string) }, new[] { "name" });
            CheckMethod("UnloadSceneFromValue", typeof(bool), 150, 0,
                new[] { typeof(int), typeof(bool) }, new[] { "val", "unloadPermanentScenes" });
            CheckMethod("UnloadSceneFromString", typeof(bool), 145, 0, new[] { typeof(string) }, new[] { "name" });
            CheckMethod("UnloadAllScenes", typeof(void), 150, 0, new[] { typeof(bool) }, new[] { "unloadPermanentScenes" });
            CheckMethod("GetSceneFromID", typeof(string), 150, 0, new[] { typeof(int) }, new[] { "id" });
            CheckMethod("LoadPermanently", typeof(void), 150, 0, new[] { typeof(int) }, new[] { "identifier" });
            CheckMethod("IsPermanentScene", typeof(bool), 150, 0, new[] { typeof(int) }, new[] { "identifier" });
            ConstructorInfo[] constructors = owner.GetConstructors(Declared & ~BindingFlags.Static);
            Assert.That(constructors.Length, Is.EqualTo(1));
            Assert.That((int)constructors[0].Attributes, Is.EqualTo(6278));
            Assert.That(constructors[0].GetParameters().Length, Is.EqualTo(0));
            Assert.That((int)constructors[0].GetMethodImplementationFlags(), Is.EqualTo(0));
            Assert.That(owner.TypeInitializer, Is.Not.Null);
            Assert.That((int)owner.TypeInitializer.Attributes, Is.EqualTo(6289));
            Assert.That(owner.TypeInitializer.GetParameters().Length, Is.EqualTo(0));
            Assert.That((int)owner.TypeInitializer.GetMethodImplementationFlags(), Is.EqualTo(0));
            IList<CustomAttributeData> attributes = owner.GetCustomAttributesData();
            Assert.That(attributes.Count, Is.EqualTo(2));
            CheckOption(attributes[0], Option.ArrayBoundsChecks);
            CheckOption(attributes[1], Option.NullChecks);
            CheckEnum(typeof(OverlayPageIdentifier), new[] { "None", "general_information", "general_text_entry" },
                new[] { 0, Information, TextEntry });
            CheckEnum(typeof(MenuPageIdentifier), new[] { "None" }, new[] { 0 });
            CheckEnum(typeof(DialogPageIdentifier), new[] { "None" }, new[] { 0 });
            CheckEnum(typeof(SubPageIdentifier), new[] { "None" }, new[] { 0 });
        }

        private static void CheckEnum(Type type, string[] names, int[] values)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLAutoGenerated"));
            Assert.That(type.IsEnum, Is.True);
            Assert.That(Enum.GetUnderlyingType(type), Is.SameAs(typeof(int)));
            CollectionAssert.AreEqual(names, Enum.GetNames(type));
            Array actual = Enum.GetValues(type);
            Assert.That(actual.Length, Is.EqualTo(values.Length));
            for (int i = 0; i < values.Length; i++) Assert.That((int)actual.GetValue(i), Is.EqualTo(values[i]));
        }

        private static void CheckOption(CustomAttributeData data, Option option)
        {
            Assert.That(data.AttributeType, Is.SameAs(typeof(Il2CppSetOptionAttribute)));
            Assert.That(data.Constructor.DeclaringType, Is.SameAs(typeof(Il2CppSetOptionAttribute)));
            ParameterInfo[] parameters = data.Constructor.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(2));
            Assert.That(parameters[0].ParameterType, Is.SameAs(typeof(Option)));
            Assert.That(parameters[1].ParameterType, Is.SameAs(typeof(object)));
            Assert.That(data.ConstructorArguments.Count, Is.EqualTo(2));
            Assert.That(data.ConstructorArguments[0].ArgumentType, Is.SameAs(typeof(Option)));
            Assert.That(Convert.ToInt32(data.ConstructorArguments[0].Value), Is.EqualTo((int)option));
            Assert.That(data.ConstructorArguments[1].ArgumentType, Is.SameAs(typeof(bool)));
            Assert.That(data.ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(data.NamedArguments.Count, Is.EqualTo(0));
        }

        private static void CheckField(FieldInfo field, string name, Type type)
        {
            Assert.That(field.Name, Is.EqualTo(name));
            Assert.That(field.DeclaringType, Is.SameAs(typeof(GuiPageLoader)));
            Assert.That(field.FieldType, Is.SameAs(type));
            Assert.That((int)field.Attributes, Is.EqualTo(17));
            Assert.That(field.GetCustomAttributesData().Count, Is.EqualTo(0));
        }

        private static void CheckMethod(string name, Type returns, int flags, int genericCount,
            Type[] parameterTypes, string[] parameterNames)
        {
            MethodInfo method = typeof(GuiPageLoader).GetMethod(name, Declared);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.DeclaringType, Is.SameAs(typeof(GuiPageLoader)));
            Assert.That(method.ReturnType, Is.SameAs(returns));
            Assert.That((int)method.Attributes, Is.EqualTo(flags));
            Assert.That((int)method.GetMethodImplementationFlags(), Is.EqualTo(0));
            Assert.That(method.GetCustomAttributesData().Count, Is.EqualTo(0));
            Type[] generic = method.GetGenericArguments();
            Assert.That(generic.Length, Is.EqualTo(genericCount));
            if (genericCount != 0)
            {
                Assert.That(method.IsGenericMethodDefinition, Is.True);
                Assert.That(generic[0].Name, Is.EqualTo("T"));
                Assert.That(generic[0].GenericParameterPosition, Is.EqualTo(0));
                Assert.That((int)generic[0].GenericParameterAttributes, Is.EqualTo(0));
                Assert.That(generic[0].GetGenericParameterConstraints().Length, Is.EqualTo(0));
                Assert.That(generic[0].DeclaringMethod, Is.SameAs(method));
            }
            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(parameterNames.Length));
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                Assert.That(parameter.Name, Is.EqualTo(parameterNames[i]));
                Assert.That(parameter.Position, Is.EqualTo(i));
                Assert.That(parameter.ParameterType, Is.SameAs(parameterTypes == null ? generic[0] : parameterTypes[i]));
                bool optional = name == "UnloadSceneFromValue" && i == 1;
                Assert.That((int)parameter.Attributes, Is.EqualTo(optional ? 4112 : 0));
                Assert.That(parameter.IsOptional, Is.EqualTo(optional));
                Assert.That(parameter.HasDefaultValue, Is.EqualTo(optional));
                if (optional)
                {
                    Assert.That(parameter.DefaultValue, Is.EqualTo(false));
                    Assert.That(parameter.RawDefaultValue, Is.EqualTo(false));
                }
                // Stored original parameter CA rows are zero. Mono can project
                // OptionalAttribute; this proposal makes no unobserved CAD claim.
            }
        }

        private static FieldInfo Field(string name, Type type)
        {
            FieldInfo field = typeof(GuiPageLoader).GetField(name, Declared);
            Assert.That(field, Is.Not.Null);
            CheckField(field, name, type);
            return field;
        }

        private static Snapshot SuitableState()
        {
            var lookup = (Dictionary<int, string>)Field("m_sceneNameLookup", typeof(Dictionary<int, string>)).GetValue(null);
            var permanent = (List<int>)Field("m_permanentScenes", typeof(List<int>)).GetValue(null);
            bool initialised = (bool)Field("m_isInitialised", typeof(bool)).GetValue(null);
            Assert.That(lookup, Is.Not.Null);
            Assert.That(permanent, Is.Not.Null);
            // Reject partial initialization or foreign map entries before any
            // public mutation. Existing permanent records are retained exactly.
            if (initialised) RequireMappings();
            else Assert.That(lookup.Count, Is.EqualTo(0));
            return new Snapshot(lookup, permanent, initialised);
        }

        private static void InitialiseSuitableState()
        {
            Snapshot before = SuitableState();
            GuiPageLoader.InitialiseGUIPageLoader();
            RequireMappings();
            before.RequireInitialisationResult();
        }

        private static void RequireMappings()
        {
            var lookup = (Dictionary<int, string>)Field("m_sceneNameLookup", typeof(Dictionary<int, string>)).GetValue(null);
            Assert.That((bool)Field("m_isInitialised", typeof(bool)).GetValue(null), Is.True);
            Assert.That(lookup.Count, Is.EqualTo(2));
            Assert.That(lookup[Information], Is.EqualTo("s_overlay_general_information"));
            Assert.That(lookup[TextEntry], Is.EqualTo("s_overlay_general_text_entry"));
        }

        private sealed class Snapshot
        {
            internal readonly Dictionary<int, string> Lookup;
            internal readonly List<int> Permanent;
            internal readonly int[] PermanentValues;
            private readonly KeyValuePair<int, string>[] entries;
            private readonly bool initialised;

            internal Snapshot(Dictionary<int, string> lookup, List<int> permanent, bool flag)
            {
                Lookup = lookup;
                Permanent = permanent;
                PermanentValues = permanent.ToArray();
                entries = new KeyValuePair<int, string>[lookup.Count];
                ((ICollection<KeyValuePair<int, string>>)lookup).CopyTo(entries, 0);
                initialised = flag;
            }

            internal void RequireIdentitiesAndPermanentPrefix(bool appended)
            {
                Assert.That(Field("m_sceneNameLookup", typeof(Dictionary<int, string>)).GetValue(null), Is.SameAs(Lookup));
                Assert.That(Field("m_permanentScenes", typeof(List<int>)).GetValue(null), Is.SameAs(Permanent));
                Assert.That(Permanent.Count, Is.EqualTo(PermanentValues.Length + (appended ? 1 : 0)));
                for (int i = 0; i < PermanentValues.Length; i++) Assert.That(Permanent[i], Is.EqualTo(PermanentValues[i]));
            }

            internal void RequireInitialisationResult()
            {
                RequireIdentitiesAndPermanentPrefix(false);
                if (initialised) RequireLookupUnchanged();
            }

            internal void RequireUnchanged()
            {
                RequireIdentitiesAndPermanentPrefix(false);
                Assert.That((bool)Field("m_isInitialised", typeof(bool)).GetValue(null), Is.EqualTo(initialised));
                RequireLookupUnchanged();
            }

            internal void RequireLookupUnchanged()
            {
                Assert.That((bool)Field("m_isInitialised", typeof(bool)).GetValue(null), Is.EqualTo(initialised));
                var actual = new KeyValuePair<int, string>[Lookup.Count];
                ((ICollection<KeyValuePair<int, string>>)Lookup).CopyTo(actual, 0);
                CollectionAssert.AreEqual(entries, actual);
                for (int i = 0; i < entries.Length; i++)
                    Assert.That(actual[i].Value, Is.SameAs(entries[i].Value));
            }
        }
    }
}
