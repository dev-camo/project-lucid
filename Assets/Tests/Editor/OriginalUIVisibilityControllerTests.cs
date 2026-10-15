using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class OriginalUIVisibilityControllerTests
{
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance |
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void InputVisibilityRetainsCompleteAuthoredFieldsMethodsAndOrderedOptions()
    {
        Type type = typeof(UIInputTypeVisibilityController);
        Owner(type);
        Options(type, Option.NullChecks, Option.ArrayBoundsChecks);
        Fields(type, new[] {
            F("m_pointerUIOverrides", typeof(UIVisibilityGroupOverrider), 1),
            F("m_controllerUIOverrides", typeof(UIVisibilityGroupOverrider), 1),
            F("m_inputBridge", typeof(InputBridge), 1) });
        FieldInfo[] fields = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
        for (int i = 0; i < fields.Length; i++)
        {
            IList<CustomAttributeData> attributes = fields[i].GetCustomAttributesData();
            Assert.AreEqual(i < 2 ? 1 : 0, attributes.Count);
            if (i < 2) EmptyAttribute(attributes[0], typeof(SerializeField));
        }
        Method(type.GetMethod("OnEnable", Declared), "OnEnable", 129, Type.EmptyTypes, new string[0]);
        Method(type.GetMethod("OnUIRuntimeConfigurationValid", Declared), "OnUIRuntimeConfigurationValid", 129,
            new[] { typeof(UIRuntimeConfiguration) }, new[] { "uiRuntimeConfiguration" });
        Method(type.GetMethod("OnDisable", Declared), "OnDisable", 129, Type.EmptyTypes, new string[0]);
        Method(type.GetMethod("OnUpdateLastInputType", Declared), "OnUpdateLastInputType", 129,
            new[] { typeof(UIInputType) }, new[] { "lastInputType" });
        Assert.AreEqual(4, type.GetMethods(Declared).Length);
        Constructor(type);
        Assert.AreEqual(0, type.GetProperties(Declared).Length);
        Assert.AreEqual(0, type.GetEvents(Declared).Length);
        // Reflection data only: none of these lifecycle or deferred callbacks is invoked.
    }

    [Test]
    public void PlatformVisibilityRetainsCompleteAuthoredBindingAndUnconditionalStartDeclaration()
    {
        Type type = typeof(UIPlatformVisibilityController);
        Owner(type);
        Options(type, Option.ArrayBoundsChecks, Option.NullChecks);
        Fields(type, new[] { F("m_macOsUiOverrides", typeof(UIVisibilityGroupOverrider), 1) });
        IList<CustomAttributeData> attributes = type.GetField("m_macOsUiOverrides", Declared).GetCustomAttributesData();
        Assert.AreEqual(1, attributes.Count); EmptyAttribute(attributes[0], typeof(SerializeField));
        Method(type.GetMethod("Start", Declared), "Start", 129, Type.EmptyTypes, new string[0]);
        Assert.AreEqual(1, type.GetMethods(Declared).Length);
        Constructor(type);
        Assert.AreEqual(0, type.GetNestedTypes(Declared).Length);
        Assert.AreEqual(0, type.GetProperties(Declared).Length);
        Assert.AreEqual(0, type.GetEvents(Declared).Length);
        // The original native macOS Start is unconditional. Declaration reflection alone
        // cannot test its body or infer another platform's original preprocessor selection.
    }

    [Test]
    public void InputVisibilityNaturalCaptureRetainsWholeCurrentRoleWithoutQueuedCallbackExecution()
    {
        Type owner = typeof(UIInputTypeVisibilityController);
        Type[] nested = owner.GetNestedTypes(Declared);
        Assert.AreEqual(1, nested.Length);
        Type capture = nested.Single();
        Assert.AreEqual(owner, capture.DeclaringType);
        Assert.AreEqual(1048835, (int)capture.Attributes);
        Assert.AreEqual(typeof(object), capture.BaseType);
        Assert.IsTrue(capture.Name.StartsWith("<>c__DisplayClass", StringComparison.Ordinal));
        IList<CustomAttributeData> attributes = capture.GetCustomAttributesData();
        Assert.AreEqual(1, attributes.Count); EmptyAttribute(attributes[0], typeof(CompilerGeneratedAttribute));
        FieldInfo[] fields = capture.GetFields(Declared);
        CollectionAssert.AreEquivalent(new[] { "<>4__this", "uiRuntimeConfiguration" }, fields.Select(f => f.Name).ToArray());
        Field(fields.Single(f => f.Name == "<>4__this"), F("<>4__this", owner, 6));
        Field(fields.Single(f => f.Name == "uiRuntimeConfiguration"), F("uiRuntimeConfiguration", typeof(UIRuntimeConfiguration), 6));
        foreach (FieldInfo field in fields) Assert.AreEqual(0, field.GetCustomAttributesData().Count);
        MethodInfo[] methods = capture.GetMethods(Declared);
        Assert.AreEqual(1, methods.Length);
        MethodInfo callback = methods.Single();
        Assert.IsTrue(callback.Name.StartsWith("<OnUIRuntimeConfigurationValid>b__", StringComparison.Ordinal));
        Method(callback, callback.Name, 131, new[] { typeof(UIManager) }, new[] { "_" });
        Constructor(capture);
        Assert.AreEqual(0, capture.GetInterfaces().Length);
        Assert.AreEqual(0, capture.GetNestedTypes(Declared).Length);
        Assert.AreEqual(0, capture.GetProperties(Declared).Length);
        Assert.AreEqual(0, capture.GetEvents(Declared).Length);
        // Original display-class4_0 and callback b__0 are shipping identities. This case
        // discovers the current compiler's complete role without fixing its ordinal,
        // constructing a capture, invoking a callback or granting native parity.
    }

    [Test]
    public void InputVisibilityInactiveOwnedBindingsRoundTripWithoutBridgeOrSubscription()
    {
        var roots = new List<GameObject>();
        object inputAction = InputBridge.OnUpdateLastInputType;
        object shutdownAction = InputBridge.OnShutdown;
        try
        {
            UIInputTypeVisibilityController controller = Inactive<UIInputTypeVisibilityController>("Owned inactive input visibility", roots);
            UIVisibilityGroupOverrider pointer = Inactive<UIVisibilityGroupOverrider>("Owned inactive pointer overrides", roots);
            UIVisibilityGroupOverrider selection = Inactive<UIVisibilityGroupOverrider>("Owned inactive selection overrides", roots);
            PassiveOverride(pointer); PassiveOverride(selection);
            using (var serialized = new SerializedObject(controller))
            {
                serialized.Update();
                SerializedProperty pointerField = serialized.FindProperty("m_pointerUIOverrides");
                SerializedProperty selectionField = serialized.FindProperty("m_controllerUIOverrides");
                Assert.IsNotNull(pointerField); Assert.IsNotNull(selectionField);
                Assert.AreEqual(SerializedPropertyType.ObjectReference, pointerField.propertyType);
                Assert.AreEqual(SerializedPropertyType.ObjectReference, selectionField.propertyType);
                Assert.IsTrue(pointerField.objectReferenceValue == null);
                Assert.IsTrue(selectionField.objectReferenceValue == null);
                Assert.IsNull(serialized.FindProperty("m_inputBridge")); // Original private field is not serialized.
                pointerField.objectReferenceValue = pointer;
                selectionField.objectReferenceValue = selection;
                Assert.IsTrue(serialized.ApplyModifiedPropertiesWithoutUndo());
                serialized.Update();
                Assert.AreSame(pointer, serialized.FindProperty("m_pointerUIOverrides").objectReferenceValue);
                Assert.AreSame(selection, serialized.FindProperty("m_controllerUIOverrides").objectReferenceValue);
            }
            Assert.AreSame(pointer, typeof(UIInputTypeVisibilityController).GetField("m_pointerUIOverrides", Declared).GetValue(controller));
            Assert.AreSame(selection, typeof(UIInputTypeVisibilityController).GetField("m_controllerUIOverrides", Declared).GetValue(controller));
            InputBridge bridge = (InputBridge)typeof(UIInputTypeVisibilityController).GetField("m_inputBridge", Declared).GetValue(controller);
            Assert.IsTrue(bridge == null); // Unity null semantics; no assumption about an Editor wrapper's CLR identity.
            foreach (GameObject root in roots) Assert.IsFalse(root.activeInHierarchy);
            PassiveOverride(pointer); PassiveOverride(selection);
            Assert.AreSame(inputAction, InputBridge.OnUpdateLastInputType);
            Assert.AreSame(shutdownAction, InputBridge.OnShutdown);
        }
        finally
        {
            try { DestroyAll(roots, roots.Count - 1); }
            finally
            {
                Assert.AreSame(inputAction, InputBridge.OnUpdateLastInputType);
                Assert.AreSame(shutdownAction, InputBridge.OnShutdown);
            }
        }
    }

    [Test]
    public void PlatformVisibilityInactiveOwnedBindingRoundTripsWithoutActivatingOverrides()
    {
        var roots = new List<GameObject>();
        object inputAction = InputBridge.OnUpdateLastInputType;
        object shutdownAction = InputBridge.OnShutdown;
        try
        {
            UIPlatformVisibilityController controller = Inactive<UIPlatformVisibilityController>("Owned inactive platform visibility", roots);
            UIVisibilityGroupOverrider overrides = Inactive<UIVisibilityGroupOverrider>("Owned inactive macOS overrides", roots);
            PassiveOverride(overrides);
            using (var serialized = new SerializedObject(controller))
            {
                serialized.Update();
                SerializedProperty field = serialized.FindProperty("m_macOsUiOverrides");
                Assert.IsNotNull(field); Assert.AreEqual(SerializedPropertyType.ObjectReference, field.propertyType);
                Assert.IsTrue(field.objectReferenceValue == null);
                field.objectReferenceValue = overrides;
                Assert.IsTrue(serialized.ApplyModifiedPropertiesWithoutUndo());
                serialized.Update(); Assert.AreSame(overrides, serialized.FindProperty("m_macOsUiOverrides").objectReferenceValue);
            }
            Assert.AreSame(overrides, typeof(UIPlatformVisibilityController).GetField("m_macOsUiOverrides", Declared).GetValue(controller));
            foreach (GameObject root in roots) Assert.IsFalse(root.activeInHierarchy);
            PassiveOverride(overrides);
            Assert.AreSame(inputAction, InputBridge.OnUpdateLastInputType);
            Assert.AreSame(shutdownAction, InputBridge.OnShutdown);
            // Start is deliberately not called: the genuine override's SystemRef is
            // published by Awake, and activating it here would require a real UI service.
        }
        finally
        {
            try { DestroyAll(roots, roots.Count - 1); }
            finally
            {
                Assert.AreSame(inputAction, InputBridge.OnUpdateLastInputType);
                Assert.AreSame(shutdownAction, InputBridge.OnShutdown);
            }
        }
    }

    // 0x06002ffe x86-64 0x60b650: Start loads m_macOsUiOverrides and tail-jumps to
    // ActivateOverrides with no null test or platform predicate. Mutations caught:
    // a null guard or platform check (null case no longer faults) and a body that
    // skips the call (bound case no longer reaches ActivateOverrides). The managed
    // NullReferenceException stands in for the unchecked native call
    // ([Il2CppSetOption(NullChecks, false)]); native fault timing stays held.
    [Test]
    public void PlatformVisibilityStartCallsOverridesUnconditionallyWithoutNullGuard()
    {
        var roots = new List<GameObject>();
        object inputAction = InputBridge.OnUpdateLastInputType;
        object shutdownAction = InputBridge.OnShutdown;
        MethodInfo start = typeof(UIPlatformVisibilityController).GetMethod("Start", Declared);
        try
        {
            UIPlatformVisibilityController controller = Inactive<UIPlatformVisibilityController>("Owned inactive platform visibility start", roots);
            var unbound = Assert.Throws<TargetInvocationException>(() => start.Invoke(controller, null));
            Assert.IsInstanceOf<NullReferenceException>(unbound.InnerException);
            StringAssert.DoesNotContain("ActivateOverrides", unbound.InnerException.StackTrace); // Faults at the call site in Start.

            // Bound to an Awake-less overrider, the call reaches ActivateOverrides, which
            // faults on its unpublished system reference before allocating any handle.
            UIVisibilityGroupOverrider overrides = Inactive<UIVisibilityGroupOverrider>("Owned inactive macOS overrides start", roots);
            PassiveOverride(overrides);
            typeof(UIPlatformVisibilityController).GetField("m_macOsUiOverrides", Declared).SetValue(controller, overrides);
            var bound = Assert.Throws<TargetInvocationException>(() => start.Invoke(controller, null));
            Assert.IsInstanceOf<NullReferenceException>(bound.InnerException);
            StringAssert.Contains("UIVisibilityGroupOverrider.ActivateOverrides", bound.InnerException.StackTrace);
            PassiveOverride(overrides);
            foreach (GameObject root in roots) Assert.IsFalse(root.activeInHierarchy);
            Assert.AreSame(inputAction, InputBridge.OnUpdateLastInputType);
            Assert.AreSame(shutdownAction, InputBridge.OnShutdown);
        }
        finally
        {
            try { DestroyAll(roots, roots.Count - 1); }
            finally
            {
                Assert.AreSame(inputAction, InputBridge.OnUpdateLastInputType);
                Assert.AreSame(shutdownAction, InputBridge.OnShutdown);
            }
        }
    }

    private static T Inactive<T>(string name, List<GameObject> roots) where T : Component
    {
        GameObject root = new GameObject(name); roots.Add(root); root.SetActive(false);
        T component = root.AddComponent<T>(); Assert.IsFalse(root.activeInHierarchy);
        Assert.AreSame(root, component.gameObject); return component;
    }

    private static void PassiveOverride(UIVisibilityGroupOverrider component)
    {
        Assert.IsFalse(component.gameObject.activeInHierarchy);
        Type type = typeof(UIVisibilityGroupOverrider);
        Assert.IsNull(type.GetField("m_uiManagerSystemRef", Declared).GetValue(component));
        Assert.IsNull(type.GetField("m_stackableDataHandle", Declared).GetValue(component));
        var dictionary = (Dictionary<string, bool>)type.GetField("m_overridesDictionary", Declared).GetValue(component);
        Assert.IsNotNull(dictionary); Assert.AreEqual(0, dictionary.Count);
        // Only read genuine constructor state; no Awake/Activate/Deactivate or registry call.
    }

    private static void DestroyAll(List<GameObject> roots, int index)
    {
        if (index < 0) return;
        try { if (roots[index] != null) Object.DestroyImmediate(roots[index]); }
        finally { DestroyAll(roots, index - 1); }
    }

    private sealed class FieldContract { public string Name; public Type Type; public int Flags; }
    private static FieldContract F(string name, Type type, int flags) { return new FieldContract { Name = name, Type = type, Flags = flags }; }
    private static void Fields(Type type, FieldContract[] expected)
    {
        FieldInfo[] actual = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
        Assert.AreEqual(expected.Length, actual.Length);
        for (int i = 0; i < actual.Length; i++) Field(actual[i], expected[i]);
    }
    private static void Field(FieldInfo actual, FieldContract expected)
    {
        Assert.AreEqual(expected.Name, actual.Name); Assert.AreEqual(expected.Type, actual.FieldType);
        Assert.AreEqual(expected.Flags, (int)actual.Attributes);
        Assert.IsFalse(actual.IsStatic); Assert.IsFalse(actual.IsLiteral); Assert.IsFalse(actual.IsInitOnly);
        Assert.IsFalse(actual.IsNotSerialized);
    }
    private static void Owner(Type type)
    {
        Assert.AreEqual(1048577, (int)type.Attributes); Assert.AreEqual(typeof(MonoBehaviour), type.BaseType);
        Assert.AreEqual("Game.Runtime", type.Assembly.GetName().Name); Assert.IsFalse(type.IsGenericType);
    }
    private static void Constructor(Type type)
    {
        ConstructorInfo constructor = type.GetConstructors(Declared).Single();
        Assert.AreEqual(6278, (int)constructor.Attributes); Assert.AreEqual(0, constructor.GetParameters().Length);
        Assert.AreEqual(0, constructor.GetCustomAttributesData().Count);
    }
    private static void Method(MethodInfo method, string name, int flags, Type[] types, string[] names)
    {
        Assert.IsNotNull(method); Assert.AreEqual(name, method.Name); Assert.AreEqual(flags, (int)method.Attributes);
        Assert.AreEqual(typeof(void), method.ReturnType); Assert.IsFalse(method.IsGenericMethod);
        ParameterInfo[] parameters = method.GetParameters();
        CollectionAssert.AreEqual(types, parameters.Select(p => p.ParameterType).ToArray());
        CollectionAssert.AreEqual(names, parameters.Select(p => p.Name).ToArray());
        foreach (ParameterInfo parameter in parameters)
        {
            Assert.AreEqual(0, (int)parameter.Attributes); Assert.IsFalse(parameter.HasDefaultValue);
            Assert.AreEqual(0, parameter.GetCustomAttributesData().Count);
        }
        Assert.AreEqual(0, method.GetCustomAttributesData().Count);
        Assert.AreEqual(0, method.ReturnParameter.GetCustomAttributesData().Count);
    }
    private static void Options(Type type, params Option[] options)
    {
        IList<CustomAttributeData> attributes = type.GetCustomAttributesData(); Assert.AreEqual(options.Length, attributes.Count);
        for (int i = 0; i < attributes.Count; i++)
        {
            CustomAttributeData attribute = attributes[i]; Assert.AreEqual(typeof(Il2CppSetOptionAttribute), attribute.AttributeType);
            CollectionAssert.AreEqual(new[] { typeof(Option), typeof(object) }, attribute.Constructor.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.AreEqual(2, attribute.ConstructorArguments.Count);
            Assert.AreEqual(typeof(Option), attribute.ConstructorArguments[0].ArgumentType);
            Assert.AreEqual((int)options[i], Convert.ToInt32(attribute.ConstructorArguments[0].Value));
            Assert.AreEqual(typeof(bool), attribute.ConstructorArguments[1].ArgumentType);
            Assert.AreEqual(false, attribute.ConstructorArguments[1].Value); Assert.AreEqual(0, attribute.NamedArguments.Count);
        }
    }
    private static void EmptyAttribute(CustomAttributeData attribute, Type type)
    {
        Assert.AreEqual(type, attribute.AttributeType); Assert.AreEqual(0, attribute.Constructor.GetParameters().Length);
        Assert.AreEqual(0, attribute.ConstructorArguments.Count); Assert.AreEqual(0, attribute.NamedArguments.Count);
    }
}
