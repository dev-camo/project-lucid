using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using Hardlight.UI.Binding;
using HardlightProject;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;

// Test-owned carrier only. It supplies serialization fields, never runtime systems,
// lifecycle callbacks, App/Start/font-loader instances, fonts or readiness.
public sealed class ProjectLucidGenericSerializationCarrier : ScriptableObject
{
    [SerializeField] private UnityEvent<bool> m_onInteractableValueChanged = new UnityEvent<bool>();
    [SerializeField] private List<AssetReferenceT<FontAssetAndFallbacks>> m_fontAssetsAndFallBackCollectionRefs =
        new List<AssetReferenceT<FontAssetAndFallbacks>>();
    [SerializeField] private List<AssetReferenceT<TMP_FontAsset>> m_fontReferences =
        new List<AssetReferenceT<TMP_FontAsset>>();
    [SerializeField] private ProjectLucidGenericSerializationSlot<int> m_closedData =
        new ProjectLucidGenericSerializationSlot<int>();
    [SerializeField] private Bindable<bool> m_nonSerializableBindable = new Bindable<bool>();
    [SerializeField] private Dictionary<string, int> m_dictionary = new Dictionary<string, int>();
    [SerializeField] private UIWidgetButtonStart m_bonusZoneButton;
    [SerializeField] private UIWidgetButtonStart m_challengesButton;
    [SerializeField] private UnityEngine.Object m_ownedObject;

    // These are read-only observations of owned values, not original private-field accessors.
    public UnityEvent<bool> Event => m_onInteractableValueChanged;
    public IReadOnlyList<AssetReferenceT<FontAssetAndFallbacks>> CollectionReferences => m_fontAssetsAndFallBackCollectionRefs;
    public IReadOnlyList<AssetReferenceT<TMP_FontAsset>> FontReferences => m_fontReferences;
    public ProjectLucidGenericSerializationSlot<int> ClosedData => m_closedData;
}

[Serializable]
public sealed class ProjectLucidGenericSerializationSlot<T>
{
    public T value;
    public List<T> values = new List<T>();
}

public sealed class OriginalGenericSerializationFrontierTests
{
    private const BindingFlags Own = BindingFlags.DeclaredOnly | BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly string[] ReferenceFields =
        { "m_AssetGUID", "m_SubObjectName", "m_SubObjectType", "m_EditorAssetChanged" };
    private static readonly string[] CallFields =
        { "m_Target", "m_TargetAssemblyTypeName", "m_MethodName", "m_Mode", "m_Arguments", "m_CallState" };
    private static readonly string[] ArgumentFields =
        { "m_ObjectArgument", "m_ObjectArgumentAssemblyTypeName", "m_IntArgument", "m_FloatArgument", "m_StringArgument", "m_BoolArgument" };

