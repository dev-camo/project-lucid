// Controlled verification of three preserved Ability definition records.
using System;
using System.Linq;
using System.Reflection;
using HardlightProject;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
using Settings = HardlightProject.CameraHeadingOverride.CameraHeadingOverrideSettings;

namespace ProjectLucid.Verification
{
    public static class AbilityLeafDefinitionVerification
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private sealed class Checks
        {
            public int Count;
            public void Require(bool value, string message)
            {
                if (!value) throw new InvalidOperationException(message);
                ++Count;
            }
        }

        private static Type OriginalType(Checks c, string name)
        {
            Assembly assembly = typeof(Settings).Assembly;
            c.Require(assembly.FullName == "Game.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" && typeof(Settings).Module == assembly.ManifestModule, "Genuine settings must come from Game.Runtime.");
            Type type = assembly.GetType("HardlightProject." + name, true);
            c.Require(type.Assembly == assembly && !type.IsGenericType && type.GetNestedTypes(Declared).Length == 0,
                "Original leaf owner, generic or nested declaration changed: " + name);
            return type;
        }

        private static void Shape(Checks c, Type type, int flags, Type baseType, int fields, int properties, int methods)
        {
            c.Require((int)type.Attributes == flags && type.BaseType == baseType, "Original type flags/base changed: " + type.Name);
            c.Require(type.GetFields(Declared).Length == fields && type.GetProperties(Declared).Length == properties &&
                type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length == methods,
                "Original complete leaf member count changed: " + type.Name);
            c.Require(type.GetInterfaces().OrderBy(t => t.FullName, StringComparer.Ordinal).SequenceEqual(
                baseType.GetInterfaces().OrderBy(t => t.FullName, StringComparer.Ordinal)),
                "Leaf acquired an additional reflected interface: " + type.Name);
        }

        private static object Scalar(CustomAttributeTypedArgument value)
        {
            object result = value.Value;
            while (result is CustomAttributeTypedArgument) result = ((CustomAttributeTypedArgument)result).Value;
            return result;
        }

