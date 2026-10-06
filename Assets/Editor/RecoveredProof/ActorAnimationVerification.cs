using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    public static class ActorAnimationVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); ++checks; }
        private static FieldInfo Field(Type t, string name) => t.GetField(name, Own);
        private static void Put(object instance, string name, object value) => Field(instance.GetType(), name).SetValue(instance, value);
        private static T Get<T>(object instance, string name) => (T)Field(instance.GetType(), name).GetValue(instance);
        private static void Call(object instance, string name)
        {
            try { instance.GetType().GetMethod(name, Own).Invoke(instance, null); }
            catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
        private static bool Throws<T>(Action action) where T : Exception
        { try { action(); return false; } catch (T) { return true; } }
        private static string Canon(Type t)
        {
            if (t == null) return null;
            if (t.IsGenericParameter) return (t.DeclaringMethod == null ? "!" : "!!") + t.GenericParameterPosition;
            if (t.IsArray) return Canon(t.GetElementType()) + "[]";
            if (t.IsByRef) return Canon(t.GetElementType()) + "&";
            if (t.IsGenericType) return t.GetGenericTypeDefinition().FullName + "<" + string.Join(",", t.GetGenericArguments().Select(Canon)) + ">";
            return t.FullName;
        }
        private static AnimationParameterWrapper Wrapper(string name, int cached)
        {
            var result = new AnimationParameterWrapper(); Put(result, "m_parameterName", name); Put(result, "m_parameterHash", cached); return result;
        }
        private static object Interval<T, V>(T min, T max, V value) where T : IComparable<T>
        {
            Type type = typeof(IntervalList<T,V>).GetNestedType("IntervalType", Own).MakeGenericType(typeof(T), typeof(V));
            object interval = Activator.CreateInstance(type); Put(interval,"m_min",min); Put(interval,"m_max",max); Put(interval,"m_value",value); return interval;
        }
        private static IList Intervals<T,V>(IntervalList<T,V> list) where T : IComparable<T> => (IList)Get<object>(list,"m_intervals");
        private sealed class Comparable : IComparable<Comparable>
        {
            public int value;
            public List<int> calls;
            public Action<Comparable> callback;
            public int CompareTo(Comparable other) { calls.Add(other.value); callback?.Invoke(other); return value.CompareTo(other.value); }
        }
        private static ActorAnimationDefinition Definition()
        {
            // Test-only uninitialized genuine instance: constructor and Unity lifecycle
            // are explicitly not exercised in the managed hosts.
            var d = (ActorAnimationDefinition)FormatterServices.GetUninitializedObject(typeof(ActorAnimationDefinition));
            Put(d,"m_parameters",Array.Empty<ActorAnimationDefinition.AnimationParameter>());
            Put(d,"m_audioParameters",Array.Empty<ActorAnimationDefinition.AudioParameter>());
            Put(d,"m_pfxParameters",Array.Empty<ActorAnimationDefinition.PFXParameter>());
            Put(d,"m_pfxParametersLeaveState",Array.Empty<ActorAnimationDefinition.PFXParameterLeaveState>());
            Put(d,"m_fullscreenEffectParameters",Array.Empty<ActorAnimationDefinition.FullscreenEffectParameter>());
            Put(d,"m_parametersLookup",new Dictionary<ActorAnimationType,List<AnimationParameterWrapper>>(HardlightProject.HardlightEnumComparers.ActorAnimationTypeComparer));
            return d;
        }
        private static Dictionary<ActorAnimationType,List<AnimationParameterWrapper>> Lookup(ActorAnimationDefinition d) => Get<Dictionary<ActorAnimationType,List<AnimationParameterWrapper>>>(d,"m_parametersLookup");
        public static int RunManaged()
        {
            checks=0;
            {
            Type type=typeof(IntervalList<,>);
            Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1056769,"Hardlight.IntervalList`2 exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Object","Hardlight.IntervalList`2 original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_intervals"}),"Hardlight.IntervalList`2 complete field order");
            Check(Canon(Field(type,"m_intervals").FieldType)=="System.Collections.Generic.List`1<Hardlight.IntervalList`2+IntervalType<!0,!1>>" && (int)Field(type,"m_intervals").Attributes==1,"Hardlight.IntervalList`2.m_intervals exact field type/flags");
            Check(Field(type,"m_intervals").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField","UnityEngine.TooltipAttribute"}),"Hardlight.IntervalList`2.m_intervals original attribute order (NonSerialized field flag separately checked)");
            Check(type.GetGenericArguments()[0].Name=="TInterval" && (int)type.GetGenericArguments()[0].GenericParameterAttributes==0 && type.GetGenericArguments()[0].GetGenericParameterConstraints().Select(Canon).SequenceEqual(new string[]{"System.IComparable`1<!0>"}),"Hardlight.IntervalList`2 original type parameter constraint TInterval");
            Check(type.GetGenericArguments()[1].Name=="TValue" && (int)type.GetGenericArguments()[1].GenericParameterAttributes==0 && type.GetGenericArguments()[1].GetGenericParameterConstraints().Select(Canon).SequenceEqual(new string[]{}),"Hardlight.IntervalList`2 original type parameter constraint TValue");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"TryGetIntervalValue",".ctor"}),"Hardlight.IntervalList`2 full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[0].GetParameters().Length==2 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Boolean" && members[0].GetParameters()[0].Name=="t" && Canon(members[0].GetParameters()[0].ParameterType)=="!0" && (int)members[0].GetParameters()[0].Attributes==0 && members[0].GetParameters()[1].Name=="value" && Canon(members[0].GetParameters()[1].ParameterType)=="!1&" && (int)members[0].GetParameters()[1].Attributes==2,"Hardlight.IntervalList`2 0x06000f89 exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[1].GetParameters().Length==0,"Hardlight.IntervalList`2 0x06000f8a exact signature/flags/parameter names");
            }
            {
            Type type=typeof(IntervalList<,>).GetNestedType("IntervalType",Own);
            Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1056771,"Hardlight.IntervalList`2+IntervalType exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Object","Hardlight.IntervalList`2+IntervalType original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_min","m_max","m_value"}),"Hardlight.IntervalList`2+IntervalType complete field order");
            Check(Canon(Field(type,"m_min").FieldType)=="!0" && (int)Field(type,"m_min").Attributes==1,"Hardlight.IntervalList`2+IntervalType.m_min exact field type/flags");
            Check(Field(type,"m_min").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute","UnityEngine.SerializeField"}),"Hardlight.IntervalList`2+IntervalType.m_min original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_max").FieldType)=="!0" && (int)Field(type,"m_max").Attributes==1,"Hardlight.IntervalList`2+IntervalType.m_max exact field type/flags");
            Check(Field(type,"m_max").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute","UnityEngine.SerializeField"}),"Hardlight.IntervalList`2+IntervalType.m_max original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_value").FieldType)=="!1" && (int)Field(type,"m_value").Attributes==1,"Hardlight.IntervalList`2+IntervalType.m_value exact field type/flags");
            Check(Field(type,"m_value").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField","UnityEngine.TooltipAttribute"}),"Hardlight.IntervalList`2+IntervalType.m_value original attribute order (NonSerialized field flag separately checked)");
            Check(type.GetGenericArguments()[0].Name=="TInterval" && (int)type.GetGenericArguments()[0].GenericParameterAttributes==0 && type.GetGenericArguments()[0].GetGenericParameterConstraints().Select(Canon).SequenceEqual(new string[]{"System.IComparable`1<!0>"}),"Hardlight.IntervalList`2+IntervalType original type parameter constraint TInterval");
            Check(type.GetGenericArguments()[1].Name=="TValue" && (int)type.GetGenericArguments()[1].GenericParameterAttributes==0 && type.GetGenericArguments()[1].GetGenericParameterConstraints().Select(Canon).SequenceEqual(new string[]{}),"Hardlight.IntervalList`2+IntervalType original type parameter constraint TValue");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"IsValid","get_Value",".ctor"}),"Hardlight.IntervalList`2+IntervalType full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[0].GetParameters().Length==1 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Boolean" && members[0].GetParameters()[0].Name=="t" && Canon(members[0].GetParameters()[0].ParameterType)=="!0" && (int)members[0].GetParameters()[0].Attributes==0,"Hardlight.IntervalList`2+IntervalType 0x06000f8b exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="!1","Hardlight.IntervalList`2+IntervalType 0x06000f8c exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[2].GetParameters().Length==0,"Hardlight.IntervalList`2+IntervalType 0x06000f8d exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1048833,"HardlightProject.ActorAnimationDefinition exact type flags/assembly");
            Check(Canon(type.BaseType)=="UnityEngine.ScriptableObject","HardlightProject.ActorAnimationDefinition original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_parameters","m_audioParameters","m_pfxParameters","m_pfxParametersLeaveState","m_fullscreenEffectParameters","m_parametersLookup"}),"HardlightProject.ActorAnimationDefinition complete field order");
            Check(Canon(Field(type,"m_parameters").FieldType)=="HardlightProject.ActorAnimationDefinition+AnimationParameter[]" && (int)Field(type,"m_parameters").Attributes==1,"HardlightProject.ActorAnimationDefinition.m_parameters exact field type/flags");
            Check(Field(type,"m_parameters").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition.m_parameters original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_audioParameters").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter[]" && (int)Field(type,"m_audioParameters").Attributes==1,"HardlightProject.ActorAnimationDefinition.m_audioParameters exact field type/flags");
            Check(Field(type,"m_audioParameters").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition.m_audioParameters original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_pfxParameters").FieldType)=="HardlightProject.ActorAnimationDefinition+PFXParameter[]" && (int)Field(type,"m_pfxParameters").Attributes==1,"HardlightProject.ActorAnimationDefinition.m_pfxParameters exact field type/flags");
            Check(Field(type,"m_pfxParameters").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition.m_pfxParameters original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_pfxParametersLeaveState").FieldType)=="HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState[]" && (int)Field(type,"m_pfxParametersLeaveState").Attributes==1,"HardlightProject.ActorAnimationDefinition.m_pfxParametersLeaveState exact field type/flags");
            Check(Field(type,"m_pfxParametersLeaveState").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition.m_pfxParametersLeaveState original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_fullscreenEffectParameters").FieldType)=="HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter[]" && (int)Field(type,"m_fullscreenEffectParameters").Attributes==1,"HardlightProject.ActorAnimationDefinition.m_fullscreenEffectParameters exact field type/flags");
            Check(Field(type,"m_fullscreenEffectParameters").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition.m_fullscreenEffectParameters original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_parametersLookup").FieldType)=="System.Collections.Generic.Dictionary`2<HardlightProject.ActorAnimationType,System.Collections.Generic.List`1<HardlightProject.AnimationParameterWrapper>>" && (int)Field(type,"m_parametersLookup").Attributes==33,"HardlightProject.ActorAnimationDefinition.m_parametersLookup exact field type/flags");
            Check(Field(type,"m_parametersLookup").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition.m_parametersLookup original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_Audio","get_PFXParameters","get_PFXParametersLeaveState","get_FullscreenEffectParameters","OnEnable","RebuildAnimationParameters","IterateAnimationHashesForType","OnValidate",".ctor"}),"HardlightProject.ActorAnimationDefinition full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Collections.Generic.IReadOnlyList`1<HardlightProject.ActorAnimationDefinition+AudioParameter>","HardlightProject.ActorAnimationDefinition 0x060019c4 exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Collections.Generic.IReadOnlyList`1<HardlightProject.ActorAnimationDefinition+PFXParameterBase>","HardlightProject.ActorAnimationDefinition 0x060019c5 exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[2].GetParameters().Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Collections.Generic.IReadOnlyList`1<HardlightProject.ActorAnimationDefinition+PFXParameterBase>","HardlightProject.ActorAnimationDefinition 0x060019c6 exact signature/flags/parameter names");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[3].GetParameters().Length==0 && Canon(((MethodInfo)members[3]).ReturnType)=="System.Collections.Generic.IReadOnlyList`1<HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter>","HardlightProject.ActorAnimationDefinition 0x060019c7 exact signature/flags/parameter names");
            Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && members[4].GetParameters().Length==0 && Canon(((MethodInfo)members[4]).ReturnType)=="System.Void","HardlightProject.ActorAnimationDefinition 0x060019c8 exact signature/flags/parameter names");
            Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && members[5].GetParameters().Length==0 && Canon(((MethodInfo)members[5]).ReturnType)=="System.Void","HardlightProject.ActorAnimationDefinition 0x060019c9 exact signature/flags/parameter names");
            Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[6].GetParameters().Length==2 && Canon(((MethodInfo)members[6]).ReturnType)=="System.Void" && members[6].GetParameters()[0].Name=="animationType" && Canon(members[6].GetParameters()[0].ParameterType)=="HardlightProject.ActorAnimationType" && (int)members[6].GetParameters()[0].Attributes==0 && members[6].GetParameters()[1].Name=="iterator" && Canon(members[6].GetParameters()[1].ParameterType)=="System.Action`1<System.Int32>" && (int)members[6].GetParameters()[1].Attributes==0,"HardlightProject.ActorAnimationDefinition 0x060019ca exact signature/flags/parameter names");
            Check(members[7].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && members[7].GetParameters().Length==0 && Canon(((MethodInfo)members[7]).ReturnType)=="System.Void","HardlightProject.ActorAnimationDefinition 0x060019cb exact signature/flags/parameter names");
            Check(members[8].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[8].GetParameters().Length==0,"HardlightProject.ActorAnimationDefinition 0x060019cc exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.AnimationParameter);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1057026,"HardlightProject.ActorAnimationDefinition+AnimationParameter exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Object","HardlightProject.ActorAnimationDefinition+AnimationParameter original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"Name","AnimationType","ParameterName"}),"HardlightProject.ActorAnimationDefinition+AnimationParameter complete field order");
            Check(Canon(Field(type,"Name").FieldType)=="System.String" && (int)Field(type,"Name").Attributes==6,"HardlightProject.ActorAnimationDefinition+AnimationParameter.Name exact field type/flags");
            Check(Field(type,"Name").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.HideInInspector"}),"HardlightProject.ActorAnimationDefinition+AnimationParameter.Name original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"AnimationType").FieldType)=="HardlightProject.ActorAnimationType" && (int)Field(type,"AnimationType").Attributes==6,"HardlightProject.ActorAnimationDefinition+AnimationParameter.AnimationType exact field type/flags");
            Check(Field(type,"AnimationType").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AnimationParameter.AnimationType original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"ParameterName").FieldType)=="HardlightProject.AnimationParameterWrapper" && (int)Field(type,"ParameterName").Attributes==6,"HardlightProject.ActorAnimationDefinition+AnimationParameter.ParameterName exact field type/flags");
            Check(Field(type,"ParameterName").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AnimationParameter.ParameterName original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"OnBeforeSerialize","OnAfterDeserialize","GetString",".ctor"}),"HardlightProject.ActorAnimationDefinition+AnimationParameter full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Final, Virtual, HideBySig, VtableLayoutMask") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Void","HardlightProject.ActorAnimationDefinition+AnimationParameter 0x060019cd exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Final, Virtual, HideBySig, VtableLayoutMask") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Void","HardlightProject.ActorAnimationDefinition+AnimationParameter 0x060019ce exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && members[2].GetParameters().Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.String","HardlightProject.ActorAnimationDefinition+AnimationParameter 0x060019cf exact signature/flags/parameter names");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[3].GetParameters().Length==0,"HardlightProject.ActorAnimationDefinition+AnimationParameter 0x060019d0 exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.AudioParameter);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1057026,"HardlightProject.ActorAnimationDefinition+AudioParameter exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Object","HardlightProject.ActorAnimationDefinition+AudioParameter original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"onEnter","onLeave","Clip","Behaviour","BehaviourOn","DelaySeconds"}),"HardlightProject.ActorAnimationDefinition+AudioParameter complete field order");
            Check(Canon(Field(type,"onEnter").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction" && (int)Field(type,"onEnter").Attributes==6,"HardlightProject.ActorAnimationDefinition+AudioParameter.onEnter exact field type/flags");
            Check(Field(type,"onEnter").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter.onEnter original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"onLeave").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction" && (int)Field(type,"onLeave").Attributes==6,"HardlightProject.ActorAnimationDefinition+AudioParameter.onLeave exact field type/flags");
            Check(Field(type,"onLeave").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter.onLeave original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"Clip").FieldType)=="HardlightProject.ActorAudioTypes" && (int)Field(type,"Clip").Attributes==6,"HardlightProject.ActorAnimationDefinition+AudioParameter.Clip exact field type/flags");
            Check(Field(type,"Clip").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"Hardlight.Utils.HashEnumAttribute"}),"HardlightProject.ActorAnimationDefinition+AudioParameter.Clip original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"Behaviour").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour" && (int)Field(type,"Behaviour").Attributes==6,"HardlightProject.ActorAnimationDefinition+AudioParameter.Behaviour exact field type/flags");
            Check(Field(type,"Behaviour").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.HideInInspector"}),"HardlightProject.ActorAnimationDefinition+AudioParameter.Behaviour original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"BehaviourOn").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip" && (int)Field(type,"BehaviourOn").Attributes==6,"HardlightProject.ActorAnimationDefinition+AudioParameter.BehaviourOn exact field type/flags");
            Check(Field(type,"BehaviourOn").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter.BehaviourOn original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"DelaySeconds").FieldType)=="System.Single" && (int)Field(type,"DelaySeconds").Attributes==6,"HardlightProject.ActorAnimationDefinition+AudioParameter.DelaySeconds exact field type/flags");
            Check(Field(type,"DelaySeconds").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute"}),"HardlightProject.ActorAnimationDefinition+AudioParameter.DelaySeconds original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{".ctor"}),"HardlightProject.ActorAnimationDefinition+AudioParameter full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[0].GetParameters().Length==0,"HardlightProject.ActorAnimationDefinition+AudioParameter 0x060019d1 exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.AudioParameter.ClipBehaviour);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==258,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Enum","HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"value__","EndOnLeaveState","OneShot","Looping","LoopingUntilExplicitlyStopped","EndLoopOnLeave","OneShotOnLeaveState","None"}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour complete field order");
            Check(Canon(Field(type,"value__").FieldType)=="System.Int32" && (int)Field(type,"value__").Attributes==1542,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.value__ exact field type/flags");
            Check(Field(type,"value__").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.value__ original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"EndOnLeaveState").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour" && (int)Field(type,"EndOnLeaveState").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.EndOnLeaveState exact field type/flags");
            Check(Convert.ToDouble(Field(type,"EndOnLeaveState").GetRawConstantValue())==0d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.EndOnLeaveState original literal");
            Check(Field(type,"EndOnLeaveState").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.EndOnLeaveState original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"OneShot").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour" && (int)Field(type,"OneShot").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.OneShot exact field type/flags");
            Check(Convert.ToDouble(Field(type,"OneShot").GetRawConstantValue())==1d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.OneShot original literal");
            Check(Field(type,"OneShot").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.OneShot original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"Looping").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour" && (int)Field(type,"Looping").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.Looping exact field type/flags");
            Check(Convert.ToDouble(Field(type,"Looping").GetRawConstantValue())==2d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.Looping original literal");
            Check(Field(type,"Looping").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.Looping original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"LoopingUntilExplicitlyStopped").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour" && (int)Field(type,"LoopingUntilExplicitlyStopped").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.LoopingUntilExplicitlyStopped exact field type/flags");
            Check(Convert.ToDouble(Field(type,"LoopingUntilExplicitlyStopped").GetRawConstantValue())==3d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.LoopingUntilExplicitlyStopped original literal");
            Check(Field(type,"LoopingUntilExplicitlyStopped").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.LoopingUntilExplicitlyStopped original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"EndLoopOnLeave").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour" && (int)Field(type,"EndLoopOnLeave").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.EndLoopOnLeave exact field type/flags");
            Check(Convert.ToDouble(Field(type,"EndLoopOnLeave").GetRawConstantValue())==4d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.EndLoopOnLeave original literal");
            Check(Field(type,"EndLoopOnLeave").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.EndLoopOnLeave original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"OneShotOnLeaveState").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour" && (int)Field(type,"OneShotOnLeaveState").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.OneShotOnLeaveState exact field type/flags");
            Check(Convert.ToDouble(Field(type,"OneShotOnLeaveState").GetRawConstantValue())==5d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.OneShotOnLeaveState original literal");
            Check(Field(type,"OneShotOnLeaveState").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.OneShotOnLeaveState original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"None").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour" && (int)Field(type,"None").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.None exact field type/flags");
            Check(Convert.ToDouble(Field(type,"None").GetRawConstantValue())==6d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.None original literal");
            Check(Field(type,"None").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour.None original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipBehaviour full method order");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.AudioParameter.BehaviourOfClip);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==258,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Enum","HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"value__","None","OneShot","Loop","OneShotVO","LoopRaw","OneShotRaw"}),"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip complete field order");
            Check(Canon(Field(type,"value__").FieldType)=="System.Int32" && (int)Field(type,"value__").Attributes==1542,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.value__ exact field type/flags");
            Check(Field(type,"value__").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.value__ original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"None").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip" && (int)Field(type,"None").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.None exact field type/flags");
            Check(Convert.ToDouble(Field(type,"None").GetRawConstantValue())==-1d,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.None original literal");
            Check(Field(type,"None").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.None original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"OneShot").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip" && (int)Field(type,"OneShot").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.OneShot exact field type/flags");
            Check(Convert.ToDouble(Field(type,"OneShot").GetRawConstantValue())==0d,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.OneShot original literal");
            Check(Field(type,"OneShot").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.OneShot original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"Loop").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip" && (int)Field(type,"Loop").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.Loop exact field type/flags");
            Check(Convert.ToDouble(Field(type,"Loop").GetRawConstantValue())==1d,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.Loop original literal");
            Check(Field(type,"Loop").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.Loop original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"OneShotVO").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip" && (int)Field(type,"OneShotVO").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.OneShotVO exact field type/flags");
            Check(Convert.ToDouble(Field(type,"OneShotVO").GetRawConstantValue())==2d,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.OneShotVO original literal");
            Check(Field(type,"OneShotVO").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.OneShotVO original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"LoopRaw").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip" && (int)Field(type,"LoopRaw").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.LoopRaw exact field type/flags");
            Check(Convert.ToDouble(Field(type,"LoopRaw").GetRawConstantValue())==3d,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.LoopRaw original literal");
            Check(Field(type,"LoopRaw").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.LoopRaw original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"OneShotRaw").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip" && (int)Field(type,"OneShotRaw").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.OneShotRaw exact field type/flags");
            Check(Convert.ToDouble(Field(type,"OneShotRaw").GetRawConstantValue())==4d,"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.OneShotRaw original literal");
            Check(Field(type,"OneShotRaw").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip.OneShotRaw original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+BehaviourOfClip full method order");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.AudioParameter.ClipAction);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==258,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Enum","HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"value__","None","Play","Stop"}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction complete field order");
            Check(Canon(Field(type,"value__").FieldType)=="System.Int32" && (int)Field(type,"value__").Attributes==1542,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.value__ exact field type/flags");
            Check(Field(type,"value__").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.value__ original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"None").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction" && (int)Field(type,"None").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.None exact field type/flags");
            Check(Convert.ToDouble(Field(type,"None").GetRawConstantValue())==0d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.None original literal");
            Check(Field(type,"None").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.None original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"Play").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction" && (int)Field(type,"Play").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.Play exact field type/flags");
            Check(Convert.ToDouble(Field(type,"Play").GetRawConstantValue())==1d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.Play original literal");
            Check(Field(type,"Play").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.Play original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"Stop").FieldType)=="HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction" && (int)Field(type,"Stop").Attributes==32854,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.Stop exact field type/flags");
            Check(Convert.ToDouble(Field(type,"Stop").GetRawConstantValue())==2d,"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.Stop original literal");
            Check(Field(type,"Stop").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction.Stop original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+AudioParameter+ClipAction full method order");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.PFXParameterCondition);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1057026,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Object","HardlightProject.ActorAnimationDefinition+PFXParameterCondition original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_conditionType","m_comparisonType","m_criteria"}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition complete field order");
            Check(Canon(Field(type,"m_conditionType").FieldType)=="HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType" && (int)Field(type,"m_conditionType").Attributes==1,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition.m_conditionType exact field type/flags");
            Check(Field(type,"m_conditionType").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition.m_conditionType original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_comparisonType").FieldType)=="HardlightProject.ComparisonType" && (int)Field(type,"m_comparisonType").Attributes==1,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition.m_comparisonType exact field type/flags");
            Check(Field(type,"m_comparisonType").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition.m_comparisonType original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_criteria").FieldType)=="System.Single" && (int)Field(type,"m_criteria").Attributes==1,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition.m_criteria exact field type/flags");
            Check(Field(type,"m_criteria").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition.m_criteria original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_ConditionType","get_ComparisonType","get_Criteria",".ctor"}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType","HardlightProject.ActorAnimationDefinition+PFXParameterCondition 0x060019d2 exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="HardlightProject.ComparisonType","HardlightProject.ActorAnimationDefinition+PFXParameterCondition 0x060019d3 exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[2].GetParameters().Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Single","HardlightProject.ActorAnimationDefinition+PFXParameterCondition 0x060019d4 exact signature/flags/parameter names");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[3].GetParameters().Length==0,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition 0x060019d5 exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.PFXParameterCondition.PFXParameterConditionType);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==258,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Enum","HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"value__","ActorXZVelocity","ActorImpactVelocity","BrainMovement"}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType complete field order");
            Check(Canon(Field(type,"value__").FieldType)=="System.Int32" && (int)Field(type,"value__").Attributes==1542,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.value__ exact field type/flags");
            Check(Field(type,"value__").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.value__ original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"ActorXZVelocity").FieldType)=="HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType" && (int)Field(type,"ActorXZVelocity").Attributes==32854,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.ActorXZVelocity exact field type/flags");
            Check(Convert.ToDouble(Field(type,"ActorXZVelocity").GetRawConstantValue())==0d,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.ActorXZVelocity original literal");
            Check(Field(type,"ActorXZVelocity").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.ActorXZVelocity original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"ActorImpactVelocity").FieldType)=="HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType" && (int)Field(type,"ActorImpactVelocity").Attributes==32854,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.ActorImpactVelocity exact field type/flags");
            Check(Convert.ToDouble(Field(type,"ActorImpactVelocity").GetRawConstantValue())==1d,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.ActorImpactVelocity original literal");
            Check(Field(type,"ActorImpactVelocity").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.ActorImpactVelocity original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"BrainMovement").FieldType)=="HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType" && (int)Field(type,"BrainMovement").Attributes==32854,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.BrainMovement exact field type/flags");
            Check(Convert.ToDouble(Field(type,"BrainMovement").GetRawConstantValue())==2d,"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.BrainMovement original literal");
            Check(Field(type,"BrainMovement").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType.BrainMovement original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+PFXParameterCondition+PFXParameterConditionType full method order");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.PFXParameterBase);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1056898,"HardlightProject.ActorAnimationDefinition+PFXParameterBase exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Object","HardlightProject.ActorAnimationDefinition+PFXParameterBase original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_pfxTrigger","m_conditions"}),"HardlightProject.ActorAnimationDefinition+PFXParameterBase complete field order");
            Check(Canon(Field(type,"m_pfxTrigger").FieldType)=="HardlightProject.ActorParticleTriggerType" && (int)Field(type,"m_pfxTrigger").Attributes==1,"HardlightProject.ActorAnimationDefinition+PFXParameterBase.m_pfxTrigger exact field type/flags");
            Check(Field(type,"m_pfxTrigger").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField","UnityEngine.Serialization.FormerlySerializedAsAttribute"}),"HardlightProject.ActorAnimationDefinition+PFXParameterBase.m_pfxTrigger original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_conditions").FieldType)=="HardlightProject.ActorAnimationDefinition+PFXParameterCondition[]" && (int)Field(type,"m_conditions").Attributes==1,"HardlightProject.ActorAnimationDefinition+PFXParameterBase.m_conditions exact field type/flags");
            Check(Field(type,"m_conditions").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition+PFXParameterBase.m_conditions original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_PFXTrigger","get_Conditions","get_EvaluateContinuously","get_EndOnLeaveState","get_RemoveEmittedParticles","get_RemoveDelaySeconds",".ctor"}),"HardlightProject.ActorAnimationDefinition+PFXParameterBase full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="HardlightProject.ActorParticleTriggerType","HardlightProject.ActorAnimationDefinition+PFXParameterBase 0x060019d6 exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Collections.Generic.IReadOnlyCollection`1<HardlightProject.ActorAnimationDefinition+PFXParameterCondition>","HardlightProject.ActorAnimationDefinition+PFXParameterBase 0x060019d7 exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask, Abstract, SpecialName") && members[2].GetParameters().Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Boolean","HardlightProject.ActorAnimationDefinition+PFXParameterBase 0x060019d8 exact signature/flags/parameter names");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask, Abstract, SpecialName") && members[3].GetParameters().Length==0 && Canon(((MethodInfo)members[3]).ReturnType)=="System.Boolean","HardlightProject.ActorAnimationDefinition+PFXParameterBase 0x060019d9 exact signature/flags/parameter names");
            Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask, Abstract, SpecialName") && members[4].GetParameters().Length==0 && Canon(((MethodInfo)members[4]).ReturnType)=="System.Boolean","HardlightProject.ActorAnimationDefinition+PFXParameterBase 0x060019da exact signature/flags/parameter names");
            Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask, Abstract, SpecialName") && members[5].GetParameters().Length==0 && Canon(((MethodInfo)members[5]).ReturnType)=="System.Single","HardlightProject.ActorAnimationDefinition+PFXParameterBase 0x060019db exact signature/flags/parameter names");
            Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Family, HideBySig, SpecialName, RTSpecialName") && members[6].GetParameters().Length==0,"HardlightProject.ActorAnimationDefinition+PFXParameterBase 0x060019dc exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.PFXParameter);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1057026,"HardlightProject.ActorAnimationDefinition+PFXParameter exact type flags/assembly");
            Check(Canon(type.BaseType)=="HardlightProject.ActorAnimationDefinition+PFXParameterBase","HardlightProject.ActorAnimationDefinition+PFXParameter original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_evaluateContinuously","m_endOnLeaveState","m_removeEmittedParticles","m_removeDelaySeconds"}),"HardlightProject.ActorAnimationDefinition+PFXParameter complete field order");
            Check(Canon(Field(type,"m_evaluateContinuously").FieldType)=="System.Boolean" && (int)Field(type,"m_evaluateContinuously").Attributes==1,"HardlightProject.ActorAnimationDefinition+PFXParameter.m_evaluateContinuously exact field type/flags");
            Check(Field(type,"m_evaluateContinuously").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute","UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition+PFXParameter.m_evaluateContinuously original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_endOnLeaveState").FieldType)=="System.Boolean" && (int)Field(type,"m_endOnLeaveState").Attributes==1,"HardlightProject.ActorAnimationDefinition+PFXParameter.m_endOnLeaveState exact field type/flags");
            Check(Field(type,"m_endOnLeaveState").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField","UnityEngine.Serialization.FormerlySerializedAsAttribute"}),"HardlightProject.ActorAnimationDefinition+PFXParameter.m_endOnLeaveState original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_removeEmittedParticles").FieldType)=="System.Boolean" && (int)Field(type,"m_removeEmittedParticles").Attributes==1,"HardlightProject.ActorAnimationDefinition+PFXParameter.m_removeEmittedParticles exact field type/flags");
            Check(Field(type,"m_removeEmittedParticles").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"Hardlight.ShowIfAttribute","UnityEngine.TooltipAttribute","UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition+PFXParameter.m_removeEmittedParticles original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_removeDelaySeconds").FieldType)=="System.Single" && (int)Field(type,"m_removeDelaySeconds").Attributes==1,"HardlightProject.ActorAnimationDefinition+PFXParameter.m_removeDelaySeconds exact field type/flags");
            Check(Field(type,"m_removeDelaySeconds").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"Hardlight.ShowIfAttribute","UnityEngine.TooltipAttribute","UnityEngine.SerializeField"}),"HardlightProject.ActorAnimationDefinition+PFXParameter.m_removeDelaySeconds original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_EvaluateContinuously","get_EndOnLeaveState","get_RemoveEmittedParticles","get_RemoveDelaySeconds",".ctor"}),"HardlightProject.ActorAnimationDefinition+PFXParameter full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, SpecialName") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Boolean","HardlightProject.ActorAnimationDefinition+PFXParameter 0x060019dd exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, SpecialName") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Boolean","HardlightProject.ActorAnimationDefinition+PFXParameter 0x060019de exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, SpecialName") && members[2].GetParameters().Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Boolean","HardlightProject.ActorAnimationDefinition+PFXParameter 0x060019df exact signature/flags/parameter names");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, SpecialName") && members[3].GetParameters().Length==0 && Canon(((MethodInfo)members[3]).ReturnType)=="System.Single","HardlightProject.ActorAnimationDefinition+PFXParameter 0x060019e0 exact signature/flags/parameter names");
            Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[4].GetParameters().Length==0,"HardlightProject.ActorAnimationDefinition+PFXParameter 0x060019e1 exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.PFXParameterLeaveState);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1057026,"HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState exact type flags/assembly");
            Check(Canon(type.BaseType)=="HardlightProject.ActorAnimationDefinition+PFXParameterBase","HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState complete field order");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_EvaluateContinuously","get_EndOnLeaveState","get_RemoveEmittedParticles","get_RemoveDelaySeconds",".ctor"}),"HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, SpecialName") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Boolean","HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState 0x060019e2 exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, SpecialName") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Boolean","HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState 0x060019e3 exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, SpecialName") && members[2].GetParameters().Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Boolean","HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState 0x060019e4 exact signature/flags/parameter names");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, SpecialName") && members[3].GetParameters().Length==0 && Canon(((MethodInfo)members[3]).ReturnType)=="System.Single","HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState 0x060019e5 exact signature/flags/parameter names");
            Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[4].GetParameters().Length==0,"HardlightProject.ActorAnimationDefinition+PFXParameterLeaveState 0x060019e6 exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinition.FullscreenEffectParameter);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1057026,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Object","HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"Name","FullscreenShaderParametersType","UntilStopped","Duration","ApplyCurveOverDuration","TriggerOnExit","DelayStopSeconds"}),"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter complete field order");
            Check(Canon(Field(type,"Name").FieldType)=="System.String" && (int)Field(type,"Name").Attributes==6,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.Name exact field type/flags");
            Check(Field(type,"Name").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.HideInInspector"}),"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.Name original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"FullscreenShaderParametersType").FieldType)=="HardlightProject.FullscreenShaderParametersType" && (int)Field(type,"FullscreenShaderParametersType").Attributes==6,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.FullscreenShaderParametersType exact field type/flags");
            Check(Field(type,"FullscreenShaderParametersType").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.FullscreenShaderParametersType original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"UntilStopped").FieldType)=="System.Boolean" && (int)Field(type,"UntilStopped").Attributes==6,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.UntilStopped exact field type/flags");
            Check(Field(type,"UntilStopped").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute"}),"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.UntilStopped original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"Duration").FieldType)=="System.Single" && (int)Field(type,"Duration").Attributes==6,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.Duration exact field type/flags");
            Check(Field(type,"Duration").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.MinAttribute","UnityEngine.TooltipAttribute"}),"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.Duration original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"ApplyCurveOverDuration").FieldType)=="System.Boolean" && (int)Field(type,"ApplyCurveOverDuration").Attributes==6,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.ApplyCurveOverDuration exact field type/flags");
            Check(Field(type,"ApplyCurveOverDuration").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute"}),"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.ApplyCurveOverDuration original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"TriggerOnExit").FieldType)=="System.Boolean" && (int)Field(type,"TriggerOnExit").Attributes==6,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.TriggerOnExit exact field type/flags");
            Check(Field(type,"TriggerOnExit").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute"}),"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.TriggerOnExit original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"DelayStopSeconds").FieldType)=="System.Single" && (int)Field(type,"DelayStopSeconds").Attributes==6,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.DelayStopSeconds exact field type/flags");
            Check(Field(type,"DelayStopSeconds").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute","Hardlight.ShowIfAttribute"}),"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter.DelayStopSeconds original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"OnBeforeSerialize","OnAfterDeserialize","UpdateProperties","OnValidate",".ctor"}),"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Final, Virtual, HideBySig, VtableLayoutMask") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Void","HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter 0x060019e7 exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Final, Virtual, HideBySig, VtableLayoutMask") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Void","HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter 0x060019e8 exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && members[2].GetParameters().Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Void","HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter 0x060019e9 exact signature/flags/parameter names");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[3].GetParameters().Length==1 && Canon(((MethodInfo)members[3]).ReturnType)=="System.Void" && members[3].GetParameters()[0].Name=="context" && Canon(members[3].GetParameters()[0].ParameterType)=="UnityEngine.Object" && (int)members[3].GetParameters()[0].Attributes==0,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter 0x060019ea exact signature/flags/parameter names");
            Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[4].GetParameters().Length==0,"HardlightProject.ActorAnimationDefinition+FullscreenEffectParameter 0x060019eb exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.ActorAnimationDefinitionGroup);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1048577,"HardlightProject.ActorAnimationDefinitionGroup exact type flags/assembly");
            Check(Canon(type.BaseType)=="HardlightProject.DefinitionDataType`2<System.String,HardlightProject.ActorAnimationDefinition>","HardlightProject.ActorAnimationDefinitionGroup original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{}),"HardlightProject.ActorAnimationDefinitionGroup complete field order");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"GetElementKey","GetKeyComparer",".ctor"}),"HardlightProject.ActorAnimationDefinitionGroup full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Family, Virtual, HideBySig") && members[0].GetParameters().Length==1 && Canon(((MethodInfo)members[0]).ReturnType)=="System.String" && members[0].GetParameters()[0].Name=="data" && Canon(members[0].GetParameters()[0].ParameterType)=="HardlightProject.ActorAnimationDefinition" && (int)members[0].GetParameters()[0].Attributes==0,"HardlightProject.ActorAnimationDefinitionGroup 0x060019ec exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Family, Virtual, HideBySig") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Collections.Generic.IEqualityComparer`1<System.String>","HardlightProject.ActorAnimationDefinitionGroup 0x060019ed exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[2].GetParameters().Length==0,"HardlightProject.ActorAnimationDefinitionGroup 0x060019ee exact signature/flags/parameter names");
            }
            {
            Type type=typeof(HardlightProject.AnimationParameterWrapper);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1056769,"HardlightProject.AnimationParameterWrapper exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Object","HardlightProject.AnimationParameterWrapper original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_parameterName","m_parameterHash"}),"HardlightProject.AnimationParameterWrapper complete field order");
            Check(Canon(Field(type,"m_parameterName").FieldType)=="System.String" && (int)Field(type,"m_parameterName").Attributes==1,"HardlightProject.AnimationParameterWrapper.m_parameterName exact field type/flags");
            Check(Field(type,"m_parameterName").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"HardlightProject.AnimationParameterWrapper.m_parameterName original attribute order (NonSerialized field flag separately checked)");
            Check(Canon(Field(type,"m_parameterHash").FieldType)=="System.Int32" && (int)Field(type,"m_parameterHash").Attributes==129,"HardlightProject.AnimationParameterWrapper.m_parameterHash exact field type/flags");
            Check(Field(type,"m_parameterHash").GetCustomAttributesData().Where(a=>a.AttributeType.FullName!="System.NonSerializedAttribute").Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.AnimationParameterWrapper.m_parameterHash original attribute order (NonSerialized field flag separately checked)");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_ParameterName","TryGetAnimationHash",".ctor"}),"HardlightProject.AnimationParameterWrapper full method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.String","HardlightProject.AnimationParameterWrapper 0x060038d2 exact signature/flags/parameter names");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[1].GetParameters().Length==1 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Boolean" && members[1].GetParameters()[0].Name=="hash" && Canon(members[1].GetParameters()[0].ParameterType)=="System.Int32&" && (int)members[1].GetParameters()[0].Attributes==2,"HardlightProject.AnimationParameterWrapper 0x060038d3 exact signature/flags/parameter names");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && members[2].GetParameters().Length==0,"HardlightProject.AnimationParameterWrapper 0x060038d4 exact signature/flags/parameter names");
            }

            var empty = new IntervalList<int,string>(); string text="sentinel";
            Check(Intervals(empty).Count==0,"interval constructor creates an empty list");
            Check(!empty.TryGetIntervalValue(2,out text)&&text==null,"miss resets reference out value");
            Intervals(empty).Add(Interval(2,5,"first")); Intervals(empty).Add(Interval(4,9,"second"));
            foreach(int x in new[]{1,2,4,5,8,9})
            {
                bool found=empty.TryGetIntervalValue(x,out text);
                string expected=x>=2&&x<5?"first":x>=4&&x<9?"second":null;
                Check(found==(expected!=null)&&text==expected,"inclusive min/exclusive max/first overlap "+x);
            }
            var reverse = new IntervalList<int,int>(); Intervals(reverse).Add(Interval(4,4,1)); Intervals(reverse).Add(Interval(8,3,2));
            int number=77; Check(!reverse.TryGetIntervalValue(4,out number)&&number==0,"empty/reversed intervals miss");
            var floating = new IntervalList<float,int>(); Intervals(floating).Add(Interval(float.NaN,0f,3));
            Check(floating.TryGetIntervalValue(float.NaN,out number)&&number==3,"Single.CompareTo NaN equals NaN minimum and precedes finite maximum");
            Check(floating.TryGetIntervalValue(-1f,out number)&&number==3,"finite query follows NaN minimum");
            Check(!floating.TryGetIntervalValue(0f,out number)&&number==0,"maximum remains excluded with NaN minimum");
            var custom = new IntervalList<Comparable,int>(); var trace = new List<int>();
            var query = new Comparable {value=4,calls=trace}; var min=new Comparable {value=3}; var max=new Comparable {value=7};
            Intervals(custom).Add(Interval(min,max,42));
            Check(custom.TryGetIntervalValue(query,out number)&&number==42&&trace.SequenceEqual(new[]{3,7}),"query receiver compares minimum then maximum");
            trace.Clear();query.value=2;Check(!custom.TryGetIntervalValue(query,out number)&&trace.SequenceEqual(new[]{3}),"maximum comparison short circuits after minimum miss");
            query.value=4;trace.Clear();query.callback=o=> { if(o.value==7)throw new ApplicationException("comparison"); };
            number=99;Check(Throws<ApplicationException>(()=>custom.TryGetIntervalValue(query,out number))&&number==99&&trace.SequenceEqual(new[]{3,7}),"exception leaves out storage untouched");
            query.callback=o=>Intervals(custom).Clear();query.value=2;number=99;
            Check(Throws<InvalidOperationException>(()=>custom.TryGetIntervalValue(query,out number))&&number==99,"callback mutation invalidates next enumerator move");
            query.callback=null;Intervals(custom).Add(Interval(min,max,42));query.value=4;
            Check(custom.TryGetIntervalValue(query,out number)&&number==42,"list remains usable after exceptional enumeration");
            query.callback=o=>Intervals(custom).Clear();number=99;
            Check(custom.TryGetIntervalValue(query,out number)&&number==42&&Intervals(custom).Count==0,"successful match returns captured interval despite list version mutation");
            var nullEntry=new IntervalList<int,int>();Intervals(nullEntry).Add(null);number=99;
            Check(Throws<NullReferenceException>(()=>nullEntry.TryGetIntervalValue(1,out number))&&number==99,"null authored interval is not skipped");
            Put(nullEntry,"m_intervals",null);Check(Throws<NullReferenceException>(()=>nullEntry.TryGetIntervalValue(1,out number)),"null authored list is not repaired");

            var wrapper=new AnimationParameterWrapper();int hash=888;
            Check(wrapper.ParameterName==null&&Get<int>(wrapper,"m_parameterHash")==-1,"wrapper original constructor defaults");
            foreach(string name in new[]{null,""}) {Put(wrapper,"m_parameterName",name);Check(!wrapper.TryGetAnimationHash(out hash)&&hash==-1,"empty wrapper misses");}
            foreach(int cache in new[]{0,17,-2,int.MinValue,int.MaxValue})
            {
                Put(wrapper,"m_parameterName","first");Put(wrapper,"m_parameterHash",cache);
                Check(wrapper.TryGetAnimationHash(out hash)&&hash==cache,"non-sentinel cached hash "+cache);
                Put(wrapper,"m_parameterName","edited");Check(wrapper.TryGetAnimationHash(out hash)&&hash==cache,"changed name retains hash "+cache);
                Put(wrapper,"m_parameterName","");Check(!wrapper.TryGetAnimationHash(out hash)&&hash==-1&&Get<int>(wrapper,"m_parameterHash")==cache,"empty name preserves cache "+cache);
            }
            var audio = new ActorAnimationDefinition.AudioParameter();
            Check(audio.Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.OneShot&&audio.onEnter==0&&audio.onLeave==0&&audio.Clip==0&&audio.BehaviourOn==0&&audio.DelaySeconds==0f,"audio defaults preserve OneShot only");
            var condition=new ActorAnimationDefinition.PFXParameterCondition();
            Check(condition.ConditionType==0&&condition.ComparisonType==ComparisonType.GreaterThanEqual&&condition.Criteria==0f,"PFX condition defaults");
            var pfx=new ActorAnimationDefinition.PFXParameter();
            Check(!pfx.EvaluateContinuously&&pfx.EndOnLeaveState&&!pfx.RemoveEmittedParticles&&pfx.RemoveDelaySeconds==0f&&pfx.Conditions==null&&pfx.PFXTrigger==0,"PFX defaults and null conditions");
            foreach(bool remove in new[]{false,true})foreach(bool end in new[]{false,true})
            { Put(pfx,"m_removeEmittedParticles",remove);Put(pfx,"m_endOnLeaveState",end);Check(pfx.RemoveEmittedParticles==(remove&&end),"PFX removal gating"); }
            foreach(float delay in new[]{-1f,0f,2f,float.NaN,float.PositiveInfinity})
            {Put(pfx,"m_removeDelaySeconds",delay);Check(float.IsNaN(delay)?float.IsNaN(pfx.RemoveDelaySeconds):pfx.RemoveDelaySeconds==delay,"PFX delay is ungated");}
            Put(pfx,"m_evaluateContinuously",true);Check(pfx.EvaluateContinuously,"PFX evaluate reads authored field");
            var conditions=new[]{condition};Field(typeof(ActorAnimationDefinition.PFXParameterBase),"m_conditions").SetValue(pfx,conditions);
            Check(ReferenceEquals(pfx.Conditions,conditions),"PFX condition view is original array");
            var leave=new ActorAnimationDefinition.PFXParameterLeaveState();
            Check(!leave.EvaluateContinuously&&!leave.EndOnLeaveState&&!leave.RemoveEmittedParticles&&leave.RemoveDelaySeconds==0f&&leave.Conditions==null,"leave-state four genuine constant getters");

            var parameter=new ActorAnimationDefinition.AnimationParameter();
            Check(parameter.Name==null&&parameter.ParameterName==null&&parameter.AnimationType==0,"animation parameter constructor does not create wrapper");
            parameter.Name="prior";Check(Throws<NullReferenceException>(parameter.OnBeforeSerialize)&&parameter.Name=="prior","failed callback preserves Name");
            parameter.ParameterName=Wrapper("Speed",12);
            foreach(ActorAnimationType key in new[]{default(ActorAnimationType),(ActorAnimationType)int.MaxValue,Enum.GetValues(typeof(ActorAnimationType)).Cast<ActorAnimationType>().First(k=>(int)k!=0)})
            {parameter.AnimationType=key;parameter.OnBeforeSerialize();Check(parameter.Name==key.GetString()+" - Speed","before serialization genuine animation enum registry");parameter.Name="stale";parameter.OnAfterDeserialize();Check(parameter.Name==key.GetString()+" - Speed","after deserialize restores authored name");}
            Put(parameter.ParameterName,"m_parameterName",null);parameter.OnBeforeSerialize();Check(parameter.Name==parameter.AnimationType.GetString()+" - ","null wrapper name concatenates empty text");
            var fullscreen=new ActorAnimationDefinition.FullscreenEffectParameter();
            Check(fullscreen.Name==null&&fullscreen.Duration==0f&&!fullscreen.UntilStopped&&!fullscreen.ApplyCurveOverDuration&&!fullscreen.TriggerOnExit&&fullscreen.DelayStopSeconds==0f,"fullscreen original defaults");
            CultureInfo culture=CultureInfo.CurrentCulture;
            try
            {
                foreach(string locale in new[]{"en-US","fr-FR"})
                {CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(locale);foreach(bool until in new[]{false,true})foreach(bool exit in new[]{false,true})
                { fullscreen.UntilStopped=until;fullscreen.TriggerOnExit=exit;fullscreen.Duration=1.25f;fullscreen.OnBeforeSerialize();string expected=fullscreen.FullscreenShaderParametersType.GetString()+(until?" (until stopped)":string.Format(" ({0}s)",1.25f)+(exit?" (Trigger on exit)":""));Check(fullscreen.Name==expected,"fullscreen current-culture duration/early-return "+locale);fullscreen.Name="stale";fullscreen.OnAfterDeserialize();Check(fullscreen.Name==expected,"fullscreen after callback"); }}
                foreach(float duration in new[]{float.NaN,float.PositiveInfinity,-2f,0f})
                {fullscreen.UntilStopped=false;fullscreen.TriggerOnExit=false;fullscreen.Duration=duration;fullscreen.OnBeforeSerialize();Check(fullscreen.Name==fullscreen.FullscreenShaderParametersType.GetString()+string.Format(" ({0}s)",duration),"fullscreen duration retains nonfinite/negative values");}
            }
            finally { CultureInfo.CurrentCulture=culture; }
            fullscreen.Name="keep";fullscreen.OnValidate(null);Check(fullscreen.Name=="keep","fullscreen OnValidate genuine RET");

            var definition=Definition();ActorAnimationType a=default,b=(ActorAnimationType)int.MaxValue;
            var w1=Wrapper("one",10);var w2=Wrapper("two",20);var missing=Wrapper("",30);
            var parameters=new[]{new ActorAnimationDefinition.AnimationParameter {AnimationType=a,ParameterName=w1},new ActorAnimationDefinition.AnimationParameter {AnimationType=a,ParameterName=missing},new ActorAnimationDefinition.AnimationParameter {AnimationType=b,ParameterName=w2}};
            Put(definition,"m_parameters",parameters);Call(definition,"OnEnable");
            Check(Lookup(definition).Count==2&&Lookup(definition)[a].SequenceEqual(new[]{w1,missing})&&ReferenceEquals(Lookup(definition)[b][0],w2),"rebuild groups ordered wrappers");
            var hashes=new List<int>();definition.IterateAnimationHashesForType(a,hashes.Add);Check(hashes.SequenceEqual(new[]{10}),"iterate skips empty wrappers");
            definition.IterateAnimationHashesForType((ActorAnimationType)123456,null);Check(true,"missing key does not validate callback");
            Check(Throws<NullReferenceException>(()=>definition.IterateAnimationHashesForType(a,null)),"present valid hash invokes null callback");
            var view=new[]{audio};Put(definition,"m_audioParameters",view);Check(ReferenceEquals(definition.Audio,view),"audio view retains array identity");
            var pfxView=new[]{pfx};Put(definition,"m_pfxParameters",pfxView);Check(ReferenceEquals(definition.PFXParameters,pfxView),"PFX covariant view retains array identity");
            var leaveView=new[]{leave};Put(definition,"m_pfxParametersLeaveState",leaveView);Check(ReferenceEquals(definition.PFXParametersLeaveState,leaveView),"PFX leave view retains array identity");
            var fullView=new[]{fullscreen};Put(definition,"m_fullscreenEffectParameters",fullView);Check(ReferenceEquals(definition.FullscreenEffectParameters,fullView),"fullscreen view retains array identity");
            Put(definition,"m_parameters",new[]{parameters[0],null});Check(Throws<NullReferenceException>(()=>Call(definition,"RebuildAnimationParameters"))&&Lookup(definition).Count==1&&Lookup(definition)[a].Count==1,"malformed array keeps partial rebuild");
            Put(definition,"m_parameters",null);Check(Throws<NullReferenceException>(()=>Call(definition,"RebuildAnimationParameters"))&&Lookup(definition).Count==0,"null array clears lookup before failure");
            Put(definition,"m_parameters",new[]{new ActorAnimationDefinition.AnimationParameter {AnimationType=a,ParameterName=null}});Call(definition,"RebuildAnimationParameters");
            Check(Lookup(definition)[a].Count==1&&Lookup(definition)[a][0]==null,"null wrapper is stored");
            Check(Throws<NullReferenceException>(()=>definition.IterateAnimationHashesForType(a,hashes.Add)),"null stored wrapper fails at iteration");
            Put(definition,"m_parameters",parameters);Call(definition,"RebuildAnimationParameters");
            hashes.Clear();Check(Throws<InvalidOperationException>(()=>definition.IterateAnimationHashesForType(a,h=>{hashes.Add(h);Lookup(definition)[a].Add(w2);}))&&hashes.SequenceEqual(new[]{10}),"callback list mutation invalidates enumerator");
            Call(definition,"RebuildAnimationParameters");hashes.Clear();definition.IterateAnimationHashesForType(a,h=>{hashes.Add(h);Lookup(definition).Clear();});
            Check(hashes.SequenceEqual(new[]{10}),"dictionary clearing does not replace captured list");
            Put(definition,"m_audioParameters",new[]{new ActorAnimationDefinition.AudioParameter {Behaviour=ActorAnimationDefinition.AudioParameter.ClipBehaviour.LoopingUntilExplicitlyStopped},new ActorAnimationDefinition.AudioParameter {Behaviour=ActorAnimationDefinition.AudioParameter.ClipBehaviour.EndLoopOnLeave},new ActorAnimationDefinition.AudioParameter {Behaviour=ActorAnimationDefinition.AudioParameter.ClipBehaviour.Looping}});
            Call(definition,"OnValidate");Check(Lookup(definition).Count==2&&definition.Audio[0].Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.LoopingUntilExplicitlyStopped&&definition.Audio[1].Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.EndLoopOnLeave&&definition.Audio[2].Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.Looping,"only exact persistent modes count; successful validate rebuilds");
            Put(definition,"m_audioParameters",new ActorAnimationDefinition.AudioParameter[]{null});
            Check(Throws<NullReferenceException>(()=>Call(definition,"OnValidate"))&&Lookup(definition).Count==2,"null audio prevents later lookup rebuild");
            return checks;
        }

        // Genuine Unity object constructors/hash/JSON paths are separate from managed tests.
        // Root owns executing this method in the real Editor.
        public static int RunEngine()
        {
            checks=0;
            ActorAnimationDefinition d=null;
            ActorAnimationDefinitionGroup group=null;
            try
            {
                d=ScriptableObject.CreateInstance<ActorAnimationDefinition>();
                group=ScriptableObject.CreateInstance<ActorAnimationDefinitionGroup>();
                Check(d.Audio.Count==0&&d.PFXParameters.Count==0&&d.PFXParametersLeaveState.Count==0&&d.FullscreenEffectParameters.Count==0&&Lookup(d).Count==0,"genuine definition constructor/lifecycle defaults");
                Check(ReferenceEquals(Lookup(d).Comparer,HardlightProject.HardlightEnumComparers.ActorAnimationTypeComparer),"constructor uses original generated comparer");
                var wrapper=new AnimationParameterWrapper();Put(wrapper,"m_parameterName","Speed");int hash;
                Check(wrapper.TryGetAnimationHash(out hash)&&hash==Animator.StringToHash("Speed")&&Get<int>(wrapper,"m_parameterHash")==hash,"real engine hashes and stores first name");
                Put(wrapper,"m_parameterName","Changed");Check(wrapper.TryGetAnimationHash(out hash)&&hash==Animator.StringToHash("Speed"),"real hash cache survives name changes");
                Put(wrapper,"m_parameterName","");Check(!wrapper.TryGetAnimationHash(out hash)&&hash==-1,"real cached hash empty-name miss");
                JsonUtility.FromJsonOverwrite("{\"m_parameterName\":\"Speed\"}",wrapper);
                Check(wrapper.TryGetAnimationHash(out hash)&&hash==Animator.StringToHash("Speed"),"JSON preserves nonserialized cached hash");
                var restored=JsonUtility.FromJson<AnimationParameterWrapper>("{\"m_parameterName\":\"Walk\"}");
                Check(restored.ParameterName=="Walk"&&restored.TryGetAnimationHash(out hash)&&hash==Animator.StringToHash("Walk"),"JSON created wrapper uses original hash initializer");
                Check(!JsonUtility.ToJson(wrapper).Contains("m_parameterHash"),"nonserialized hash absent from JSON");
                var json=JsonUtility.FromJson<ActorAnimationDefinition.PFXParameter>("{\"m_endOnLeaveState\":true,\"m_removeEmittedParticles\":true,\"m_removeDelaySeconds\":2.5}");
                Check(json.EndOnLeaveState&&json.RemoveEmittedParticles&&json.RemoveDelaySeconds==2.5f,"actual PFX private serialized fields");
                d.name="AnimationFixture";group.m_elements=new[]{new DefinitionDataType<string,ActorAnimationDefinition>.DefinitionElement<ActorAnimationDefinition>(d)};
                var data=group.GetData();Check(data.Count==1&&ReferenceEquals(data["AnimationFixture"],d),"real animation definition group key/base dictionary");
                Check(group.GetType().GetMethod("GetKeyComparer",Own).Invoke(group,null)==null,"group genuine null comparer body");
                Put(d,"m_parameters",new[]{new ActorAnimationDefinition.AnimationParameter {AnimationType=default,ParameterName=restored}});Call(d,"OnEnable");
                var hashes=new List<int>();d.IterateAnimationHashesForType(default,hashes.Add);Check(hashes.SequenceEqual(new[]{Animator.StringToHash("Walk")}),"genuine definition/hash iteration");
                var effect=JsonUtility.FromJson<ActorAnimationDefinition.FullscreenEffectParameter>("{\"Duration\":1.5,\"TriggerOnExit\":true}");
                Check(effect.Name==effect.FullscreenShaderParametersType.GetString()+string.Format(" ({0}s)",1.5f)+" (Trigger on exit)","actual JSON invokes fullscreen callback");
                var intervals=JsonUtility.FromJson<IntervalList<float,int>>("{\"m_intervals\":[{\"m_min\":1,\"m_max\":2,\"m_value\":9}]}");int intervalValue;
                Check(intervals.TryGetIntervalValue(1f,out intervalValue)&&intervalValue==9,"actual interval private nested generic serialization minimum");
                Check(!intervals.TryGetIntervalValue(2f,out intervalValue)&&intervalValue==0,"actual interval JSON maximum exclusion");
                Check(JsonUtility.ToJson(intervals).Contains("m_value"),"actual interval nested fields remain serialized");

                // A genuine registered HLUnityCore/FastAction host isolates the callback
                // boundary. This does not exercise HLUnityCore Awake or native plugins.
                FieldInfo registry=typeof(ProcessManager).GetField("s_systemDictionary",Own);
                object previousRegistry=registry.GetValue(null);
                var host=(HLUnityCore)FormatterServices.GetUninitializedObject(typeof(HLUnityCore));
                Field(typeof(HLUnityCore),"m_logErrorHandler").SetValue(host,new FastAction<string,int>());
                registry.SetValue(null,Activator.CreateInstance(registry.FieldType));
                try
                {
                    ProcessManager.RegisterSystem(host);
                    var first=new ActorAnimationDefinition.AudioParameter {Behaviour=ActorAnimationDefinition.AudioParameter.ClipBehaviour.Looping};
                    var second=new ActorAnimationDefinition.AudioParameter {Behaviour=ActorAnimationDefinition.AudioParameter.ClipBehaviour.EndOnLeaveState};
                    var third=new ActorAnimationDefinition.AudioParameter {Behaviour=ActorAnimationDefinition.AudioParameter.ClipBehaviour.Looping};
                    var originalAudio=new[]{first,second,third};Put(d,"m_audioParameters",originalAudio);
                    int calls=0;string error=null;int code=-1;var observedModes=new List<ActorAnimationDefinition.AudioParameter.ClipBehaviour>();
                    Action<string,int> callback=(message,errorCode)=>
                    {
                        error=message;code=errorCode;observedModes.Add(originalAudio[++calls].Behaviour);
                        Put(d,"m_audioParameters",Array.Empty<ActorAnimationDefinition.AudioParameter>());
                    };
                    host.AddLogErrorHandler(callback);
                    Call(d,"OnValidate");
                    host.RemoveLogErrorHandler(callback);
                    Check(calls==2&&code==0&&error=="Error in AnimationFixture: Must not have more than one AudioParameter with behaviour Looping or EndOnLeaveState","actual asset-name/exact ordered boxed-mode message and genuine logging boundary");
                    Check(observedModes.SequenceEqual(new[]{ActorAnimationDefinition.AudioParameter.ClipBehaviour.EndOnLeaveState,ActorAnimationDefinition.AudioParameter.ClipBehaviour.Looping}),"audio log callback sees each mode before mutation");
                    Check(first.Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.Looping&&second.Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.OneShot&&third.Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.OneShot,"every later persistent mode changed to OneShot");
                    Check(d.Audio.Count==0&&Lookup(d).Count==1,"validation keeps captured original array after callback replaces field and then rebuilds");

                    second.Behaviour=ActorAnimationDefinition.AudioParameter.ClipBehaviour.EndOnLeaveState;
                    Put(d,"m_audioParameters",new[]{first,second});Put(d,"m_parameters",Array.Empty<ActorAnimationDefinition.AnimationParameter>());
                    Action<string,int> throwing=(message,errorCode)=>throw new ApplicationException("diagnostic callback");
                    host.AddLogErrorHandler(throwing);
                    Check(Throws<ApplicationException>(()=>Call(d,"OnValidate"))&&second.Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.EndOnLeaveState&&Lookup(d).Count==1,"diagnostic exception prevents mode mutation and later dictionary rebuild");
                    host.RemoveLogErrorHandler(throwing);
                    // A thrown original FastAction callback leaves dispatch active;
                    // removal is queued and the same listener still throws next time.
                    Check(Throws<ApplicationException>(()=>Call(d,"OnValidate"))&&second.Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.EndOnLeaveState&&Lookup(d).Count==1,"failed dispatch retains the deferred listener removal and validation stops again");
                    // A separate genuine dispatcher isolates the successful path.
                    Field(typeof(HLUnityCore),"m_logErrorHandler").SetValue(host,new FastAction<string,int>());
                    Call(d,"OnValidate");
                    Check(second.Behaviour==ActorAnimationDefinition.AudioParameter.ClipBehaviour.OneShot&&Lookup(d).Count==0,"empty real host handler still completes correction and rebuild");
                }
                finally { registry.SetValue(null,previousRegistry); }
            }
            finally
            {
                try { if(group!=null)UnityEngine.Object.DestroyImmediate(group); }
                finally { if(d!=null)UnityEngine.Object.DestroyImmediate(d); }
            }
            return checks;
        }
    }
}