    // Shipped field rows: HLModernUI.Runtime UIInteractable 0x02000021 holds 0x04000071..74 with
    // m_onInteractableValueChanged second (0x04000072); Game.Runtime FontAssetLanguageSetter
    // 0x02000958 holds 0x04002681..86 with m_fontAssetsAndFallBackCollectionRefs first.
    [Test]
    public void OriginalGenericFieldSignaturesAndEditorProjectionRemainExplicit()
    {
        RequireField(typeof(UIInteractable), "m_onInteractableValueChanged", typeof(UnityEvent<bool>));
        RequireField(typeof(FontAssetLanguageSetter), "m_fontAssetsAndFallBackCollectionRefs",
            typeof(List<AssetReferenceT<FontAssetAndFallbacks>>));
        RequireOriginalFieldOrder(typeof(UIInteractable),
            "m_interactable", "m_onInteractableValueChanged", "m_canvasGroupAllowInteraction", "m_canvasGroupCache");
        RequireOriginalFieldOrder(typeof(FontAssetLanguageSetter),
            "m_fontAssetsAndFallBackCollectionRefs", "m_fontAssetsAndFallBackCollection", "m_asyncOperationHandles",
            "m_appRef", "m_assetManager", "m_isRespondingToLanguageChange");
        RequireField(typeof(ProjectLucidGenericSerializationCarrier), "m_onInteractableValueChanged", typeof(UnityEvent<bool>));
        RequireField(typeof(ProjectLucidGenericSerializationCarrier), "m_fontAssetsAndFallBackCollectionRefs",
            typeof(List<AssetReferenceT<FontAssetAndFallbacks>>));
        Assert.That(typeof(UnityEvent<>).Assembly.GetName().Name, Is.EqualTo("UnityEngine.CoreModule"));
        Assert.That(typeof(UnityEvent<>).IsSerializable, Is.True);
        Assert.That(typeof(UnityEvent<>).GetGenericArguments()[0].GetGenericParameterConstraints(), Is.Empty);
        Assert.That(typeof(AssetReferenceT<>).Assembly.GetName().Name, Is.EqualTo("Unity.Addressables"));
        Assert.That(typeof(AssetReferenceT<>).IsSerializable, Is.True);
        Assert.That(typeof(AssetReferenceT<>).GetGenericArguments()[0].GetGenericParameterConstraints(),
            Is.EqualTo(new[] { typeof(UnityEngine.Object) }));
        Assert.That(typeof(AssetReferenceT<FontAssetAndFallbacks>).BaseType, Is.EqualTo(typeof(AssetReference)));
        // The runtime shipping contract has only the first three serialized fields.
        // This Editor observation explicitly retains its fourth conditional field.
        Assert.That(typeof(AssetReference).GetFields(Own).Where(IsSerializationCandidate)
            .OrderBy(field => field.MetadataToken).Select(field => field.Name).ToArray(), Is.EqualTo(ReferenceFields));
        Assert.That(typeof(Bindable<bool>).IsSerializable, Is.False);
        Assert.That(typeof(ProjectLucidGenericSerializationCarrier).GetMethods(Own).Any(method =>
            new[] { "Awake", "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "OnBeforeSerialize", "OnAfterDeserialize" }
                .Contains(method.Name)), Is.False);
        Assert.That(typeof(ISerializationCallbackReceiver).IsAssignableFrom(typeof(ProjectLucidGenericSerializationCarrier)), Is.False);
    }

    [Test]
    public void OwnedClosedUnityEventExposesExactEmptyPersistentCallTree()
    {
        Observe((owned, serialized, lease) =>
        {
            Assert.That(owned.Event.GetPersistentEventCount(), Is.EqualTo(0));
            SerializedProperty field = Property(serialized, "m_onInteractableValueChanged", SerializedPropertyType.Generic);
            Assert.That(Children(field), Is.EqualTo(new[] { "m_PersistentCalls" }));
            SerializedProperty group = Relative(field, "m_PersistentCalls", SerializedPropertyType.Generic);
            Assert.That(Children(group), Is.EqualTo(new[] { "m_Calls" }));
            SerializedProperty calls = group.FindPropertyRelative("m_Calls");
            Assert.That(calls, Is.Not.Null); Assert.That(calls.isArray, Is.True);
            Assert.That(calls.arraySize, Is.EqualTo(0));
            Assert.That(serialized.hasModifiedProperties, Is.False);
        });
    }

    [Test]
    public void OwnedPersistentCallPayloadRoundTripsWithoutDispatchOrTargetActivation()
    {
        Observe((owned, serialized, lease) =>
        {
            var target = lease.Own(new GameObject("owned generic serialization target"));
            target.SetActive(false);
            var argument = lease.Own(new GameObject("owned generic serialization argument"));
            argument.SetActive(false);
            SerializedProperty calls = serialized.FindProperty("m_onInteractableValueChanged.m_PersistentCalls.m_Calls");
            Assert.That(calls, Is.Not.Null); Assert.That(calls.isArray, Is.True);
            calls.arraySize = 1;
            SerializedProperty call = calls.GetArrayElementAtIndex(0);
            Assert.That(Children(call), Is.EqualTo(CallFields));
            Relative(call, "m_Target", SerializedPropertyType.ObjectReference).objectReferenceValue = target;
            Relative(call, "m_TargetAssemblyTypeName", SerializedPropertyType.String).stringValue = "UnityEngine.GameObject, UnityEngine.CoreModule";
            Relative(call, "m_MethodName", SerializedPropertyType.String).stringValue = "SetActive";
            Relative(call, "m_Mode", SerializedPropertyType.Enum).intValue = (int)PersistentListenerMode.EventDefined;
            Relative(call, "m_CallState", SerializedPropertyType.Enum).intValue = (int)UnityEventCallState.Off;
            SerializedProperty args = Relative(call, "m_Arguments", SerializedPropertyType.Generic);
            Assert.That(Children(args), Is.EqualTo(ArgumentFields));
            Relative(args, "m_ObjectArgument", SerializedPropertyType.ObjectReference).objectReferenceValue = argument;
            Relative(args, "m_ObjectArgumentAssemblyTypeName", SerializedPropertyType.String).stringValue = "UnityEngine.GameObject, UnityEngine.CoreModule";
            Relative(args, "m_IntArgument", SerializedPropertyType.Integer).intValue = -19;
            Relative(args, "m_FloatArgument", SerializedPropertyType.Float).floatValue = 3.25f;
            Relative(args, "m_StringArgument", SerializedPropertyType.String).stringValue = "owned payload";
            Relative(args, "m_BoolArgument", SerializedPropertyType.Boolean).boolValue = true;
            Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
            serialized.Update();
            Assert.That(owned.Event.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(owned.Event.GetPersistentTarget(0), Is.SameAs(target));
            Assert.That(owned.Event.GetPersistentMethodName(0), Is.EqualTo("SetActive"));
            Assert.That(owned.Event.GetPersistentListenerState(0), Is.EqualTo(UnityEventCallState.Off));
            Assert.That(Relative(args, "m_ObjectArgument", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.SameAs(argument));
            // Editor JSON is an asset-reference projection. These owned transient
            // objects have no asset file, so the installed Editor writes null pointers.
            var editorCopy = lease.Own(ScriptableObject.CreateInstance<ProjectLucidGenericSerializationCarrier>());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(owned), editorCopy);
            using (var projected = new SerializedObject(editorCopy))
            {
                projected.Update();
                SerializedProperty projectedCalls = projected.FindProperty("m_onInteractableValueChanged.m_PersistentCalls.m_Calls");
                Assert.That(projectedCalls, Is.Not.Null); Assert.That(projectedCalls.isArray, Is.True);
                Assert.That(projectedCalls.arraySize, Is.EqualTo(1));
                SerializedProperty row = projectedCalls.GetArrayElementAtIndex(0);
                Assert.That(Children(row), Is.EqualTo(CallFields));
                Assert.That(Relative(row, "m_Target", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.Null);
                Assert.That(Relative(row, "m_MethodName", SerializedPropertyType.String).stringValue, Is.EqualTo("SetActive"));
                // Real PersistentCall and ArgumentCache serialization callbacks
                // retain the exact UnityEngine type-forwarder assembly spelling.
                Assert.That(Relative(row, "m_TargetAssemblyTypeName", SerializedPropertyType.String).stringValue, Is.EqualTo("UnityEngine.GameObject, UnityEngine"));
                Assert.That(Relative(row, "m_Mode", SerializedPropertyType.Enum).intValue, Is.EqualTo((int)PersistentListenerMode.EventDefined));
                Assert.That(Relative(row, "m_CallState", SerializedPropertyType.Enum).intValue, Is.EqualTo((int)UnityEventCallState.Off));
                SerializedProperty projectedArgs = Relative(row, "m_Arguments", SerializedPropertyType.Generic);
                Assert.That(Children(projectedArgs), Is.EqualTo(ArgumentFields));
                Assert.That(Relative(projectedArgs, "m_ObjectArgument", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.Null);
                Assert.That(Relative(projectedArgs, "m_ObjectArgumentAssemblyTypeName", SerializedPropertyType.String).stringValue, Is.EqualTo("UnityEngine.GameObject, UnityEngine"));
                Assert.That(Relative(projectedArgs, "m_IntArgument", SerializedPropertyType.Integer).intValue, Is.EqualTo(-19));
                Assert.That(Relative(projectedArgs, "m_FloatArgument", SerializedPropertyType.Float).floatValue, Is.EqualTo(3.25f));
                Assert.That(Relative(projectedArgs, "m_StringArgument", SerializedPropertyType.String).stringValue, Is.EqualTo("owned payload"));
                Assert.That(Relative(projectedArgs, "m_BoolArgument", SerializedPropertyType.Boolean).boolValue, Is.True);
            }
            Assert.That(editorCopy.Event.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(editorCopy.Event.GetPersistentTarget(0), Is.Null);
            Assert.That(editorCopy.Event.GetPersistentMethodName(0), Is.EqualTo("SetActive"));
            Assert.That(editorCopy.Event.GetPersistentListenerState(0), Is.EqualTo(UnityEventCallState.Off));
            serialized.Update();
            Assert.That(owned.Event.GetPersistentTarget(0), Is.SameAs(target));
            Assert.That(Relative(serialized.FindProperty("m_onInteractableValueChanged.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0).FindPropertyRelative("m_Arguments"), "m_ObjectArgument", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.SameAs(argument));
            // The same-session serializer preserves InstanceID references. Keep
            // all original strict pointer identity and payload assertions below.
            var copy = lease.Own(ScriptableObject.CreateInstance<ProjectLucidGenericSerializationCarrier>());
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(owned), copy);
            using (var copied = new SerializedObject(copy))
            {
                copied.Update();
                SerializedProperty row = copied.FindProperty("m_onInteractableValueChanged.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(0);
                Assert.That(Children(row), Is.EqualTo(CallFields));
                Assert.That(Relative(row, "m_Target", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.SameAs(target));
                Assert.That(Relative(row, "m_MethodName", SerializedPropertyType.String).stringValue, Is.EqualTo("SetActive"));
                Assert.That(Relative(row, "m_TargetAssemblyTypeName", SerializedPropertyType.String).stringValue, Is.EqualTo("UnityEngine.GameObject, UnityEngine"));
                Assert.That(Relative(row, "m_Mode", SerializedPropertyType.Enum).intValue, Is.EqualTo((int)PersistentListenerMode.EventDefined));
                Assert.That(Relative(row, "m_CallState", SerializedPropertyType.Enum).intValue, Is.EqualTo((int)UnityEventCallState.Off));
                SerializedProperty copiedArgs = Relative(row, "m_Arguments", SerializedPropertyType.Generic);
                Assert.That(Children(copiedArgs), Is.EqualTo(ArgumentFields));
                Assert.That(Relative(copiedArgs, "m_ObjectArgument", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.SameAs(argument));
                Assert.That(Relative(copiedArgs, "m_ObjectArgumentAssemblyTypeName", SerializedPropertyType.String).stringValue, Is.EqualTo("UnityEngine.GameObject, UnityEngine"));
                Assert.That(Relative(copiedArgs, "m_IntArgument", SerializedPropertyType.Integer).intValue, Is.EqualTo(-19));
                Assert.That(Relative(copiedArgs, "m_FloatArgument", SerializedPropertyType.Float).floatValue, Is.EqualTo(3.25f));
                Assert.That(Relative(copiedArgs, "m_StringArgument", SerializedPropertyType.String).stringValue, Is.EqualTo("owned payload"));
                Assert.That(Relative(copiedArgs, "m_BoolArgument", SerializedPropertyType.Boolean).boolValue, Is.True);
            }
            Assert.That(target.activeSelf, Is.False); Assert.That(argument.activeSelf, Is.False);
            // No Invoke, listener registration or original UIInteractable setter is called.
        });
    }

    [Test]
    public void OwnedTypedAddressableListsPreserveGuidOrderAndEditorChildrenWithoutLoads()
    {
        Observe((owned, serialized, lease) =>
        {
            string[] guids = { "c6bc5f88793a44caaab2e67bd2a45af1", "00411bdfebdc243b5b97ae0f721d16b0",
                "824c341753e3d4732af8fb81d6d0b65a", "36555d6dfb0164ae2982ca7fc963c182" };
            SerializedProperty list = serialized.FindProperty("m_fontAssetsAndFallBackCollectionRefs");
            Assert.That(list, Is.Not.Null); Assert.That(list.isArray, Is.True); Assert.That(list.arraySize, Is.EqualTo(0));
            list.arraySize = guids.Length;
            for (int i = 0; i < guids.Length; i++) WriteReference(list.GetArrayElementAtIndex(i), guids[i], i == 1 ? "owned subobject" : "");
            SerializedProperty fonts = serialized.FindProperty("m_fontReferences");
            Assert.That(fonts, Is.Not.Null); Assert.That(fonts.isArray, Is.True); Assert.That(fonts.arraySize, Is.EqualTo(0));
            fonts.arraySize = 2;
            WriteReference(fonts.GetArrayElementAtIndex(0), guids[0], "");
            WriteReference(fonts.GetArrayElementAtIndex(1), guids[0], "");
            Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True); serialized.Update();
            Assert.That(owned.CollectionReferences.Select(value => value.AssetGUID).ToArray(), Is.EqualTo(guids));
            Assert.That(owned.CollectionReferences[1].SubObjectName, Is.EqualTo("owned subobject"));
            Assert.That(owned.FontReferences.Select(value => value.AssetGUID).ToArray(), Is.EqualTo(new[] { guids[0], guids[0] }));
            var copy = lease.Own(ScriptableObject.CreateInstance<ProjectLucidGenericSerializationCarrier>());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(owned), copy);
            using (var copied = new SerializedObject(copy))
            {
                copied.Update();
                Assert.That(copy.CollectionReferences.Select(value => value.AssetGUID).ToArray(), Is.EqualTo(guids));
                Assert.That(copy.FontReferences.Select(value => value.AssetGUID).ToArray(), Is.EqualTo(new[] { guids[0], guids[0] }));
                for (int i = 0; i < guids.Length; i++) AssertReference(copied.FindProperty("m_fontAssetsAndFallBackCollectionRefs").GetArrayElementAtIndex(i), guids[i], i == 1 ? "owned subobject" : "");
            }
            // GUID values are serialization payloads only; no Asset/editorAsset/RuntimeKey/load/release/ValidateAsset path is used.
        });
    }

    [Test]
    public void OwnedGenericDataSubstitutesIntWhileNonSerializableBindableAndDictionaryStayAbsent()
    {
        Observe((owned, serialized, lease) =>
        {
            SerializedProperty data = Property(serialized, "m_closedData", SerializedPropertyType.Generic);
            Assert.That(Children(data), Is.EqualTo(new[] { "value", "values" }));
            SerializedProperty value = Relative(data, "value", SerializedPropertyType.Integer);
            Assert.That(value.intValue, Is.EqualTo(0)); value.intValue = -23;
            SerializedProperty values = data.FindPropertyRelative("values");
            Assert.That(values, Is.Not.Null); Assert.That(values.isArray, Is.True); Assert.That(values.arraySize, Is.EqualTo(0));
            values.arraySize = 2; values.GetArrayElementAtIndex(0).intValue = 17; values.GetArrayElementAtIndex(1).intValue = -9;
            Assert.That(serialized.FindProperty("m_nonSerializableBindable"), Is.Null);
            Assert.That(serialized.FindProperty("m_dictionary"), Is.Null);
            Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True); serialized.Update();
            Assert.That(owned.ClosedData.value, Is.EqualTo(-23)); Assert.That(owned.ClosedData.values, Is.EqualTo(new[] { 17, -9 }));
            var copy = lease.Own(ScriptableObject.CreateInstance<ProjectLucidGenericSerializationCarrier>());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(owned), copy);
            Assert.That(copy.ClosedData.value, Is.EqualTo(-23)); Assert.That(copy.ClosedData.values, Is.EqualTo(new[] { 17, -9 }));
        });
    }

    // Game.Runtime UIContainerStart 0x02000947 field rows 0x0400262d..42; the two buttons are
    // rows 0x0400262f and 0x04002630, between m_mediaAlertBadge and m_challengesAnalyticsEvent.
    [Test]
    public void OwnedReferenceOnlyCarrierUsesPPtrPropertiesWithoutConstructingOriginalButtons()
    {
        RequireField(typeof(UIContainerStart), "m_bonusZoneButton", typeof(UIWidgetButtonStart));
        RequireField(typeof(UIContainerStart), "m_challengesButton", typeof(UIWidgetButtonStart));
        RequireOriginalFieldOrder(typeof(UIContainerStart),
            "m_sweetDreamsCategory", "m_mediaAlertBadge", "m_bonusZoneButton", "m_challengesButton",
            "m_challengesAnalyticsEvent", "CanContinue", "CanLoad", "FirstTime", "BonusZoneUnlocked",
            "ChallengesProgressAdventure", "RewardsFormatString", "OrbRewardsCollected", "OrbRewardsTotal",
            "MoonRewardsCollected", "MoonRewardsTotal", "m_saveManagerRef", "m_app", "m_progressionManager",
            "m_transitionManager", "m_levelManager", "m_ornamentManager", "m_musicTrackManager");
        Observe((owned, serialized, lease) =>
        {
            foreach (string name in new[] { "m_bonusZoneButton", "m_challengesButton" })
            {
                SerializedProperty pointer = Property(serialized, name, SerializedPropertyType.ObjectReference);
                Assert.That(pointer.objectReferenceValue, Is.Null);
                Assert.That(pointer.FindPropertyRelative("m_interactable"), Is.Null);
                Assert.That(pointer.FindPropertyRelative("m_onInteractableValueChanged"), Is.Null);
            }
            var referenced = lease.Own(ScriptableObject.CreateInstance<ProjectLucidGenericSerializationCarrier>());
            Property(serialized, "m_ownedObject", SerializedPropertyType.ObjectReference).objectReferenceValue = referenced;
            Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True); serialized.Update();
            Assert.That(Property(serialized, "m_ownedObject", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.SameAs(referenced));
            // Editor asset-reference JSON cannot retain this unsaved object.
            // Observe that projection independently from the in-session control.
            var editorCopy = lease.Own(ScriptableObject.CreateInstance<ProjectLucidGenericSerializationCarrier>());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(owned), editorCopy);
            using (var projected = new SerializedObject(editorCopy))
            {
                projected.Update();
                Assert.That(Property(projected, "m_ownedObject", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.Null);
                Assert.That(Property(projected, "m_bonusZoneButton", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.Null);
                Assert.That(Property(projected, "m_challengesButton", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.Null);
            }
            serialized.Update();
            Assert.That(Property(serialized, "m_ownedObject", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.SameAs(referenced));
            var copy = lease.Own(ScriptableObject.CreateInstance<ProjectLucidGenericSerializationCarrier>());
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(owned), copy);
            using (var copied = new SerializedObject(copy))
            {
                copied.Update();
                Assert.That(Property(copied, "m_ownedObject", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.SameAs(referenced));
                Assert.That(Property(copied, "m_bonusZoneButton", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.Null);
                Assert.That(Property(copied, "m_challengesButton", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.Null);
            }
        });
    }

    private static void WriteReference(SerializedProperty row, string guid, string subobject)
    {
        Assert.That(Children(row), Is.EqualTo(ReferenceFields));
        Relative(row, "m_AssetGUID", SerializedPropertyType.String).stringValue = guid;
        Relative(row, "m_SubObjectName", SerializedPropertyType.String).stringValue = subobject;
        Relative(row, "m_SubObjectType", SerializedPropertyType.String).stringValue = "";
        Assert.That(Relative(row, "m_EditorAssetChanged", SerializedPropertyType.Boolean).boolValue, Is.False);
    }
    private static void AssertReference(SerializedProperty row, string guid, string subobject)
    {
        Assert.That(Children(row), Is.EqualTo(ReferenceFields));
        Assert.That(Relative(row, "m_AssetGUID", SerializedPropertyType.String).stringValue, Is.EqualTo(guid));
        Assert.That(Relative(row, "m_SubObjectName", SerializedPropertyType.String).stringValue, Is.EqualTo(subobject));
        Assert.That(Relative(row, "m_SubObjectType", SerializedPropertyType.String).stringValue, Is.EqualTo(""));
        Assert.That(Relative(row, "m_EditorAssetChanged", SerializedPropertyType.Boolean).boolValue, Is.False);
    }
    private static void RequireField(Type owner, string name, Type expected)
    {
        FieldInfo field = owner.GetField(name, Own); Assert.That(field, Is.Not.Null);
        Assert.That(field.Attributes, Is.EqualTo(FieldAttributes.Private)); Assert.That(field.FieldType, Is.EqualTo(expected));
        var attributes = field.GetCustomAttributesData(); Assert.That(attributes.Count, Is.EqualTo(1));
        Assert.That(attributes[0].AttributeType, Is.EqualTo(typeof(SerializeField)));
        Assert.That(attributes[0].ConstructorArguments.Count, Is.EqualTo(0)); Assert.That(attributes[0].NamedArguments.Count, Is.EqualTo(0));
    }
    // Complete declared instance fields in metadata-token order against the shipped field rows.
    private static void RequireOriginalFieldOrder(Type owner, params string[] names)
    {
        Assert.That(owner.GetFields(Own).OrderBy(field => field.MetadataToken).Select(field => field.Name).ToArray(),
            Is.EqualTo(names), owner.FullName);
    }
    private static bool IsSerializationCandidate(FieldInfo field) => !field.IsStatic && !field.IsLiteral && !field.IsInitOnly &&
        (field.Attributes & FieldAttributes.NotSerialized) == 0 &&
        (field.IsPublic || field.GetCustomAttributesData().Any(attribute => attribute.AttributeType == typeof(SerializeField)));
    private static SerializedProperty Property(SerializedObject serialized, string name, SerializedPropertyType type)
    {
        var result = serialized.FindProperty(name); Assert.That(result, Is.Not.Null, name);
        Assert.That(result.propertyType, Is.EqualTo(type), name); return result;
    }
    private static SerializedProperty Relative(SerializedProperty parent, string name, SerializedPropertyType type)
    {
        var result = parent.FindPropertyRelative(name); Assert.That(result, Is.Not.Null, parent.propertyPath + "." + name);
        Assert.That(result.propertyType, Is.EqualTo(type), result.propertyPath); return result;
    }
    private static string[] Children(SerializedProperty parent)
    {
        var names = new List<string>(); using (var iterator = parent.Copy()) using (var end = parent.GetEndProperty())
        {
            if (iterator.Next(true)) do
            {
                if (SerializedProperty.EqualContents(iterator, end) || iterator.depth <= parent.depth) break;
                if (iterator.depth == parent.depth + 1) names.Add(iterator.name);
            } while (iterator.Next(false));
        }
        return names.ToArray();
    }
    private sealed class Lease
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        public T Own<T>(T value) where T : UnityEngine.Object { objects.Add(value); return value; }
        public Exception DestroyAll()
        {
            Exception failure = null;
            for (int i = objects.Count - 1; i >= 0; i--) try { if (!ReferenceEquals(objects[i], null)) UnityEngine.Object.DestroyImmediate(objects[i]); }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            return failure;
        }
    }
    private static void Observe(Action<ProjectLucidGenericSerializationCarrier, SerializedObject, Lease> observe)
    {
        var lease = new Lease(); SerializedObject serialized = null; Exception primary = null, cleanup = null;
        try
        {
            var owned = lease.Own(ScriptableObject.CreateInstance<ProjectLucidGenericSerializationCarrier>());
            serialized = new SerializedObject(owned); serialized.Update(); observe(owned, serialized, lease);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            try { if (serialized != null) serialized.Dispose(); } catch (Exception error) { cleanup = error; }
            Exception destroy = lease.DestroyAll(); if (destroy != null) cleanup = cleanup == null ? destroy : new AggregateException(cleanup, destroy);
        }
        if (primary != null && cleanup != null) throw new AggregateException("Owned serialization observation and cleanup failed.", primary, cleanup);
        if (primary != null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup != null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }
}
