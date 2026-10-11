using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight.UI.Binding;
using Hardlight.UI.Binding.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class OriginalStringFormatterVector2SerializationTests
{
    [Test]
    public void InactiveOriginalFormatterPreservesVector2PayloadThroughEditorJson()
    {
        Assert.IsFalse(Application.isPlaying);
        List<GameObject> owned = new List<GameObject>();
        List<Exception> cleanup = new List<Exception>();
        Exception primary = null;
        try
        {
            StringFormatter source = CreateInactive(owned, "Owned Vector2 source");
            StringFormatter destination = CreateInactive(owned, "Owned Vector2 destination");
            Vector2 nodePosition = new Vector2(-12.5f, 3.25f);
            Vector2 pan = new Vector2(18.75f, -2.5f);
            using (SerializedObject setup = new SerializedObject(source))
            {
                setup.Update();
                RequireVector2(setup, "nodePosition", Vector2.zero);
                RequireVector2(setup, "pan", Vector2.zero);
                RequireNotSerialized(setup);
                setup.FindProperty("nodePosition").vector2Value = nodePosition;
                setup.FindProperty("pan").vector2Value = pan;
                Assert.IsTrue(setup.ApplyModifiedPropertiesWithoutUndo());
                setup.Update();
                RequireVector2(setup, "nodePosition", nodePosition);
                RequireVector2(setup, "pan", pan);
                RequireNotSerialized(setup);
            }
            using (SerializedObject before = new SerializedObject(destination))
            {
                before.Update();
                RequireVector2(before, "nodePosition", Vector2.zero);
                RequireVector2(before, "pan", Vector2.zero);
                RequireNotSerialized(before);
            }

            string json = EditorJsonUtility.ToJson(source);
            StringAssert.Contains("\"nodePosition\"", json);
            StringAssert.Contains("\"pan\"", json);
            StringAssert.DoesNotContain("\"dynamicNodePosition\"", json);
            StringAssert.DoesNotContain("\"dynamicPan\"", json);
            EditorJsonUtility.FromJsonOverwrite(json, destination);
            using (SerializedObject observed = new SerializedObject(destination))
            {
                observed.Update();
                RequireVector2(observed, "nodePosition", nodePosition);
                RequireVector2(observed, "pan", pan);
                RequireNotSerialized(observed);
            }
            // Observe the genuine original deserialization callback's copies.
            // These exact declared private fields are read only, never written;
            // the callback is entered by Unity deserialization, not invoked here.
            Assert.AreEqual(nodePosition, ReadOriginalDynamic(destination, "dynamicNodePosition"));
            Assert.AreEqual(pan, ReadOriginalDynamic(destination, "dynamicPan"));
            using (SerializedObject unchanged = new SerializedObject(source))
            {
                unchanged.Update();
                RequireVector2(unchanged, "nodePosition", nodePosition);
                RequireVector2(unchanged, "pan", pan);
            }
            foreach (GameObject value in owned)
            {
                Assert.IsFalse(value.activeSelf);
                Assert.IsFalse(value.activeInHierarchy);
                Assert.IsFalse(value.GetComponent<StringFormatter>().isActiveAndEnabled);
            }
            Assert.IsFalse(Application.isPlaying);
        }
        catch (Exception error)
        {
            primary = error;
        }
        finally
        {
            for (int i = owned.Count - 1; i >= 0; --i)
            {
                try
                {
                    if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
                }
                catch (Exception error)
                {
                    cleanup.Add(error);
                }
            }
        }
        if (primary != null && cleanup.Count != 0)
        {
            cleanup.Insert(0, primary);
            throw new AggregateException("Owned Vector2 path and cleanup both failed", cleanup);
        }
        if (primary != null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup.Count != 0) throw new AggregateException("Owned Vector2 cleanup failed", cleanup);
    }

    private static StringFormatter CreateInactive(List<GameObject> owned, string name)
    {
        GameObject value = new GameObject(name);
        owned.Add(value);
        value.SetActive(false);
        StringFormatter formatter = value.AddComponent<StringFormatter>();
        Assert.IsNotNull(formatter);
        Assert.IsFalse(formatter.isActiveAndEnabled);
        Assert.IsFalse(value.activeInHierarchy);
        Assert.IsNotNull(formatter.Output);
        return formatter;
    }

    private static void RequireVector2(SerializedObject observed, string name, Vector2 expected)
    {
        SerializedProperty property = observed.FindProperty(name);
        Assert.IsNotNull(property);
        Assert.AreEqual(SerializedPropertyType.Vector2, property.propertyType);
        Assert.AreEqual(expected, property.vector2Value);
        SerializedProperty x = property.FindPropertyRelative("x");
        SerializedProperty y = property.FindPropertyRelative("y");
        Assert.IsNotNull(x);
        Assert.IsNotNull(y);
        Assert.AreEqual(SerializedPropertyType.Float, x.propertyType);
        Assert.AreEqual(SerializedPropertyType.Float, y.propertyType);
        Assert.AreEqual(expected.x, x.floatValue);
        Assert.AreEqual(expected.y, y.floatValue);
    }

    private static void RequireNotSerialized(SerializedObject observed)
    {
        Assert.IsNull(observed.FindProperty("dynamicNodePosition"));
        Assert.IsNull(observed.FindProperty("dynamicPan"));
    }

    private static Vector2 ReadOriginalDynamic(StringFormatter owner, string name)
    {
        FieldInfo field = typeof(BinderNode).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(field);
        Assert.AreEqual(typeof(Vector2), field.FieldType);
        Assert.IsFalse(field.IsStatic);
        Assert.AreEqual(typeof(BinderNode), field.DeclaringType);
        return (Vector2)field.GetValue(owner);
    }
}
