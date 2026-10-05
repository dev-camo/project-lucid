using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded original dependency checks. Managed paths use genuine installed
    // engine assemblies but never invoke Transform, PhysX, or serializer icalls.
    public static class ActorCollisionLeavesVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static int checks;
        private static void Require(bool result, string label)
        {
            checks++;
            if (!result) throw new InvalidOperationException(label);
        }
        private static bool Close(float a, float b, float tolerance = 0.000001f)
        { return float.IsNaN(a) ? float.IsNaN(b) : Mathf.Abs(a-b) <= tolerance; }
        private static bool Same(Vector3 a, Vector3 b)
        { return a.x == b.x && a.y == b.y && a.z == b.z; }
        private static bool Same(Quaternion a, Quaternion b)
        { return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w; }
        private static bool Close(Quaternion a, Quaternion b)
        { return Close(a.x,b.x) && Close(a.y,b.y) && Close(a.z,b.z) && Close(a.w,b.w); }
        private static FieldInfo Field(Type type, string name) { return type.GetField(name, Own); }
        private static object Read(object target, string name) { return Field(target.GetType(), name).GetValue(target); }
        private static void Write(object target, string name, object value) { Field(target.GetType(), name).SetValue(target,value); }
        private static T Uninitialized<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
        private static string[] AttributeNames(MemberInfo member)
        {
            // Serializable is represented by the original type flag and appears
            // as a reflection pseudoattribute. Actual compiler metadata has only
            // CompilerGenerated on the backing fields; retain every other attribute.
            // This bounded declaration check grants no serialized layout approval.
            return member.GetCustomAttributesData().Select(a=>a.AttributeType.FullName)
                .Where(n=>n!="System.SerializableAttribute").ToArray();
        }
        private static string Value(CustomAttributeTypedArgument value)
        {
            Type type = value.Value as Type;
            return type == null ? Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) : type.FullName;
        }
        // Reflection orders named attribute members differently on .NET and Mono.
        // Compare their named values by ordinal name; native/source blobs remain
        // independently pinned and no raw metadata order approval is claimed.
        private static string[] AttributeValues(MemberInfo member)
        {
            return member.GetCustomAttributesData()
                .Where(a=>a.AttributeType.FullName!="System.SerializableAttribute")
                .Select(a=>a.AttributeType.FullName+"("+string.Join("|",a.ConstructorArguments.Select(Value))+")"+
                    "["+string.Join("|",a.NamedArguments.OrderBy(n=>n.MemberName,StringComparer.Ordinal).Select(n=>n.MemberName+"="+Value(n.TypedValue)))+"]").ToArray();
        }
        private static void Shape()
        {
            {
                Type type = typeof(Hardlight.ColliderData);
                Require(type.Assembly.GetName().Name == "HLUnityCore.Runtime" && type.BaseType.FullName == "UnityEngine.MonoBehaviour" && (int)type.Attributes == 1048577, "Hardlight.ColliderData complete type flags/base/assembly");
                Require(type.GetFields(Own).Length == 3, "Hardlight.ColliderData full field count");
                Require(type.GetMethods(Own).Length + type.GetConstructors(Own).Length == 7, "Hardlight.ColliderData full method count");
                Require(AttributeNames(type).SequenceEqual(new string[] {"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute","UnityEngine.RequireComponent"}), "Hardlight.ColliderData exact type attribute order");
                Require(AttributeValues(type).SequenceEqual(new string[] {"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]","UnityEngine.RequireComponent(UnityEngine.Collider)[]"}), "Hardlight.ColliderData authored type attribute values");
                FieldInfo f0 = Field(type, "m_ignoreMovement");
                Require(f0 != null && f0.FieldType.FullName == "System.Boolean" && (int)f0.Attributes == 1, "Hardlight.ColliderData.m_ignoreMovement field type/flags");
                Require(AttributeNames(f0).SequenceEqual(new string[] {"UnityEngine.SerializeField","UnityEngine.TooltipAttribute"}), "Hardlight.ColliderData.m_ignoreMovement exact authored attribute order");
                Require(AttributeValues(f0).SequenceEqual(new string[] {"UnityEngine.SerializeField()[]","UnityEngine.TooltipAttribute(Option to ignore a collider as tracked for moving.)[]"}), "Hardlight.ColliderData.m_ignoreMovement authored field attribute values");
                FieldInfo f1 = Field(type, "<HasMovement>k__BackingField");
                Require(f1 != null && f1.FieldType.FullName == "System.Boolean" && (int)f1.Attributes == 1, "Hardlight.ColliderData.<HasMovement>k__BackingField field type/flags");
                Require(AttributeNames(f1).SequenceEqual(new string[] {"System.Runtime.CompilerServices.CompilerGeneratedAttribute"}), "Hardlight.ColliderData.<HasMovement>k__BackingField exact authored attribute order");
                Require(AttributeValues(f1).SequenceEqual(new string[] {"System.Runtime.CompilerServices.CompilerGeneratedAttribute()[]"}), "Hardlight.ColliderData.<HasMovement>k__BackingField authored field attribute values");
                FieldInfo f2 = Field(type, "<MovementVelocity>k__BackingField");
                Require(f2 != null && f2.FieldType.FullName == "UnityEngine.Vector3" && (int)f2.Attributes == 1, "Hardlight.ColliderData.<MovementVelocity>k__BackingField field type/flags");
                Require(AttributeNames(f2).SequenceEqual(new string[] {"System.Runtime.CompilerServices.CompilerGeneratedAttribute"}), "Hardlight.ColliderData.<MovementVelocity>k__BackingField exact authored attribute order");
                Require(AttributeValues(f2).SequenceEqual(new string[] {"System.Runtime.CompilerServices.CompilerGeneratedAttribute()[]"}), "Hardlight.ColliderData.<MovementVelocity>k__BackingField authored field attribute values");
                Require(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[] {"m_ignoreMovement","<HasMovement>k__BackingField","<MovementVelocity>k__BackingField"}), "Hardlight.ColliderData ordered fields");
                MethodBase[] methods = type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Require(methods.Select(m=>m.Name).SequenceEqual(new string[] {"get_HasMovement","set_HasMovement","get_MovementVelocity","set_MovementVelocity","get_IgnoreMovement","SetMovement",".ctor"}), "Hardlight.ColliderData original method declaration order");
                {
                    ParameterInfo[] p = methods[0].GetParameters();
                    Require((int)methods[0].Attributes == 2182 && ((MethodInfo)methods[0]).ReturnType.FullName == "System.Boolean" && p.Length == 0, "Hardlight.ColliderData 0x060000fb exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[1].GetParameters();
                    Require((int)methods[1].Attributes == 2177 && ((MethodInfo)methods[1]).ReturnType.FullName == "System.Void" && p.Length == 1 && p[0].Name == "value" && p[0].ParameterType.FullName == "System.Boolean" && (int)p[0].Attributes == 0, "Hardlight.ColliderData 0x060000fc exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[2].GetParameters();
                    Require((int)methods[2].Attributes == 2182 && ((MethodInfo)methods[2]).ReturnType.FullName == "UnityEngine.Vector3" && p.Length == 0, "Hardlight.ColliderData 0x060000fd exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[3].GetParameters();
                    Require((int)methods[3].Attributes == 2177 && ((MethodInfo)methods[3]).ReturnType.FullName == "System.Void" && p.Length == 1 && p[0].Name == "value" && p[0].ParameterType.FullName == "UnityEngine.Vector3" && (int)p[0].Attributes == 0, "Hardlight.ColliderData 0x060000fe exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[4].GetParameters();
                    Require((int)methods[4].Attributes == 2182 && ((MethodInfo)methods[4]).ReturnType.FullName == "System.Boolean" && p.Length == 0, "Hardlight.ColliderData 0x060000ff exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[5].GetParameters();
                    Require((int)methods[5].Attributes == 134 && ((MethodInfo)methods[5]).ReturnType.FullName == "System.Void" && p.Length == 2 && p[0].Name == "hasMovement" && p[0].ParameterType.FullName == "System.Boolean" && (int)p[0].Attributes == 0 && p[1].Name == "movementVelocity" && p[1].ParameterType.FullName == "UnityEngine.Vector3" && (int)p[1].Attributes == 0, "Hardlight.ColliderData 0x06000100 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[6].GetParameters();
                    Require((int)methods[6].Attributes == 6278 && true && p.Length == 0, "Hardlight.ColliderData 0x06000101 exact source signature/flags");
                }
            }
            {
                Type type = typeof(HardlightProject.TransformFakeParent);
                Require(type.Assembly.GetName().Name == "Game.Runtime" && type.BaseType.FullName == "UnityEngine.MonoBehaviour" && (int)type.Attributes == 1048577, "HardlightProject.TransformFakeParent complete type flags/base/assembly");
                Require(type.GetFields(Own).Length == 4, "HardlightProject.TransformFakeParent full field count");
                Require(type.GetMethods(Own).Length + type.GetConstructors(Own).Length == 6, "HardlightProject.TransformFakeParent full method count");
                Require(AttributeNames(type).SequenceEqual(new string[] {"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute"}), "HardlightProject.TransformFakeParent exact type attribute order");
                Require(AttributeValues(type).SequenceEqual(new string[] {"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]"}), "HardlightProject.TransformFakeParent authored type attribute values");
                FieldInfo f0 = Field(type, "<IgnoreRotation>k__BackingField");
                Require(f0 != null && f0.FieldType.FullName == "System.Boolean" && (int)f0.Attributes == 1, "HardlightProject.TransformFakeParent.<IgnoreRotation>k__BackingField field type/flags");
                Require(AttributeNames(f0).SequenceEqual(new string[] {"System.Runtime.CompilerServices.CompilerGeneratedAttribute"}), "HardlightProject.TransformFakeParent.<IgnoreRotation>k__BackingField exact authored attribute order");
                Require(AttributeValues(f0).SequenceEqual(new string[] {"System.Runtime.CompilerServices.CompilerGeneratedAttribute()[]"}), "HardlightProject.TransformFakeParent.<IgnoreRotation>k__BackingField authored field attribute values");
                FieldInfo f1 = Field(type, "m_targetParent");
                Require(f1 != null && f1.FieldType.FullName == "UnityEngine.Transform" && (int)f1.Attributes == 1, "HardlightProject.TransformFakeParent.m_targetParent field type/flags");
                Require(AttributeNames(f1).SequenceEqual(new string[] {}), "HardlightProject.TransformFakeParent.m_targetParent exact authored attribute order");
                Require(AttributeValues(f1).SequenceEqual(new string[] {}), "HardlightProject.TransformFakeParent.m_targetParent authored field attribute values");
                FieldInfo f2 = Field(type, "m_translationOffset");
                Require(f2 != null && f2.FieldType.FullName == "UnityEngine.Vector3" && (int)f2.Attributes == 1, "HardlightProject.TransformFakeParent.m_translationOffset field type/flags");
                Require(AttributeNames(f2).SequenceEqual(new string[] {}), "HardlightProject.TransformFakeParent.m_translationOffset exact authored attribute order");
                Require(AttributeValues(f2).SequenceEqual(new string[] {}), "HardlightProject.TransformFakeParent.m_translationOffset authored field attribute values");
                FieldInfo f3 = Field(type, "m_rotationOffset");
                Require(f3 != null && f3.FieldType.FullName == "UnityEngine.Quaternion" && (int)f3.Attributes == 1, "HardlightProject.TransformFakeParent.m_rotationOffset field type/flags");
                Require(AttributeNames(f3).SequenceEqual(new string[] {}), "HardlightProject.TransformFakeParent.m_rotationOffset exact authored attribute order");
                Require(AttributeValues(f3).SequenceEqual(new string[] {}), "HardlightProject.TransformFakeParent.m_rotationOffset authored field attribute values");
                Require(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[] {"<IgnoreRotation>k__BackingField","m_targetParent","m_translationOffset","m_rotationOffset"}), "HardlightProject.TransformFakeParent ordered fields");
                MethodBase[] methods = type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Require(methods.Select(m=>m.Name).SequenceEqual(new string[] {"get_IgnoreRotation","set_IgnoreRotation","StartParenting","Evaluate","Stop",".ctor"}), "HardlightProject.TransformFakeParent original method declaration order");
                {
                    ParameterInfo[] p = methods[0].GetParameters();
                    Require((int)methods[0].Attributes == 2182 && ((MethodInfo)methods[0]).ReturnType.FullName == "System.Boolean" && p.Length == 0, "HardlightProject.TransformFakeParent 0x06002ea3 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[1].GetParameters();
                    Require((int)methods[1].Attributes == 2177 && ((MethodInfo)methods[1]).ReturnType.FullName == "System.Void" && p.Length == 1 && p[0].Name == "value" && p[0].ParameterType.FullName == "System.Boolean" && (int)p[0].Attributes == 0, "HardlightProject.TransformFakeParent 0x06002ea4 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[2].GetParameters();
                    Require((int)methods[2].Attributes == 134 && ((MethodInfo)methods[2]).ReturnType.FullName == "System.Void" && p.Length == 4 && p[0].Name == "targetParent" && p[0].ParameterType.FullName == "UnityEngine.Transform" && (int)p[0].Attributes == 0 && p[1].Name == "translationOffset" && p[1].ParameterType.FullName == "UnityEngine.Vector3" && (int)p[1].Attributes == 0 && p[2].Name == "rotationOffset" && p[2].ParameterType.FullName == "UnityEngine.Quaternion" && (int)p[2].Attributes == 0 && p[3].Name == "ignoreRotation" && p[3].ParameterType.FullName == "System.Boolean" && (int)p[3].Attributes == 0, "HardlightProject.TransformFakeParent 0x06002ea5 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[3].GetParameters();
                    Require((int)methods[3].Attributes == 134 && ((MethodInfo)methods[3]).ReturnType.FullName == "System.Boolean" && p.Length == 2 && p[0].Name == "position" && p[0].ParameterType.FullName == "UnityEngine.Vector3&" && (int)p[0].Attributes == 2 && p[1].Name == "rotation" && p[1].ParameterType.FullName == "UnityEngine.Quaternion&" && (int)p[1].Attributes == 2, "HardlightProject.TransformFakeParent 0x06002ea6 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[4].GetParameters();
                    Require((int)methods[4].Attributes == 134 && ((MethodInfo)methods[4]).ReturnType.FullName == "System.Void" && p.Length == 0, "HardlightProject.TransformFakeParent 0x06002ea7 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[5].GetParameters();
                    Require((int)methods[5].Attributes == 6278 && true && p.Length == 0, "HardlightProject.TransformFakeParent 0x06002ea8 exact source signature/flags");
                }
            }
            {
                Type type = typeof(HardlightProject.CollisionPlaneDefinition);
                Require(type.Assembly.GetName().Name == "Game.Runtime" && type.BaseType.FullName == "UnityEngine.ScriptableObject" && (int)type.Attributes == 1048577, "HardlightProject.CollisionPlaneDefinition complete type flags/base/assembly");
                Require(type.GetFields(Own).Length == 4, "HardlightProject.CollisionPlaneDefinition full field count");
                Require(type.GetMethods(Own).Length + type.GetConstructors(Own).Length == 3, "HardlightProject.CollisionPlaneDefinition full method count");
                Require(AttributeNames(type).SequenceEqual(new string[] {"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute","UnityEngine.CreateAssetMenuAttribute"}), "HardlightProject.CollisionPlaneDefinition exact type attribute order");
                Require(AttributeValues(type).SequenceEqual(new string[] {"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]","UnityEngine.CreateAssetMenuAttribute()[fileName=CollisionPlaneDefinition|menuName=HardlightProject/DefinitionData/Definitions/CollisionPlaneDefinition]"}), "HardlightProject.CollisionPlaneDefinition authored type attribute values");
                FieldInfo f0 = Field(type, "Normal");
                Require(f0 != null && f0.FieldType.FullName == "UnityEngine.Vector3" && (int)f0.Attributes == 6, "HardlightProject.CollisionPlaneDefinition.Normal field type/flags");
                Require(AttributeNames(f0).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute"}), "HardlightProject.CollisionPlaneDefinition.Normal exact authored attribute order");
                Require(AttributeValues(f0).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute(Vector that describes the plane's normal.)[]"}), "HardlightProject.CollisionPlaneDefinition.Normal authored field attribute values");
                FieldInfo f1 = Field(type, "Angle");
                Require(f1 != null && f1.FieldType.FullName == "System.Single" && (int)f1.Attributes == 6, "HardlightProject.CollisionPlaneDefinition.Angle field type/flags");
                Require(AttributeNames(f1).SequenceEqual(new string[] {"UnityEngine.RangeAttribute","UnityEngine.TooltipAttribute"}), "HardlightProject.CollisionPlaneDefinition.Angle exact authored attribute order");
                Require(AttributeValues(f1).SequenceEqual(new string[] {"UnityEngine.RangeAttribute(0|90)[]","UnityEngine.TooltipAttribute(The threshold angle that validates the plane. 90 defines the entire plane.)[]"}), "HardlightProject.CollisionPlaneDefinition.Angle authored field attribute values");
                FieldInfo f2 = Field(type, "CollisionOverrides");
                Require(f2 != null && f2.FieldType.FullName == "HardlightProject.CollisionPlaneDefinition+CollisionMotion[]" && (int)f2.Attributes == 6, "HardlightProject.CollisionPlaneDefinition.CollisionOverrides field type/flags");
                Require(AttributeNames(f2).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute"}), "HardlightProject.CollisionPlaneDefinition.CollisionOverrides exact authored attribute order");
                Require(AttributeValues(f2).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute(Logic for motion towards the plane.)[]"}), "HardlightProject.CollisionPlaneDefinition.CollisionOverrides authored field attribute values");
                FieldInfo f3 = Field(type, "m_angleCosine");
                Require(f3 != null && f3.FieldType.FullName == "System.Single" && (int)f3.Attributes == 1, "HardlightProject.CollisionPlaneDefinition.m_angleCosine field type/flags");
                Require(AttributeNames(f3).SequenceEqual(new string[] {"UnityEngine.HideInInspector","UnityEngine.SerializeField"}), "HardlightProject.CollisionPlaneDefinition.m_angleCosine exact authored attribute order");
                Require(AttributeValues(f3).SequenceEqual(new string[] {"UnityEngine.HideInInspector()[]","UnityEngine.SerializeField()[]"}), "HardlightProject.CollisionPlaneDefinition.m_angleCosine authored field attribute values");
                Require(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[] {"Normal","Angle","CollisionOverrides","m_angleCosine"}), "HardlightProject.CollisionPlaneDefinition ordered fields");
                MethodBase[] methods = type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Require(methods.Select(m=>m.Name).SequenceEqual(new string[] {"get_AngleCosine","OnValidate",".ctor"}), "HardlightProject.CollisionPlaneDefinition original method declaration order");
                {
                    ParameterInfo[] p = methods[0].GetParameters();
                    Require((int)methods[0].Attributes == 2182 && ((MethodInfo)methods[0]).ReturnType.FullName == "System.Single" && p.Length == 0, "HardlightProject.CollisionPlaneDefinition 0x06001c21 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[1].GetParameters();
                    Require((int)methods[1].Attributes == 129 && ((MethodInfo)methods[1]).ReturnType.FullName == "System.Void" && p.Length == 0, "HardlightProject.CollisionPlaneDefinition 0x06001c22 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[2].GetParameters();
                    Require((int)methods[2].Attributes == 6278 && true && p.Length == 0, "HardlightProject.CollisionPlaneDefinition 0x06001c23 exact source signature/flags");
                }
            }
            {
                Type type = typeof(HardlightProject.CollisionPlaneDefinition.CollisionMotion);
                Require(type.Assembly.GetName().Name == "Game.Runtime" && type.BaseType.FullName == "System.Object" && (int)type.Attributes == 1056770, "HardlightProject.CollisionPlaneDefinition+CollisionMotion complete type flags/base/assembly");
                Require(type.GetFields(Own).Length == 8, "HardlightProject.CollisionPlaneDefinition+CollisionMotion full field count");
                Require(type.GetMethods(Own).Length + type.GetConstructors(Own).Length == 5, "HardlightProject.CollisionPlaneDefinition+CollisionMotion full method count");
                Require(AttributeNames(type).SequenceEqual(new string[] {"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion exact type attribute order");
                Require(AttributeValues(type).SequenceEqual(new string[] {"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion authored type attribute values");
                FieldInfo f0 = Field(type, "Axis");
                Require(f0 != null && f0.FieldType.FullName == "UnityEngine.Vector3" && (int)f0.Attributes == 6, "HardlightProject.CollisionPlaneDefinition+CollisionMotion.Axis field type/flags");
                Require(AttributeNames(f0).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.Axis exact authored attribute order");
                Require(AttributeValues(f0).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute(Axis to evaluate angle around.)[]"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.Axis authored field attribute values");
                FieldInfo f1 = Field(type, "AngleMin");
                Require(f1 != null && f1.FieldType.FullName == "System.Single" && (int)f1.Attributes == 6, "HardlightProject.CollisionPlaneDefinition+CollisionMotion.AngleMin field type/flags");
                Require(AttributeNames(f1).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute","UnityEngine.RangeAttribute"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.AngleMin exact authored attribute order");
                Require(AttributeValues(f1).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute(Minimum angle of range from 0 inclusive to 90 exclusive. Angle is reflected so 90 encompasses the entire plane.)[]","UnityEngine.RangeAttribute(0|90)[]"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.AngleMin authored field attribute values");
                FieldInfo f2 = Field(type, "AngleMax");
                Require(f2 != null && f2.FieldType.FullName == "System.Single" && (int)f2.Attributes == 6, "HardlightProject.CollisionPlaneDefinition+CollisionMotion.AngleMax field type/flags");
                Require(AttributeNames(f2).SequenceEqual(new string[] {"UnityEngine.RangeAttribute","UnityEngine.TooltipAttribute"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.AngleMax exact authored attribute order");
                Require(AttributeValues(f2).SequenceEqual(new string[] {"UnityEngine.RangeAttribute(0|90)[]","UnityEngine.TooltipAttribute(Maximum angle of range from 0 inclusive to 90 exclusive. Angle is reflected so 90 encompasses the entire plane.)[]"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.AngleMax authored field attribute values");
                FieldInfo f3 = Field(type, "OrientateHeadingToPlane");
                Require(f3 != null && f3.FieldType.FullName == "System.Boolean" && (int)f3.Attributes == 6, "HardlightProject.CollisionPlaneDefinition+CollisionMotion.OrientateHeadingToPlane field type/flags");
                Require(AttributeNames(f3).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.OrientateHeadingToPlane exact authored attribute order");
                Require(AttributeValues(f3).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute(Allows character to orientate their forward motion to the plane.)[]"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.OrientateHeadingToPlane authored field attribute values");
                FieldInfo f4 = Field(type, "OrientateBodyToGravityThresholdAngle");
                Require(f4 != null && f4.FieldType.FullName == "System.Single" && (int)f4.Attributes == 6, "HardlightProject.CollisionPlaneDefinition+CollisionMotion.OrientateBodyToGravityThresholdAngle field type/flags");
                Require(AttributeNames(f4).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute","UnityEngine.RangeAttribute"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.OrientateBodyToGravityThresholdAngle exact authored attribute order");
                Require(AttributeValues(f4).SequenceEqual(new string[] {"UnityEngine.TooltipAttribute(Character's rigid body feet will orientate to the plane if it is within the angle of the current gravity.)[]","UnityEngine.RangeAttribute(0|90)[]"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.OrientateBodyToGravityThresholdAngle authored field attribute values");
                FieldInfo f5 = Field(type, "m_angleMinCosine");
                Require(f5 != null && f5.FieldType.FullName == "System.Single" && (int)f5.Attributes == 1, "HardlightProject.CollisionPlaneDefinition+CollisionMotion.m_angleMinCosine field type/flags");
                Require(AttributeNames(f5).SequenceEqual(new string[] {"UnityEngine.HideInInspector","UnityEngine.SerializeField"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.m_angleMinCosine exact authored attribute order");
                Require(AttributeValues(f5).SequenceEqual(new string[] {"UnityEngine.HideInInspector()[]","UnityEngine.SerializeField()[]"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.m_angleMinCosine authored field attribute values");
                FieldInfo f6 = Field(type, "m_angleMaxCosine");
                Require(f6 != null && f6.FieldType.FullName == "System.Single" && (int)f6.Attributes == 1, "HardlightProject.CollisionPlaneDefinition+CollisionMotion.m_angleMaxCosine field type/flags");
                Require(AttributeNames(f6).SequenceEqual(new string[] {"UnityEngine.HideInInspector","UnityEngine.SerializeField"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.m_angleMaxCosine exact authored attribute order");
                Require(AttributeValues(f6).SequenceEqual(new string[] {"UnityEngine.HideInInspector()[]","UnityEngine.SerializeField()[]"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.m_angleMaxCosine authored field attribute values");
                FieldInfo f7 = Field(type, "m_orientateBodyToGravityThresholdAngleCosine");
                Require(f7 != null && f7.FieldType.FullName == "System.Single" && (int)f7.Attributes == 1, "HardlightProject.CollisionPlaneDefinition+CollisionMotion.m_orientateBodyToGravityThresholdAngleCosine field type/flags");
                Require(AttributeNames(f7).SequenceEqual(new string[] {"UnityEngine.HideInInspector","UnityEngine.SerializeField"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.m_orientateBodyToGravityThresholdAngleCosine exact authored attribute order");
                Require(AttributeValues(f7).SequenceEqual(new string[] {"UnityEngine.HideInInspector()[]","UnityEngine.SerializeField()[]"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion.m_orientateBodyToGravityThresholdAngleCosine authored field attribute values");
                Require(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[] {"Axis","AngleMin","AngleMax","OrientateHeadingToPlane","OrientateBodyToGravityThresholdAngle","m_angleMinCosine","m_angleMaxCosine","m_orientateBodyToGravityThresholdAngleCosine"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion ordered fields");
                MethodBase[] methods = type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Require(methods.Select(m=>m.Name).SequenceEqual(new string[] {"get_AngleMinCosine","get_AngleMaxCosine","get_OrientateBodyToGravityThresholdAngleCosine","RefreshCache",".ctor"}), "HardlightProject.CollisionPlaneDefinition+CollisionMotion original method declaration order");
                {
                    ParameterInfo[] p = methods[0].GetParameters();
                    Require((int)methods[0].Attributes == 2182 && ((MethodInfo)methods[0]).ReturnType.FullName == "System.Single" && p.Length == 0, "HardlightProject.CollisionPlaneDefinition+CollisionMotion 0x06001c24 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[1].GetParameters();
                    Require((int)methods[1].Attributes == 2182 && ((MethodInfo)methods[1]).ReturnType.FullName == "System.Single" && p.Length == 0, "HardlightProject.CollisionPlaneDefinition+CollisionMotion 0x06001c25 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[2].GetParameters();
                    Require((int)methods[2].Attributes == 2182 && ((MethodInfo)methods[2]).ReturnType.FullName == "System.Single" && p.Length == 0, "HardlightProject.CollisionPlaneDefinition+CollisionMotion 0x06001c26 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[3].GetParameters();
                    Require((int)methods[3].Attributes == 134 && ((MethodInfo)methods[3]).ReturnType.FullName == "System.Void" && p.Length == 0, "HardlightProject.CollisionPlaneDefinition+CollisionMotion 0x06001c27 exact source signature/flags");
                }
                {
                    ParameterInfo[] p = methods[4].GetParameters();
                    Require((int)methods[4].Attributes == 6278 && true && p.Length == 0, "HardlightProject.CollisionPlaneDefinition+CollisionMotion 0x06001c28 exact source signature/flags");
                }
            }
        }

        public static int RunManaged()
        {
            checks = 0;
            Shape();
            ColliderData collider = Uninitialized<ColliderData>();
            Require(!collider.HasMovement && !collider.IgnoreMovement && Same(collider.MovementVelocity, Vector3.zero), "collider zero state without engine constructor");
            collider.SetMovement(true, new Vector3(3f,-2f,9f));
            Require(collider.HasMovement && Same(collider.MovementVelocity,new Vector3(3f,-2f,9f)), "moving collider stores velocity");
            collider.SetMovement(false, new Vector3(-8f,1f,4f));
            Require(!collider.HasMovement && Same(collider.MovementVelocity,new Vector3(-8f,1f,4f)), "inactive collider keeps caller velocity");
            Write(collider,"m_ignoreMovement",true);
            collider.SetMovement(true,new Vector3(4f,7f,-3f));
            Require(collider.IgnoreMovement && collider.HasMovement && Same(collider.MovementVelocity,new Vector3(4f,7f,-3f)), "ignore flag does not gate movement storage");
            collider.SetMovement(false,new Vector3(float.NaN,float.PositiveInfinity,float.NegativeInfinity));
            Require(!collider.HasMovement && float.IsNaN(collider.MovementVelocity.x) && float.IsPositiveInfinity(collider.MovementVelocity.y) && float.IsNegativeInfinity(collider.MovementVelocity.z), "collider retains nonfinite components");

            TransformFakeParent parent = Uninitialized<TransformFakeParent>();
            Require(!parent.IgnoreRotation && Read(parent,"m_targetParent")==null && Same((Vector3)Read(parent,"m_translationOffset"),Vector3.zero) && Same((Quaternion)Read(parent,"m_rotationOffset"),new Quaternion(0f,0f,0f,0f)), "fake parent original zero constructor fields");
            Quaternion offset = new Quaternion(2f,-3f,4f,5f);
            parent.StartParenting(null,new Vector3(3f,-2f,9f),offset,true);
            Require(parent.IgnoreRotation && Read(parent,"m_targetParent")==null, "start stores null target and flag");
            Require(Same((Vector3)Read(parent,"m_translationOffset"),new Vector3(3f,-2f,9f)) && Same((Quaternion)Read(parent,"m_rotationOffset"),offset), "start stores raw offsets without normalization");
            Vector3 position; Quaternion rotation;
            Require(!parent.Evaluate(out position,out rotation) && Same(position,Vector3.zero) && Same(rotation,Quaternion.identity), "null parent evaluates false and resets outs");
            parent.Stop();
            Require(Read(parent,"m_targetParent")==null && Same((Vector3)Read(parent,"m_translationOffset"),Vector3.zero) && Same((Quaternion)Read(parent,"m_rotationOffset"),Quaternion.identity), "stop clears target and restores identity offset");
            Require(parent.IgnoreRotation, "stop retains ignore rotation flag");
            parent.Stop();
            Require(parent.IgnoreRotation && !parent.Evaluate(out position,out rotation) && Same(position,Vector3.zero) && Same(rotation,Quaternion.identity), "stop is repeatable and inactive evaluation deterministic");
            parent.StartParenting(null,new Vector3(float.NaN,float.PositiveInfinity,-8f),offset,false);
            Require(!parent.IgnoreRotation && float.IsNaN(((Vector3)Read(parent,"m_translationOffset")).x) && float.IsPositiveInfinity(((Vector3)Read(parent,"m_translationOffset")).y), "start replaces flag and keeps nonfinite offset");
            Require(!parent.Evaluate(out position,out rotation) && Same(position,Vector3.zero) && Same(rotation,Quaternion.identity), "inactive evaluation ignores stale nonfinite offsets");

            CollisionPlaneDefinition.CollisionMotion motion = new CollisionPlaneDefinition.CollisionMotion();
            Require(Same(motion.Axis,Vector3.zero) && motion.AngleMin==0f && motion.AngleMax==0f && !motion.OrientateHeadingToPlane && motion.OrientateBodyToGravityThresholdAngle==0f, "motion defaults remain unconfigured");
            Require(motion.AngleMinCosine==0f && motion.AngleMaxCosine==0f && motion.OrientateBodyToGravityThresholdAngleCosine==0f, "motion constructor does not refresh cosine cache");
            motion.RefreshCache();
            Require(motion.AngleMinCosine==1f && motion.AngleMaxCosine==1f && motion.OrientateBodyToGravityThresholdAngleCosine==1f, "zero angles refresh to one");
            motion.Axis = new Vector3(2f,0f,4f); motion.OrientateHeadingToPlane=true;
            motion.AngleMin=60f; motion.AngleMax=30f; motion.OrientateBodyToGravityThresholdAngle=45f;
            Require(motion.AngleMinCosine==1f && motion.AngleMaxCosine==1f, "angle writes do not refresh cached values");
            motion.RefreshCache();
            Require(Close(motion.AngleMinCosine,0.5f) && Close(motion.AngleMaxCosine,0.8660254f) && Close(motion.OrientateBodyToGravityThresholdAngleCosine,0.70710677f), "independent unsorted angle caches");
            Require(Same(motion.Axis,new Vector3(2f,0f,4f)) && motion.OrientateHeadingToPlane, "refresh leaves axis and heading flag unchanged");
            float[] angles = { -720f,-180f,-135f,-90f,-60f,-0f,0f,30f,89.9f,90f,120f,180f,270f,360f,10000000f,float.NaN,float.PositiveInfinity,float.NegativeInfinity };
            foreach(float angle in angles)
            {
                motion.AngleMin=angle; motion.AngleMax=-angle; motion.OrientateBodyToGravityThresholdAngle=angle;
                motion.RefreshCache();
                float expected = (float)Math.Abs(Math.Cos(SingleRadians(angle)));
                Require(Close(motion.AngleMinCosine,expected) && Close(motion.AngleMaxCosine,expected) && Close(motion.OrientateBodyToGravityThresholdAngleCosine,expected), "absolute angle cache including out-of-range/nonfinite " + angle);
            }
            CollisionPlaneDefinition plane = Uninitialized<CollisionPlaneDefinition>();
            Require(Same(plane.Normal,Vector3.zero) && plane.Angle==0f && plane.CollisionOverrides==null && plane.AngleCosine==0f, "plane retains original zero state without native constructor");
            MethodInfo validate = typeof(CollisionPlaneDefinition).GetMethod("OnValidate",Own);
            plane.Normal = new Vector3(3f,-2f,7f); plane.Angle=120f; plane.CollisionOverrides=new CollisionPlaneDefinition.CollisionMotion[0];
            validate.Invoke(plane,null);
            Require(Close(plane.AngleCosine,-0.5f), "outer plane cache keeps signed cosine beyond inspector range");
            Require(Same(plane.Normal,new Vector3(3f,-2f,7f)) && plane.Angle==120f, "validation does not normalize normal or clamp angle");
            CollisionPlaneDefinition.CollisionMotion first = new CollisionPlaneDefinition.CollisionMotion { AngleMin=60f, AngleMax=120f, OrientateBodyToGravityThresholdAngle=180f };
            CollisionPlaneDefinition.CollisionMotion last = new CollisionPlaneDefinition.CollisionMotion { AngleMin=30f, AngleMax=45f, OrientateBodyToGravityThresholdAngle=90f };
            plane.Angle=60f; plane.CollisionOverrides=new[]{first,last}; validate.Invoke(plane,null);
            Require(Close(plane.AngleCosine,0.5f) && Close(first.AngleMinCosine,0.5f) && Close(first.AngleMaxCosine,0.5f) && Close(first.OrientateBodyToGravityThresholdAngleCosine,1f), "outer validation refreshes first complete override");
            Require(Close(last.AngleMinCosine,0.8660254f) && Close(last.AngleMaxCosine,0.70710677f) && Close(last.OrientateBodyToGravityThresholdAngleCosine,0f), "outer validation refreshes last complete override");
            plane.CollisionOverrides=null; plane.Angle=180f;
            Require(ValidationNullFails(validate,plane) && Close(plane.AngleCosine,-1f), "null override array fails after outer cache write");
            first.AngleMin=0f; last.AngleMin=0f; plane.CollisionOverrides=new[]{first,null,last}; plane.Angle=30f;
            Require(ValidationNullFails(validate,plane), "null entry is not skipped");
            Require(first.AngleMinCosine==1f && Close(last.AngleMinCosine,0.8660254f) && Close(plane.AngleCosine,0.8660254f), "null entry preserves prefix refresh and prior suffix cache");
            plane.CollisionOverrides=new[]{first,first}; first.AngleMin=120f; validate.Invoke(plane,null);
            Require(Close(first.AngleMinCosine,0.5f) && ReferenceEquals(plane.CollisionOverrides[0],plane.CollisionOverrides[1]), "duplicate override identity retained");
            foreach(float angle in angles)
            {
                plane.Angle=angle; plane.CollisionOverrides=new CollisionPlaneDefinition.CollisionMotion[0]; validate.Invoke(plane,null);
                float expected=(float)Math.Cos(SingleRadians(angle));
                Require(Close(plane.AngleCosine,expected), "signed plane cache including out-of-range/nonfinite " + angle);
            }
            return checks;
        }
        // The original ARM multiply is single precision before the cosf call.
        // Mono may retain excess precision when a float expression is immediately
        // promoted to double. Materialize its IEEE Single bits for the independent
        // System.Math reference; keep the original code and tolerance unchanged.
        private static double SingleRadians(float angle)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(angle * 0.017453292f), 0);
        }

        private static bool ValidationNullFails(MethodInfo validate, CollisionPlaneDefinition plane)
        {
            try { validate.Invoke(plane,null); }
            catch(TargetInvocationException e) { return e.InnerException is NullReferenceException; }
            return false;
        }

        // These fixtures require the real engine and are deliberately not run by
        // standalone CLR proofs. Parent scale, quaternion multiplication order,
        // Unity fake-null, and native component creation are concrete boundaries.
        public static int RunEngine()
        {
            checks=0;
            GameObject carrier = new GameObject("Lucid original fake parent fixture");
            GameObject target = new GameObject("Lucid original parent target fixture");
            GameObject colliderObject = new GameObject("Lucid original collider data fixture");
            try
            {
                TransformFakeParent parent = carrier.AddComponent<TransformFakeParent>();
                Require(!parent.IgnoreRotation && Same((Quaternion)Read(parent,"m_rotationOffset"),new Quaternion(0f,0f,0f,0f)), "native fake parent constructor remains zero initialized");
                Collider box = colliderObject.AddComponent<BoxCollider>();
                ColliderData collider = colliderObject.AddComponent<ColliderData>();
                Require(colliderObject.GetComponent<Collider>()==box && !collider.HasMovement && !collider.IgnoreMovement && Same(collider.MovementVelocity,Vector3.zero), "genuine collider component and defaults");
                collider.SetMovement(true,new Vector3(1f,2f,3f));
                Require(collider.HasMovement && Same(collider.MovementVelocity,new Vector3(1f,2f,3f)), "engine moving collider data");
                Write(collider,"m_ignoreMovement",true); collider.SetMovement(false,new Vector3(8f,9f,-4f));
                Require(collider.IgnoreMovement && !collider.HasMovement && Same(collider.MovementVelocity,new Vector3(8f,9f,-4f)), "engine ignore flag retains inactive velocity");
                target.transform.position = new Vector3(11f,-3f,7f);
                Quaternion parentRotation = Quaternion.Euler(0f,90f,0f);
                Quaternion rotationOffset = Quaternion.Euler(90f,0f,0f);
                target.transform.rotation = parentRotation;
                target.transform.localScale = new Vector3(3f,5f,7f);
                Vector3 translationOffset = new Vector3(1f,2f,3f);
                Vector3 position; Quaternion rotation;
                parent.StartParenting(target.transform,translationOffset,rotationOffset,false);
                Require(parent.Evaluate(out position,out rotation), "live target evaluates");
                Require(Close(rotation,parentRotation*rotationOffset), "parent rotation multiplied before offset");
                Require(Same(position,new Vector3(13f,-6f,6f)) || (position-new Vector3(13f,-6f,6f)).sqrMagnitude<0.0000001f, "translation uses composed rotation without scale");
                Require((position-target.transform.TransformPoint(translationOffset)).sqrMagnitude>1f, "fake parenting differs from scaled TransformPoint");
                Require(carrier.transform.parent==null && Same(carrier.transform.position,Vector3.zero), "evaluation does not reparent or move carrier");
                parent.StartParenting(target.transform,translationOffset,rotationOffset,true);
                Vector3 ignoredPosition; Quaternion ignoredRotation;
                Require(parent.IgnoreRotation && parent.Evaluate(out ignoredPosition,out ignoredRotation) && (ignoredPosition-position).sqrMagnitude<0.0000001f && Close(ignoredRotation,rotation), "ignore rotation flag preserves original unused evaluate behavior");
                target.transform.position=new Vector3(-4f,8f,2f); target.transform.rotation=Quaternion.identity;
                Require(parent.Evaluate(out position,out rotation) && Close(rotation,rotationOffset) && (position-new Vector3(-3f,5f,4f)).sqrMagnitude<0.0000001f, "evaluation reads current target transform every call");
                parent.Stop();
                Require(parent.IgnoreRotation && !parent.Evaluate(out position,out rotation) && Same(position,Vector3.zero) && Same(rotation,Quaternion.identity), "engine stop retains flag and clears outs");
                parent.StartParenting(target.transform,Vector3.one,Quaternion.identity,false);
                UnityEngine.Object.DestroyImmediate(target);
                Require(!parent.Evaluate(out position,out rotation) && Same(position,Vector3.zero) && Same(rotation,Quaternion.identity), "destroyed parent uses genuine Unity fake-null");
                return checks;
            }
            finally
            {
                if(target!=null)UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(carrier);
                UnityEngine.Object.DestroyImmediate(colliderObject);
            }
        }
    }
}
