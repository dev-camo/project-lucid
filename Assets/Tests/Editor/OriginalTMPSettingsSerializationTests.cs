using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

// Profiles the pinned com.unity.textmeshpro 3.2.0-pre.4 package (Packages/manifest.json)
// that the original player ships. A version-drift tripwire, not game-code evidence.
public sealed class OriginalTMPSettingsSerializationTests
{
    private const BindingFlags OwnInstance = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags OwnStatic = BindingFlags.Static | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Test]
    public void OwnedTMPSettingsExposesBuiltinVector2FieldsAndZeroChildren()
    {
        ObserveOwnedSettings(serialized =>
        {
            AssertVector2(serialized, "m_defaultTextMeshProTextContainerSize");
            AssertVector2(serialized, "m_defaultTextMeshProUITextContainerSize");
            Assert.That(serialized.hasModifiedProperties, Is.False);
        });
    }

    [Test]
    public void OwnedTMPSettingsOmitsNonSerializableLineBreakingTableBeforeHashSetTraversal()
    {
        ObserveOwnedSettings(serialized =>
        {
            // The real field still exists in the managed declaration. This asserts
            // serializer exclusion of its plain class, not deletion or a HashSet rewrite.
            Assert.That(serialized.FindProperty("m_linebreakingRules"), Is.Null);
            Assert.That(serialized.FindProperty("m_linebreakingRules.leadingCharacters"), Is.Null);
            Assert.That(serialized.FindProperty("m_linebreakingRules.followingCharacters"), Is.Null);
            Assert.That(serialized.hasModifiedProperties, Is.False);
        });
    }

    private static void AssertVector2(SerializedObject serialized, string name)
    {
        SerializedProperty vector = serialized.FindProperty(name);
        Assert.That(vector, Is.Not.Null, name);
        Assert.That(vector.propertyPath, Is.EqualTo(name));
        Assert.That(vector.propertyType, Is.EqualTo(SerializedPropertyType.Vector2));
        SerializedProperty x = vector.FindPropertyRelative("x");
        SerializedProperty y = vector.FindPropertyRelative("y");
        Assert.That(x, Is.Not.Null, name + ".x");
        Assert.That(y, Is.Not.Null, name + ".y");
        Assert.That(x.propertyPath, Is.EqualTo(name + ".x"));
        Assert.That(y.propertyPath, Is.EqualTo(name + ".y"));
        Assert.That(x.propertyType, Is.EqualTo(SerializedPropertyType.Float));
        Assert.That(y.propertyType, Is.EqualTo(SerializedPropertyType.Float));
        Assert.That(x.floatValue, Is.EqualTo(0f));
        Assert.That(y.floatValue, Is.EqualTo(0f));
        Assert.That(vector.vector2Value, Is.EqualTo(Vector2.zero));
    }

    private static FieldInfo ExactBoundary()
    {
        Type settings = typeof(TMP_Settings);
        Assert.That(settings.Assembly.GetName().Name, Is.EqualTo("Unity.TextMeshPro"));
        Assert.That(settings.BaseType, Is.EqualTo(typeof(ScriptableObject)));
        Assert.That(settings.TypeInitializer, Is.Null);
        Assert.That(settings.GetFields(OwnStatic).Select(field => field.Name).ToArray(),
            Is.EqualTo(new[] { "s_Instance" }));
        FieldInfo singleton = settings.GetField("s_Instance", OwnStatic);
        Assert.That(singleton, Is.Not.Null);
        Assert.That(singleton.FieldType, Is.EqualTo(settings));
        Assert.That(singleton.Attributes, Is.EqualTo(FieldAttributes.Private | FieldAttributes.Static));

        // These checks observe declarations, never invoke lifecycle callbacks.
        string[] callbacks = { "Awake", "Reset", "OnEnable", "OnDisable", "OnDestroy", "OnValidate",
            "OnBeforeSerialize", "OnAfterDeserialize" };
        foreach (string callback in callbacks)
            Assert.That(settings.GetMethods(OwnInstance | OwnStatic).Any(method => method.Name == callback),
                Is.False, callback);
        Assert.That(typeof(ISerializationCallbackReceiver).IsAssignableFrom(settings), Is.False);

        RequireSerializedField(settings, "m_defaultTextMeshProTextContainerSize", typeof(Vector2));
        RequireSerializedField(settings, "m_defaultTextMeshProUITextContainerSize", typeof(Vector2));
        Type line = typeof(TMP_Settings.LineBreakingTable);
        RequireSerializedField(settings, "m_linebreakingRules", line);
        Assert.That(line.IsSerializable, Is.False);
        Assert.That(line.BaseType, Is.EqualTo(typeof(object)));
        Assert.That(line.GetCustomAttributesData().Count, Is.EqualTo(0));
        FieldInfo[] fields = line.GetFields(OwnInstance);
        Assert.That(fields.Select(field => field.Name).ToArray(),
            Is.EqualTo(new[] { "leadingCharacters", "followingCharacters" }));
        foreach (FieldInfo field in fields)
        {
            Assert.That(field.Attributes, Is.EqualTo(FieldAttributes.Public));
            Assert.That(field.FieldType, Is.EqualTo(typeof(System.Collections.Generic.HashSet<uint>)));
            Assert.That(field.FieldType.GetGenericTypeDefinition().Assembly.GetName().Name,
                Is.EqualTo("System.Core"));
            Assert.That(field.FieldType.GetGenericArguments(), Is.EqualTo(new[] { typeof(uint) }));
            Assert.That(field.GetCustomAttributesData().Count, Is.EqualTo(0));
        }
        return singleton;
    }

    private static void RequireSerializedField(Type owner, string name, Type type)
    {
        FieldInfo field = owner.GetField(name, OwnInstance);
        Assert.That(field, Is.Not.Null, name);
        Assert.That(field.FieldType, Is.EqualTo(type));
        Assert.That(field.Attributes, Is.EqualTo(FieldAttributes.Private));
        var attributes = field.GetCustomAttributesData();
        Assert.That(attributes.Count, Is.EqualTo(1), name);
        CustomAttributeData attribute = attributes[0];
        Assert.That(attribute.AttributeType, Is.EqualTo(typeof(SerializeField)));
        Assert.That(attribute.Constructor, Is.EqualTo(typeof(SerializeField).GetConstructor(Type.EmptyTypes)));
        Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(0));
        Assert.That(attribute.NamedArguments.Count, Is.EqualTo(0));
    }

    private static void ObserveOwnedSettings(Action<SerializedObject> observe)
    {
        FieldInfo singleton = ExactBoundary();
        object originalSingleton = singleton.GetValue(null);
        TMP_Settings owned = null;
        SerializedObject serialized = null;
        Exception observationFailure = null;
        Exception cleanupFailure = null;
        try
        {
            // Normal Unity construction only. No TMP static getter, Resources load,
            // asset creation, singleton assignment or component lifecycle is used.
            owned = ScriptableObject.CreateInstance<TMP_Settings>();
            Assert.That(owned == null, Is.False);
            Assert.That(ReferenceEquals(singleton.GetValue(null), originalSingleton), Is.True);
            serialized = new SerializedObject(owned);
            Assert.That(ReferenceEquals(serialized.targetObject, owned), Is.True);
            serialized.Update();
            observe(serialized);
            Assert.That(ReferenceEquals(singleton.GetValue(null), originalSingleton), Is.True);
        }
        catch (Exception error)
        {
            observationFailure = error;
        }
        finally
        {
            // Each cleanup gets an independent attempt, including if construction,
            // observation or native wrapper disposal fails. Foreign settings are untouched.
            try
            {
                if (serialized != null)
                    serialized.Dispose();
            }
            catch (Exception error)
            {
                cleanupFailure = error;
            }
            try
            {
                if (!ReferenceEquals(owned, null))
                    UnityEngine.Object.DestroyImmediate(owned);
            }
            catch (Exception error)
            {
                cleanupFailure = cleanupFailure == null ? error : new AggregateException(cleanupFailure, error);
            }
            try
            {
                Assert.That(ReferenceEquals(singleton.GetValue(null), originalSingleton), Is.True,
                    "The genuine TMP_Settings singleton reference changed during owned construction/destruction.");
            }
            catch (Exception error)
            {
                cleanupFailure = cleanupFailure == null ? error : new AggregateException(cleanupFailure, error);
            }
        }
        if (observationFailure != null && cleanupFailure != null)
            throw new AggregateException("Serialization observation and owned cleanup both failed.",
                observationFailure, cleanupFailure);
        if (observationFailure != null)
            ExceptionDispatchInfo.Capture(observationFailure).Throw();
        if (cleanupFailure != null)
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
    }
}
