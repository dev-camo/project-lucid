using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Verification
{
    // Controlled owned-object checks. Never activate or invoke the original UI lifecycle.
    public static class UIOriginalLeafVerification
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const int UnknownAudioPayload = -2147483201;

        private sealed class Checks
        {
            public int Count;
            public void Require(bool condition, string message)
            {
                Count++;
                if (!condition) throw new InvalidOperationException("Original UI leaf verification: " + message);
            }
        }

        public static int VerifyOwnedInactiveMenuCamera()
        {
            var c = new Checks();
            Type owner = typeof(GUICameraManager);
            CheckOwner(c, owner, "GUICameraManager", 1048577, typeof(MonoSingleton<GUICameraManager>));
            CheckOptions(c, owner, 2, 1);
            Type[] inheritedInterfaces = owner.BaseType.GetInterfaces();
            Type[] actualInterfaces = owner.GetInterfaces();
            c.Require(actualInterfaces.Length == inheritedInterfaces.Length, "camera acquired no additional interface");
            foreach (Type contract in actualInterfaces) c.Require(Array.IndexOf(inheritedInterfaces, contract) >= 0, "genuine inherited camera interface");
            FieldInfo cameraField = Fields(c, owner, new[] { "m_menuCamera" }, new[] { typeof(Camera) })[0];
            CheckFieldAttributes(c, cameraField, new[] { typeof(SerializeField) });
            Getter(c, owner, "MenuCamera", cameraField);
            Toggle(c, owner, cameraField);
            BaseOnlyConstructor(c, owner, 6278, typeof(MonoSingleton<GUICameraManager>));
            c.Require(owner.GetMethods(Declared).Length == 2 && owner.GetProperties(Declared).Length == 1 && owner.GetEvents(Declared).Length == 0 && owner.GetNestedTypes(Declared).Length == 0, "complete camera declaration counts");
            var state = new StateWatch(c);
            GameObject managerObject = null, firstObject = null, secondObject = null;
            try
            {
                managerObject = Inactive(c, "Owned original UI camera manager");
                GUICameraManager manager = managerObject.AddComponent<GUICameraManager>();
                state.AssertUnchanged(c, "after inactive manager creation");
                c.Require(ReferenceEquals(manager.MenuCamera, null), "camera constructor stored CLR null");
                firstObject = Inactive(c, "Owned first UI Camera");
                Camera first = firstObject.AddComponent<Camera>();
                cameraField.SetValue(manager, first);
                c.Require(ReferenceEquals(manager.MenuCamera, first), "first camera identity");
                first.enabled = true;
                manager.ToggleCameras(false);
                c.Require(!first.enabled, "actual camera disabled by false");
                manager.ToggleCameras(true);
                c.Require(first.enabled, "actual camera enabled by true");
                secondObject = Inactive(c, "Owned replacement UI Camera");
                Camera second = secondObject.AddComponent<Camera>();
                cameraField.SetValue(manager, second);
                c.Require(ReferenceEquals(manager.MenuCamera, second), "replacement camera identity");
                second.enabled = true;
                manager.ToggleCameras(false);
                c.Require(!second.enabled && first.enabled, "toggle uses fresh replacement and leaves first untouched");
                cameraField.SetValue(manager, null);
                c.Require(ReferenceEquals(manager.MenuCamera, null), "explicit camera CLR null is returned");
                Exception fault = null;
                try { manager.ToggleCameras(true); }
                catch (Exception exception) { fault = exception; }
                c.Require(fault != null && (fault is NullReferenceException || fault is MissingReferenceException), "real null camera faults in controlled Unity environment");
                c.Require(first.enabled && !second.enabled, "null fault changes neither prior camera");
                state.AssertUnchanged(c, "after all camera calls");
            }
            finally
            {
                try { if (!ReferenceEquals(managerObject, null)) UObject.DestroyImmediate(managerObject); }
                finally
                {
                    try { if (!ReferenceEquals(secondObject, null)) UObject.DestroyImmediate(secondObject); }
                    finally
                    {
                        try { if (!ReferenceEquals(firstObject, null)) UObject.DestroyImmediate(firstObject); }
                        finally { state.AssertUnchanged(c, "after destruction of never-activated owned camera objects"); }
                    }
                }
            }
            return c.Count;
        }

        public static int VerifyOwnedInactiveConfigurationStoredGetters()
        {
            var c = new Checks();
            Type owner = typeof(UIRuntimeConfiguration);
            CheckOwner(c, owner, "Hardlight.UIRuntimeConfiguration", 1048833, typeof(MonoBehaviour));
            c.Require(owner.GetInterfaces().Length == 1 && owner.GetInterfaces()[0] == typeof(ISystem), "genuine empty ISystem identity");
            CheckOptions(c, owner, 1, 2);
            string[] names = { "m_scrollUpInput", "m_scrollDownInput", "m_navigateTowardsSound", "m_inputModule", "m_inputBridge", "m_shouldHighlightSelection", "m_shouldAutoRotateScreen", "m_shouldAutoNavigateUISelection" };
            Type[] types = { typeof(InputSupplier), typeof(InputSupplier), typeof(HLAudioTypes), typeof(BaseInputModule), typeof(InputBridge), typeof(bool), typeof(bool), typeof(bool) };
            FieldInfo[] fields = Fields(c, owner, names, types);
            CheckFieldAttributes(c, fields[0], new[] { typeof(SerializeField) });
            CheckFieldAttributes(c, fields[1], new[] { typeof(SerializeField) });
            CheckFieldAttributes(c, fields[2], new[] { typeof(HashEnumAttribute), typeof(SerializeField) });
            var hash = fields[2].GetCustomAttributesData()[0];
            c.Require(hash.ConstructorArguments.Count == 1 && hash.ConstructorArguments[0].ArgumentType == typeof(Type) && Equals(hash.ConstructorArguments[0].Value, typeof(HLAudioTypes)) && hash.NamedArguments.Count == 0, "genuine HLAudioTypes hash-enum argument");
            CheckFieldAttributes(c, fields[3], new[] { typeof(SerializeField) });
            CheckFieldAttributes(c, fields[4], new[] { typeof(SerializeField) });
            Tooltip(c, fields[5], true, "Should the UI system highlight the active selection?");
            Tooltip(c, fields[6], true, "Should the UI system cope with autorotation?");
            Tooltip(c, fields[7], false, "Should the UI system automatically navigate when no explicit button navigation is specified?");
            string[] properties = { "ScrollUpInput", "ScrollDownInput", "NavigateTowardsSound", "InputBridge", "InputModule", "ShouldHighlightSelection", "ShouldAutoRotateScreen", "ShouldAutoNavigateUISelection" };
            int[] fieldIndices = { 0, 1, 2, 4, 3, 5, 6, 7 };
            PropertyInfo[] actualProperties = owner.GetProperties(Declared);
            Array.Sort(actualProperties, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            c.Require(actualProperties.Length == 8, "complete eight configuration properties");
            for (int i = 0; i < properties.Length; ++i)
            {
                c.Require(actualProperties[i].Name == properties[i], "exact configuration property order " + i);
                Getter(c, owner, properties[i], fields[fieldIndices[i]]);
            }
            c.Require(owner.GetMethods(Declared).Length == 10 && owner.GetEvents(Declared).Length == 0 && owner.GetNestedTypes(Declared).Length == 0, "complete configuration declaration counts");
            MethodInfo[] configurationMethods = owner.GetMethods(Declared);
            Array.Sort(configurationMethods, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            for (int i = 0; i < 8; ++i) c.Require(configurationMethods[i].Name == "get_" + properties[i], "configuration getter declaration order " + i);
            for (int i = 8; i < 10; ++i)
            {
                MethodInfo callback = configurationMethods[i];
                c.Require(callback.Name == (i == 8 ? "Awake" : "OnDestroy") && (int)callback.Attributes == 129 && (int)callback.GetMethodImplementationFlags() == 0 && callback.ReturnType == typeof(void) && callback.GetParameters().Length == 0 && !callback.IsGenericMethod && callback.GetCustomAttributesData().Count == 0, "exact held lifecycle declaration " + i);
            }
            BaseOnlyConstructor(c, owner, 6278, typeof(MonoBehaviour));
            c.Require(typeof(HLAudioTypes).Assembly.GetName().Name == "HLAutoGenerated" && Enum.GetUnderlyingType(typeof(HLAudioTypes)) == typeof(int), "actual signed audio enum provider");
            c.Require(!Enum.IsDefined(typeof(HLAudioTypes), (HLAudioTypes)UnknownAudioPayload), "authored unknown audio payload is genuinely undefined");
            var state = new StateWatch(c);
            GameObject gameObject = null;
            try
            {
                gameObject = Inactive(c, "Owned original UI configuration");
                UIRuntimeConfiguration configuration = gameObject.AddComponent<UIRuntimeConfiguration>();
                state.AssertUnchanged(c, "after inactive configuration creation");
                ObjectDefaults(c, configuration);
                c.Require((int)configuration.NavigateTowardsSound == 0 && !configuration.ShouldHighlightSelection && !configuration.ShouldAutoRotateScreen && !configuration.ShouldAutoNavigateUISelection, "no authored primitive constructor defaults");
                for (int bits = 0; bits < 8; ++bits)
                {
                    bool highlight = (bits & 1) != 0, rotate = (bits & 2) != 0, navigate = (bits & 4) != 0;
                    int sound = bits == 0 ? 0 : UnknownAudioPayload;
                    string json = "{\"m_navigateTowardsSound\":" + sound.ToString(CultureInfo.InvariantCulture) + ",\"m_shouldHighlightSelection\":" + JsonBool(highlight) + ",\"m_shouldAutoRotateScreen\":" + JsonBool(rotate) + ",\"m_shouldAutoNavigateUISelection\":" + JsonBool(navigate) + "}";
                    JsonUtility.FromJsonOverwrite(json, configuration);
                    c.Require((int)configuration.NavigateTowardsSound == sound, "signed enum stored getter " + bits);
                    c.Require(configuration.ShouldHighlightSelection == highlight, "independent highlight bit " + bits);
                    c.Require(configuration.ShouldAutoRotateScreen == rotate, "independent rotate bit " + bits);
                    c.Require(configuration.ShouldAutoNavigateUISelection == navigate, "independent navigate bit " + bits);
                    ObjectDefaults(c, configuration);
                    string written = JsonUtility.ToJson(configuration);
                    JsonPrimitive(c, written, "m_navigateTowardsSound", sound.ToString(CultureInfo.InvariantCulture));
                    JsonPrimitive(c, written, "m_shouldHighlightSelection", JsonBool(highlight));
                    JsonPrimitive(c, written, "m_shouldAutoRotateScreen", JsonBool(rotate));
                    JsonPrimitive(c, written, "m_shouldAutoNavigateUISelection", JsonBool(navigate));
                }
                state.AssertUnchanged(c, "after stored getters and primitive JSON roundtrips");
            }
            finally
            {
                try { if (!ReferenceEquals(gameObject, null)) UObject.DestroyImmediate(gameObject); }
                finally { state.AssertUnchanged(c, "after destruction of never-activated configuration"); }
            }
            return c.Count;
        }

        public static int VerifyOriginalAbstractBridgeAndSupplierMetadata()
        {
            var c = new Checks();
            Type owner = typeof(InputBridge);
            CheckOwner(c, owner, "Hardlight.InputBridge", 1048705, typeof(ScriptableObject));
            c.Require(owner.GetCustomAttributesData().Count == 0 && owner.GetInterfaces().Length == 0 && owner.GetNestedTypes(Declared).Length == 0 && owner.GetProperties(Declared).Length == 0 && owner.GetEvents(Declared).Length == 0, "entire abstract bridge extra declaration absence");
            FieldInfo[] fields = owner.GetFields(Declared);
            Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            c.Require(fields.Length == 2, "exact bridge static field count");
            Type[] fieldTypes = { typeof(FastAction<UIInputType>), typeof(FastAction) };
            string[] fieldNames = { "OnUpdateLastInputType", "OnShutdown" };
            for (int i = 0; i < fields.Length; ++i)
                c.Require(fields[i].Name == fieldNames[i] && fields[i].FieldType == fieldTypes[i] && fields[i].DeclaringType == owner && (int)fields[i].Attributes == 22 && fields[i].GetCustomAttributesData().Count == 0, "public mutable static bridge field " + i);
            Type supplier = typeof(InputSupplier);
            var contracts = new[]
            {
                new Contract("SetInputModule", typeof(void), new[] { typeof(BaseInputModule) }, new[] { "inputModule" }),
                new Contract("InputModuleExists", typeof(bool), Type.EmptyTypes, new string[0]),
                new Contract("SetHardwareCursorVisibility", typeof(void), new[] { typeof(bool) }, new[] { "visibility" }),
                new Contract("GetHardwareCursorVisibility", typeof(bool), Type.EmptyTypes, new string[0]),
                new Contract("DoesInputTypeRequireSelection", typeof(bool), new[] { typeof(UIInputType) }, new[] { "inputType" }),
                new Contract("GetLastInputType", typeof(UIInputType), Type.EmptyTypes, new string[0]),
                new Contract("DeselectObjectIfCurrentlySelected", typeof(void), new[] { typeof(GameObject) }, new[] { "gameObject" }),
                new Contract("SetSelectedGameObjectWithMemory", typeof(void), new[] { typeof(GameObject), typeof(BaseEventData) }, new[] { "selectableGameObject", "eventData" }, 0),
                new Contract("IsObjectSelected", typeof(bool), new[] { typeof(GameObject) }, new[] { "selectedObject" }),
                new Contract("RegisterForInputUpdateEvents", typeof(void), Type.EmptyTypes, new string[0]),
                new Contract("UnregisterForInputUpdateEvents", typeof(void), Type.EmptyTypes, new string[0]),
                new Contract("RegisterInputListener", typeof(void), new[] { supplier, typeof(Action<Vector2>), typeof(int) }, new[] { "inputSupplier", "callback", "joystickIndex" }, 2),
                new Contract("UnregisterInputListener", typeof(void), new[] { supplier, typeof(Action<Vector2>), typeof(int) }, new[] { "inputSupplier", "callback", "joystickIndex" }, 2),
                new Contract("RegisterInputListener", typeof(void), new[] { supplier, typeof(Action<List<Vector2>>), typeof(int) }, new[] { "inputSupplier", "callback", "joystickIndex" }, 2),
                new Contract("UnregisterInputListener", typeof(void), new[] { supplier, typeof(Action<List<Vector2>>), typeof(int) }, new[] { "inputSupplier", "callback", "joystickIndex" }, 2),
                new Contract("RegisterInputListenerOnDown", typeof(void), new[] { supplier, typeof(Action<float>), typeof(int) }, new[] { "inputSupplier", "callback", "joystickIndex" }, 2),
                new Contract("RegisterInputListenerOnHeld", typeof(void), new[] { supplier, typeof(Action<float>), typeof(int) }, new[] { "inputSupplier", "callback", "joystickIndex" }, 2),
                new Contract("RegisterInputListenerOnUp", typeof(void), new[] { supplier, typeof(Action<float>), typeof(int) }, new[] { "inputSupplier", "callback", "joystickIndex" }, 2),
                new Contract("UnregisterInputListener", typeof(void), new[] { supplier, typeof(Action<float>), typeof(int) }, new[] { "inputSupplier", "callback", "joystickIndex" }, 2)
            };
            MethodInfo[] methods = owner.GetMethods(Declared);
            Array.Sort(methods, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            c.Require(methods.Length == contracts.Length, "all19 abstract declarations, no invented methods");
            for (int i = 0; i < contracts.Length; ++i)
            {
                MethodInfo method = methods[i]; Contract contract = contracts[i];
                c.Require(method.Name == contract.Name && method.DeclaringType == owner && method.ReturnType == contract.Return && (int)method.Attributes == 1478 && (int)method.GetMethodImplementationFlags() == 0 && !method.IsGenericMethod && method.CallingConvention == (CallingConventions.Standard | CallingConventions.HasThis) && method.GetMethodBody() == null && method.GetCustomAttributesData().Count == 0 && method.ReturnParameter.GetCustomAttributesData().Count == 0 && method.ReturnParameter.Attributes == ParameterAttributes.Retval && method.ReturnParameter.Position == -1 && method.ReturnParameter.ParameterType == method.ReturnType, "exact ordered stripped abstract method " + i);
                ParameterInfo[] parameters = method.GetParameters();
                c.Require(parameters.Length == contract.Parameters.Length, "abstract parameter count " + i);
                for (int j = 0; j < parameters.Length; ++j)
                {
                    ParameterInfo parameter = parameters[j]; bool optional = contract.FirstOptional >= 0 && j >= contract.FirstOptional;
                    c.Require(parameter.Name == contract.ParameterNames[j] && parameter.Position == j && parameter.ParameterType == contract.Parameters[j] && (int)parameter.Attributes == (optional ? 4112 : 0) && (optional ? (parameter.GetCustomAttributesData().Count == 1 && parameter.GetCustomAttributesData()[0].AttributeType == typeof(System.Runtime.InteropServices.OptionalAttribute) && parameter.GetCustomAttributesData()[0].Constructor.DeclaringType == typeof(System.Runtime.InteropServices.OptionalAttribute) && parameter.GetCustomAttributesData()[0].ConstructorArguments.Count == 0 && parameter.GetCustomAttributesData()[0].NamedArguments.Count == 0) : parameter.GetCustomAttributesData().Count == 0) && parameter.HasDefaultValue == optional, "exact abstract parameter identity/default marker " + i + "/" + j);
                    if (optional) c.Require(contract.FirstOptional == 0 ? parameter.DefaultValue == null : Equals(parameter.DefaultValue, -1), "precise optional default " + i + "/" + j);
                }
            }
            BaseOnlyConstructor(c, owner, 6276, typeof(ScriptableObject));
            CheckOwner(c, supplier, "Hardlight.InputSupplier", 1048705, typeof(ScriptableObject));
            c.Require(supplier.GetFields(Declared).Length == 0 && supplier.GetMethods(Declared).Length == 0 && supplier.GetProperties(Declared).Length == 0 && supplier.GetEvents(Declared).Length == 0 && supplier.GetNestedTypes(Declared).Length == 0 && supplier.GetInterfaces().Length == 0 && supplier.GetCustomAttributesData().Count == 0, "whole fieldless abstract supplier contract");
            BaseOnlyConstructor(c, supplier, 6276, typeof(ScriptableObject));
            // Never allocate either abstract type or require the static actions to remain null.
            return c.Count;
        }

        private sealed class Contract
        {
            public readonly string Name; public readonly Type Return; public readonly Type[] Parameters; public readonly string[] ParameterNames; public readonly int FirstOptional;
            public Contract(string name, Type result, Type[] parameters, string[] names, int firstOptional = -1)
            { Name = name; Return = result; Parameters = parameters; ParameterNames = names; FirstOptional = firstOptional; }
        }

        private static void CheckOwner(Checks c, Type owner, string name, int attributes, Type baseType)
        {
            c.Require(owner.FullName == name && owner.Assembly.FullName == "HLUnityUI.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" && owner.Module == owner.Assembly.ManifestModule && (int)owner.Attributes == attributes && owner.BaseType == baseType && !owner.IsGenericType && owner.DeclaringType == null && owner.TypeInitializer == null, "loaded exact original type " + name);
            c.Require(typeof(FastAction).Assembly.GetName().Name == "HLUnityCore.Runtime" && typeof(HLAudioTypes).Assembly.GetName().Name == "HLAutoGenerated" && typeof(BaseInputModule).Assembly.GetName().Name == "UnityEngine.UI", "genuine Core/HLA/UI providers");
        }

        private static FieldInfo[] Fields(Checks c, Type owner, string[] names, Type[] types)
        {
            FieldInfo[] fields = owner.GetFields(Declared); Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            c.Require(fields.Length == names.Length, "whole original field count " + owner.Name);
            for (int i = 0; i < fields.Length; ++i)
                c.Require(fields[i].Name == names[i] && fields[i].FieldType == types[i] && fields[i].DeclaringType == owner && (int)fields[i].Attributes == 1, "exact mutable private field order/type " + names[i]);
            return fields;
        }

        private static void CheckFieldAttributes(Checks c, FieldInfo field, Type[] expected)
        {
            IList<CustomAttributeData> attributes = field.GetCustomAttributesData();
            c.Require(attributes.Count == expected.Length, "field attribute count " + field.Name);
            for (int i = 0; i < expected.Length; ++i)
                c.Require(attributes[i].AttributeType == expected[i] && attributes[i].Constructor.DeclaringType == expected[i] && attributes[i].Constructor.Module == expected[i].Module && attributes[i].NamedArguments.Count == 0, "ordered genuine field attribute " + field.Name + "/" + i);
            for (int i = 0; i < expected.Length; ++i)
                if (expected[i] == typeof(SerializeField)) c.Require(attributes[i].ConstructorArguments.Count == 0, "no serialization attribute arguments " + field.Name);
        }

        private static void Tooltip(Checks c, FieldInfo field, bool tooltipFirst, string text)
        {
            CheckFieldAttributes(c, field, tooltipFirst ? new[] { typeof(TooltipAttribute), typeof(SerializeField) } : new[] { typeof(SerializeField), typeof(TooltipAttribute) });
            CustomAttributeData tooltip = field.GetCustomAttributesData()[tooltipFirst ? 0 : 1];
            c.Require(tooltip.ConstructorArguments.Count == 1 && tooltip.ConstructorArguments[0].ArgumentType == typeof(string) && Equals(tooltip.ConstructorArguments[0].Value, text), "exact authored Tooltip " + field.Name);
        }

        private static object AttributeValue(CustomAttributeTypedArgument argument)
        {
            object value = argument.Value;
            return value is CustomAttributeTypedArgument ? AttributeValue((CustomAttributeTypedArgument)value) : value;
        }

        private static void CheckOptions(Checks c, Type owner, int first, int second)
        {
            IList<CustomAttributeData> attributes = owner.GetCustomAttributesData();
            c.Require(attributes.Count == 2, "exact two class options " + owner.Name);
            int[] expected = { first, second };
            for (int i = 0; i < 2; ++i)
            {
                CustomAttributeData attribute = attributes[i];
                c.Require(attribute.AttributeType == typeof(Il2CppSetOptionAttribute) && attribute.Constructor.DeclaringType == typeof(Il2CppSetOptionAttribute) && attribute.Constructor.Module == typeof(Il2CppSetOptionAttribute).Module && attribute.NamedArguments.Count == 0 && attribute.ConstructorArguments.Count == 2, "genuine option constructor " + owner.Name);
                c.Require(attribute.Constructor.GetParameters().Length == 2 && attribute.Constructor.GetParameters()[0].ParameterType == typeof(Option) && attribute.Constructor.GetParameters()[1].ParameterType == typeof(object) && attribute.ConstructorArguments[0].ArgumentType == typeof(Option) && Convert.ToInt32(AttributeValue(attribute.ConstructorArguments[0]), CultureInfo.InvariantCulture) == expected[i] && Equals(AttributeValue(attribute.ConstructorArguments[1]), false), "original option order/value " + owner.Name + "/" + i);
            }
        }

        private static void Getter(Checks c, Type owner, string name, FieldInfo field)
        {
            PropertyInfo property = owner.GetProperty(name, Declared);
            c.Require(property != null && property.DeclaringType == owner && property.PropertyType == field.FieldType && (int)property.Attributes == 0 && property.GetIndexParameters().Length == 0 && property.GetSetMethod(true) == null && property.GetCustomAttributesData().Count == 0, "exact stored property " + name);
            MethodInfo method = property.GetGetMethod(true);
            c.Require(method != null && method.Name == "get_" + name && method.DeclaringType == owner && method.ReturnType == field.FieldType && method.GetParameters().Length == 0 && (int)method.Attributes == 2182 && (int)method.GetMethodImplementationFlags() == 0 && !method.IsGenericMethod && method.CallingConvention == (CallingConventions.Standard | CallingConventions.HasThis) && method.GetCustomAttributesData().Count == 0 && method.ReturnParameter.GetCustomAttributesData().Count == 0 && method.ReturnParameter.Attributes == ParameterAttributes.Retval && method.ReturnParameter.Position == -1 && method.ReturnParameter.ParameterType == method.ReturnType, "exact getter name/signature/flags " + name);
            MethodBody body = EmptyBody(c, method, "stored getter " + name); byte[] il = body.GetILAsByteArray();
            c.Require(il.Length == 7 && il[0] == 0x02 && il[1] == 0x7b && il[6] == 0x2a, "entire direct field getter CIL " + name);
            FieldInfo operand = method.Module.ResolveField(BitConverter.ToInt32(il, 2));
            c.Require(operand.Module == field.Module && operand.MetadataToken == field.MetadataToken && operand.DeclaringType == owner && operand.Name == field.Name && operand.FieldType == field.FieldType, "exact getter field operand scope " + name);
        }

        private static MethodBody EmptyBody(Checks c, MethodBase method, string name)
        {
            MethodBody body = method.GetMethodBody();
            c.Require(body != null && body.MaxStackSize == 8 && !body.InitLocals && body.LocalVariables.Count == 0 && body.ExceptionHandlingClauses.Count == 0, "complete physical body without locals/EH " + name);
            return body;
        }

        private static void Toggle(Checks c, Type owner, FieldInfo field)
        {
            MethodInfo method = owner.GetMethod("ToggleCameras", Declared, null, new[] { typeof(bool) }, null);
            c.Require(method != null && method.DeclaringType == owner && (int)method.Attributes == 134 && (int)method.GetMethodImplementationFlags() == 0 && method.ReturnType == typeof(void) && method.GetCustomAttributesData().Count == 0 && method.GetParameters()[0].Name == "on" && (int)method.GetParameters()[0].Attributes == 0 && !method.GetParameters()[0].HasDefaultValue && !method.IsGenericMethod, "exact toggle declaration");
            byte[] il = EmptyBody(c, method, "toggle").GetILAsByteArray();
            c.Require(il.Length == 13 && il[0] == 0x02 && il[1] == 0x7b && il[6] == 0x03 && il[7] == 0x6f && il[12] == 0x2a, "entire unguarded enabled forwarding CIL");
            FieldInfo operand = method.Module.ResolveField(BitConverter.ToInt32(il, 2));
            MethodBase setter = method.Module.ResolveMethod(BitConverter.ToInt32(il, 8));
            MethodInfo expected = typeof(Behaviour).GetProperty("enabled").GetSetMethod();
            c.Require(operand.Module == field.Module && operand.MetadataToken == field.MetadataToken && operand.DeclaringType == owner && operand.FieldType == typeof(Camera), "actual toggle stored camera operand");
            c.Require(setter.Module == expected.Module && setter.MetadataToken == expected.MetadataToken && setter.DeclaringType == typeof(Behaviour) && setter.Name == "set_enabled" && !setter.IsStatic && setter.GetParameters().Length == 1 && setter.GetParameters()[0].ParameterType == typeof(bool), "actual Unity Behaviour.enabled setter operand");
        }

        private static void BaseOnlyConstructor(Checks c, Type owner, int attributes, Type baseType)
        {
            ConstructorInfo[] constructors = owner.GetConstructors(Declared);
            c.Require(constructors.Length == 1, "one original instance constructor " + owner.Name);
            ConstructorInfo constructor = constructors[0];
            c.Require(constructor.Name == ".ctor" && constructor.DeclaringType == owner && (int)constructor.Attributes == attributes && (int)constructor.GetMethodImplementationFlags() == 0 && constructor.GetParameters().Length == 0 && constructor.CallingConvention == (CallingConventions.Standard | CallingConventions.HasThis) && constructor.GetCustomAttributesData().Count == 0, "exact base-only constructor declaration " + owner.Name);
            byte[] il = EmptyBody(c, constructor, "constructor " + owner.Name).GetILAsByteArray();
            // Require the complete current base-only call/ret shape; no opcode relaxation.
            c.Require(il.Length == 7 && il[0] == 0x02 && il[1] == 0x28 && il[6] == 0x2a, "entire base-call/ret constructor " + owner.Name);
            MethodBase call = constructor.Module.ResolveMethod(BitConverter.ToInt32(il, 2));
            ConstructorInfo expected = baseType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            c.Require(expected != null && call is ConstructorInfo && call.Name == ".ctor" && call.Module == expected.Module && call.MetadataToken == expected.MetadataToken && call.DeclaringType == baseType && call.GetParameters().Length == 0, "exact genuine base constructor operand " + owner.Name);
        }

        private static GameObject Inactive(Checks c, string name)
        {
            var gameObject = new GameObject(name);
            try
            {
                gameObject.SetActive(false);
                c.Require(!gameObject.activeSelf && !gameObject.activeInHierarchy, "owned GameObject inactive before adding original component");
                return gameObject;
            }
            catch
            {
                UObject.DestroyImmediate(gameObject);
                throw;
            }
        }

        private static void ObjectDefaults(Checks c, UIRuntimeConfiguration configuration)
        {
            c.Require(ReferenceEquals(configuration.ScrollUpInput, null) && ReferenceEquals(configuration.ScrollDownInput, null) && ReferenceEquals(configuration.InputBridge, null) && ReferenceEquals(configuration.InputModule, null), "four genuine abstract/module reference getters remain CLR null");
        }

        private static string JsonBool(bool value) { return value ? "true" : "false"; }
        private static void JsonPrimitive(Checks c, string json, string name, string value)
        {
            MatchCollection matches = Regex.Matches(json, "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(true|false|-?[0-9]+)(?=\\s*[,}])", RegexOptions.CultureInvariant);
            c.Require(matches.Count == 1 && matches[0].Groups[1].Value == value, "exact serialized primitive key/value " + name);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object value) { return RuntimeHelpers.GetHashCode(value); }
        }

        // Read existing registry/FastAction state directly; never dispatch existing callbacks or reset services.
        private sealed class StateWatch
        {
            private readonly List<FieldState> fields = new List<FieldState>();
            private readonly List<DictionaryState> dictionaries = new List<DictionaryState>();
            private readonly List<ListState> lists = new List<ListState>();
            private readonly HashSet<object> visited = new HashSet<object>(new ReferenceComparer());
            public StateWatch(Checks c)
            {
                Type process = typeof(ProcessManager);
                string[] names = { "s_systemDictionary", "s_systemActionLookup", "s_actionList", "s_systemActionInProgress", "s_logger" };
                FieldInfo[] actual = process.GetFields(Declared);
                c.Require(actual.Length == names.Length, "complete ProcessManager static storage shape");
                foreach (string name in names)
                {
                    FieldInfo field = process.GetField(name, Declared);
                    c.Require(field != null && field.IsStatic && field.IsPrivate && field.DeclaringType == process, "genuine ProcessManager storage " + name);
                    CaptureField(field, null);
                }
                FieldInfo singleton = typeof(MonoSingleton<GUICameraManager>).GetField("<Instance>k__BackingField", Declared);
                c.Require(singleton != null && singleton.FieldType == typeof(GUICameraManager) && singleton.IsStatic && singleton.IsPrivate && !singleton.IsInitOnly, "genuine singleton backing field");
                CaptureField(singleton, null);
                foreach (string name in new[] { "OnUpdateLastInputType", "OnShutdown" })
                {
                    FieldInfo action = typeof(InputBridge).GetField(name, Declared);
                    c.Require(action != null && (int)action.Attributes == 22, "actual original mutable static action " + name);
                    CaptureField(action, null);
                }
            }
            private void CaptureField(FieldInfo field, object owner)
            {
                object value = field.GetValue(owner);
                fields.Add(new FieldState(field, owner, value)); Capture(value);
            }
            private void Capture(object value)
            {
                if (value == null || value is string || value is ValueType || value is Delegate || !visited.Add(value)) return;
                IDictionary dictionary = value as IDictionary;
                if (dictionary != null)
                {
                    var state = new DictionaryState(dictionary); dictionaries.Add(state);
                    foreach (DictionaryEntry entry in dictionary) { Capture(entry.Key); Capture(entry.Value); }
                    return;
                }
                IList list = value as IList;
                if (list != null)
                {
                    lists.Add(new ListState(list)); foreach (object item in list) Capture(item); return;
                }
                Type type = value.GetType();
                if (type.Assembly != typeof(ProcessManager).Assembly) return;
                for (Type at = type; at != null; at = at.BaseType)
                {
                    string name = at.IsGenericType ? at.GetGenericTypeDefinition().FullName : at.FullName;
                    if (name != "Hardlight.ProcessManager+SystemInfo" && name != "Hardlight.SystemRef" && name != "Hardlight.SystemRef`1" && name != "Hardlight.FastAction" && name != "Hardlight.FastAction`1" && name != "Hardlight.FastActionBase`2") continue;
                    foreach (FieldInfo field in at.GetFields(Declared)) if (!field.IsStatic) CaptureField(field, value);
                }
            }
            public void AssertUnchanged(Checks c, string phase)
            {
                foreach (FieldState field in fields) c.Require(Same(field.Value, field.Field.GetValue(field.Owner)), "global/nested field unchanged " + field.Field.Name + " " + phase);
                foreach (DictionaryState dictionary in dictionaries) dictionary.AssertUnchanged(c, phase);
                foreach (ListState list in lists) list.AssertUnchanged(c, phase);
            }
            private static bool Same(object a, object b)
            { return a is ValueType ? Equals(a, b) : ReferenceEquals(a, b); }
            private sealed class FieldState
            {
                public readonly FieldInfo Field; public readonly object Owner, Value;
                public FieldState(FieldInfo field, object owner, object value) { Field = field; Owner = owner; Value = value; }
            }
            private sealed class DictionaryState
            {
                private readonly IDictionary dictionary; private readonly List<DictionaryEntry> entries = new List<DictionaryEntry>();
                public DictionaryState(IDictionary source) { dictionary = source; foreach (DictionaryEntry entry in source) entries.Add(entry); }
                public void AssertUnchanged(Checks c, string phase)
                {
                    c.Require(dictionary.Count == entries.Count, "existing dictionary count unchanged " + phase);
                    int index = 0;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        c.Require(index < entries.Count && Same(entry.Key, entries[index].Key) && Same(entry.Value, entries[index].Value), "existing dictionary exact ordered contents unchanged " + phase); index++;
                    }
                    c.Require(index == entries.Count, "entire dictionary enumeration unchanged " + phase);
                }
            }
            private sealed class ListState
            {
                private readonly IList list; private readonly object[] entries;
                public ListState(IList source) { list = source; entries = new object[source.Count]; source.CopyTo(entries, 0); }
                public void AssertUnchanged(Checks c, string phase)
                {
                    c.Require(list.Count == entries.Length, "existing list count unchanged " + phase);
                    for (int i = 0; i < entries.Length; ++i) c.Require(Same(list[i], entries[i]), "existing list exact contents unchanged " + phase);
                }
            }
        }
    }
}
