using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests.Editor
{
    // Tests reconstructed original-configuration22 declarations and genuinely owned data.
    // No shipping assets, App, configuration providers, networking or native plugins are used.
    // Compiler metadata and Unity serialization are separate from the shipping native proof.
    public sealed class OriginalProceduralAndCrossPromoConfigurationTests
    {
        private const string DevEndpoint = "https://gridserver-dev.hardlightdev.com/info";
        private const string ProdEndpoint = "https://gridserver.hardlightgames.com/info";
        private const long WideThreshold = 0x100000005L;
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void ProceduralMesh_DefaultsRemainDistinctFromAuthoredSettings()
        {
            WithOwned(scope =>
            {
                var mesh = scope.Create<ProceduralMesh_SystemConfig>();
                AssertMesh(mesh, new[] { true, false, false, false, false, false });
                AssertBaseDefaults(mesh);
            });
        }

        [Test]
        public void CrossPromo_DefaultsRetainOriginalStringsWidthsAndUnwrittenFields()
        {
            WithOwned(scope =>
            {
                var cross = scope.Create<CrossPromoConfigurationAsset>();
                // Read the untouched managed getters before any SerializedObject transaction.
                Assert.That(cross.GameTitle, Is.Null);
                Assert.That(cross.RouterPropertyName, Is.Null);
                Assert.That(cross.PlatformName, Is.Null);
                Assert.That(cross.InvalidContentLengthThreshold, Is.EqualTo(1024L));
                Assert.That(cross.IncomingFolder, Is.EqualTo("grid_incoming"));
                Assert.That(cross.DestinationFolder, Is.EqualTo("grid"));
                Assert.That(cross.MetaFilename, Is.EqualTo("settings.json"));
                Assert.That(cross.CanUseLocalStorage, Is.True);
                Assert.That(cross.IconImagePoolSize, Is.EqualTo(8));
                Assert.That(cross.MainImagePoolSize, Is.EqualTo(3));
                Assert.That(cross.Debug, Is.False);
                Assert.That(cross.Server, Is.EqualTo(DevEndpoint));
                using (var serialized = new SerializedObject(cross))
                    Assert.That(Property(serialized, "m_server", SerializedPropertyType.Enum).intValue, Is.Zero);
                AssertBaseDefaults(cross);
            });
        }

        [Test]
        public void AllSeventeenDirectGettersProjectGenuineSerializedTransactions()
        {
            WithOwned(scope =>
            {
                var mesh = scope.Create<ProceduralMesh_SystemConfig>();
                var cross = scope.Create<CrossPromoConfigurationAsset>();
                // Opposite patterns test both values of every Boolean field in independent applies.
                SetMesh(mesh, new[] { false, true, false, true, false, true });
                AssertMesh(mesh, new[] { false, true, false, true, false, true });
                SetCross(cross, "title-one", "router-two", "platform-three", 0,
                    WideThreshold, "incoming-four", "destination-five", "meta-six", false, -11, 42, true);
                AssertCross(cross, "title-one", "router-two", "platform-three", DevEndpoint,
                    WideThreshold, "incoming-four", "destination-five", "meta-six", false, -11, 42, true);
                SetMesh(mesh, new[] { true, false, true, false, true, false });
                AssertMesh(mesh, new[] { true, false, true, false, true, false });
                SetCross(cross, "second-title", "second-router", "second-platform", 1,
                    WideThreshold + 3, "second-incoming", "second-destination", "second-meta", true, 17, -23, false);
                AssertCross(cross, "second-title", "second-router", "second-platform", ProdEndpoint,
                    WideThreshold + 3, "second-incoming", "second-destination", "second-meta", true, 17, -23, false);
            });
        }

        [Test]
        public void AuthoredScalarSettingsRoundTripThroughOnlyFreshOwnedAssets()
        {
            WithOwned(scope =>
            {
                var mesh = scope.Create<ProceduralMesh_SystemConfig>();
                var cross = scope.Create<CrossPromoConfigurationAsset>();
                // Scalar values are the exact frozen shipped documents; their PPtr graph is separate.
                // Shipped Data/resources.assets (sha256 ba5d4b45...cc30a426): MonoBehaviour
                // "ProceduralMeshSystemConfig" (m_Name at 0x20df34) stores bools 1,0,1,1,0,0 after
                // an empty base PPtr/list; "ProdHLCrossPromoConfiguration" (m_Name at 0x20f304)
                // stores dreamteam/sdt_router/ios, m_server 1, 1024, grid_incoming/grid/
                // settings.json, 1, 8, 3, m_debug 0. The Dev document differs in m_server 0, m_debug 1.
                SetMesh(mesh, new[] { true, false, true, true, false, false });
                SetCross(cross, "dreamteam", "sdt_router", "ios", 1, 1024L,
                    "grid_incoming", "grid", "settings.json", true, 8, 3, false);
                string folder = scope.CreateAssetFolder();
                string meshPath = folder + "/OwnedMesh.asset";
                string crossPath = folder + "/OwnedCross.asset";
                AssetDatabase.CreateAsset(mesh, meshPath);
                AssetDatabase.CreateAsset(cross, crossPath);
                AssetDatabase.SaveAssetIfDirty(mesh);
                AssetDatabase.SaveAssetIfDirty(cross);
                // Only these normally owned assets are unloaded; no global reload or provider reset.
                Resources.UnloadAsset(mesh);
                Resources.UnloadAsset(cross);
                AssetDatabase.ImportAsset(meshPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(crossPath, ImportAssetOptions.ForceSynchronousImport);
                mesh = scope.Own(AssetDatabase.LoadAssetAtPath<ProceduralMesh_SystemConfig>(meshPath));
                cross = scope.Own(AssetDatabase.LoadAssetAtPath<CrossPromoConfigurationAsset>(crossPath));
                AssertMesh(mesh, new[] { true, false, true, true, false, false });
                AssertCross(cross, "dreamteam", "sdt_router", "ios", ProdEndpoint, 1024L,
                    "grid_incoming", "grid", "settings.json", true, 8, 3, false);
                AssertBaseDefaults(mesh);
                AssertBaseDefaults(cross);
            });
        }

        [Test]
        public void ServerSelectsDevOnlyForZeroAndProdForEveryTestedNonzero()
        {
            WithOwned(scope =>
            {
                var cross = scope.Create<CrossPromoConfigurationAsset>();
                foreach (int value in new[] { 0, 1, -1, 2 })
                {
                    using (var serialized = new SerializedObject(cross))
                    {
                        // intValue preserves undefined underlying values; enumValueIndex would not.
                        Property(serialized, "m_server", SerializedPropertyType.Enum).intValue = value;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    using (var observed = new SerializedObject(cross))
                        Assert.That(Property(observed, "m_server", SerializedPropertyType.Enum).intValue,
                            Is.EqualTo(value), "Unity must preserve this actual underlying value before the getter assertion.");
                    Assert.That(cross.Server, Is.EqualTo(value == 0 ? DevEndpoint : ProdEndpoint));
                }
            });
        }

        [Test]
        public void EmptyValidatePreservesCompleteOwnedStateAndOriginalBaseReferences()
        {
            WithOwned(scope =>
            {
                var mesh = scope.Create<ProceduralMesh_SystemConfig>();
                var cross = scope.Create<CrossPromoConfigurationAsset>();
                mesh.name = "owned-mesh-state";
                cross.name = "owned-cross-state";
                SetMesh(mesh, new[] { false, true, true, false, true, false });
                SetCross(cross, "validate-title", "validate-router", "validate-platform", -1,
                    WideThreshold, "validate-incoming", "validate-destination", "validate-meta", false, -11, 42, true);
                SetBaseState(mesh, cross, new[] { "m_lockToGameCamera", "m_lockToGameCamera", "" });
                SetBaseState(cross, mesh, new[] { "m_server", "m_debug", "unknown-original-list-entry" });
                string meshBefore = EditorJsonUtility.ToJson(mesh, false);
                string crossBefore = EditorJsonUtility.ToJson(cross, false);
                int meshIdentity = mesh.GetInstanceID(), crossIdentity = cross.GetInstanceID();
                mesh.Validate();
                cross.Validate();
                string meshAfter = EditorJsonUtility.ToJson(mesh, false);
                string crossAfter = EditorJsonUtility.ToJson(cross, false);
                TestContext.Progress.WriteLine("Validate mesh before=" + meshBefore + " after=" + meshAfter);
                TestContext.Progress.WriteLine("Validate cross before=" + crossBefore + " after=" + crossAfter);
                Assert.That(meshAfter, Is.EqualTo(meshBefore), "Complete serialized owned mesh state");
                Assert.That(crossAfter, Is.EqualTo(crossBefore), "Complete serialized owned cross state");
                Assert.That(mesh.GetInstanceID(), Is.EqualTo(meshIdentity));
                Assert.That(cross.GetInstanceID(), Is.EqualTo(crossIdentity));
                AssertBaseState(mesh, cross, new[] { "m_lockToGameCamera", "m_lockToGameCamera", "" });
                AssertBaseState(cross, mesh, new[] { "m_server", "m_debug", "unknown-original-list-entry" });
            });
        }

        [Test]
        public void CompleteDeclaredContractAndOrderedAttributesRemainOriginal()
        {
            // Expected orders are the original metadata v31 CAD rows: mesh 0x0200004a
            // [CreateAssetMenu(fileName, menuName, order 0), ArrayBoundsChecks, NullChecks]
            // with one SerializeField per bool; cross 0x02000006 [CreateAssetMenu] and fields
            // 0x04000020..0x0400002b in the per-field Tooltip/Header/SerializeField order below.
            // ServerDev/ServerProd 0x0400001e/1f have no CAD row.
            Type mesh = typeof(ProceduralMesh_SystemConfig), cross = typeof(CrossPromoConfigurationAsset);
            AssertOwner(mesh, "HLSplines.Runtime", "Hardlight.ProceduralMesh_SystemConfig");
            AssertOwner(cross, "HLCrossPromo.Runtime", "Hardlight.CrossPromoConfigurationAsset");
            AssertAttributes(mesh, Menu("ProceduralMeshSystemConfig", "Hardlight/Procedural/Mesh System Config"),
                OptionAttribute(Option.ArrayBoundsChecks), OptionAttribute(Option.NullChecks));
            AssertAttributes(cross, Menu("HLCrossPromoConfiguration", "Hardlight/CrossPromo/Module Configuration"));
            AssertFields(mesh, MeshFields.Select(n => new FieldSpec(n, typeof(bool), 1, false, null,
                new[] { Marker(typeof(SerializeField)) })).ToArray());
            AssertFields(cross,
                new FieldSpec("ServerDev", typeof(string), 0x8051, true, DevEndpoint),
                new FieldSpec("ServerProd", typeof(string), 0x8051, true, ProdEndpoint),
                Field("m_gameTitle", typeof(string), Tooltip("Title of the game."), Header("Game"), Serialize()),
                Field("m_routerPropertyName", typeof(string), Tooltip("Router property name, ensure that we source the correct metadata from the server."), Serialize()),
                Field("m_platformName", typeof(string), Serialize(), Tooltip("Platform name, ensure that we source the correct platform from the metadata.")),
                Field("m_server", typeof(CrossPromoConfigurationAsset.ServerType), Header("Server"), Tooltip("The server url to use."), Serialize()),
                Field("m_invalidContentLengthThreshold", typeof(long), Serialize(), Tooltip("Size of the response that should be considered valid.")),
                Field("m_incomingFolder", typeof(string), Serialize(), Tooltip("Name of the folder where files are first downloaded."), Header("Filesystem")),
                Field("m_destinationFolder", typeof(string), Serialize(), Tooltip("Name of the folder where files are stored.")),
                Field("m_metaFilename", typeof(string), Tooltip("Name of the meta file."), Serialize()),
                Field("m_canUseLocalStorage", typeof(bool), Tooltip("Can this platform use local storage?"), Serialize()),
                Field("m_iconImagePoolSize", typeof(int), Header("System"), Tooltip("Default size of the icon images"), Serialize()),
                Field("m_mainImagePoolSize", typeof(int), Tooltip("Default size of the main images"), Serialize()),
                Field("m_debug", typeof(bool), Serialize(), Tooltip("Enable debugging logs")));
            AssertPropertiesAndMethods(mesh, MeshProperties.Select(n => new PropertySpec(n, typeof(bool))).ToArray());
            AssertPropertiesAndMethods(cross, new[] {
                P("MetaFilename", typeof(string)), P("DestinationFolder", typeof(string)), P("IncomingFolder", typeof(string)),
                P("Server", typeof(string)), P("GameTitle", typeof(string)), P("RouterPropertyName", typeof(string)),
                P("PlatformName", typeof(string)), P("CanUseLocalStorage", typeof(bool)), P("IconImagePoolSize", typeof(int)),
                P("MainImagePoolSize", typeof(int)), P("InvalidContentLengthThreshold", typeof(long)), P("Debug", typeof(bool)) });
            Type server = typeof(CrossPromoConfigurationAsset.ServerType);
            Assert.That(server.FullName, Is.EqualTo("Hardlight.CrossPromoConfigurationAsset+ServerType"));
            Assert.That(server.Assembly, Is.SameAs(cross.Assembly));
            Assert.That(server.DeclaringType, Is.SameAs(cross));
            Assert.That(server.BaseType, Is.SameAs(typeof(Enum)));
            Assert.That((int)server.Attributes, Is.EqualTo(0x0102));
            Assert.That(Enum.GetUnderlyingType(server), Is.SameAs(typeof(int)));
            AssertAttributes(server);
            Assert.That(server.GetMethods(Declared), Is.Empty);
            Assert.That(server.GetConstructors(Declared), Is.Empty);
            Assert.That(server.GetProperties(Declared), Is.Empty);
            Assert.That(server.GetEvents(Declared), Is.Empty);
            Assert.That(server.GetNestedTypes(Declared), Is.Empty);
            AssertFields(server, new FieldSpec("value__", typeof(int), 0x0606, false, null),
                new FieldSpec("Dev", server, 0x8056, true, 0), new FieldSpec("Prod", server, 0x8056, true, 1));
            Assert.That(Enum.GetNames(server), Is.EqualTo(new[] { "Dev", "Prod" }));
            Assert.That(cross.GetNestedTypes(Declared), Is.EqualTo(new[] { server }));
            Assert.That(mesh.GetNestedTypes(Declared), Is.Empty);
        }

        private static readonly string[] MeshFields = { "m_precomputeMeshLocations", "m_resetMeshStorage",
            "m_lockToGameCamera", "m_useCameraFrustum", "m_loadBalancePopulate", "m_loadBalanceUnpopulate" };
        private static readonly string[] MeshProperties = { "PrecomputeMeshLocations", "ResetMeshStorage",
            "LockToGameCamera", "UseCameraFrustum", "LoadBalancePopulate", "LoadBalanceUnpopulate" };

        private static SerializedProperty Property(SerializedObject serialized, string name, SerializedPropertyType type)
        {
            SerializedProperty property = serialized.FindProperty(name);
            Assert.That(property, Is.Not.Null, name);
            Assert.That(property.propertyType, Is.EqualTo(type), name);
            return property;
        }

        private static void SetMesh(ProceduralMesh_SystemConfig mesh, bool[] values)
        {
            Assert.That(values.Length, Is.EqualTo(6));
            using (var serialized = new SerializedObject(mesh))
            {
                for (int i = 0; i < MeshFields.Length; i++) Property(serialized, MeshFields[i], SerializedPropertyType.Boolean).boolValue = values[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssertMesh(ProceduralMesh_SystemConfig mesh, bool[] values)
        {
            Assert.That(mesh.PrecomputeMeshLocations, Is.EqualTo(values[0]));
            Assert.That(mesh.ResetMeshStorage, Is.EqualTo(values[1]));
            Assert.That(mesh.LockToGameCamera, Is.EqualTo(values[2]));
            Assert.That(mesh.UseCameraFrustum, Is.EqualTo(values[3]));
            Assert.That(mesh.LoadBalancePopulate, Is.EqualTo(values[4]));
            Assert.That(mesh.LoadBalanceUnpopulate, Is.EqualTo(values[5]));
            using (var observed = new SerializedObject(mesh))
                for (int i = 0; i < MeshFields.Length; i++) Assert.That(Property(observed, MeshFields[i], SerializedPropertyType.Boolean).boolValue, Is.EqualTo(values[i]), MeshFields[i]);
        }

        private static void SetCross(CrossPromoConfigurationAsset cross, string title, string router, string platform,
            int server, long threshold, string incoming, string destination, string meta, bool local, int icon, int main, bool debug)
        {
            using (var serialized = new SerializedObject(cross))
            {
                Property(serialized, "m_gameTitle", SerializedPropertyType.String).stringValue = title;
                Property(serialized, "m_routerPropertyName", SerializedPropertyType.String).stringValue = router;
                Property(serialized, "m_platformName", SerializedPropertyType.String).stringValue = platform;
                Property(serialized, "m_server", SerializedPropertyType.Enum).intValue = server;
                Property(serialized, "m_invalidContentLengthThreshold", SerializedPropertyType.Integer).longValue = threshold;
                Property(serialized, "m_incomingFolder", SerializedPropertyType.String).stringValue = incoming;
                Property(serialized, "m_destinationFolder", SerializedPropertyType.String).stringValue = destination;
                Property(serialized, "m_metaFilename", SerializedPropertyType.String).stringValue = meta;
                Property(serialized, "m_canUseLocalStorage", SerializedPropertyType.Boolean).boolValue = local;
                Property(serialized, "m_iconImagePoolSize", SerializedPropertyType.Integer).intValue = icon;
                Property(serialized, "m_mainImagePoolSize", SerializedPropertyType.Integer).intValue = main;
                Property(serialized, "m_debug", SerializedPropertyType.Boolean).boolValue = debug;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssertCross(CrossPromoConfigurationAsset cross, string title, string router, string platform,
            string server, long threshold, string incoming, string destination, string meta, bool local, int icon, int main, bool debug)
        {
            Assert.That(cross.GameTitle, Is.EqualTo(title));
            Assert.That(cross.RouterPropertyName, Is.EqualTo(router));
            Assert.That(cross.PlatformName, Is.EqualTo(platform));
            Assert.That(cross.Server, Is.EqualTo(server));
            Assert.That(cross.InvalidContentLengthThreshold, Is.EqualTo(threshold));
            Assert.That(cross.IncomingFolder, Is.EqualTo(incoming));
            Assert.That(cross.DestinationFolder, Is.EqualTo(destination));
            Assert.That(cross.MetaFilename, Is.EqualTo(meta));
            Assert.That(cross.CanUseLocalStorage, Is.EqualTo(local));
            Assert.That(cross.IconImagePoolSize, Is.EqualTo(icon));
            Assert.That(cross.MainImagePoolSize, Is.EqualTo(main));
            Assert.That(cross.Debug, Is.EqualTo(debug));
            using (var observed = new SerializedObject(cross))
            {
                Assert.That(Property(observed, "m_gameTitle", SerializedPropertyType.String).stringValue, Is.EqualTo(title));
                Assert.That(Property(observed, "m_routerPropertyName", SerializedPropertyType.String).stringValue, Is.EqualTo(router));
                Assert.That(Property(observed, "m_platformName", SerializedPropertyType.String).stringValue, Is.EqualTo(platform));
                Assert.That(Property(observed, "m_server", SerializedPropertyType.Enum).intValue, Is.EqualTo(server == DevEndpoint ? 0 : 1));
                Assert.That(Property(observed, "m_invalidContentLengthThreshold", SerializedPropertyType.Integer).longValue, Is.EqualTo(threshold));
                Assert.That(Property(observed, "m_incomingFolder", SerializedPropertyType.String).stringValue, Is.EqualTo(incoming));
                Assert.That(Property(observed, "m_destinationFolder", SerializedPropertyType.String).stringValue, Is.EqualTo(destination));
                Assert.That(Property(observed, "m_metaFilename", SerializedPropertyType.String).stringValue, Is.EqualTo(meta));
                Assert.That(Property(observed, "m_canUseLocalStorage", SerializedPropertyType.Boolean).boolValue, Is.EqualTo(local));
                Assert.That(Property(observed, "m_iconImagePoolSize", SerializedPropertyType.Integer).intValue, Is.EqualTo(icon));
                Assert.That(Property(observed, "m_mainImagePoolSize", SerializedPropertyType.Integer).intValue, Is.EqualTo(main));
                Assert.That(Property(observed, "m_debug", SerializedPropertyType.Boolean).boolValue, Is.EqualTo(debug));
            }
        }

        private static void AssertBaseDefaults(SystemConfigurationAsset asset)
        {
            using (var serialized = new SerializedObject(asset))
            {
                Assert.That(Property(serialized, "m_baseAsset", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.Null);
                var overrides = serialized.FindProperty("m_overriddenFields");
                Assert.That(overrides, Is.Not.Null);
                Assert.That(overrides.isArray, Is.True);
                Assert.That(overrides.arraySize, Is.Zero);
            }
        }

        private static void SetBaseState(SystemConfigurationAsset owner, SystemConfigurationAsset reference, string[] overrides)
        {
            using (var serialized = new SerializedObject(owner))
            {
                Property(serialized, "m_baseAsset", SerializedPropertyType.ObjectReference).objectReferenceValue = reference;
                var list = serialized.FindProperty("m_overriddenFields");
                Assert.That(list, Is.Not.Null);
                Assert.That(list.isArray, Is.True);
                list.arraySize = overrides.Length;
                for (int i = 0; i < overrides.Length; i++) list.GetArrayElementAtIndex(i).stringValue = overrides[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssertBaseState(SystemConfigurationAsset owner, SystemConfigurationAsset reference, string[] overrides)
        {
            using (var observed = new SerializedObject(owner))
            {
                Assert.That(Property(observed, "m_baseAsset", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.SameAs(reference));
                var list = observed.FindProperty("m_overriddenFields");
                Assert.That(list, Is.Not.Null);
                Assert.That(list.isArray, Is.True);
                Assert.That(list.arraySize, Is.EqualTo(overrides.Length));
                for (int i = 0; i < overrides.Length; i++) Assert.That(list.GetArrayElementAtIndex(i).stringValue, Is.EqualTo(overrides[i]));
            }
        }

        private static void AssertOwner(Type owner, string assembly, string fullName)
        {
            Assert.That(owner.FullName, Is.EqualTo(fullName));
            Assert.That(owner.Assembly.GetName().Name, Is.EqualTo(assembly));
            Assert.That(owner.BaseType, Is.SameAs(typeof(SystemConfigurationAsset)));
            Assert.That(owner.IsGenericType, Is.False);
            Assert.That((int)owner.Attributes, Is.EqualTo(0x00100001));
            Assert.That(owner.GetInterfaces().Except(owner.BaseType.GetInterfaces()), Is.Empty);
            Assert.That(owner.GetEvents(Declared), Is.Empty);
        }

        private static void AssertPropertiesAndMethods(Type owner, PropertySpec[] expected)
        {
            PropertyInfo[] properties = owner.GetProperties(Declared).OrderBy(p => p.MetadataToken).ToArray();
            Assert.That(properties.Select(p => p.Name), Is.EqualTo(expected.Select(p => p.Name)));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(properties[i].PropertyType, Is.SameAs(expected[i].Type));
                Assert.That((int)properties[i].Attributes, Is.Zero);
                Assert.That(properties[i].GetIndexParameters(), Is.Empty);
                Assert.That(properties[i].GetSetMethod(true), Is.Null);
                Assert.That(properties[i].GetGetMethod(true), Is.Not.Null);
                AssertAttributes(properties[i]);
            }
            MethodInfo[] methods = owner.GetMethods(Declared).OrderBy(m => m.MetadataToken).ToArray();
            Assert.That(methods.Select(m => m.Name), Is.EqualTo(expected.Select(p => "get_" + p.Name).Concat(new[] { "Validate" })));
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                Assert.That(method.IsGenericMethod, Is.False);
                Assert.That(method.GetParameters(), Is.Empty);
                Assert.That(method.ReturnType, Is.SameAs(i < expected.Length ? expected[i].Type : typeof(void)));
                Assert.That((int)method.Attributes, Is.EqualTo(i < expected.Length ? 0x0886 : 0x00c6));
                Assert.That((int)method.GetMethodImplementationFlags(), Is.Zero);
                AssertAttributes(method);
                AssertAttributes(method.ReturnParameter);
                if (i < expected.Length)
                    Assert.That(method, Is.EqualTo(properties[i].GetGetMethod(true)), "Original property/getter association");
                if (i == expected.Length)
                    Assert.That(method.GetBaseDefinition().DeclaringType, Is.SameAs(typeof(SystemConfigurationAsset)));
            }
            ConstructorInfo[] constructors = owner.GetConstructors(Declared);
            Assert.That(constructors.Length, Is.EqualTo(1));
            Assert.That(constructors[0].GetParameters(), Is.Empty);
            Assert.That((int)constructors[0].Attributes, Is.EqualTo(0x1886));
            Assert.That((int)constructors[0].GetMethodImplementationFlags(), Is.Zero);
            AssertAttributes(constructors[0]);
        }

        private static void AssertFields(Type owner, params FieldSpec[] expected)
        {
            FieldInfo[] fields = owner.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(expected.Select(f => f.Name)));
            for (int i = 0; i < fields.Length; i++)
            {
                Assert.That(fields[i].FieldType, Is.SameAs(expected[i].Type));
                Assert.That((int)fields[i].Attributes, Is.EqualTo(expected[i].Flags));
                Assert.That(fields[i].IsLiteral, Is.EqualTo(expected[i].Literal));
                if (expected[i].Literal) Assert.That(fields[i].GetRawConstantValue(), Is.EqualTo(expected[i].Value));
                AssertAttributes(fields[i], expected[i].Attributes);
            }
        }

        private sealed class FieldSpec
        {
            public readonly string Name;
            public readonly Type Type;
            public readonly int Flags;
            public readonly bool Literal;
            public readonly object Value;
            public readonly AttributeSpec[] Attributes;
            public FieldSpec(string name, Type type, int flags, bool literal, object value, params AttributeSpec[] attributes)
            { Name = name; Type = type; Flags = flags; Literal = literal; Value = value; Attributes = attributes; }
        }
        private sealed class PropertySpec
        {
            public readonly string Name;
            public readonly Type Type;
            public PropertySpec(string name, Type type) { Name = name; Type = type; }
        }
        private sealed class AttributeSpec
        {
            public readonly Type Type;
            public readonly Type[] DeclaredParameters;
            public readonly Type[] ValueTypes;
            public readonly object[] Values;
            public readonly string[] NamedNames;
            public readonly object[] NamedValues;
            public AttributeSpec(Type type, Type[] declaredParameters, Type[] valueTypes, object[] values,
                string[] namedNames = null, object[] namedValues = null)
            { Type = type; DeclaredParameters = declaredParameters; ValueTypes = valueTypes; Values = values;
                NamedNames = namedNames ?? Array.Empty<string>(); NamedValues = namedValues ?? Array.Empty<object>(); }
        }
        private static PropertySpec P(string name, Type type) => new PropertySpec(name, type);
        private static FieldSpec Field(string name, Type type, params AttributeSpec[] attrs) => new FieldSpec(name, type, 1, false, null, attrs);
        private static AttributeSpec Marker(Type type) => new AttributeSpec(type, Type.EmptyTypes, Type.EmptyTypes, Array.Empty<object>());
        private static AttributeSpec Serialize() => Marker(typeof(SerializeField));
        private static AttributeSpec Tooltip(string value) => new AttributeSpec(typeof(TooltipAttribute), new[] { typeof(string) }, new[] { typeof(string) }, new object[] { value });
        private static AttributeSpec Header(string value) => new AttributeSpec(typeof(HeaderAttribute), new[] { typeof(string) }, new[] { typeof(string) }, new object[] { value });
        private static AttributeSpec Menu(string file, string menu) => new AttributeSpec(typeof(CreateAssetMenuAttribute),
            Type.EmptyTypes, Type.EmptyTypes, Array.Empty<object>(), new[] { "fileName", "menuName", "order" }, new object[] { file, menu, 0 });
        private static AttributeSpec OptionAttribute(Option option) => new AttributeSpec(typeof(Il2CppSetOptionAttribute),
            new[] { typeof(Option), typeof(object) }, new[] { typeof(Option), typeof(bool) }, new object[] { (int)option, false });

        private static void AssertAttributes(MemberInfo member, params AttributeSpec[] expected)
            => AssertAttributeList(CustomAttributeData.GetCustomAttributes(member), expected, member.Name);
        private static void AssertAttributes(ParameterInfo parameter, params AttributeSpec[] expected)
            => AssertAttributeList(CustomAttributeData.GetCustomAttributes(parameter), expected, "return parameter");
        private static void AssertAttributeList(IList<CustomAttributeData> actual, AttributeSpec[] expected, string context)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length), context);
            for (int i = 0; i < expected.Length; i++)
            {
                var a = actual[i]; var e = expected[i];
                Assert.That(a.AttributeType, Is.SameAs(e.Type), context);
                Assert.That(a.Constructor.DeclaringType, Is.SameAs(e.Type));
                Assert.That(a.Constructor.GetParameters().Select(p => p.ParameterType), Is.EqualTo(e.DeclaredParameters));
                Assert.That(a.ConstructorArguments.Select(v => v.ArgumentType), Is.EqualTo(e.ValueTypes));
                Assert.That(a.ConstructorArguments.Select(v => v.Value), Is.EqualTo(e.Values));
                Assert.That(a.NamedArguments.Select(n => n.MemberName), Is.EqualTo(e.NamedNames));
                Assert.That(a.NamedArguments.Select(n => n.TypedValue.Value), Is.EqualTo(e.NamedValues));
                foreach (var n in a.NamedArguments)
                {
                    Assert.That(n.IsField, Is.False);
                    Assert.That(n.MemberInfo.DeclaringType, Is.SameAs(e.Type));
                    Assert.That(n.TypedValue.ArgumentType, Is.SameAs(n.MemberName == "order" ? typeof(int) : typeof(string)));
                }
            }
        }

        private static void WithOwned(Action<OwnedScope> action)
        {
            var scope = new OwnedScope();
            ExceptionDispatchInfo primary = null;
            try { action(scope); }
            catch (Exception failure)
            {
                primary = ExceptionDispatchInfo.Capture(failure);
                scope.RecordFailureState();
            }
            List<Exception> cleanup = scope.Cleanup();
            if (primary != null) primary.Throw();
            if (cleanup.Count != 0) throw new AggregateException("Owned configuration fixture cleanup failed.", cleanup);
        }

        private sealed class OwnedScope
        {
            private readonly List<ScriptableObject> objects = new List<ScriptableObject>();
            private readonly List<string> folders = new List<string>();
            public T Create<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());
            public T Own<T>(T value) where T : ScriptableObject
            {
                Assert.That(value, Is.Not.Null);
                objects.Add(value);
                return value;
            }
            public string CreateAssetFolder()
            {
                string name = "ProjectLucidOwnedConfig22_" + Guid.NewGuid().ToString("N");
                string path = "Assets/" + name;
                Assert.That(AssetDatabase.IsValidFolder(path), Is.False);
                string guid = AssetDatabase.CreateFolder("Assets", name);
                if (!string.IsNullOrEmpty(guid)) folders.Add(path);
                Assert.That(guid, Is.Not.Empty);
                return path;
            }
            public void RecordFailureState()
            {
                foreach (var owned in objects)
                {
                    try
                    {
                        if (owned != null)
                            TestContext.Progress.WriteLine("Owned failure state type=" + owned.GetType().FullName +
                                " id=" + owned.GetInstanceID() + " json=" + EditorJsonUtility.ToJson(owned, false));
                    }
                    catch (Exception secondary) { ReportSecondary("failure-state capture", secondary); }
                }
            }
            public List<Exception> Cleanup()
            {
                var failures = new List<Exception>();
                for (int i = folders.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (AssetDatabase.IsValidFolder(folders[i]) && !AssetDatabase.DeleteAsset(folders[i]))
                            throw new InvalidOperationException("Could not delete normally owned folder " + folders[i]);
                    }
                    catch (Exception failure) { failures.Add(failure); ReportSecondary("folder cleanup", failure); }
                }
                for (int i = objects.Count - 1; i >= 0; i--)
                {
                    try { if (objects[i] != null) Object.DestroyImmediate(objects[i], true); }
                    catch (Exception failure) { failures.Add(failure); ReportSecondary("object cleanup", failure); }
                }
                return failures;
            }
            private static void ReportSecondary(string step, Exception failure)
            {
                try { TestContext.Progress.WriteLine("Secondary owned-fixture " + step + " failure: " + failure); }
                catch (Exception) { /* Reporting must not replace the original test or cleanup failure. */ }
            }
        }
    }
}
