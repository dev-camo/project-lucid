using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using HardlightProject;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // These checks retain the two genuine reversal math APIs and bind the whole
    // seventeen-method movement owner plus complete original TransformUtils.
    // Engine cases own and destroy every Unity object.
    public static class OriginalCharacterTransformVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        public static int RunReversalMath()
        {
            int checks = 0;
            Direction(Vector3.forward, Vector3.forward, false, "same direction", ref checks);
            Direction(Vector3.forward, Vector3.back, true, "opposite direction", ref checks);
            Direction(Vector3.forward, Vector3.right, false, "exact orthogonal boundary", ref checks);
            Direction(Vector3.zero, Vector3.back, false, "zero vector", ref checks);
            Direction(new Vector3(3, 0, 0), new Vector3(-2, 0, 0), true, "scaled opposite vectors", ref checks);
            Direction(new Vector3(3, 0, 0), new Vector3(2, 0, 0), false, "scaled same vectors", ref checks);
            Direction(new Vector3(float.Epsilon, 0, 0), new Vector3(-float.Epsilon, 0, 0), false, "underflow to negative zero", ref checks);
            Direction(new Vector3(-0f, 0, 0), Vector3.right, false, "negative zero", ref checks);
            Direction(new Vector3(float.NaN, 0, 0), Vector3.right, false, "unordered dot", ref checks);
            Direction(new Vector3(float.PositiveInfinity, 0, 0), Vector3.right, false, "positive infinity", ref checks);
            Direction(new Vector3(float.PositiveInfinity, 0, 0), Vector3.left, true, "negative infinity", ref checks);
            Direction(new Vector3(float.PositiveInfinity, 0, 0), Vector3.zero, false, "infinity times zero", ref checks);
            Rotation(Quaternion.identity, Quaternion.identity, false, "same rotation", ref checks);
            Rotation(Quaternion.identity, new Quaternion(0, 1, 0, 0), true, "half yaw", ref checks);
            Rotation(Quaternion.identity, new Quaternion(1, 0, 0, 0), true, "half pitch", ref checks);
            Rotation(Quaternion.identity, new Quaternion(0, 0, 1, 0), false, "half roll", ref checks);
            Rotation(new Quaternion(0, 1, 0, 0), new Quaternion(0, -1, 0, 0), false, "antipodal representation", ref checks);
            Rotation(Quaternion.identity, new Quaternion(0, 0, 0, 0), false, "zero quaternion", ref checks);
            Rotation(Quaternion.identity, new Quaternion(0, 2, 0, 0), true, "nonunit quaternion remains unnormalized", ref checks);
            Rotation(Quaternion.identity, new Quaternion(0, 0.5f, 0, 0.5f), false, "scaled quarter rotation", ref checks);
            Rotation(Quaternion.identity, new Quaternion(float.NaN, 0, 0, 1), false, "unordered rotated vector", ref checks);
            Rotation(new Quaternion(0, 2, 0, 0), new Quaternion(0, 2, 0, 0), false, "same nonunit rotation", ref checks);
            return checks;
        }

        public static int RunReversalDeclarations()
        {
            int checks = 0;
            Type type = typeof(CharacterMovementUtilities);
            Require(type.FullName == "HardlightProject.CharacterMovementUtilities" && type.Assembly.GetName().Name == "Game.Runtime" && (int)type.Attributes == 1048961, "original movement utility identity and flags", ref checks);
            Require(type.GetFields(Own).Length == 0, "original fieldless utility", ref checks);
            Require(type.BaseType == typeof(object) && !type.IsGenericType && type.GetConstructors(Own).Length == 0 && type.GetProperties(Own).Length == 0 && type.GetEvents(Own).Length == 0 && type.GetNestedTypes(Own).Length == 0 && type.GetInterfaces().Length == 0, "original whole utility has no additional declaration categories", ref checks);
            MethodInfo[] methods = type.GetMethods(Own);
            var signatures = new Dictionary<string, string>
            {
                { "ProcessTurningMovement#7", "System.Void|0|HardlightProject.TurningMovementInput:input:0;UnityEngine.Vector3&:forwardVelocity:2;UnityEngine.Vector3&:tangentVelocity:2;UnityEngine.Vector3&:planeVelocity:0;UnityEngine.Vector3&:inputDirection:2;System.Single&:effectiveInputMagnitude:2;UnityEngine.Quaternion&:inputRotation:2" },
                { "UpdateStickyControls#2", "System.Void|0|HardlightProject.Character:character:0;System.Single&:turnAngle:0" },
                { "UpdateStickyInput#6", "System.Void|0|HardlightProject.Character:character:0;System.Single&:turnAngle:0;System.Boolean&:turnInputInverted:0;System.Single:inputMagnitude:0;System.Single:upDeg:0;System.Single:forwardDeg:0" },
                { "UpdateStickyCamera#6", "System.Void|0|HardlightProject.Character:character:0;System.Boolean&:turnCameraActive:0;UnityEngine.Quaternion&:turnCameraRotation:0;UnityEngine.Quaternion:cameraRotation:0;System.Single:inputMagnitude:0;System.Single:upDeg:0" },
                { "ProcessAirControl#6", "System.Void|1|HardlightProject.Character:character:0;HardlightProject.CharacterAbility_Movement`1<T>:ability:0;HardlightProject.CharacterAbilityDefinition_MovementAir:abilityDef:0;System.Boolean:maintainHeading:0;System.Single:deltaTime:0;System.Boolean:applyDeceleration:0" },
                { "CalculateIntendedTurnDelta#3", "System.Single|0|HardlightProject.Character:character:0;UnityEngine.Vector3:characterForward:0;System.Single&:turnAngle:0" },
                { "CalculateEffectiveInputMagnitude#3", "System.Single|0|System.Single:intendedMagnitude:0;System.Single:intendedTurnDelta:0;HardlightProject.CharacterTraits+TurnTraits:turnTraits:0" },
                { "ProcessDecelerationCurve#6", "System.Void|0|System.Single&:forwardSpeed:0;HardlightProject.CharacterAbilityDefinition+Motion:motion:0;UnityEngine.AnimationCurve:curve:0;System.Single:progress:0;System.Single:deltaTime:0;System.Single:motionMultiplier:4112" },
                { "AreDirectionsReversed#2", "System.Boolean|0|UnityEngine.Vector3:fromDirection:0;UnityEngine.Vector3:toDirection:0" },
                { "AreRotationsReversed#2", "System.Boolean|0|UnityEngine.Quaternion:fromRotation:0;UnityEngine.Quaternion:toRotation:0" },
                { "GetIntendedTurnAngle#2", "System.Single|0|HardlightProject.Character:character:0;UnityEngine.Vector2:intendedTurnVector:0" },
                { "GetIntendedTurnRotation#1", "UnityEngine.Quaternion|0|HardlightProject.Character:character:0" },
                { "GetIntendedTurnRotation#2", "UnityEngine.Quaternion|0|HardlightProject.Character:character:0;System.Single:intendedTurnAngle:0" },
                { "TryGetIntendedTurnRotation#4", "System.Boolean|0|HardlightProject.Character:character:0;System.Single&:intendedTurnAngle:2;UnityEngine.Quaternion&:intendedTurnRotation:2;System.Boolean:useRawInput:4112" },
                { "GetIntendedToCurrentForwardAngle#1", "System.Single|0|HardlightProject.Character:character:0" },
                { "GetIntendedToCameraForwardAngle#1", "System.Single|0|HardlightProject.Character:character:0" },
                { "GetIntendedForward#2", "UnityEngine.Vector3|0|HardlightProject.Character:character:0;UnityEngine.Vector2:intendedTurnVector:0" },
            };
            Require(methods.Length == 17 && methods.Select(x => x.Name + "#" + x.GetParameters().Length).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(signatures.Keys.OrderBy(x => x, StringComparer.Ordinal)), "all seventeen original declarations including both rotation overloads", ref checks);
            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            // Shipping metadata orders options2/1 then ExtensionAttribute. Genuine
            // managed extension compilation emits ExtensionAttribute first. Bind
            // this exact observed managed order; original order parity stays held.
            Require(attributes.Count == 3 && attributes[0].AttributeType == typeof(ExtensionAttribute) && attributes[0].AttributeType.Assembly.GetName().Name == "mscorlib" && attributes[0].Constructor.DeclaringType == typeof(ExtensionAttribute) && attributes[0].ConstructorArguments.Count == 0 && attributes[0].NamedArguments.Count == 0 && type.IsDefined(typeof(ExtensionAttribute), false), "complete natural extension type attribute in observed managed order", ref checks);
            for (int i = 1; i < attributes.Count; i++)
            {
                CustomAttributeData attribute = attributes[i];
                ParameterInfo[] constructor = attribute.Constructor.GetParameters();
                Require(attribute.AttributeType == typeof(Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute) && attribute.AttributeType.Assembly.GetName().Name == "HLUnityCore.Runtime" && constructor.Length == 2 && constructor[0].ParameterType == typeof(Unity.IL2CPP.CompilerServices.Option) && constructor[1].ParameterType == typeof(object), "original option attribute provider and declared constructor", ref checks);
                Require(attribute.ConstructorArguments.Count == 2 && attribute.ConstructorArguments[0].ArgumentType == typeof(Unity.IL2CPP.CompilerServices.Option) && (int)attribute.ConstructorArguments[0].Value == (i == 1 ? 2 : 1) && attribute.ConstructorArguments[1].ArgumentType == typeof(bool) && !(bool)attribute.ConstructorArguments[1].Value && attribute.NamedArguments.Count == 0, "ordered ArrayBoundsChecks and NullChecks options", ref checks);
            }
            var simpleTypes = new Dictionary<string, Type>
            {
                { "System.Void", typeof(void) }, { "System.Boolean", typeof(bool) }, { "System.Single", typeof(float) },
                { "UnityEngine.Vector2", typeof(Vector2) }, { "UnityEngine.Vector3", typeof(Vector3) }, { "UnityEngine.Quaternion", typeof(Quaternion) },
                { "UnityEngine.AnimationCurve", typeof(AnimationCurve) }, { "HardlightProject.Character", typeof(Character) },
                { "HardlightProject.TurningMovementInput", typeof(TurningMovementInput) },
                { "HardlightProject.CharacterTraits+TurnTraits", typeof(CharacterTraits.TurnTraits) },
                { "HardlightProject.CharacterAbilityDefinition+Motion", typeof(CharacterAbilityDefinition.Motion) },
                { "HardlightProject.CharacterAbilityDefinition_MovementAir", typeof(CharacterAbilityDefinition_MovementAir) }
            };
            foreach (MethodInfo method in methods)
            {
                ParameterInfo[] parameters = method.GetParameters();
                string key = method.Name + "#" + parameters.Length;
                bool privateMethod = method.Name == "UpdateStickyInput" || method.Name == "UpdateStickyCamera" || method.Name == "CalculateIntendedTurnDelta" || method.Name == "CalculateEffectiveInputMagnitude";
                bool extension = method.Name == "GetIntendedTurnAngle" || method.Name == "GetIntendedTurnRotation" || method.Name == "TryGetIntendedTurnRotation" || method.Name == "GetIntendedToCurrentForwardAngle" || method.Name == "GetIntendedToCameraForwardAngle" || method.Name == "GetIntendedForward";
                IList<CustomAttributeData> methodAttributes = method.GetCustomAttributesData();
                Require((int)method.Attributes == (privateMethod ? 145 : 150) && (int)method.GetMethodImplementationFlags() == 0 && method.ReturnParameter.GetCustomAttributesData().Count == 0 && methodAttributes.Count == (extension ? 1 : 0) && method.IsDefined(typeof(ExtensionAttribute), false) == extension && (!extension || (methodAttributes[0].AttributeType == typeof(ExtensionAttribute) && methodAttributes[0].AttributeType.Assembly.GetName().Name == "mscorlib" && methodAttributes[0].ConstructorArguments.Count == 0 && methodAttributes[0].NamedArguments.Count == 0)), method.Name + " original access, implementation and exact extension attributes", ref checks);
                Type[] generics = method.GetGenericArguments();
                Func<Type, bool> exactType = parameterType =>
                {
                    Type expected;
                    if (parameterType.IsByRef)
                    {
                        Type element = parameterType.GetElementType();
                        return simpleTypes.TryGetValue(element.FullName, out expected) && parameterType == expected.MakeByRefType();
                    }
                    if (parameterType.IsGenericType)
                        return parameterType.GetGenericTypeDefinition() == typeof(CharacterAbility_Movement<>) && parameterType.GetGenericArguments().SequenceEqual(generics);
                    return simpleTypes.TryGetValue(parameterType.FullName, out expected) && parameterType == expected;
                };
                string actual = TypeName(method.ReturnType) + "|" + generics.Length + "|" + string.Join(";", parameters.Select(x => TypeName(x.ParameterType) + ":" + x.Name + ":" + (int)x.Attributes));
                // Mono synthesizes one flag-derived Out or Optional attribute;
                // the recorded stored parameter custom-attribute rows are zero.
                bool parameterProjection = parameters.All(x =>
                {
                    IList<CustomAttributeData> projected = x.GetCustomAttributesData();
                    Type expected = x.Attributes == ParameterAttributes.Out ? typeof(System.Runtime.InteropServices.OutAttribute) : x.Attributes == (ParameterAttributes.Optional | ParameterAttributes.HasDefault) ? typeof(System.Runtime.InteropServices.OptionalAttribute) : null;
                    return expected == null ? projected.Count == 0 : projected.Count == 1 && projected[0].AttributeType == expected && expected.Assembly.GetName().Name == "mscorlib" && projected[0].Constructor.DeclaringType == expected && projected[0].Constructor.GetParameters().Length == 0 && projected[0].ConstructorArguments.Count == 0 && projected[0].NamedArguments.Count == 0;
                });
                bool parameterDefaults = parameters.All(x => x.HasDefaultValue ? ((key == "ProcessDecelerationCurve#6" && x.Name == "motionMultiplier" && x.DefaultValue is float && (float)x.DefaultValue == 1f) || (key == "TryGetIntendedTurnRotation#4" && x.Name == "useRawInput" && x.DefaultValue is bool && (bool)x.DefaultValue)) : (x.Name != "motionMultiplier" && x.Name != "useRawInput"));
                Require(signatures.ContainsKey(key) && signatures[key] == actual && exactType(method.ReturnType) && parameters.All(x => exactType(x.ParameterType)) && parameterProjection && parameterDefaults, method.Name + " exact original typed signature, all parameter rows and two defaults", ref checks);
                Require(method.Name == "ProcessAirControl" ? (method.IsGenericMethodDefinition && generics.Length == 1 && generics[0].Name == "T" && generics[0].GenericParameterPosition == 0 && generics[0].GenericParameterAttributes == GenericParameterAttributes.None && generics[0].GetCustomAttributesData().Count == 0 && generics[0].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(CharacterAbilityDefinition_Movement) })) : (!method.IsGenericMethod && generics.Length == 0), method.Name + " original generic declaration and constraint", ref checks);
            }
            return checks;
        }

        public static int RunTransformDeclarations()
        {
            int checks = 0;
            Type type = typeof(TransformUtils);
            Require(type.FullName == "Hardlight.TransformUtils" && type.Assembly.GetName().Name == "HLUnityCore.Runtime" && (int)type.Attributes == 1048961, "original complete transform utility identity", ref checks);
            FieldInfo[] fields = type.GetFields(Own);
            Require(fields.Length == 1 && fields[0].Name == "PathSeparator" && fields[0].FieldType == typeof(string) && (int)fields[0].Attributes == 32854 && (string)fields[0].GetRawConstantValue() == "/", "original single constant field", ref checks);
            Require(type.IsDefined(typeof(ExtensionAttribute), false), "natural original extension type attribute", ref checks);
            MethodInfo[] methods = type.GetMethods(Own);
            Require(methods.Length == 11, "all eleven original declarations", ref checks);
            var signatures = new Dictionary<string, string>
            {
                { "ResetTransformToIdentity", "System.Void|UnityEngine.Transform:transform" },
                { "CopyLocalTransform", "System.Void|UnityEngine.Transform:transformTo;UnityEngine.Transform:transformFrom" },
                { "ParentAndRetainLocalTransform", "System.Void|UnityEngine.Transform:child;UnityEngine.Transform:parent" },
                { "HasZeroScale", "System.Boolean|UnityEngine.GameObject:objectToCheck" },
                { "CalculateBoundingRect", "UnityEngine.Rect|UnityEngine.Transform:transform" },
                { "CalculateScreenRect", "UnityEngine.Rect|UnityEngine.Transform:transform;UnityEngine.Camera:camera" },
                { "SortImmediateChildrenOfType", "System.Void|UnityEngine.Transform:transform;System.Comparison`1<T>:comparison;System.Collections.Generic.List`1<T>:tempChildList" },
                { "GetImmediateChildrenOfType", "System.Void|UnityEngine.Transform:transform;System.Collections.Generic.List`1<T>:children" },
                { "IsParentOf", "System.Boolean|UnityEngine.Transform:parent;UnityEngine.Transform:child" },
                { "GetParentsSceneHierarchy", "System.String|UnityEngine.Transform:transform" },
                { "GetSceneHierarchy", "System.String|UnityEngine.Transform:transform" }
            };
            foreach (MethodInfo method in methods)
            {
                bool extension = method.Name == "IsParentOf" || method.Name == "GetParentsSceneHierarchy" || method.Name == "GetSceneHierarchy";
                Require((int)method.Attributes == 150 && method.IsDefined(typeof(ExtensionAttribute), false) == extension, method.Name + " original extension and access flags", ref checks);
                string actual = TypeName(method.ReturnType) + "|" + string.Join(";", method.GetParameters().Select(x => TypeName(x.ParameterType) + ":" + x.Name));
                Require(signatures.ContainsKey(method.Name) && signatures[method.Name] == actual && method.GetParameters().All(x => (int)x.Attributes == 0 && !x.HasDefaultValue), method.Name + " exact original signature", ref checks);
            }
            Type getT = type.GetMethod("GetImmediateChildrenOfType").GetGenericArguments()[0];
            Require(getT.Name == "T" && getT.GenericParameterAttributes == GenericParameterAttributes.None && getT.GetGenericParameterConstraints().Length == 0, "original unconstrained child collection", ref checks);
            Type sortT = type.GetMethod("SortImmediateChildrenOfType").GetGenericArguments()[0];
            Require(sortT.Name == "T" && sortT.GenericParameterAttributes == GenericParameterAttributes.None && sortT.GetGenericParameterConstraints().SequenceEqual(new[] { typeof(Component) }), "original Component sorting constraint", ref checks);
            return checks;
        }

        public static int RunLocalTransformOperations()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                Transform from = owned.Create("from").transform;
                Transform to = owned.Create("to").transform;
                from.localPosition = new Vector3(3, -4, 5);
                from.localRotation = new Quaternion(0, 1, 0, 0);
                from.localScale = new Vector3(2, 3, 4);
                TransformUtils.CopyLocalTransform(to, from);
                Require(Near(to.localPosition, new Vector3(3, -4, 5)), "copy local position", ref checks);
                Require(Near(to.localRotation, new Quaternion(0, 1, 0, 0)), "copy local rotation", ref checks);
                Require(Near(to.localScale, new Vector3(2, 3, 4)), "copy local scale", ref checks);
                Transform parent = owned.Create("new parent").transform;
                parent.position = new Vector3(40, 50, 60);
                parent.rotation = new Quaternion(0, 1, 0, 0);
                parent.localScale = new Vector3(5, 6, 7);
                Vector3 oldWorld = to.position;
                TransformUtils.ParentAndRetainLocalTransform(to, parent);
                Require(to.parent == parent, "actual parent changes", ref checks);
                Require(Near(to.localPosition, new Vector3(3, -4, 5)) && Near(to.localRotation, new Quaternion(0, 1, 0, 0)) && Near(to.localScale, new Vector3(2, 3, 4)), "all three locals survive reparenting", ref checks);
                Require(!Near(to.position, oldWorld), "world transform follows the new parent with retained locals", ref checks);
                TransformUtils.ParentAndRetainLocalTransform(to, null);
                Require(to.parent == null && Near(to.localPosition, new Vector3(3, -4, 5)) && Near(to.localScale, new Vector3(2, 3, 4)), "null parent detaches and retains locals", ref checks);
                TransformUtils.CopyLocalTransform(from, from);
                Require(Near(from.localPosition, new Vector3(3, -4, 5)) && Near(from.localScale, new Vector3(2, 3, 4)), "self-copy retains locals", ref checks);
                TransformUtils.ResetTransformToIdentity(to);
                Require(Near(to.localPosition, Vector3.zero) && Near(to.localRotation, Quaternion.identity) && Near(to.localScale, Vector3.one), "reset all three local identity values", ref checks);
            }
            return checks;
        }

        public static int RunHierarchyAndScale()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                Transform root = owned.Create("root").transform;
                Transform middle = owned.Create("middle").transform;
                Transform child = owned.Create("child").transform;
                Transform unrelated = owned.Create("unrelated").transform;
                middle.SetParent(root, false); child.SetParent(middle, false);
                Require(TransformUtils.IsParentOf(root, child), "recursive ancestor", ref checks);
                Require(TransformUtils.IsParentOf(middle, child), "immediate parent", ref checks);
                Require(!TransformUtils.IsParentOf(child, child), "self is not an ancestor", ref checks);
                Require(!TransformUtils.IsParentOf(unrelated, child), "unrelated transform", ref checks);
                Require(TransformUtils.IsParentOf(null, child) && TransformUtils.IsParentOf(null, root), "null parent matches the top hierarchy boundary", ref checks);
                Require(!TransformUtils.IsParentOf(root, null) && !TransformUtils.IsParentOf(null, null), "null child exits before parent comparison", ref checks);
                Require(root.GetParentsSceneHierarchy() == string.Empty && root.GetSceneHierarchy() == "root", "root path", ref checks);
                Require(child.GetParentsSceneHierarchy() == "root/middle/" && child.GetSceneHierarchy() == "root/middle/child", "complete ancestor order and separators", ref checks);
                middle.name = "middle/embedded";
                Require(child.GetSceneHierarchy() == "root/middle/embedded/child", "original names retain embedded separators", ref checks);
                Require(!TransformUtils.HasZeroScale(child.gameObject), "ordinary ancestor scales", ref checks);
                child.localScale = new Vector3(0, 1, 1);
                Require(!TransformUtils.HasZeroScale(child.gameObject), "one zero axis is not the whole zero vector", ref checks);
                child.localScale = new Vector3(0.000001f, 0.000001f, 0.000001f);
                Require(TransformUtils.HasZeroScale(child.gameObject), "Unity approximate whole-vector zero comparison", ref checks);
                child.localScale = new Vector3(0.0001f, 0.0001f, 0.0001f);
                Require(!TransformUtils.HasZeroScale(child.gameObject), "outside the whole-vector zero tolerance", ref checks);
                root.localScale = Vector3.zero;
                Require(TransformUtils.HasZeroScale(child.gameObject), "zero scale in an ancestor", ref checks);
            }
            return checks;
        }

        public static int RunBoundingAndScreenGeometry()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                RectTransform root = owned.CreateRect("rect root", new Vector2(20, 10), new Vector2(0.5f, 0.5f));
                Require(Near(TransformUtils.CalculateBoundingRect(root), new Rect(-10, -5, 20, 10)), "single local rect", ref checks);
                RectTransform inactive = owned.CreateRect("inactive child", new Vector2(8, 30), new Vector2(0.25f, 0.75f));
                inactive.SetParent(root, false); inactive.anchoredPosition = new Vector2(900, -700); inactive.localScale = new Vector3(7, 8, 9); inactive.gameObject.SetActive(false);
                Require(Near(TransformUtils.CalculateBoundingRect(root), new Rect(-10, -22.5f, 20, 30)), "inactive child participates using its raw local rect", ref checks);
                RectTransform grandchild = owned.CreateRect("grandchild", new Vector2(60, 2), new Vector2(0.75f, 0.5f));
                grandchild.SetParent(inactive, false); grandchild.anchoredPosition = new Vector2(-800, 600);
                Require(Near(TransformUtils.CalculateBoundingRect(root), new Rect(-45, -22.5f, 60, 30)), "descendant raw rect union ignores positions and scale", ref checks);
                Transform plain = owned.Create("plain transform").transform;
                root.SetParent(plain, false);
                Require(Near(TransformUtils.CalculateBoundingRect(plain), new Rect()), "plain transform returns zero despite rect descendants", ref checks);
                Camera camera = owned.Create("synthetic camera").AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 2;
                camera.pixelRect = new Rect(100, 200, 400, 200); camera.nearClipPlane = 0.3f; camera.farClipPlane = 100;
                camera.transform.position = new Vector3(0, 0, -10); camera.transform.rotation = Quaternion.identity;
                RectTransform screen = owned.CreateRect("screen rect", new Vector2(4, 2), new Vector2(0.5f, 0.5f));
                screen.position = new Vector3(1, 2, 0);
                Require(Near(TransformUtils.CalculateScreenRect(screen, camera), new Rect(280, 320, 80, 40), 0.01f), "fixed orthographic projection with nonzero pixel origin", ref checks);
                screen.rotation = new Quaternion(0, 0, 0.7071067811865475f, 0.7071067811865475f);
                Require(Near(TransformUtils.CalculateScreenRect(screen, camera), new Rect(300, 300, 40, 80), 0.01f), "all four rotated corners determine the screen bounds", ref checks);
                Require(Near(TransformUtils.CalculateScreenRect(screen, null), new Rect()), "missing supplied camera returns zero", ref checks);
                Require(Near(TransformUtils.CalculateScreenRect(plain, camera), new Rect()), "nonrect supplied transform returns zero", ref checks);
            }
            return checks;
        }

        public static int RunImmediateChildrenAndSorting()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                Transform root = owned.Create("collection root").transform;
                Transform skip = owned.Create("skip").transform; skip.SetParent(root, false);
                BoxCollider bravo = owned.Create("bravo").AddComponent<BoxCollider>(); bravo.transform.SetParent(root, false);
                BoxCollider alpha = owned.Create("alpha").AddComponent<BoxCollider>(); alpha.transform.SetParent(root, false); alpha.gameObject.SetActive(false);
                BoxCollider nested = owned.Create("nested").AddComponent<BoxCollider>(); nested.transform.SetParent(bravo.transform, false);
                var list = new List<BoxCollider> { nested };
                List<BoxCollider>.Enumerator oldEnumerator = list.GetEnumerator();
                TransformUtils.GetImmediateChildrenOfType(root, list);
                Require(list.Count == 3 && !ReferenceEquals(list[0], null) && list[0] == null && list[0].GetType() == typeof(BoxCollider) && ReferenceEquals(list[1], bravo) && ReferenceEquals(list[2], alpha), "Editor missing-component wrapper is CLR nonnull and is retained before the live immediate children", ref checks);
                RequireThrows<InvalidOperationException>(() => oldEnumerator.MoveNext(), "collection invalidates the previous list enumerator", ref checks);
                var transforms = new List<Transform>(); TransformUtils.GetImmediateChildrenOfType(root, transforms);
                Require(transforms.SequenceEqual(new[] { skip, bravo.transform, alpha.transform }), "all immediate transforms without descendants", ref checks);
                // The Editor returns a CLR-nonnull/Unity-null wrapper for skip's
                // missing BoxCollider. Preserve its admission above, then detach
                // skip so this successful sort compares only actual components.
                skip.SetParent(null, false);
                TransformUtils.SortImmediateChildrenOfType(root, (BoxCollider a, BoxCollider b) => string.CompareOrdinal(a.name, b.name), list);
                Require(list.SequenceEqual(new[] { alpha, bravo }), "temporary list is sorted", ref checks);
                Require(root.childCount == 2 && root.GetChild(0) == alpha.transform && root.GetChild(1) == bravo.transform && skip.parent == null, "real matching siblings move to sorted indices while the missing-component child is detached", ref checks);
                Require(nested.transform.parent == bravo.transform && nested.transform.GetSiblingIndex() == 0, "grandchild hierarchy is unchanged", ref checks);
                // Begin each fault with an unmatched sibling at index zero. A
                // premature matching-child reorder must change this input order.
                skip.SetParent(root, false);
                skip.SetSiblingIndex(0); bravo.transform.SetSiblingIndex(1); alpha.transform.SetSiblingIndex(2);
                list.Add(nested);
                RequireThrows<ArgumentNullException>(() => TransformUtils.SortImmediateChildrenOfType<BoxCollider>(root, null, list), "null comparison propagates after collection", ref checks);
                Require(list.Count == 3 && !ReferenceEquals(list[0], null) && list[0] == null && list[0].GetType() == typeof(BoxCollider) && ReferenceEquals(list[1], bravo) && ReferenceEquals(list[2], alpha) && root.GetChild(0) == skip && root.GetChild(1) == bravo.transform && root.GetChild(2) == alpha.transform, "null comparison retains the Editor wrapper and live collection order without sibling writes", ref checks);
                skip.SetSiblingIndex(0); alpha.transform.SetSiblingIndex(1); bravo.transform.SetSiblingIndex(2);
                list.Add(nested);
                RequireThrows<InvalidOperationException>(() => TransformUtils.SortImmediateChildrenOfType(root, (BoxCollider a, BoxCollider b) => { throw new InvalidOperationException("synthetic comparison fault"); }, list), "List.Sort comparison failure propagates", ref checks);
                Require(list.Count == 3 && !ReferenceEquals(list[0], null) && list[0] == null && list[0].GetType() == typeof(BoxCollider) && ReferenceEquals(list[1], alpha) && ReferenceEquals(list[2], bravo) && root.GetChild(0) == skip && root.GetChild(1) == alpha.transform && root.GetChild(2) == bravo.transform, "throwing comparison retains the Editor wrapper and current live children without sibling writes", ref checks);
            }
            return checks;
        }

        public static int RunNullAndDestroyedBoundaries()
        {
            int checks = 0;
            RequireThrows<NullReferenceException>(() => TransformUtils.ResetTransformToIdentity(null), "null reset", ref checks);
            RequireThrows<NullReferenceException>(() => TransformUtils.CopyLocalTransform(null, null), "null copy", ref checks);
            RequireThrows<NullReferenceException>(() => TransformUtils.ParentAndRetainLocalTransform(null, null), "null child parenting", ref checks);
            RequireThrows<NullReferenceException>(() => TransformUtils.HasZeroScale(null), "null zero-scale object", ref checks);
            RequireThrows<NullReferenceException>(() => TransformUtils.GetParentsSceneHierarchy(null), "null parent hierarchy", ref checks);
            RequireThrows<NullReferenceException>(() => TransformUtils.GetSceneHierarchy(null), "null full hierarchy", ref checks);
            var list = new List<Transform> { null };
            RequireThrows<NullReferenceException>(() => TransformUtils.GetImmediateChildrenOfType(null, list), "null transform child collection", ref checks);
            Require(list.Count == 0, "clear happens before the null transform fault", ref checks);
            RequireThrows<NullReferenceException>(() => TransformUtils.GetImmediateChildrenOfType<Transform>(null, null), "null child list", ref checks);
            using (var owned = new OwnedObjects())
            {
                RectTransform dead = owned.CreateRect("destroyed rect", new Vector2(4, 2), Vector2.one * 0.5f);
                Object.DestroyImmediate(dead.gameObject);
                Require(!ReferenceEquals(dead, null) && dead == null, "real destroyed Unity handle differs from managed null", ref checks);
                Require(Near(TransformUtils.CalculateBoundingRect(dead), new Rect()) && Near(TransformUtils.CalculateScreenRect(dead, null), new Rect()), "destroyed rect follows Unity null guards", ref checks);
                Require(!TransformUtils.IsParentOf(null, dead), "destroyed child exits the parent query", ref checks);
                RequireThrows<MissingReferenceException>(() => TransformUtils.ResetTransformToIdentity(dead), "destroyed transform access reaches real engine fault", ref checks);
                RectTransform live = owned.CreateRect("live rect", new Vector2(4, 2), Vector2.one * 0.5f);
                Camera deadCamera = owned.Create("destroyed camera").AddComponent<Camera>(); Object.DestroyImmediate(deadCamera.gameObject);
                Require(Near(TransformUtils.CalculateScreenRect(live, deadCamera), new Rect()), "destroyed supplied camera follows Unity null guard", ref checks);
            }
            return checks;
        }

        private static void Direction(Vector3 from, Vector3 to, bool expected, string label, ref int checks)
        { Require(CharacterMovementUtilities.AreDirectionsReversed(from, to) == expected, label, ref checks); }
        private static void Rotation(Quaternion from, Quaternion to, bool expected, string label, ref int checks)
        { Require(CharacterMovementUtilities.AreRotationsReversed(from, to) == expected, label, ref checks); }
        private static void Require(bool condition, string label, ref int checks)
        { if (!condition) throw new InvalidOperationException(label); checks++; }
        private static void RequireThrows<T>(Action action, string label, ref int checks) where T : Exception
        { bool caught = false; try { action(); } catch (T) { caught = true; } Require(caught, label, ref checks); }
        private static bool Near(Vector3 a, Vector3 b, float epsilon = 0.0001f)
        { return (a - b).sqrMagnitude <= epsilon * epsilon; }
        private static bool Near(Quaternion a, Quaternion b)
        { return Math.Abs(Quaternion.Dot(a, b)) >= 0.99999f; }
        private static bool Near(Rect a, Rect b, float epsilon = 0.0001f)
        { return Math.Abs(a.x - b.x) <= epsilon && Math.Abs(a.y - b.y) <= epsilon && Math.Abs(a.width - b.width) <= epsilon && Math.Abs(a.height - b.height) <= epsilon; }
        private static string TypeName(Type type)
        {
            if (type.IsGenericParameter) return type.Name;
            if (type.IsGenericType) return type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
            return type.FullName;
        }
        private sealed class OwnedObjects : IDisposable
        {
            private readonly List<GameObject> objects = new List<GameObject>();
            public GameObject Create(string name)
            { var created = new GameObject("ProjectLucid original utility fixture " + name); created.hideFlags = HideFlags.HideAndDontSave; objects.Add(created); created.name = name; return created; }
            public RectTransform CreateRect(string name, Vector2 size, Vector2 pivot)
            { var created = new GameObject(name, typeof(RectTransform)); created.hideFlags = HideFlags.HideAndDontSave; objects.Add(created); var rect = (RectTransform)created.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.zero; rect.pivot = pivot; rect.sizeDelta = size; return rect; }
            public void Dispose()
            { for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]); objects.Clear(); }
        }
    }
}
