using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalUIHighlightTests
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Test]
        public void WholeOwnersRetainSerializedFieldsAndPublicShowContract()
        {
            Type highlight = typeof(UIHighlight);
            Type definition = typeof(UIHighlightDefinition);
            Assert.That(highlight.FullName, Is.EqualTo("Hardlight.UIHighlight"));
            Assert.That(definition.FullName, Is.EqualTo("Hardlight.UIHighlightDefinition"));
            Assert.That(highlight.Assembly.GetName().Name, Is.EqualTo("HLModernUI.Runtime"));
            Assert.That(definition.Assembly, Is.SameAs(highlight.Assembly));
            Assert.That((int)highlight.Attributes, Is.EqualTo(1048833));
            Assert.That((int)definition.Attributes, Is.EqualTo(1048833));
            Assert.That(highlight.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(definition.BaseType, Is.EqualTo(typeof(ScriptableObjectWithGuid)));
            Assert.That(highlight.GetFields(Own).OrderBy(x => x.MetadataToken).Select(x => x.Name), Is.EqualTo(new[] { "m_definition", "m_onShowHighlight" }));
            Assert.That(definition.GetFields(Own).Select(x => x.Name), Is.EqualTo(new[] { "m_prefab" }));
            SerializedField(highlight, "m_definition", definition);
            SerializedField(highlight, "m_onShowHighlight", typeof(UnityEvent));
            SerializedField(definition, "m_prefab", highlight);
            Assert.That(highlight.GetMethods(Own).OrderBy(x => x.MetadataToken).Select(x => x.Name), Is.EqualTo(new[] { "get_Definition", "OnShowHighlight" }));
            Assert.That(definition.GetMethods(Own).Select(x => x.Name), Is.EqualTo(new[] { "get_Prefab" }));
            Assert.That(highlight.GetProperties(Own).Length, Is.EqualTo(1));
            Assert.That(definition.GetProperties(Own).Length, Is.EqualTo(1));
            Assert.That(highlight.GetProperty("Definition").PropertyType, Is.EqualTo(definition));
            Assert.That(definition.GetProperty("Prefab").PropertyType, Is.EqualTo(highlight));
            Assert.That(highlight.GetProperty("Definition").GetSetMethod(true), Is.Null);
            Assert.That(definition.GetProperty("Prefab").GetSetMethod(true), Is.Null);
            MethodInfo show = highlight.GetMethod("OnShowHighlight", Own);
            Assert.That(show.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(show.GetParameters().Length, Is.EqualTo(0));
            Assert.That((int)show.Attributes, Is.EqualTo(134));
            Assert.That(highlight.GetConstructors(Own).Single().GetParameters().Length, Is.EqualTo(0));
            Assert.That(definition.GetConstructors(Own).Single().GetParameters().Length, Is.EqualTo(0));
            Assert.That(highlight.GetNestedTypes(Own).Length + definition.GetNestedTypes(Own).Length + highlight.GetEvents(Own).Length + definition.GetEvents(Own).Length, Is.EqualTo(0));
        }

        [Test]
        public void NormalOwnedAttachmentKeepsReferenceGettersAndNaturallyObservedShowBranch()
        {
            using (var owned = new OwnedHighlight())
            {
                Assert.That(ReferenceEquals(owned.Highlight.Definition, DefinitionField.GetValue(owned.Highlight)), Is.True);
                Assert.That(ReferenceEquals(owned.Highlight.Definition, null), Is.True);
                Assert.That(ReferenceEquals(owned.Definition.Prefab, PrefabField.GetValue(owned.Definition)), Is.True);
                Assert.That(ReferenceEquals(owned.Definition.Prefab, null), Is.True);
                UnityEvent actual = ReadEvent(owned.Highlight);
                // No field is assigned. A naturally null event exercises the
                // original null branch; a naturally allocated empty event is a
                // separate genuine serialization observation, not that branch.
                if (actual != null) Assert.That(actual.GetPersistentEventCount(), Is.EqualTo(0));
                Assert.DoesNotThrow(() => owned.Highlight.OnShowHighlight());
                Assert.That(ReferenceEquals(ReadEvent(owned.Highlight), actual), Is.True);
            }
        }

        [Test]
        public void GenuineEventRunsOwnedListenersSynchronouslyAndRemovesOnlyThoseListeners()
        {
            using (var owned = new OwnedHighlight(true))
            {
                UnityEvent actual = RequireNaturallyAllocatedEvent(owned.Highlight);
                var calls = new List<string>();
                UnityAction first = () => calls.Add("first");
                UnityAction second = () => calls.Add("second");
                try
                {
                    actual.AddListener(first);
                    actual.AddListener(second);
                    owned.Highlight.OnShowHighlight();
                    Assert.That(calls, Is.EqualTo(new[] { "first", "second" }));
                    actual.RemoveListener(first);
                    calls.Clear();
                    owned.Highlight.OnShowHighlight();
                    Assert.That(calls, Is.EqualTo(new[] { "second" }));
                    actual.RemoveListener(second);
                    calls.Clear();
                    owned.Highlight.OnShowHighlight();
                    Assert.That(calls.Count, Is.EqualTo(0));
                    Assert.That(ReferenceEquals(ReadEvent(owned.Highlight), actual), Is.True);
                }
                finally
                {
                    try { actual.RemoveListener(first); }
                    finally { actual.RemoveListener(second); }
                }
            }
        }

        [Test]
        public void ListenerExceptionEscapesShowSynchronouslyAndLaterOwnedListenerRemainsRegistered()
        {
            using (var owned = new OwnedHighlight(true))
            {
                UnityEvent actual = RequireNaturallyAllocatedEvent(owned.Highlight);
                var expected = new InvalidOperationException("owned highlight listener fault");
                int firstCalls = 0;
                int laterCalls = 0;
                UnityAction first = () => { firstCalls++; throw expected; };
                UnityAction later = () => laterCalls++;
                try
                {
                    actual.AddListener(first);
                    actual.AddListener(later);
                    InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => owned.Highlight.OnShowHighlight());
                    Assert.That(ReferenceEquals(thrown, expected), Is.True);
                    Assert.That(firstCalls, Is.EqualTo(1));
                    Assert.That(laterCalls, Is.EqualTo(0));
                    actual.RemoveListener(first);
                    owned.Highlight.OnShowHighlight();
                    Assert.That(firstCalls, Is.EqualTo(1));
                    Assert.That(laterCalls, Is.EqualTo(1));
                    Assert.That(ReferenceEquals(ReadEvent(owned.Highlight), actual), Is.True);
                }
                finally
                {
                    try { actual.RemoveListener(first); }
                    finally { actual.RemoveListener(later); }
                }
            }
        }

        [Test]
        public void TwoNormallyOwnedComponentsKeepDistinctEventAndDefinitionObjectIdentities()
        {
            using (var first = new OwnedHighlight(true))
            using (var second = new OwnedHighlight(true))
            {
                UnityEvent firstEvent = RequireNaturallyAllocatedEvent(first.Highlight);
                UnityEvent secondEvent = RequireNaturallyAllocatedEvent(second.Highlight);
                Assert.That(ReferenceEquals(firstEvent, secondEvent), Is.False);
                Assert.That(ReferenceEquals(first.Definition, second.Definition), Is.False);
                int firstCalls = 0;
                int secondCalls = 0;
                UnityAction a = () => firstCalls++;
                UnityAction b = () => secondCalls++;
                try
                {
                    firstEvent.AddListener(a);
                    secondEvent.AddListener(b);
                    first.Highlight.OnShowHighlight();
                    Assert.That(firstCalls, Is.EqualTo(1));
                    Assert.That(secondCalls, Is.EqualTo(0));
                    second.Highlight.OnShowHighlight();
                    Assert.That(firstCalls, Is.EqualTo(1));
                    Assert.That(secondCalls, Is.EqualTo(1));
                }
                finally
                {
                    try { firstEvent.RemoveListener(a); }
                    finally { secondEvent.RemoveListener(b); }
                }
            }
        }

        private static readonly FieldInfo DefinitionField = typeof(UIHighlight).GetField("m_definition", Own);
        private static readonly FieldInfo EventField = typeof(UIHighlight).GetField("m_onShowHighlight", Own);
        private static readonly FieldInfo PrefabField = typeof(UIHighlightDefinition).GetField("m_prefab", Own);

        private static void SerializedField(Type owner, string name, Type expected)
        {
            FieldInfo field = owner.GetField(name, Own);
            Assert.That(field.DeclaringType, Is.EqualTo(owner));
            Assert.That(field.FieldType, Is.EqualTo(expected));
            Assert.That((int)field.Attributes, Is.EqualTo(1));
            IList<CustomAttributeData> attributes = field.GetCustomAttributesData();
            Assert.That(attributes.Count, Is.EqualTo(1));
            Assert.That(attributes[0].AttributeType, Is.EqualTo(typeof(SerializeField)));
            Assert.That(attributes[0].ConstructorArguments.Count + attributes[0].NamedArguments.Count, Is.EqualTo(0));
        }

        private static UnityEvent ReadEvent(UIHighlight highlight)
        {
            return (UnityEvent)EventField.GetValue(highlight);
        }

        private static UnityEvent RequireNaturallyAllocatedEvent(UIHighlight highlight)
        {
            UnityEvent actual = ReadEvent(highlight);
            if (actual == null)
                throw new InvalidOperationException("Owned AddComponent has a null serialized UnityEvent: listener coverage is an unfulfilled Engine allocation prerequisite; no field injection is permitted.");
            Assert.That(actual.GetPersistentEventCount(), Is.EqualTo(0));
            return actual;
        }

        private sealed class OwnedHighlight : IDisposable
        {
            private GameObject host;
            private string prefabFolder;
            private bool ownsPrefabFolder;
            public UIHighlight Highlight { get; private set; }
            public UIHighlightDefinition Definition { get; private set; }

            public OwnedHighlight(bool serializedPrefab = false)
            {
                try
                {
                    host = new GameObject("ProjectLucid owned original UI highlight");
                    if (!serializedPrefab) host.hideFlags = HideFlags.HideAndDontSave;
                    host.SetActive(false);
                    Highlight = host.AddComponent<UIHighlight>();
                    if (serializedPrefab) SerializeAndInstantiateOwnedPrefab();
                    if (serializedPrefab) host.hideFlags = HideFlags.HideAndDontSave;
                    Definition = ScriptableObject.CreateInstance<UIHighlightDefinition>();
                    Definition.hideFlags = HideFlags.HideAndDontSave;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private void SerializeAndInstantiateOwnedPrefab()
            {
                // A null inline event on plain AddComponent is preserved by the
                // standalone case. Listener cases require Unity's normal prefab
                // save/import/instantiate lifecycle to materialize the event;
                // no private field or SerializedProperty value is assigned.
                prefabFolder = "Assets/LucidOwnedUIHighlight_" + Guid.NewGuid().ToString("N");
                string project = Directory.GetParent(Application.dataPath).FullName;
                Assert.That(Directory.Exists(Path.Combine(project, prefabFolder)), Is.False);
                Assert.That(File.Exists(Path.Combine(project, prefabFolder + ".meta")), Is.False);
                string folderGuid = AssetDatabase.CreateFolder("Assets", Path.GetFileName(prefabFolder));
                ownsPrefabFolder = !String.IsNullOrEmpty(folderGuid);
                Assert.That(ownsPrefabFolder, Is.True, "create fresh owned prefab folder");
                string prefabPath = prefabFolder + "/OwnedUIHighlight.prefab";
                bool saved;
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(host, prefabPath, out saved);
                Assert.That(saved && savedPrefab != null, Is.True, "save genuine inactive component prefab");
                Assert.That(host.activeSelf, Is.False);
                AssetDatabase.ForceReserializeAssets(new[] { prefabPath });
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(imported != null && !imported.activeSelf, Is.True, "import owned inactive prefab");
                Object.DestroyImmediate(host);
                host = Object.Instantiate(imported);
                Assert.That(host.activeSelf, Is.False);
                Highlight = host.GetComponent<UIHighlight>();
                Assert.That(Highlight, Is.Not.Null);
                using (var serialized = new SerializedObject(Highlight))
                {
                    serialized.Update();
                    SerializedProperty field = serialized.FindProperty("m_onShowHighlight");
                    Assert.That(field, Is.Not.Null);
                    Assert.That(field.propertyType, Is.EqualTo(SerializedPropertyType.Generic));
                    SerializedProperty calls = field.FindPropertyRelative("m_PersistentCalls.m_Calls");
                    Assert.That(calls != null && calls.isArray, Is.True);
                    Assert.That(calls.arraySize, Is.EqualTo(0));
                }
                // The unchanged listener prerequisite below still rejects null.
                // Serialized shape alone does not establish runtime allocation.
            }

            public void Dispose()
            {
                try { if (Definition != null) Object.DestroyImmediate(Definition); }
                finally
                {
                    try { if (host != null) Object.DestroyImmediate(host); }
                    finally
                    {
                        if (ownsPrefabFolder)
                        {
                            Assert.That(AssetDatabase.DeleteAsset(prefabFolder), Is.True, "remove only owned prefab folder and assets");
                            ownsPrefabFolder = false;
                        }
                    }
                }
            }
        }
    }
}
