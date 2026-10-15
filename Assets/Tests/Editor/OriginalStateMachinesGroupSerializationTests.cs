using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Element = HardlightProject.DefinitionDataType<string, Hardlight.FiniteStateMachineScriptableObject>.DefinitionElement<Hardlight.FiniteStateMachineScriptableObject>;

namespace ProjectLucid.Tests.Editor
{
    // Genuine owned storage and callbacks only; no group lookup, FSM acquisition
    // or supplied-asset binding. Rebuilt declarations do not prove native layout.
    public sealed class OriginalStateMachinesGroupSerializationTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // Shipped HLUnityCore.Runtime attribute blobs, in blob order: type 0x02000094 has
        // CreateAssetMenu (fileName "FSM", menuName, order 0) then two Il2CppSetOption rows
        // (NullChecks, ArrayBoundsChecks; false). Field rows 0x040001f6..fc carry exactly the
        // SerializeField/Header/Tooltip/TextArea(5, 30) sequences and literals listed below;
        // 0x040001fd..0x04000200 have no attribute rows.
        [Test]
        public void FsmDeclarationRetainsOriginalAttributeOrderAndCurrentConstructorTypes()
        {
            Type owner = typeof(FiniteStateMachineScriptableObject);
            Assert.That(owner.Attributes, Is.EqualTo(TypeAttributes.Public | TypeAttributes.BeforeFieldInit));
            IList<CustomAttributeData> attributes = owner.GetCustomAttributesData();
            CollectionAssert.AreEqual(new[] { typeof(CreateAssetMenuAttribute),
                typeof(Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute),
                typeof(Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute) }, attributes.Select(a => a.AttributeType));
            CustomAttributeData menu = attributes[0];
            Assert.That(menu.Constructor.GetParameters(), Is.Empty);
            Assert.That(menu.ConstructorArguments, Is.Empty);
            CollectionAssert.AreEqual(new[] { "fileName", "menuName", "order" }, menu.NamedArguments.Select(a => a.MemberName));
            CollectionAssert.AreEqual(new[] { typeof(string), typeof(string), typeof(int) }, menu.NamedArguments.Select(a => a.TypedValue.ArgumentType));
            CollectionAssert.AreEqual(new object[] { "FSM", "Hardlight/HLUnityCore/Finite State Machine", 0 }, menu.NamedArguments.Select(a => a.TypedValue.Value));
            Assert.That(menu.NamedArguments.All(a => !a.IsField), Is.True);
            for (int i = 1; i < 3; i++)
            {
                CustomAttributeData option = attributes[i];
                Assert.That(option.AttributeType.Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
                // Observe current parameter types independently of the boxed Boolean payload.
                CollectionAssert.AreEqual(new[] { typeof(Unity.IL2CPP.CompilerServices.Option), typeof(object) },
                    option.Constructor.GetParameters().Select(p => p.ParameterType));
                Assert.That(option.ConstructorArguments.Count, Is.EqualTo(2));
                Assert.That(option.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Unity.IL2CPP.CompilerServices.Option)));
                Assert.That(option.ConstructorArguments[0].Value, Is.TypeOf<int>());
                Assert.That(option.ConstructorArguments[0].Value, Is.EqualTo(i));
                Assert.That(option.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
                Assert.That(option.ConstructorArguments[1].Value, Is.TypeOf<bool>());
                Assert.That(option.ConstructorArguments[1].Value, Is.EqualTo(false));
                Assert.That(option.NamedArguments, Is.Empty);
                Assert.That(option.Constructor, Is.EqualTo(attributes[1].Constructor));
            }

            FieldInfo[] fields = owner.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            CollectionAssert.AreEqual(new[] { "m_name", "m_isOverwriteOk", "m_reuseExisting", "m_releaseOnDestroy",
                "m_referencedScriptableObjects", "m_relativePathToJSON", "m_embeddedJSON", "m_fsmDictionary",
                "m_fsm", "m_refCount", "OnInitialisationComplete" }, fields.Select(f => f.Name));
            CollectionAssert.AreEqual(new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool),
                typeof(List<ScriptableObject>), typeof(string), typeof(string), typeof(Dictionary<string, FiniteStateMachine>),
                typeof(FiniteStateMachine), typeof(int), typeof(Action<FiniteStateMachineScriptableObject>) }, fields.Select(f => f.FieldType));
            Type[][] fieldAttributes = {
                new[] { typeof(SerializeField), typeof(TooltipAttribute) },
                new[] { typeof(HeaderAttribute), typeof(SerializeField), typeof(TooltipAttribute) },
                new[] { typeof(SerializeField), typeof(TooltipAttribute) },
                new[] { typeof(TooltipAttribute), typeof(SerializeField) },
                new[] { typeof(TooltipAttribute), typeof(SerializeField) },
                new[] { typeof(SerializeField), typeof(HeaderAttribute), typeof(TooltipAttribute) },
                new[] { typeof(SerializeField), typeof(TooltipAttribute), typeof(TextAreaAttribute) },
                Type.EmptyTypes, Type.EmptyTypes, Type.EmptyTypes, Type.EmptyTypes
            };
            string[] tooltips = {
                "The FSM JSON can contain multiple FSMs, specify which one should be used",
                "If true then it's ok for existing FSMs to be overwritten with the new FSMs where there's a name clash",
                "If true then existing FSMs will be reused when there's a name clash. The loaded FSM will be discarded",
                "If true then the loaded FSMs will be released from the FSMManager when ReleaseFSM is called",
                "Reference scriptable objects used by the FSM's states and transitions to ensure they're loaded.",
                "Path is relative to the StreamingAssets directory",
                "JSON data for the FSM. If specified 'Relative Path To JSON' is ignored"
            };
            for (int i = 0; i < fields.Length; i++)
            {
                Assert.That(fields[i].Attributes, Is.EqualTo(i == 10 ? FieldAttributes.Public : FieldAttributes.Private));
                IList<CustomAttributeData> data = fields[i].GetCustomAttributesData();
                CollectionAssert.AreEqual(fieldAttributes[i], data.Select(a => a.AttributeType));
                Assert.That(data.All(a => a.NamedArguments.Count == 0), Is.True);
                Assert.That(data.Any(a => a.AttributeType == typeof(SerializeField)), Is.EqualTo(i < 7));
                foreach (CustomAttributeData serialize in data.Where(a => a.AttributeType == typeof(SerializeField)))
                {
                    Assert.That(serialize.ConstructorArguments, Is.Empty);
                    Assert.That(serialize.Constructor.GetParameters(), Is.Empty);
                }
                if (i < 7)
                {
                    CustomAttributeData tooltip = data.Single(a => a.AttributeType == typeof(TooltipAttribute));
                    Assert.That(tooltip.ConstructorArguments.Count, Is.EqualTo(1));
                    Assert.That(tooltip.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(string)));
                    Assert.That(tooltip.ConstructorArguments[0].Value, Is.EqualTo(tooltips[i]));
                }
            }
            int[] headerFields = { 1, 5 };
            string[] headers = { "Advanced Settings", "JSON" };
            for (int i = 0; i < headerFields.Length; i++)
            {
                CustomAttributeData header = fields[headerFields[i]].GetCustomAttributesData().Single(a => a.AttributeType == typeof(HeaderAttribute));
                Assert.That(header.ConstructorArguments.Count, Is.EqualTo(1));
                Assert.That(header.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(string)));
                Assert.That(header.ConstructorArguments[0].Value, Is.EqualTo(headers[i]));
            }
            CustomAttributeData area = fields[6].GetCustomAttributesData().Single(a => a.AttributeType == typeof(TextAreaAttribute));
            CollectionAssert.AreEqual(new[] { typeof(int), typeof(int) }, area.ConstructorArguments.Select(a => a.ArgumentType));
            CollectionAssert.AreEqual(new object[] { 5, 30 }, area.ConstructorArguments.Select(a => a.Value));
            // The public Action field remains present; this does not assert serializer eligibility.
        }

        // Shipped Game.Runtime rows: StateMachinesGroup 0x020005c5 declares no fields;
        // DefinitionDataType`2 0x0200052e field m_elements 0x040012b8 [SerializeField];
        // DefinitionElement`1 0x0200052f fields Name 0x040012b9 [SerializeField, HideInInspector]
        // and Data 0x040012ba [SerializeField]. The Data-binds-TData check is the project's
        // extra-T rule; its generic parameter row is not decoded here.
        [Test]
        public void ClosedStateMachinesGroupRetainsThreeArgumentElementAndOriginalConstraints()
        {
            Type owner = typeof(StateMachinesGroup), element = typeof(Element);
            Assert.That(owner.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            Assert.That(owner.BaseType, Is.EqualTo(typeof(DefinitionDataType<string, FiniteStateMachineScriptableObject>)));
            Assert.That(owner.GetFields(Declared), Is.Empty);
            Assert.That(typeof(FiniteStateMachineScriptableObject).Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
            Type[] baseParameters = typeof(DefinitionDataType<,>).GetGenericArguments();
            Type[] elementParameters = element.GetGenericTypeDefinition().GetGenericArguments();
            CollectionAssert.AreEqual(new[] { "TKey", "TData" }, baseParameters.Select(p => p.Name));
            CollectionAssert.AreEqual(new[] { "TKey", "TData", "T" }, elementParameters.Select(p => p.Name));
            foreach (Type parameter in baseParameters.Concat(elementParameters))
            {
                Assert.That(parameter.GenericParameterAttributes, Is.EqualTo(GenericParameterAttributes.None));
                CollectionAssert.AreEqual(parameter.Name == "TData" ? new[] { typeof(ScriptableObject) } : Type.EmptyTypes,
                    parameter.GetGenericParameterConstraints());
            }
            FieldInfo array = owner.GetField("m_elements", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(array, Is.Not.Null); Assert.That(array.DeclaringType, Is.EqualTo(owner.BaseType));
            Assert.That(array.IsDefined(typeof(SerializeField), false), Is.True);
            Assert.That(array.IsDefined(typeof(SerializeReference), false), Is.False);
            Assert.That(array.FieldType, Is.EqualTo(typeof(Element[]))); Assert.That(array.FieldType.GetArrayRank(), Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { typeof(string), typeof(FiniteStateMachineScriptableObject), typeof(FiniteStateMachineScriptableObject) }, element.GetGenericArguments());
            Assert.That(element.IsSerializable && element.IsNestedPublic, Is.True);
            Assert.That(element.BaseType, Is.EqualTo(typeof(DefinitionDataType.DefinitionElement)));
            Assert.That(typeof(ISerializationCallbackReceiver).IsAssignableFrom(element), Is.True);
            FieldInfo[] fields = element.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            CollectionAssert.AreEqual(new[] { "Name", "Data" }, fields.Select(f => f.Name));
            CollectionAssert.AreEqual(new[] { typeof(string), typeof(FiniteStateMachineScriptableObject) }, fields.Select(f => f.FieldType));
            Assert.That(fields.All(f => f.IsPublic && f.IsDefined(typeof(SerializeField), false)), Is.True);
            Assert.That(fields[0].IsDefined(typeof(HideInInspector), false), Is.True);
            Assert.That(fields[1].IsDefined(typeof(HideInInspector), false), Is.False);
            Assert.That(element.GetGenericTypeDefinition().GetField("Data").FieldType, Is.EqualTo(elementParameters[1]), "Data binds enclosing TData, not the extra T.");
            Assert.That(typeof(DefinitionDataType<string, FiniteStateMachineScriptableObject>.DefinitionElement<int>).GetField("Data").FieldType,
                Is.EqualTo(typeof(FiniteStateMachineScriptableObject)), "The unconstrained extra argument does not replace Data's type.");
        }

        [Test]
        public void GenuineOwnedGroupPreservesOrderedNameAndDataStorageAfterReimport()
        {
            Owned.Run(owned => {
                Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText), "Observe the existing project mode without changing it.");
                var first = owned.Asset<FiniteStateMachineScriptableObject>("First.asset");
                var second = owned.Asset<FiniteStateMachineScriptableObject>("Second.asset");
                first.name = "LucidOwnedFirst_" + Guid.NewGuid().ToString("N");
                second.name = "LucidOwnedSecond_" + Guid.NewGuid().ToString("N");
                var group = owned.Transient<StateMachinesGroup>();
                Assert.That(group.m_elements, Is.Null, "Observe construction before asset serialization authors the array.");
                owned.Store(group, "Group.asset");
                group.m_elements = new[] { new Element(first), new Element(second), new Element(null), new Element(first) };
                string[] names = { first.name, second.name, "NONE", first.name };
                var identities = new[] { ObserveIdentity(first), ObserveIdentity(second), null, ObserveIdentity(first) };
                string[] pointers = identities.Select(i => i == null ? "{fileID: 0}" : "{fileID: " + i.FileId.ToString(CultureInfo.InvariantCulture) + ", guid: " + i.Guid + ", type: 2}").ToArray();
                AssetIdentity groupIdentity = ObserveIdentity(group);
                MonoScript groupScript = Script(group, "Assets/Scripts/Definitions/StateMachinesGroup.cs", "ae4b1ceada534d30958f790f93e84d54");
                MonoScript fsmScript = Script(first, "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/FiniteStateMachineScriptableObject.cs", "3e17c152cb4643c4baf8f426127adbee");
                AssetIdentity groupScriptIdentity = ObserveIdentity(groupScript), fsmScriptIdentity = ObserveIdentity(fsmScript);
                owned.Save(first); owned.Save(second); owned.Save(group);
                string before = File.ReadAllText(owned.Physical("Group.asset"));
                AssertStoredPairs(before, names, pointers);
                // Unload only the owned group; its genuine target assets stay loaded.
                Resources.UnloadAsset(group);
                owned.Reimport("First.asset"); owned.Reimport("Second.asset"); owned.Reimport("Group.asset");
                var loaded = owned.Load<StateMachinesGroup>("Group.asset");
                AssertIdentity(loaded, groupIdentity); Assert.That(MonoScript.FromScriptableObject(loaded), Is.EqualTo(groupScript));
                AssertIdentity(MonoScript.FromScriptableObject(loaded), groupScriptIdentity);
                Assert.That(loaded.m_elements, Is.Not.Null); Assert.That(loaded.m_elements.Length, Is.EqualTo(4));
                var data = owned.Observe(loaded); data.Update();
                Assert.That(Property(data.FindProperty("m_Script"), SerializedPropertyType.ObjectReference).objectReferenceValue, Is.EqualTo(groupScript));
                SerializedProperty array = Property(data.FindProperty("m_elements"), SerializedPropertyType.Generic);
                Assert.That(array.isArray, Is.True); Assert.That(array.arraySize, Is.EqualTo(4));
                for (int i = 0; i < 4; i++)
                {
                    Element actual = loaded.m_elements[i]; Assert.That(actual, Is.Not.Null); Assert.That(actual.Name, Is.EqualTo(names[i]));
                    SerializedProperty row = array.GetArrayElementAtIndex(i);
                    Assert.That(Property(row.FindPropertyRelative("Name"), SerializedPropertyType.String).stringValue, Is.EqualTo(names[i]));
                    Object reference = Property(row.FindPropertyRelative("Data"), SerializedPropertyType.ObjectReference).objectReferenceValue;
                    Assert.That(reference, Is.EqualTo(actual.Data));
                    if (identities[i] == null) Assert.That(reference, Is.Null);
                    else
                    {
                        AssertIdentity(reference, identities[i]);
                        Assert.That(actual.Data.GetType(), Is.EqualTo(typeof(FiniteStateMachineScriptableObject)));
                        Assert.That(MonoScript.FromScriptableObject(actual.Data), Is.EqualTo(fsmScript));
                        AssertIdentity(MonoScript.FromScriptableObject(actual.Data), fsmScriptIdentity);
                    }
                }
                AssertStoredPairs(File.ReadAllText(owned.Physical("Group.asset")), names, pointers);
            });
        }

        [Test]
        public void GenuineOwnedElementCallbacksRetainCurrentNameAndNullOrDestroyedNone()
        {
            Owned.Run(owned => {
                var target = owned.Transient<FiniteStateMachineScriptableObject>(); target.name = "OwnedOriginalName";
                var element = new Element(target); Assert.That(element.Name, Is.EqualTo("OwnedOriginalName"));
                Assert.That(element.Data, Is.SameAs(target));
                ISerializationCallbackReceiver callbacks = element;
                target.name = "OwnedChangedName";
                callbacks.OnAfterDeserialize(); Assert.That(element.Name, Is.EqualTo("OwnedOriginalName"), "The original empty callback does not repair a stale Name.");
                callbacks.OnBeforeSerialize(); Assert.That(element.Name, Is.EqualTo("OwnedChangedName")); Assert.That(element.Data, Is.SameAs(target));
                var empty = new Element(null); Assert.That(empty.Name, Is.EqualTo("NONE")); Assert.That(ReferenceEquals(empty.Data, null), Is.True);
                ((ISerializationCallbackReceiver)empty).OnBeforeSerialize(); ((ISerializationCallbackReceiver)empty).OnAfterDeserialize();
                Assert.That(empty.Name, Is.EqualTo("NONE")); Assert.That(ReferenceEquals(empty.Data, null), Is.True);
                Object.DestroyImmediate(target);
                Assert.That(ReferenceEquals(element.Data, null), Is.False); Assert.That(element.Data == null, Is.True, "Genuine Unity-null differs from CLR-null.");
                callbacks.OnBeforeSerialize(); Assert.That(element.Name, Is.EqualTo("NONE")); Assert.That(ReferenceEquals(element.Data, target), Is.True);
                callbacks.OnAfterDeserialize(); Assert.That(element.Name, Is.EqualTo("NONE"));
                var destroyed = new Element(target); Assert.That(destroyed.Name, Is.EqualTo("NONE")); Assert.That(ReferenceEquals(destroyed.Data, target), Is.True);
            });
        }

        private sealed class AssetIdentity { public string Guid; public long FileId; }
        private static AssetIdentity ObserveIdentity(Object value)
        {
            string guid; long id; Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out id), Is.True);
            Assert.That(guid, Is.Not.Empty); Assert.That(id, Is.GreaterThan(0)); return new AssetIdentity { Guid = guid, FileId = id };
        }
        private static void AssertIdentity(Object value, AssetIdentity expected)
        {
            AssetIdentity actual = ObserveIdentity(value); Assert.That(actual.Guid, Is.EqualTo(expected.Guid)); Assert.That(actual.FileId, Is.EqualTo(expected.FileId));
        }
        private static MonoScript Script(ScriptableObject value, string path, string guid)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Assert.That(script, Is.Not.Null); Assert.That(script.GetClass(), Is.EqualTo(value.GetType()));
            Assert.That(MonoScript.FromScriptableObject(value), Is.EqualTo(script)); Assert.That(ObserveIdentity(script).Guid, Is.EqualTo(guid)); return script;
        }
        private static SerializedProperty Property(SerializedProperty value, SerializedPropertyType type)
        {
            Assert.That(value, Is.Not.Null); Assert.That(value.propertyType, Is.EqualTo(type)); return value;
        }
        private static void AssertStoredPairs(string text, string[] names, string[] pointers)
        {
            Assert.That(Regex.Matches(text, "^  m_elements:\\r?$", RegexOptions.Multiline).Count, Is.EqualTo(1));
            MatchCollection rows = Regex.Matches(text, "^  - Name: ([^\\r\\n]*)\\r?\\n    Data: (\\{[^\\r\\n]*\\})\\r?$", RegexOptions.Multiline);
            Assert.That(rows.Count, Is.EqualTo(names.Length));
            for (int i = 0; i < names.Length; i++)
            {
                Assert.That(rows[i].Groups[1].Value, Is.EqualTo(names[i])); Assert.That(rows[i].Groups[2].Value, Is.EqualTo(pointers[i]));
            }
        }

        private sealed class Owned
        {
            private readonly string folder = "Assets/__LucidOwnedStateMachines_" + Guid.NewGuid().ToString("N");
            private readonly string root = Directory.GetParent(Application.dataPath).FullName;
            private readonly List<Object> objects = new List<Object>();
            private readonly List<SerializedObject> serialized = new List<SerializedObject>();
            private bool created;
            public string Physical(string file) => Path.Combine(root, folder, file);
            public T Transient<T>() where T : ScriptableObject { T value = ScriptableObject.CreateInstance<T>(); objects.Add(value); return value; }
            public T Asset<T>(string file) where T : ScriptableObject { T value = Transient<T>(); Store(value, file); return value; }
            public void Store(ScriptableObject value, string file) => AssetDatabase.CreateAsset(value, folder + "/" + file);
            public void Save(Object value) { EditorUtility.SetDirty(value); AssetDatabase.SaveAssetIfDirty(value); }
            public void Reimport(string file) => AssetDatabase.ImportAsset(folder + "/" + file, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            public T Load<T>(string file) where T : ScriptableObject
            {
                T value = AssetDatabase.LoadAssetAtPath<T>(folder + "/" + file); Assert.That(value, Is.Not.Null);
                if (!objects.Any(o => ReferenceEquals(o, value))) objects.Add(value); return value;
            }
            public SerializedObject Observe(Object value) { var data = new SerializedObject(value); serialized.Add(data); return data; }
            public static void Run(Action<Owned> body)
            {
                var owned = new Owned(); ExceptionDispatchInfo primary = null; var secondary = new List<Exception>();
                try
                {
                    string physical = Path.Combine(owned.root, owned.folder);
                    for (string part = owned.root; part != null; part = Path.GetDirectoryName(part))
                        if (Directory.Exists(part)) Assert.That((int)(File.GetAttributes(part) & FileAttributes.ReparsePoint), Is.EqualTo(0));
                    Assert.That(Directory.Exists(physical) || File.Exists(physical) || File.Exists(physical + ".meta"), Is.False);
                    Assert.That(AssetDatabase.IsValidFolder(owned.folder), Is.False);
                    owned.created = !String.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(owned.folder))); Assert.That(owned.created, Is.True);
                    body(owned);
                }
                catch (Exception failure) { primary = ExceptionDispatchInfo.Capture(failure); }
                finally
                {
                    foreach (SerializedObject value in owned.serialized) try { value.Dispose(); } catch (Exception failure) { secondary.Add(failure); }
                    if (owned.created)
                        try { Assert.That(AssetDatabase.DeleteAsset(owned.folder), Is.True, "Delete only this test's owned scratch."); } catch (Exception failure) { secondary.Add(failure); }
                    foreach (Object value in owned.objects)
                        try { if (value != null && !AssetDatabase.Contains(value)) Object.DestroyImmediate(value); } catch (Exception failure) { secondary.Add(failure); }
                    if (owned.created)
                        try
                        {
                            string physical = Path.Combine(owned.root, owned.folder);
                            Assert.That(AssetDatabase.IsValidFolder(owned.folder), Is.False);
                            Assert.That(Directory.Exists(physical) || File.Exists(physical) || File.Exists(physical + ".meta"), Is.False, "Owned scratch and metadata are physically absent.");
                        }
                        catch (Exception failure) { secondary.Add(failure); }
                }
                if (primary != null)
                {
                    if (secondary.Count != 0) try { primary.SourceException.Data["OwnedStateMachinesSecondaryFailures"] = new AggregateException(secondary); } catch { }
                    primary.Throw();
                }
                if (secondary.Count != 0) throw new AggregateException(secondary);
            }
        }
    }
}