        private static void Options(Checks c, Type type, int first, int second, bool menu)
        {
            var attributes = type.GetCustomAttributesData().Where(a => a.AttributeType != typeof(SerializableAttribute)).ToArray();
            c.Require(attributes.Length == (menu ? 3 : 2), "Original type attribute count changed: " + type.Name);
            int offset = 0;
            if (menu)
            {
                var a = attributes[0];
                c.Require(a.AttributeType == typeof(CreateAssetMenuAttribute) && a.ConstructorArguments.Count == 0 &&
                    a.NamedArguments.Count == 2 && a.NamedArguments[0].MemberName == "fileName" &&
                    (string)Scalar(a.NamedArguments[0].TypedValue) == "TrackerCameraDefinition" &&
                    a.NamedArguments[1].MemberName == "menuName" &&
                    (string)Scalar(a.NamedArguments[1].TypedValue) == "HardlightProject/DefinitionData/Definitions/TrackerCameraDefinition",
                    "Original camera menu declaration changed.");
                offset = 1;
            }
            int[] expected = { first, second };
            for (int i = 0; i != expected.Length; ++i)
            {
                var a = attributes[offset + i];
                c.Require(a.AttributeType == typeof(Il2CppSetOptionAttribute) &&
                    a.AttributeType.Assembly.GetName().Name == "HLUnityCore.Runtime" &&
                    a.Constructor.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Option), typeof(object) }) &&
                    a.ConstructorArguments.Count == 2 && Convert.ToInt32(Scalar(a.ConstructorArguments[0])) == expected[i] &&
                    Scalar(a.ConstructorArguments[1]) is bool && !(bool)Scalar(a.ConstructorArguments[1]) && a.NamedArguments.Count == 0,
                    "Original IL2CPP option identity/order/value changed: " + type.Name + " index " + i);
            }
        }

        private static FieldInfo[] OrderedFields(Type type)
        {
            return type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
        }

        private static void Field(Checks c, FieldInfo field, string name, Type valueType, bool publicField, string tooltip, bool range)
        {
            c.Require(field.Name == name && field.FieldType == valueType &&
                field.Attributes == (publicField ? FieldAttributes.Public : FieldAttributes.Private),
                "Original field identity/order/type/mutability changed: " + name);
            var attributes = field.GetCustomAttributesData();
            c.Require(attributes.Count == (range || !publicField ? 2 : 1), "Original field attribute count changed: " + name);
            int tooltipIndex = 0;
            if (range)
            {
                var a = attributes[0];
                c.Require(a.AttributeType == typeof(RangeAttribute) && a.ConstructorArguments.Count == 2 &&
                    (float)Scalar(a.ConstructorArguments[0]) == -1f && (float)Scalar(a.ConstructorArguments[1]) == 1f &&
                    a.NamedArguments.Count == 0, "Original record range changed: " + name);
                tooltipIndex = 1;
            }
            var tip = attributes[tooltipIndex];
            c.Require(tip.AttributeType == typeof(TooltipAttribute) && tip.ConstructorArguments.Count == 1 &&
                (string)Scalar(tip.ConstructorArguments[0]) == tooltip && tip.NamedArguments.Count == 0,
                "Original exact field tooltip changed: " + name);
            if (!publicField)
                c.Require(attributes[1].AttributeType == typeof(SerializeField) &&
                    attributes[1].ConstructorArguments.Count == 0 && attributes[1].NamedArguments.Count == 0,
                    "Original private serialized field changed: " + name);
        }

        private static byte[] Body(Checks c, MethodBase method)
        {
            var body = method.GetMethodBody();
            c.Require(body != null && body.LocalVariables.Count == 0 && body.ExceptionHandlingClauses.Count == 0 &&
                !body.InitLocals && body.MaxStackSize == 8, "Original leaf body locals/EH/stack changed: " + method.Name);
            c.Require(!method.IsGenericMethod && method.GetParameters().Length == 0 && method.GetCustomAttributesData().Count == 0 &&
                method.GetMethodImplementationFlags() == MethodImplAttributes.IL,
                "Original leaf parameter/attribute/implementation contract changed: " + method.Name);
            return body.GetILAsByteArray();
        }

        private static int Token(byte[] il, int offset)
        {
            return il[offset] | il[offset + 1] << 8 | il[offset + 2] << 16 | il[offset + 3] << 24;
        }

        private static void Getter(Checks c, Type owner, string propertyName, FieldInfo field)
        {
            PropertyInfo property = owner.GetProperty(propertyName, Declared);
            c.Require(property != null && property.PropertyType == field.FieldType && property.Attributes == PropertyAttributes.None &&
                property.GetIndexParameters().Length == 0 && property.GetAccessors(true).Length == 1 &&
                property.GetCustomAttributesData().Count == 0, "Original getter-only property changed: " + propertyName);
            MethodInfo getter = property.GetGetMethod(true);
            // Unity Mono synthesizes Retval for reflection's rowless return parameter.
            // Stored return metadata has no flags; emission review checks it separately.
            c.Require(getter != null && getter.Name == "get_" + propertyName && (ushort)getter.Attributes == 2182 && getter.ReturnType == field.FieldType &&
                getter.ReturnParameter.Attributes == ParameterAttributes.Retval && getter.ReturnParameter.Position == -1 &&
                getter.ReturnParameter.ParameterType == getter.ReturnType && getter.ReturnParameter.GetCustomAttributesData().Count == 0,
                "Original getter flags/signature changed: " + propertyName);
            byte[] il = Body(c, getter);
            c.Require(il.Length == 7 && il[0] == 0x02 && il[1] == 0x7b && il[6] == 0x2a,
                "Original complete field getter CIL changed: " + propertyName);
            FieldInfo operand = getter.Module.ResolveField(Token(il, 2));
            c.Require(operand.Module == field.Module && operand.MetadataToken == field.MetadataToken &&
                operand.DeclaringType == owner && operand.FieldType == field.FieldType,
                "Getter no longer reads its exact declared field: " + propertyName);
        }

        private static void Constructor(Checks c, Type owner, Type baseType, int flags)
        {
            ConstructorInfo constructor = owner.GetConstructors(Declared).Single();
            c.Require((ushort)constructor.Attributes == flags && constructor.DeclaringType == owner,
                "Original constructor visibility/flags changed: " + owner.Name);
            byte[] il = Body(c, constructor);
            c.Require(il.Length == 7 && il[0] == 0x02 && il[1] == 0x28 && il[6] == 0x2a,
                "Original constructor acquired initialization or changed body: " + owner.Name);
            MethodBase operand = constructor.Module.ResolveMethod(Token(il, 2));
            c.Require(operand is ConstructorInfo && operand.DeclaringType == baseType && operand.GetParameters().Length == 0 &&
                operand.Module == baseType.Module, "Constructor base provider changed: " + owner.Name);
        }

        private static MethodInfo CameraCallback(Checks c, Type camera, FieldInfo forward, FieldInfo backward)
        {
            MethodInfo callback = camera.GetMethod("OnValidate", Declared);
            c.Require(callback != null && (ushort)callback.Attributes == 129 && callback.ReturnType == typeof(void),
                "Original private OnValidate declaration changed.");
            MethodInfo update = typeof(Settings).GetMethod("UpdateCachedValues", Declared);
            c.Require(update != null && update.DeclaringType == typeof(Settings) && (ushort)update.Attributes == 134 &&
                update.ReturnType == typeof(void) && update.GetParameters().Length == 0 && !update.IsVirtual,
                "Genuine settings provider API changed; no observation hook may be invented.");
            byte[] il = Body(c, callback);
            c.Require(il.Length == 23 && il[0] == 0x02 && il[1] == 0x7b && il[6] == 0x6f &&
                il[11] == 0x02 && il[12] == 0x7b && il[17] == 0x6f && il[22] == 0x2a,
                "Complete OnValidate must update forward before freshly loading backward.");
            FieldInfo first = callback.Module.ResolveField(Token(il, 2)), second = callback.Module.ResolveField(Token(il, 13));
            c.Require(first.Module == forward.Module && first.MetadataToken == forward.MetadataToken &&
                second.Module == backward.Module && second.MetadataToken == backward.MetadataToken,
                "OnValidate field identity/read order changed.");
            foreach (int offset in new[] { 7, 18 })
            {
                MethodBase call = callback.Module.ResolveMethod(Token(il, offset));
                c.Require(call.Module == update.Module && call.MetadataToken == update.MetadataToken && call.DeclaringType == typeof(Settings),
                    "OnValidate callback must target the genuine settings method.");
            }
            return callback;
        }

        public static int VerifyOriginalDeclarationsAndBodies()
        {
            var c = new Checks();
            Type camera = OriginalType(c, "TrackerCameraDefinition");
            Shape(c, camera, 1048577, typeof(ScriptableObject), 2, 2, 4);
            Options(c, camera, 2, 1, true);
            FieldInfo[] cameraFields = OrderedFields(camera);
            Field(c, cameraFields[0], "m_forwardHeadingOverride", typeof(Settings), false,
                "Free look camera override settings for the duration of tracking a surface forward.", false);
            Field(c, cameraFields[1], "m_backwardHeadingOverride", typeof(Settings), false,
                "Free look camera override settings for the duration of tracking a surface backward.", false);
            Getter(c, camera, "ForwardHeadingOverride", cameraFields[0]);
            Getter(c, camera, "BackwardHeadingOverride", cameraFields[1]);
            CameraCallback(c, camera, cameraFields[0], cameraFields[1]);
            Constructor(c, camera, typeof(ScriptableObject), 6278);

            Type end = OriginalType(c, "TrackerEndDefinition");
            Shape(c, end, 1048705, typeof(ScriptableObject), 1, 1, 2);
            Options(c, end, 1, 2, false);
            FieldInfo endField = OrderedFields(end).Single();
            Field(c, endField, "m_forceDirectionDistance", typeof(float), false,
                "Distance from end to force directional movement.", false);
            Getter(c, end, "ForceDirectionDistance", endField);
            Constructor(c, end, typeof(ScriptableObject), 6276);

            Type record = RestoreContract(c);
            Constructor(c, record, typeof(object), 6278);
            return c.Count;
        }

        private static Type RestoreContract(Checks c)
        {
            Type record = OriginalType(c, "VelocityRestoreParameters");
            Shape(c, record, 1056769, typeof(object), 8, 0, 1);
            c.Require(record.IsSerializable, "Original restore record must retain Serializable type flag.");
            Options(c, record, 1, 2, false);
            string[] names = { "RestoreIfGreater", "BlendDurationSeconds", "BlendX", "BlendY", "BlendZ",
                "MaximumSlopeAngle", "ExpireOnContact", "ExpireOnContactDotThreshold" };
            Type[] types = { typeof(bool), typeof(float), typeof(bool), typeof(bool), typeof(bool),
                typeof(float), typeof(LayerMask), typeof(float) };
            string[] tips = {
                "If stored velocity component is greater than the target then only restore if this is true.",
                "The maximum blend time towards stored velocity components.",
                "Blend back to stored X local velocity component.",
                "Blend back to stored Y local velocity component.",
                "Blend back to stored Z local velocity component.",
                "If slope angle exceeds this, then buff is expired.",
                "Expire buff if character makes contact with this layer and the dot of the contact normal is above the threshold.",
                "Used to check if contact normal is facing the same direction as the actor's forward (1 is facing exactly the same direction)."
            };
            FieldInfo[] fields = OrderedFields(record);
            for (int i = 0; i != fields.Length; ++i) Field(c, fields[i], names[i], types[i], true, tips[i], i == 7);
            return record;
        }

        private static void Near(Checks c, float actual, float expected, string message)
        {
            c.Require(!float.IsNaN(actual) && !float.IsInfinity(actual) && Math.Abs(actual - expected) <= 0.000001f, message);
        }

        private static void NullFault(Checks c, MethodInfo callback, ScriptableObject camera, string message)
        {
            bool failed = false;
            try { callback.Invoke(camera, null); }
            catch (TargetInvocationException e)
            {
                if (e.InnerException == null || e.InnerException.GetType() != typeof(NullReferenceException))
                    throw new InvalidOperationException(message + " had an unexpected inner exception.", e);
                failed = true;
            }
            c.Require(failed, message + " must fault through the original unguarded call.");
        }

        public static int VerifyOwnedCameraCacheAndFaultOrder()
        {
            var c = new Checks();
            Type type = OriginalType(c, "TrackerCameraDefinition");
            Shape(c, type, 1048577, typeof(ScriptableObject), 2, 2, 4);
            FieldInfo[] fields = OrderedFields(type);
            c.Require(fields[0].Name == "m_forwardHeadingOverride" && fields[1].Name == "m_backwardHeadingOverride" &&
                fields.All(f => f.FieldType == typeof(Settings) && f.Attributes == FieldAttributes.Private),
                "Owned camera setup must use the genuine exact two fields.");
            MethodInfo callback = CameraCallback(c, type, fields[0], fields[1]);
            PropertyInfo forwardGetter = type.GetProperty("ForwardHeadingOverride", Declared),
                backwardGetter = type.GetProperty("BackwardHeadingOverride", Declared);
            Getter(c, type, "ForwardHeadingOverride", fields[0]);
            Getter(c, type, "BackwardHeadingOverride", fields[1]);
            var forward = new Settings { MaximumAngleThreshold = 60f };
            var backward = new Settings { MaximumAngleThreshold = 180f };
            ScriptableObject camera = null;
            try
            {
                // Use normal Unity construction. Unexpected lifecycle logs remain failures.
                camera = ScriptableObject.CreateInstance(type);
                c.Require(camera != null && camera.GetType() == type, "Unity must create the exact original owned camera definition.");
                fields[0].SetValue(camera, forward);
                fields[1].SetValue(camera, backward);
                c.Require(ReferenceEquals(forwardGetter.GetValue(camera), forward) &&
                    ReferenceEquals(backwardGetter.GetValue(camera), backward), "Original getters must preserve independent object identities.");
                forward.MaximumAngleThreshold = 30f;
                c.Require(((Settings)forwardGetter.GetValue(camera)).MaximumAngleThreshold == 30f,
                    "Getter mutation must remain visible on the owned original reference.");
                fields[1].SetValue(camera, forward);
                c.Require(ReferenceEquals(forwardGetter.GetValue(camera), backwardGetter.GetValue(camera)),
                    "Original camera fields must retain an intentional shared reference.");
                fields[1].SetValue(camera, backward);
                forward.MaximumAngleThreshold = 60f;
                Near(c, forward.MaximumAngleThresholdCosine, 0f, "New real forward settings cache must start at zero.");
                Near(c, backward.MaximumAngleThresholdCosine, 0f, "New real backward settings cache must start at zero.");
                callback.Invoke(camera, null);
                Near(c, forward.MaximumAngleThresholdCosine, 0.5f, "Forward cache must be refreshed at sixty degrees.");
                Near(c, backward.MaximumAngleThresholdCosine, -1f, "Backward cache must be refreshed at one hundred eighty degrees.");
                forward.MaximumAngleThreshold = 90f;
                backward.MaximumAngleThreshold = 0f;
                callback.Invoke(camera, null);
                Near(c, forward.MaximumAngleThresholdCosine, 0f, "Forward dirty cache must refresh at ninety degrees.");
                Near(c, backward.MaximumAngleThresholdCosine, 1f, "Backward dirty cache must refresh at zero degrees.");

                // The real nonvirtual provider has no observer hook. Dirty cache values
                // witness the completed prefix without replacing provider dispatch.
                backward.MaximumAngleThreshold = 180f;
                fields[0].SetValue(camera, null);
                NullFault(c, callback, camera, "Null forward settings");
                Near(c, backward.MaximumAngleThresholdCosine, 1f, "Forward fault must prevent the later backward refresh.");
                forward.MaximumAngleThreshold = 0f;
                forward.UpdateCachedValues();
                Near(c, forward.MaximumAngleThresholdCosine, 1f, "Genuine provider must prepare a distinct stale forward cache.");
                forward.MaximumAngleThreshold = 180f;
                fields[0].SetValue(camera, forward);
                fields[1].SetValue(camera, null);
                NullFault(c, callback, camera, "Null backward settings");
                Near(c, forward.MaximumAngleThresholdCosine, -1f, "Forward refresh must complete before the later backward fault.");
                return c.Count;
            }
            finally
            {
                try
                {
                    if (camera != null)
                    {
                        fields[0].SetValue(camera, forward);
                        fields[1].SetValue(camera, backward);
                    }
                }
                finally { if (camera != null) UnityEngine.Object.DestroyImmediate(camera); }
            }
        }

        public static int VerifyRestoreRecordDefaultsAndMutability()
        {
            var c = new Checks();
            Type type = RestoreContract(c);
            Constructor(c, type, typeof(object), 6278);
            object record = Activator.CreateInstance(type);
            c.Require(record != null && record.GetType() == type, "Restore record must be constructed normally as its genuine original type.");
            FieldInfo[] fields = OrderedFields(type);
            foreach (FieldInfo field in fields)
            {
                object value = field.GetValue(record);
                if (field.FieldType == typeof(bool)) c.Require(!(bool)value, "Original boolean field must default false: " + field.Name);
                else if (field.FieldType == typeof(float)) c.Require((float)value == 0f, "Original scalar field must default zero: " + field.Name);
                else c.Require(field.FieldType == typeof(LayerMask) && ((LayerMask)value).value == 0,
                    "Original LayerMask field must default zero.");
            }
            object[] first = { true, 0.25f, true, false, false, 42f, (LayerMask)(1 << 7), -0.75f };
            object[] second = { false, 0.5f, true, true, false, 15f, (LayerMask)(1 << 2), 0.25f };
            object[] third = { true, 0.75f, false, true, true, 30f, (LayerMask)(1 << 12), -0.25f };
            foreach (object[] values in new[] { first, second, third })
            {
                for (int i = 0; i != fields.Length; ++i) fields[i].SetValue(record, values[i]);
                for (int i = 0; i != fields.Length; ++i)
                {
                    object actual = fields[i].GetValue(record);
                    bool equal = fields[i].FieldType == typeof(LayerMask)
                        ? ((LayerMask)actual).value == ((LayerMask)values[i]).value : actual.Equals(values[i]);
                    c.Require(equal, "Each original mutable record field must retain its independent assigned value: " + fields[i].Name);
                }
            }
            return c.Count;
        }
    }
}
