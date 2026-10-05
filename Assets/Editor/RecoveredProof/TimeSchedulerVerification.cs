using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using Unity.Profiling;
using UnityEngine;

namespace ProjectLucid
{
    // Tests use the full genuine types. The managed host's uninitialized objects
    // and reference comparer exercise chosen pure paths without fabricating an
    // engine object or claiming Unity fake-null, constructor, or gameplay parity.
    public static class TimeSchedulerVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static int checks;
        private static void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); checks++; }
        private static void Throws<T>(Action action, string name) where T : Exception
        { try { action(); } catch(T) { checks++; return; } throw new InvalidOperationException(name); }
        private static FieldInfo Field(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            { FieldInfo f=t.GetField(name,Own); if(f!=null)return f; }
            throw new InvalidOperationException("field "+name);
        }
        private static object Read(object target,string name) { return Field(target.GetType(),name).GetValue(target); }
        private static void Write(object target,string name,object value) { Field(target.GetType(),name).SetValue(target,value); }
        private static T Raw<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
        private static object Call(object target,string name,params object[] args)
        {
            try { return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args); }
            catch(TargetInvocationException e) when(e.InnerException!=null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
        private static string Canon(Type type)
        {
            if(type.IsByRef)return Canon(type.GetElementType())+"&";
            if(type.IsGenericType)return type.GetGenericTypeDefinition().FullName+"<"+string.Join(",",type.GetGenericArguments().Select(Canon))+">";
            return type.FullName;
        }
        private static string Value(CustomAttributeTypedArgument arg)
        { return arg.Value is Type t ? Canon(t) : Convert.ToString(arg.Value,System.Globalization.CultureInfo.InvariantCulture); }
        private static string[] Attributes(MemberInfo member)
        {
            // Serializable is a reflection pseudoattribute backed by the exact
            // original type flag. No compiler attribute is silently filtered.
            // Named argument values are sorted; raw blobs/order/scopes are checked
            // separately with Cecil and are not approved by this helper.
            return member.GetCustomAttributesData().Where(a=>a.AttributeType!=typeof(SerializableAttribute))
                .Select(a=>a.AttributeType.FullName+"("+string.Join("|",a.ConstructorArguments.Select(Value))+")"+
                  "["+string.Join("|",a.NamedArguments.OrderBy(n=>n.MemberName,StringComparer.Ordinal).Select(n=>n.MemberName+"="+Value(n.TypedValue)))+"]").ToArray();
        }
        private static void Shape()
        {
            {
                Type type=typeof(Hardlight.LeaderboardType);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==257, "Hardlight.LeaderboardType original type flags/assembly");
                Check(Canon(type.BaseType)=="System.Enum","Hardlight.LeaderboardType original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{}),"Hardlight.LeaderboardType ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"value__","Classic","Recurring"}),"Hardlight.LeaderboardType full ordered original fields");
                Check(Canon(Field(type,"value__").FieldType)=="System.Int64" && (int)Field(type,"value__").Attributes==1542,"Hardlight.LeaderboardType.value__ original field type/flags");
                Check(Attributes(Field(type,"value__")).SequenceEqual(new string[]{}),"Hardlight.LeaderboardType.value__ authored field attributes/values");
                Check(Canon(Field(type,"Classic").FieldType)=="Hardlight.LeaderboardType" && (int)Field(type,"Classic").Attributes==32854,"Hardlight.LeaderboardType.Classic original field type/flags");
                Check(Attributes(Field(type,"Classic")).SequenceEqual(new string[]{}),"Hardlight.LeaderboardType.Classic authored field attributes/values");
                Check(Canon(Field(type,"Recurring").FieldType)=="Hardlight.LeaderboardType" && (int)Field(type,"Recurring").Attributes==32854,"Hardlight.LeaderboardType.Recurring original field type/flags");
                Check(Attributes(Field(type,"Recurring")).SequenceEqual(new string[]{}),"Hardlight.LeaderboardType.Recurring authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{}),"Hardlight.LeaderboardType full original method declaration order");
            }
            {
                Type type=typeof(Hardlight.LeaderboardTypeEqualityComparer);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1048577, "Hardlight.LeaderboardTypeEqualityComparer original type flags/assembly");
                Check(Canon(type.BaseType)=="System.Object","Hardlight.LeaderboardTypeEqualityComparer original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{}),"Hardlight.LeaderboardTypeEqualityComparer ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{}),"Hardlight.LeaderboardTypeEqualityComparer full ordered original fields");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"Equals","GetHashCode",".ctor"}),"Hardlight.LeaderboardTypeEqualityComparer full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Final, Virtual, HideBySig, VtableLayoutMask") && pars.Length==2 && pars[0].Name=="a" && Canon(pars[0].ParameterType)=="Hardlight.LeaderboardType" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="b" && Canon(pars[1].ParameterType)=="Hardlight.LeaderboardType" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[0]).ReturnType)=="System.Boolean","Hardlight.LeaderboardTypeEqualityComparer 0x060005c8 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Final, Virtual, HideBySig, VtableLayoutMask") && pars.Length==1 && pars[0].Name=="a" && Canon(pars[0].ParameterType)=="Hardlight.LeaderboardType" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[1]).ReturnType)=="System.Int32","Hardlight.LeaderboardTypeEqualityComparer 0x060005c9 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[2].GetParameters();
                    Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.LeaderboardTypeEqualityComparer 0x060005ca original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(Hardlight.EnumComparers);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1048577, "Hardlight.EnumComparers original type flags/assembly");
                Check(Canon(type.BaseType)=="System.Object","Hardlight.EnumComparers original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]"}),"Hardlight.EnumComparers ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"LeaderboardTypeComparer","UpdateOnComparer"}),"Hardlight.EnumComparers full ordered original fields");
                Check(Canon(Field(type,"LeaderboardTypeComparer").FieldType)=="Hardlight.LeaderboardTypeEqualityComparer" && (int)Field(type,"LeaderboardTypeComparer").Attributes==54,"Hardlight.EnumComparers.LeaderboardTypeComparer original field type/flags");
                Check(Attributes(Field(type,"LeaderboardTypeComparer")).SequenceEqual(new string[]{}),"Hardlight.EnumComparers.LeaderboardTypeComparer authored field attributes/values");
                Check(Canon(Field(type,"UpdateOnComparer").FieldType)=="Hardlight.UpdateOnEqualityComparer" && (int)Field(type,"UpdateOnComparer").Attributes==54,"Hardlight.EnumComparers.UpdateOnComparer original field type/flags");
                Check(Attributes(Field(type,"UpdateOnComparer")).SequenceEqual(new string[]{}),"Hardlight.EnumComparers.UpdateOnComparer authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{".ctor",".cctor"}),"Hardlight.EnumComparers full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.EnumComparers 0x060005cb original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, Static, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.EnumComparers 0x060005cc original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(Hardlight.ITimeScaled);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==161, "Hardlight.ITimeScaled original type flags/assembly");
                Check(Attributes(type).SequenceEqual(new string[]{}),"Hardlight.ITimeScaled ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{}),"Hardlight.ITimeScaled full ordered original fields");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"OnUpdate","OnFixedUpdate","OnLateUpdate","OnPause","OnResume","get_IsPaused","set_IsPaused"}),"Hardlight.ITimeScaled full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask") && pars.Length==1 && pars[0].Name=="deltaTime" && Canon(pars[0].ParameterType)=="System.Single" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[0]).ReturnType)=="System.Void","Hardlight.ITimeScaled 0x06000e73 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask") && pars.Length==1 && pars[0].Name=="deltaTime" && Canon(pars[0].ParameterType)=="System.Single" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[1]).ReturnType)=="System.Void","Hardlight.ITimeScaled 0x06000e74 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[2].GetParameters();
                    Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask") && pars.Length==1 && pars[0].Name=="deltaTime" && Canon(pars[0].ParameterType)=="System.Single" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[2]).ReturnType)=="System.Void","Hardlight.ITimeScaled 0x06000e75 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[3].GetParameters();
                    Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask") && pars.Length==0 && Canon(((MethodInfo)members[3]).ReturnType)=="System.Void","Hardlight.ITimeScaled 0x06000e76 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[4].GetParameters();
                    Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask") && pars.Length==0 && Canon(((MethodInfo)members[4]).ReturnType)=="System.Void","Hardlight.ITimeScaled 0x06000e77 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[5].GetParameters();
                    Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask, Abstract, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[5]).ReturnType)=="System.Boolean","Hardlight.ITimeScaled 0x06000e78 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[6].GetParameters();
                    Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask, Abstract, SpecialName") && pars.Length==1 && pars[0].Name=="value" && Canon(pars[0].ParameterType)=="System.Boolean" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[6]).ReturnType)=="System.Void","Hardlight.ITimeScaled 0x06000e79 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(Hardlight.TimeCategoryConfiguration);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1048577, "Hardlight.TimeCategoryConfiguration original type flags/assembly");
                Check(Canon(type.BaseType)=="Hardlight.SystemConfigurationAsset","Hardlight.TimeCategoryConfiguration original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]","UnityEngine.CreateAssetMenuAttribute()[fileName=TimeCategoryConfiguration|menuName=Hardlight/HLUnityCore/TimeCategoryConfiguration|order=0]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]"}),"Hardlight.TimeCategoryConfiguration ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_defaultTimescale","m_categoriesPrioritised","m_unityGlobalTime","m_generatesLookupStrings","m_autoGeneratedClassName","m_autoGeneratedLookupFolder","m_autoGeneratedNamespace"}),"Hardlight.TimeCategoryConfiguration full ordered original fields");
                Check(Canon(Field(type,"m_defaultTimescale").FieldType)=="System.Single" && (int)Field(type,"m_defaultTimescale").Attributes==1,"Hardlight.TimeCategoryConfiguration.m_defaultTimescale original field type/flags");
                Check(Attributes(Field(type,"m_defaultTimescale")).SequenceEqual(new string[]{"UnityEngine.SerializeField()[]"}),"Hardlight.TimeCategoryConfiguration.m_defaultTimescale authored field attributes/values");
                Check(Canon(Field(type,"m_categoriesPrioritised").FieldType)=="System.Collections.Generic.List`1<Hardlight.TimeCategoryObject>" && (int)Field(type,"m_categoriesPrioritised").Attributes==1,"Hardlight.TimeCategoryConfiguration.m_categoriesPrioritised original field type/flags");
                Check(Attributes(Field(type,"m_categoriesPrioritised")).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute(TimeCategories in priority order)[]","UnityEngine.SerializeField()[]"}),"Hardlight.TimeCategoryConfiguration.m_categoriesPrioritised authored field attributes/values");
                Check(Canon(Field(type,"m_unityGlobalTime").FieldType)=="Hardlight.TimeCategoryObject" && (int)Field(type,"m_unityGlobalTime").Attributes==1,"Hardlight.TimeCategoryConfiguration.m_unityGlobalTime original field type/flags");
                Check(Attributes(Field(type,"m_unityGlobalTime")).SequenceEqual(new string[]{"UnityEngine.SerializeField()[]"}),"Hardlight.TimeCategoryConfiguration.m_unityGlobalTime authored field attributes/values");
                Check(Canon(Field(type,"m_generatesLookupStrings").FieldType)=="System.Boolean" && (int)Field(type,"m_generatesLookupStrings").Attributes==1,"Hardlight.TimeCategoryConfiguration.m_generatesLookupStrings original field type/flags");
                Check(Attributes(Field(type,"m_generatesLookupStrings")).SequenceEqual(new string[]{"UnityEngine.SerializeField()[]"}),"Hardlight.TimeCategoryConfiguration.m_generatesLookupStrings authored field attributes/values");
                Check(Canon(Field(type,"m_autoGeneratedClassName").FieldType)=="System.String" && (int)Field(type,"m_autoGeneratedClassName").Attributes==1,"Hardlight.TimeCategoryConfiguration.m_autoGeneratedClassName original field type/flags");
                Check(Attributes(Field(type,"m_autoGeneratedClassName")).SequenceEqual(new string[]{"UnityEngine.SerializeField()[]"}),"Hardlight.TimeCategoryConfiguration.m_autoGeneratedClassName authored field attributes/values");
                Check(Canon(Field(type,"m_autoGeneratedLookupFolder").FieldType)=="System.String" && (int)Field(type,"m_autoGeneratedLookupFolder").Attributes==1,"Hardlight.TimeCategoryConfiguration.m_autoGeneratedLookupFolder original field type/flags");
                Check(Attributes(Field(type,"m_autoGeneratedLookupFolder")).SequenceEqual(new string[]{"UnityEngine.SerializeField()[]"}),"Hardlight.TimeCategoryConfiguration.m_autoGeneratedLookupFolder authored field attributes/values");
                Check(Canon(Field(type,"m_autoGeneratedNamespace").FieldType)=="System.String" && (int)Field(type,"m_autoGeneratedNamespace").Attributes==1,"Hardlight.TimeCategoryConfiguration.m_autoGeneratedNamespace original field type/flags");
                Check(Attributes(Field(type,"m_autoGeneratedNamespace")).SequenceEqual(new string[]{"UnityEngine.SerializeField()[]"}),"Hardlight.TimeCategoryConfiguration.m_autoGeneratedNamespace authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_DefaultTimescale","get_UnityGlobalTime","get_PrioritisedCategories","Validate","AddCategory","RemoveCategory",".ctor"}),"Hardlight.TimeCategoryConfiguration full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Single","Hardlight.TimeCategoryConfiguration 0x06000e7a original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="Hardlight.TimeCategoryObject","Hardlight.TimeCategoryConfiguration 0x06000e7b original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[2].GetParameters();
                    Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Collections.Generic.IEnumerable`1<Hardlight.TimeCategoryObject>","Hardlight.TimeCategoryConfiguration 0x06000e7c original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[3].GetParameters();
                    Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[3]).ReturnType)=="System.Void","Hardlight.TimeCategoryConfiguration 0x06000e7d original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[4].GetParameters();
                    Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="categoryObject" && Canon(pars[0].ParameterType)=="Hardlight.TimeCategoryObject" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[4]).ReturnType)=="System.Void","Hardlight.TimeCategoryConfiguration 0x06000e7e original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[5].GetParameters();
                    Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="categoryObject" && Canon(pars[0].ParameterType)=="Hardlight.TimeCategoryObject" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[5]).ReturnType)=="System.Void","Hardlight.TimeCategoryConfiguration 0x06000e7f original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[6].GetParameters();
                    Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.TimeCategoryConfiguration 0x06000e80 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(Hardlight.TimeCategoryObject);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1048577, "Hardlight.TimeCategoryObject original type flags/assembly");
                Check(Canon(type.BaseType)=="Hardlight.ScriptableObjectWithGuid","Hardlight.TimeCategoryObject original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]","UnityEngine.CreateAssetMenuAttribute()[fileName=TimeCategoryObject|menuName=Hardlight/HLUnityCore/TimeCategoryObject]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]"}),"Hardlight.TimeCategoryObject ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{}),"Hardlight.TimeCategoryObject full ordered original fields");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{".ctor"}),"Hardlight.TimeCategoryObject full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.TimeCategoryObject 0x06000e81 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(Hardlight.TimeManager);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1048577, "Hardlight.TimeManager original type flags/assembly");
                Check(Canon(type.BaseType)=="UnityEngine.MonoBehaviour","Hardlight.TimeManager original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]"}),"Hardlight.TimeManager ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_gameSpeedTimeCategories","m_gameSpeedMin","m_gameSpeedMax","TimeScaleMinDelta","SettingId","m_timeSettings","m_updateObjects","m_categoryPriority","m_totalTimeLookup","m_totalFixedTimeLookup","m_totalLateUpdateTimeLookup","m_pendingSubscriptions","m_categoryProfilerMarkers","m_overrideUnityTimescaleValue","m_currentUpdateCategory","m_currentUpdateType","m_fixedTimeSetting","m_unityTimeCategory","m_gameSpeedSetting","m_gameSpeedOverrideHandle","m_overrideSpeed","s_cachedTimeSettingResult","m_timeCategoryConfiguration","<GameSpeedApplied>k__BackingField","<UnityTimescaleOverride>k__BackingField"}),"Hardlight.TimeManager full ordered original fields");
                Check(Canon(Field(type,"m_gameSpeedTimeCategories").FieldType)=="System.Collections.Generic.List`1<Hardlight.TimeCategoryObject>" && (int)Field(type,"m_gameSpeedTimeCategories").Attributes==1,"Hardlight.TimeManager.m_gameSpeedTimeCategories original field type/flags");
                Check(Attributes(Field(type,"m_gameSpeedTimeCategories")).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute(The time categories to control game speed.)[]","UnityEngine.SerializeField()[]"}),"Hardlight.TimeManager.m_gameSpeedTimeCategories authored field attributes/values");
                Check(Canon(Field(type,"m_gameSpeedMin").FieldType)=="System.Single" && (int)Field(type,"m_gameSpeedMin").Attributes==1,"Hardlight.TimeManager.m_gameSpeedMin original field type/flags");
                Check(Attributes(Field(type,"m_gameSpeedMin")).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute(The minimum allowed game time setting override.)[]","UnityEngine.SerializeField()[]"}),"Hardlight.TimeManager.m_gameSpeedMin authored field attributes/values");
                Check(Canon(Field(type,"m_gameSpeedMax").FieldType)=="System.Single" && (int)Field(type,"m_gameSpeedMax").Attributes==1,"Hardlight.TimeManager.m_gameSpeedMax original field type/flags");
                Check(Attributes(Field(type,"m_gameSpeedMax")).SequenceEqual(new string[]{"UnityEngine.TooltipAttribute(The maximum allowed game time setting override.)[]","UnityEngine.SerializeField()[]"}),"Hardlight.TimeManager.m_gameSpeedMax authored field attributes/values");
                Check(Canon(Field(type,"TimeScaleMinDelta").FieldType)=="System.Single" && (int)Field(type,"TimeScaleMinDelta").Attributes==32854,"Hardlight.TimeManager.TimeScaleMinDelta original field type/flags");
                Check(Attributes(Field(type,"TimeScaleMinDelta")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.TimeScaleMinDelta authored field attributes/values");
                Check(Canon(Field(type,"SettingId").FieldType)=="System.Int32" && (int)Field(type,"SettingId").Attributes==32849,"Hardlight.TimeManager.SettingId original field type/flags");
                Check(Attributes(Field(type,"SettingId")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.SettingId authored field attributes/values");
                Check(Canon(Field(type,"m_timeSettings").FieldType)=="Hardlight.StackableData" && (int)Field(type,"m_timeSettings").Attributes==33,"Hardlight.TimeManager.m_timeSettings original field type/flags");
                Check(Attributes(Field(type,"m_timeSettings")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_timeSettings authored field attributes/values");
                Check(Canon(Field(type,"m_updateObjects").FieldType)=="System.Collections.Generic.Dictionary`2<Hardlight.UpdateOn,System.Collections.Generic.Dictionary`2<Hardlight.TimeCategoryObject,System.Collections.Generic.List`1<Hardlight.TimeManager+TimeScaledSubscription>>>" && (int)Field(type,"m_updateObjects").Attributes==33,"Hardlight.TimeManager.m_updateObjects original field type/flags");
                Check(Attributes(Field(type,"m_updateObjects")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_updateObjects authored field attributes/values");
                Check(Canon(Field(type,"m_categoryPriority").FieldType)=="System.Collections.Generic.List`1<Hardlight.TimeCategoryObject>" && (int)Field(type,"m_categoryPriority").Attributes==33,"Hardlight.TimeManager.m_categoryPriority original field type/flags");
                Check(Attributes(Field(type,"m_categoryPriority")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_categoryPriority authored field attributes/values");
                Check(Canon(Field(type,"m_totalTimeLookup").FieldType)=="System.Collections.Generic.Dictionary`2<Hardlight.TimeCategoryObject,System.Single>" && (int)Field(type,"m_totalTimeLookup").Attributes==33,"Hardlight.TimeManager.m_totalTimeLookup original field type/flags");
                Check(Attributes(Field(type,"m_totalTimeLookup")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_totalTimeLookup authored field attributes/values");
                Check(Canon(Field(type,"m_totalFixedTimeLookup").FieldType)=="System.Collections.Generic.Dictionary`2<Hardlight.TimeCategoryObject,System.Single>" && (int)Field(type,"m_totalFixedTimeLookup").Attributes==33,"Hardlight.TimeManager.m_totalFixedTimeLookup original field type/flags");
                Check(Attributes(Field(type,"m_totalFixedTimeLookup")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_totalFixedTimeLookup authored field attributes/values");
                Check(Canon(Field(type,"m_totalLateUpdateTimeLookup").FieldType)=="System.Collections.Generic.Dictionary`2<Hardlight.TimeCategoryObject,System.Single>" && (int)Field(type,"m_totalLateUpdateTimeLookup").Attributes==33,"Hardlight.TimeManager.m_totalLateUpdateTimeLookup original field type/flags");
                Check(Attributes(Field(type,"m_totalLateUpdateTimeLookup")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_totalLateUpdateTimeLookup authored field attributes/values");
                Check(Canon(Field(type,"m_pendingSubscriptions").FieldType)=="System.Collections.Generic.List`1<Hardlight.TimeManager+TimeScaledSubscription>" && (int)Field(type,"m_pendingSubscriptions").Attributes==33,"Hardlight.TimeManager.m_pendingSubscriptions original field type/flags");
                Check(Attributes(Field(type,"m_pendingSubscriptions")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_pendingSubscriptions authored field attributes/values");
                Check(Canon(Field(type,"m_categoryProfilerMarkers").FieldType)=="System.Collections.Generic.Dictionary`2<Hardlight.TimeCategoryObject,Unity.Profiling.ProfilerMarker>" && (int)Field(type,"m_categoryProfilerMarkers").Attributes==33,"Hardlight.TimeManager.m_categoryProfilerMarkers original field type/flags");
                Check(Attributes(Field(type,"m_categoryProfilerMarkers")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_categoryProfilerMarkers authored field attributes/values");
                Check(Canon(Field(type,"m_overrideUnityTimescaleValue").FieldType)=="System.Single" && (int)Field(type,"m_overrideUnityTimescaleValue").Attributes==1,"Hardlight.TimeManager.m_overrideUnityTimescaleValue original field type/flags");
                Check(Attributes(Field(type,"m_overrideUnityTimescaleValue")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_overrideUnityTimescaleValue authored field attributes/values");
                Check(Canon(Field(type,"m_currentUpdateCategory").FieldType)=="Hardlight.TimeCategoryObject" && (int)Field(type,"m_currentUpdateCategory").Attributes==1,"Hardlight.TimeManager.m_currentUpdateCategory original field type/flags");
                Check(Attributes(Field(type,"m_currentUpdateCategory")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_currentUpdateCategory authored field attributes/values");
                Check(Canon(Field(type,"m_currentUpdateType").FieldType)=="Hardlight.UpdateOn" && (int)Field(type,"m_currentUpdateType").Attributes==1,"Hardlight.TimeManager.m_currentUpdateType original field type/flags");
                Check(Attributes(Field(type,"m_currentUpdateType")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_currentUpdateType authored field attributes/values");
                Check(Canon(Field(type,"m_fixedTimeSetting").FieldType)=="System.Single" && (int)Field(type,"m_fixedTimeSetting").Attributes==1,"Hardlight.TimeManager.m_fixedTimeSetting original field type/flags");
                Check(Attributes(Field(type,"m_fixedTimeSetting")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_fixedTimeSetting authored field attributes/values");
                Check(Canon(Field(type,"m_unityTimeCategory").FieldType)=="Hardlight.TimeCategoryObject" && (int)Field(type,"m_unityTimeCategory").Attributes==1,"Hardlight.TimeManager.m_unityTimeCategory original field type/flags");
                Check(Attributes(Field(type,"m_unityTimeCategory")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_unityTimeCategory authored field attributes/values");
                Check(Canon(Field(type,"m_gameSpeedSetting").FieldType)=="Hardlight.TimeSetting" && (int)Field(type,"m_gameSpeedSetting").Attributes==1,"Hardlight.TimeManager.m_gameSpeedSetting original field type/flags");
                Check(Attributes(Field(type,"m_gameSpeedSetting")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_gameSpeedSetting authored field attributes/values");
                Check(Canon(Field(type,"m_gameSpeedOverrideHandle").FieldType)=="Hardlight.StackableDataHandle" && (int)Field(type,"m_gameSpeedOverrideHandle").Attributes==1,"Hardlight.TimeManager.m_gameSpeedOverrideHandle original field type/flags");
                Check(Attributes(Field(type,"m_gameSpeedOverrideHandle")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_gameSpeedOverrideHandle authored field attributes/values");
                Check(Canon(Field(type,"m_overrideSpeed").FieldType)=="System.Single" && (int)Field(type,"m_overrideSpeed").Attributes==1,"Hardlight.TimeManager.m_overrideSpeed original field type/flags");
                Check(Attributes(Field(type,"m_overrideSpeed")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_overrideSpeed authored field attributes/values");
                Check(Canon(Field(type,"s_cachedTimeSettingResult").FieldType)=="Hardlight.TimeSetting" && (int)Field(type,"s_cachedTimeSettingResult").Attributes==17,"Hardlight.TimeManager.s_cachedTimeSettingResult original field type/flags");
                Check(Attributes(Field(type,"s_cachedTimeSettingResult")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.s_cachedTimeSettingResult authored field attributes/values");
                Check(Canon(Field(type,"m_timeCategoryConfiguration").FieldType)=="Hardlight.TimeCategoryConfiguration" && (int)Field(type,"m_timeCategoryConfiguration").Attributes==1,"Hardlight.TimeManager.m_timeCategoryConfiguration original field type/flags");
                Check(Attributes(Field(type,"m_timeCategoryConfiguration")).SequenceEqual(new string[]{}),"Hardlight.TimeManager.m_timeCategoryConfiguration authored field attributes/values");
                Check(Canon(Field(type,"<GameSpeedApplied>k__BackingField").FieldType)=="System.Boolean" && (int)Field(type,"<GameSpeedApplied>k__BackingField").Attributes==1,"Hardlight.TimeManager.<GameSpeedApplied>k__BackingField original field type/flags");
                Check(Attributes(Field(type,"<GameSpeedApplied>k__BackingField")).SequenceEqual(new string[]{"System.Runtime.CompilerServices.CompilerGeneratedAttribute()[]"}),"Hardlight.TimeManager.<GameSpeedApplied>k__BackingField authored field attributes/values");
                Check(Canon(Field(type,"<UnityTimescaleOverride>k__BackingField").FieldType)=="System.Boolean" && (int)Field(type,"<UnityTimescaleOverride>k__BackingField").Attributes==1,"Hardlight.TimeManager.<UnityTimescaleOverride>k__BackingField original field type/flags");
                Check(Attributes(Field(type,"<UnityTimescaleOverride>k__BackingField")).SequenceEqual(new string[]{"System.Runtime.CompilerServices.CompilerGeneratedAttribute()[]"}),"Hardlight.TimeManager.<UnityTimescaleOverride>k__BackingField authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_GameSpeedMin","get_GameSpeedMax","get_GameSpeedApplied","set_GameSpeedApplied","get_UnityTimescaleOverride","set_UnityTimescaleOverride","Awake","Reset","OnDestroy","FixedUpdate","Update","LateUpdate","PerformUpdate","UpdateCategory","Subscribe","Unsubscribe","ResetTotalTimes","GetTotalTime","GetTotalFixedTime","GetDeltaTime","GetFixedDeltaTime","GetTimescale","ApplyTimeSetting","UpdateTimeSetting","RemoveTimeSetting","TimeSettingMultiply","InitialiseGameSpeedOverride","UpdateGameSpeedOverride","UpdateGameSpeedOverride","RemoveGameSpeedOverride","OverrideUnityTimescale","GetUnityTimescale","ClearUnityTimescaleOverride",".ctor"}),"Hardlight.TimeManager full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Single","Hardlight.TimeManager 0x06000e82 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Single","Hardlight.TimeManager 0x06000e83 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[2].GetParameters();
                    Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Boolean","Hardlight.TimeManager 0x06000e84 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[3].GetParameters();
                    Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && pars.Length==1 && pars[0].Name=="value" && Canon(pars[0].ParameterType)=="System.Boolean" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[3]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e85 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[4].GetParameters();
                    Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[4]).ReturnType)=="System.Boolean","Hardlight.TimeManager 0x06000e86 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[5].GetParameters();
                    Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig, SpecialName") && pars.Length==1 && pars[0].Name=="value" && Canon(pars[0].ParameterType)=="System.Boolean" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[5]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e87 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[6].GetParameters();
                    Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[6]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e88 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[7].GetParameters();
                    Check(members[7].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[7]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e89 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[8].GetParameters();
                    Check(members[8].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[8]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e8a original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[9].GetParameters();
                    Check(members[9].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[9]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e8b original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[10].GetParameters();
                    Check(members[10].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[10]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e8c original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[11].GetParameters();
                    Check(members[11].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[11]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e8d original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[12].GetParameters();
                    Check(members[12].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && pars.Length==4 && pars[0].Name=="updateOn" && Canon(pars[0].ParameterType)=="Hardlight.UpdateOn" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="deltaTime" && Canon(pars[1].ParameterType)=="System.Single" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[2].Name=="totalTimeLookup" && Canon(pars[2].ParameterType)=="System.Collections.Generic.Dictionary`2<Hardlight.TimeCategoryObject,System.Single>" && pars[2].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[3].Name=="updateAction" && Canon(pars[3].ParameterType)=="System.Action`2<Hardlight.ITimeScaled,System.Single>" && pars[3].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[12]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e8e original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[13].GetParameters();
                    Check(members[13].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && pars.Length==3 && pars[0].Name=="updatedByCategory" && Canon(pars[0].ParameterType)=="System.Collections.Generic.List`1<Hardlight.TimeManager+TimeScaledSubscription>" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="scaledTime" && Canon(pars[1].ParameterType)=="System.Single" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[2].Name=="updateAction" && Canon(pars[2].ParameterType)=="System.Action`2<Hardlight.ITimeScaled,System.Single>" && pars[2].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[13]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e8f original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[14].GetParameters();
                    Check(members[14].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==4 && pars[0].Name=="tickedObject" && Canon(pars[0].ParameterType)=="Hardlight.ITimeScaled" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="category" && Canon(pars[1].ParameterType)=="Hardlight.TimeCategoryObject" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[2].Name=="updateOn" && Canon(pars[2].ParameterType)=="Hardlight.UpdateOn" && pars[2].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"Optional, HasDefault") && pars[3].Name=="path" && Canon(pars[3].ParameterType)=="System.String" && pars[3].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"Optional, HasDefault") && Canon(((MethodInfo)members[14]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e90 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[15].GetParameters();
                    Check(members[15].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==3 && pars[0].Name=="tickedObject" && Canon(pars[0].ParameterType)=="Hardlight.ITimeScaled" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="category" && Canon(pars[1].ParameterType)=="Hardlight.TimeCategoryObject" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[2].Name=="updateOn" && Canon(pars[2].ParameterType)=="Hardlight.UpdateOn" && pars[2].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"Optional, HasDefault") && Canon(((MethodInfo)members[15]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e91 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[16].GetParameters();
                    Check(members[16].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[16]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e92 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[17].GetParameters();
                    Check(members[17].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="category" && Canon(pars[0].ParameterType)=="Hardlight.TimeCategoryObject" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[17]).ReturnType)=="System.Single","Hardlight.TimeManager 0x06000e93 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[18].GetParameters();
                    Check(members[18].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="category" && Canon(pars[0].ParameterType)=="Hardlight.TimeCategoryObject" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[18]).ReturnType)=="System.Single","Hardlight.TimeManager 0x06000e94 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[19].GetParameters();
                    Check(members[19].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="category" && Canon(pars[0].ParameterType)=="Hardlight.TimeCategoryObject" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[19]).ReturnType)=="System.Single","Hardlight.TimeManager 0x06000e95 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[20].GetParameters();
                    Check(members[20].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="category" && Canon(pars[0].ParameterType)=="Hardlight.TimeCategoryObject" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[20]).ReturnType)=="System.Single","Hardlight.TimeManager 0x06000e96 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[21].GetParameters();
                    Check(members[21].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="category" && Canon(pars[0].ParameterType)=="Hardlight.TimeCategoryObject" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[21]).ReturnType)=="System.Single","Hardlight.TimeManager 0x06000e97 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[22].GetParameters();
                    Check(members[22].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="setting" && Canon(pars[0].ParameterType)=="Hardlight.TimeSetting" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[22]).ReturnType)=="Hardlight.StackableDataHandle","Hardlight.TimeManager 0x06000e98 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[23].GetParameters();
                    Check(members[23].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==2 && pars[0].Name=="handle" && Canon(pars[0].ParameterType)=="Hardlight.StackableDataHandle" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="setting" && Canon(pars[1].ParameterType)=="Hardlight.TimeSetting" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[23]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e99 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[24].GetParameters();
                    Check(members[24].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="handle" && Canon(pars[0].ParameterType)=="Hardlight.StackableDataHandle" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[24]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e9a original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[25].GetParameters();
                    Check(members[25].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, Static, HideBySig") && pars.Length==2 && pars[0].Name=="stackableDataContainer" && Canon(pars[0].ParameterType)=="Hardlight.StackableData+StackableDataContainer`1<Hardlight.TimeSetting>" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="result" && Canon(pars[1].ParameterType)=="Hardlight.StackableData+ResultCarrier`1<Hardlight.TimeSetting>&" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[25]).ReturnType)=="Hardlight.StackableData+OperationAction","Hardlight.TimeManager 0x06000e9b original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[26].GetParameters();
                    Check(members[26].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[26]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e9c original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[27].GetParameters();
                    Check(members[27].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="overrideSpeed" && Canon(pars[0].ParameterType)=="System.Single" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[27]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e9d original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[28].GetParameters();
                    Check(members[28].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[28]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e9e original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[29].GetParameters();
                    Check(members[29].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[29]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000e9f original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[30].GetParameters();
                    Check(members[30].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==2 && pars[0].Name=="timescale" && Canon(pars[0].ParameterType)=="System.Single" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="debugOverride" && Canon(pars[1].ParameterType)=="System.Boolean" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"Optional, HasDefault") && Canon(((MethodInfo)members[30]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000ea0 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[31].GetParameters();
                    Check(members[31].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[31]).ReturnType)=="System.Single","Hardlight.TimeManager 0x06000ea1 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[32].GetParameters();
                    Check(members[32].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[32]).ReturnType)=="System.Void","Hardlight.TimeManager 0x06000ea2 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[33].GetParameters();
                    Check(members[33].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.TimeManager 0x06000ea3 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(TimeManager).GetNestedType("TimeScaledSubscription",BindingFlags.NonPublic);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1048579, "Hardlight.TimeManager+TimeScaledSubscription original type flags/assembly");
                Check(Canon(type.BaseType)=="System.Object","Hardlight.TimeManager+TimeScaledSubscription original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]"}),"Hardlight.TimeManager+TimeScaledSubscription ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"TimeScaledObject","IsSubscribed","ProfilerMarker","TransformPath"}),"Hardlight.TimeManager+TimeScaledSubscription full ordered original fields");
                Check(Canon(Field(type,"TimeScaledObject").FieldType)=="Hardlight.ITimeScaled" && (int)Field(type,"TimeScaledObject").Attributes==6,"Hardlight.TimeManager+TimeScaledSubscription.TimeScaledObject original field type/flags");
                Check(Attributes(Field(type,"TimeScaledObject")).SequenceEqual(new string[]{}),"Hardlight.TimeManager+TimeScaledSubscription.TimeScaledObject authored field attributes/values");
                Check(Canon(Field(type,"IsSubscribed").FieldType)=="System.Boolean" && (int)Field(type,"IsSubscribed").Attributes==6,"Hardlight.TimeManager+TimeScaledSubscription.IsSubscribed original field type/flags");
                Check(Attributes(Field(type,"IsSubscribed")).SequenceEqual(new string[]{}),"Hardlight.TimeManager+TimeScaledSubscription.IsSubscribed authored field attributes/values");
                Check(Canon(Field(type,"ProfilerMarker").FieldType)=="Unity.Profiling.ProfilerMarker" && (int)Field(type,"ProfilerMarker").Attributes==6,"Hardlight.TimeManager+TimeScaledSubscription.ProfilerMarker original field type/flags");
                Check(Attributes(Field(type,"ProfilerMarker")).SequenceEqual(new string[]{}),"Hardlight.TimeManager+TimeScaledSubscription.ProfilerMarker authored field attributes/values");
                Check(Canon(Field(type,"TransformPath").FieldType)=="System.String" && (int)Field(type,"TransformPath").Attributes==6,"Hardlight.TimeManager+TimeScaledSubscription.TransformPath original field type/flags");
                Check(Attributes(Field(type,"TransformPath")).SequenceEqual(new string[]{}),"Hardlight.TimeManager+TimeScaledSubscription.TransformPath authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{".ctor"}),"Hardlight.TimeManager+TimeScaledSubscription full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.TimeManager+TimeScaledSubscription 0x06000ea4 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(TimeManager).GetNestedType("<>c",BindingFlags.NonPublic);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1057027, "Hardlight.TimeManager+<>c original type flags/assembly");
                Check(Canon(type.BaseType)=="System.Object","Hardlight.TimeManager+<>c original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"System.Runtime.CompilerServices.CompilerGeneratedAttribute()[]"}),"Hardlight.TimeManager+<>c ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"<>9","<>9__39_0","<>9__40_0","<>9__41_0"}),"Hardlight.TimeManager+<>c full ordered original fields");
                Check(Canon(Field(type,"<>9").FieldType)=="Hardlight.TimeManager+<>c" && (int)Field(type,"<>9").Attributes==54,"Hardlight.TimeManager+<>c.<>9 original field type/flags");
                Check(Attributes(Field(type,"<>9")).SequenceEqual(new string[]{}),"Hardlight.TimeManager+<>c.<>9 authored field attributes/values");
                Check(Canon(Field(type,"<>9__39_0").FieldType)=="System.Action`2<Hardlight.ITimeScaled,System.Single>" && (int)Field(type,"<>9__39_0").Attributes==22,"Hardlight.TimeManager+<>c.<>9__39_0 original field type/flags");
                Check(Attributes(Field(type,"<>9__39_0")).SequenceEqual(new string[]{}),"Hardlight.TimeManager+<>c.<>9__39_0 authored field attributes/values");
                Check(Canon(Field(type,"<>9__40_0").FieldType)=="System.Action`2<Hardlight.ITimeScaled,System.Single>" && (int)Field(type,"<>9__40_0").Attributes==22,"Hardlight.TimeManager+<>c.<>9__40_0 original field type/flags");
                Check(Attributes(Field(type,"<>9__40_0")).SequenceEqual(new string[]{}),"Hardlight.TimeManager+<>c.<>9__40_0 authored field attributes/values");
                Check(Canon(Field(type,"<>9__41_0").FieldType)=="System.Action`2<Hardlight.ITimeScaled,System.Single>" && (int)Field(type,"<>9__41_0").Attributes==22,"Hardlight.TimeManager+<>c.<>9__41_0 original field type/flags");
                Check(Attributes(Field(type,"<>9__41_0")).SequenceEqual(new string[]{}),"Hardlight.TimeManager+<>c.<>9__41_0 authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{".cctor",".ctor","<FixedUpdate>b__39_0","<Update>b__40_0","<LateUpdate>b__41_0"}),"Hardlight.TimeManager+<>c full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, Static, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.TimeManager+<>c 0x06000ea5 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.TimeManager+<>c 0x06000ea6 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[2].GetParameters();
                    Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Assembly, HideBySig") && pars.Length==2 && pars[0].Name=="timeScaledObject" && Canon(pars[0].ParameterType)=="Hardlight.ITimeScaled" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="deltaTime" && Canon(pars[1].ParameterType)=="System.Single" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[2]).ReturnType)=="System.Void","Hardlight.TimeManager+<>c 0x06000ea7 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[3].GetParameters();
                    Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Assembly, HideBySig") && pars.Length==2 && pars[0].Name=="timeScaledObject" && Canon(pars[0].ParameterType)=="Hardlight.ITimeScaled" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="deltaTime" && Canon(pars[1].ParameterType)=="System.Single" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[3]).ReturnType)=="System.Void","Hardlight.TimeManager+<>c 0x06000ea8 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[4].GetParameters();
                    Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Assembly, HideBySig") && pars.Length==2 && pars[0].Name=="timeScaledObject" && Canon(pars[0].ParameterType)=="Hardlight.ITimeScaled" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="deltaTime" && Canon(pars[1].ParameterType)=="System.Single" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[4]).ReturnType)=="System.Void","Hardlight.TimeManager+<>c 0x06000ea9 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(Hardlight.TimeSetting);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1056769, "Hardlight.TimeSetting original type flags/assembly");
                Check(Canon(type.BaseType)=="System.Object","Hardlight.TimeSetting original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]"}),"Hardlight.TimeSetting ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_overridesDictionary","DefaultTimescale","m_kvpsToModify"}),"Hardlight.TimeSetting full ordered original fields");
                Check(Canon(Field(type,"m_overridesDictionary").FieldType)=="Hardlight.SerializableDictionary`2<Hardlight.TimeCategoryObject,System.Single>" && (int)Field(type,"m_overridesDictionary").Attributes==4,"Hardlight.TimeSetting.m_overridesDictionary original field type/flags");
                Check(Attributes(Field(type,"m_overridesDictionary")).SequenceEqual(new string[]{"UnityEngine.SerializeField()[]"}),"Hardlight.TimeSetting.m_overridesDictionary authored field attributes/values");
                Check(Canon(Field(type,"DefaultTimescale").FieldType)=="System.Single" && (int)Field(type,"DefaultTimescale").Attributes==32849,"Hardlight.TimeSetting.DefaultTimescale original field type/flags");
                Check(Attributes(Field(type,"DefaultTimescale")).SequenceEqual(new string[]{}),"Hardlight.TimeSetting.DefaultTimescale authored field attributes/values");
                Check(Canon(Field(type,"m_kvpsToModify").FieldType)=="System.Collections.Generic.List`1<System.Collections.Generic.KeyValuePair`2<Hardlight.TimeCategoryObject,System.Single>>" && (int)Field(type,"m_kvpsToModify").Attributes==33,"Hardlight.TimeSetting.m_kvpsToModify original field type/flags");
                Check(Attributes(Field(type,"m_kvpsToModify")).SequenceEqual(new string[]{}),"Hardlight.TimeSetting.m_kvpsToModify authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_OverrideDictionaryLookup","Lerp","GetDefault","Clone","SetCategory","GetOverrideWithDefault","Copy",".ctor"}),"Hardlight.TimeSetting full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Collections.Generic.IReadOnlyDictionary`2<Hardlight.TimeCategoryObject,System.Single>","Hardlight.TimeSetting 0x06000edf original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==3 && pars[0].Name=="from" && Canon(pars[0].ParameterType)=="Hardlight.TimeSetting" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="to" && Canon(pars[1].ParameterType)=="Hardlight.TimeSetting" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[2].Name=="t" && Canon(pars[2].ParameterType)=="System.Single" && pars[2].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[1]).ReturnType)=="System.Void","Hardlight.TimeSetting 0x06000ee0 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[2].GetParameters();
                    Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==1 && pars[0].Name=="scaling" && Canon(pars[0].ParameterType)=="System.Single" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"Optional, HasDefault") && Canon(((MethodInfo)members[2]).ReturnType)=="Hardlight.TimeSetting","Hardlight.TimeSetting 0x06000ee1 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[3].GetParameters();
                    Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==1 && pars[0].Name=="baseSetting" && Canon(pars[0].ParameterType)=="Hardlight.TimeSetting" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[3]).ReturnType)=="Hardlight.TimeSetting","Hardlight.TimeSetting 0x06000ee2 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[4].GetParameters();
                    Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==2 && pars[0].Name=="timeCategory" && Canon(pars[0].ParameterType)=="Hardlight.TimeCategoryObject" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="timeScale" && Canon(pars[1].ParameterType)=="System.Single" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[4]).ReturnType)=="System.Void","Hardlight.TimeSetting 0x06000ee3 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[5].GetParameters();
                    Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==2 && pars[0].Name=="category" && Canon(pars[0].ParameterType)=="Hardlight.TimeCategoryObject" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="defaultValue" && Canon(pars[1].ParameterType)=="System.Single" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[5]).ReturnType)=="System.Single","Hardlight.TimeSetting 0x06000ee4 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[6].GetParameters();
                    Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="other" && Canon(pars[0].ParameterType)=="Hardlight.TimeSetting" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[6]).ReturnType)=="System.Void","Hardlight.TimeSetting 0x06000ee5 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[7].GetParameters();
                    Check(members[7].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.TimeSetting 0x06000ee6 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(Hardlight.UpdateOn);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==257, "Hardlight.UpdateOn original type flags/assembly");
                Check(Canon(type.BaseType)=="System.Enum","Hardlight.UpdateOn original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{}),"Hardlight.UpdateOn ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"value__","FixedUpdate","LateUpdate","None","Update"}),"Hardlight.UpdateOn full ordered original fields");
                Check(Canon(Field(type,"value__").FieldType)=="System.Int32" && (int)Field(type,"value__").Attributes==1542,"Hardlight.UpdateOn.value__ original field type/flags");
                Check(Attributes(Field(type,"value__")).SequenceEqual(new string[]{}),"Hardlight.UpdateOn.value__ authored field attributes/values");
                Check(Canon(Field(type,"FixedUpdate").FieldType)=="Hardlight.UpdateOn" && (int)Field(type,"FixedUpdate").Attributes==32854,"Hardlight.UpdateOn.FixedUpdate original field type/flags");
                Check(Attributes(Field(type,"FixedUpdate")).SequenceEqual(new string[]{}),"Hardlight.UpdateOn.FixedUpdate authored field attributes/values");
                Check(Canon(Field(type,"LateUpdate").FieldType)=="Hardlight.UpdateOn" && (int)Field(type,"LateUpdate").Attributes==32854,"Hardlight.UpdateOn.LateUpdate original field type/flags");
                Check(Attributes(Field(type,"LateUpdate")).SequenceEqual(new string[]{}),"Hardlight.UpdateOn.LateUpdate authored field attributes/values");
                Check(Canon(Field(type,"None").FieldType)=="Hardlight.UpdateOn" && (int)Field(type,"None").Attributes==32854,"Hardlight.UpdateOn.None original field type/flags");
                Check(Attributes(Field(type,"None")).SequenceEqual(new string[]{}),"Hardlight.UpdateOn.None authored field attributes/values");
                Check(Canon(Field(type,"Update").FieldType)=="Hardlight.UpdateOn" && (int)Field(type,"Update").Attributes==32854,"Hardlight.UpdateOn.Update original field type/flags");
                Check(Attributes(Field(type,"Update")).SequenceEqual(new string[]{}),"Hardlight.UpdateOn.Update authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{}),"Hardlight.UpdateOn full original method declaration order");
            }
            {
                Type type=typeof(Hardlight.UpdateOnEqualityComparer);
                Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1048577, "Hardlight.UpdateOnEqualityComparer original type flags/assembly");
                Check(Canon(type.BaseType)=="System.Object","Hardlight.UpdateOnEqualityComparer original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]"}),"Hardlight.UpdateOnEqualityComparer ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{}),"Hardlight.UpdateOnEqualityComparer full ordered original fields");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"Equals","GetHashCode",".ctor"}),"Hardlight.UpdateOnEqualityComparer full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Final, Virtual, HideBySig, VtableLayoutMask") && pars.Length==2 && pars[0].Name=="a" && Canon(pars[0].ParameterType)=="Hardlight.UpdateOn" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="b" && Canon(pars[1].ParameterType)=="Hardlight.UpdateOn" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[0]).ReturnType)=="System.Boolean","Hardlight.UpdateOnEqualityComparer 0x06000ee7 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Final, Virtual, HideBySig, VtableLayoutMask") && pars.Length==1 && pars[0].Name=="a" && Canon(pars[0].ParameterType)=="Hardlight.UpdateOn" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[1]).ReturnType)=="System.Int32","Hardlight.UpdateOnEqualityComparer 0x06000ee8 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[2].GetParameters();
                    Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"Hardlight.UpdateOnEqualityComparer 0x06000ee9 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(HardlightProject.TimeCategoryLookup);
                Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1048577, "HardlightProject.TimeCategoryLookup original type flags/assembly");
                Check(Canon(type.BaseType)=="Hardlight.SystemConfigurationAsset","HardlightProject.TimeCategoryLookup original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"UnityEngine.CreateAssetMenuAttribute()[fileName=TimeCategoryLookup|menuName=HardlightProject/Core Game]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]"}),"HardlightProject.TimeCategoryLookup ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"Dictionary"}),"HardlightProject.TimeCategoryLookup full ordered original fields");
                Check(Canon(Field(type,"Dictionary").FieldType)=="Hardlight.SerializableDictionary`2<HardlightProject.TimeCategory,Hardlight.TimeCategoryObject>" && (int)Field(type,"Dictionary").Attributes==6,"HardlightProject.TimeCategoryLookup.Dictionary original field type/flags");
                Check(Attributes(Field(type,"Dictionary")).SequenceEqual(new string[]{"UnityEngine.SerializeField()[]"}),"HardlightProject.TimeCategoryLookup.Dictionary authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"Validate",".ctor"}),"HardlightProject.TimeCategoryLookup full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig") && pars.Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Void","HardlightProject.TimeCategoryLookup 0x06002de3 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"HardlightProject.TimeCategoryLookup 0x06002de4 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(HardlightProject.TimeExtensions);
                Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1048961, "HardlightProject.TimeExtensions original type flags/assembly");
                Check(Canon(type.BaseType)=="System.Object","HardlightProject.TimeExtensions original base identity");
                // Native has the genuine Extension marker last. C#9 necessarily emits it first;
                // retain all exact values, record this specific order frontier, approve no metadata parity.
                Check(Attributes(type).SequenceEqual(new string[]{"System.Runtime.CompilerServices.ExtensionAttribute()[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]"}),"TimeExtensions exact C#9 order; original marker-last frontier remains explicit");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"s_config_Internal"}),"HardlightProject.TimeExtensions full ordered original fields");
                Check(Canon(Field(type,"s_config_Internal").FieldType)=="HardlightProject.TimeCategoryLookup" && (int)Field(type,"s_config_Internal").Attributes==17,"HardlightProject.TimeExtensions.s_config_Internal original field type/flags");
                Check(Attributes(Field(type,"s_config_Internal")).SequenceEqual(new string[]{}),"HardlightProject.TimeExtensions.s_config_Internal authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_s_config","SetCategory","ApplyTimeSetting_SDT","GetFixedDeltaTime","GetDeltaTime","GetTotalTime","GetTotalFixedTime","GetTimescale","Subscribe","Unsubscribe","GetScriptableObject"}),"HardlightProject.TimeExtensions full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, Static, HideBySig, SpecialName") && pars.Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="HardlightProject.TimeCategoryLookup","HardlightProject.TimeExtensions 0x06002ded original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==3 && pars[0].Name=="timeSetting" && Canon(pars[0].ParameterType)=="Hardlight.TimeSetting" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="category" && Canon(pars[1].ParameterType)=="HardlightProject.TimeCategory" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[2].Name=="timeScale" && Canon(pars[2].ParameterType)=="System.Single" && pars[2].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[1]).ReturnType)=="System.Void","HardlightProject.TimeExtensions 0x06002dee original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[2].GetParameters();
                    Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==2 && pars[0].Name=="timeManager" && Canon(pars[0].ParameterType)=="Hardlight.TimeManager" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="setting" && Canon(pars[1].ParameterType)=="HardlightProject.TimeSettings_SDT" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[2]).ReturnType)=="Hardlight.StackableDataHandle","HardlightProject.TimeExtensions 0x06002def original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[3].GetParameters();
                    Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==2 && pars[0].Name=="timeManager" && Canon(pars[0].ParameterType)=="Hardlight.TimeManager" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="category" && Canon(pars[1].ParameterType)=="HardlightProject.TimeCategory" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[3]).ReturnType)=="System.Single","HardlightProject.TimeExtensions 0x06002df0 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[4].GetParameters();
                    Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==2 && pars[0].Name=="timeManager" && Canon(pars[0].ParameterType)=="Hardlight.TimeManager" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="category" && Canon(pars[1].ParameterType)=="HardlightProject.TimeCategory" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[4]).ReturnType)=="System.Single","HardlightProject.TimeExtensions 0x06002df1 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[5].GetParameters();
                    Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==2 && pars[0].Name=="timeManager" && Canon(pars[0].ParameterType)=="Hardlight.TimeManager" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="category" && Canon(pars[1].ParameterType)=="HardlightProject.TimeCategory" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[5]).ReturnType)=="System.Single","HardlightProject.TimeExtensions 0x06002df2 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[6].GetParameters();
                    Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==2 && pars[0].Name=="timeManager" && Canon(pars[0].ParameterType)=="Hardlight.TimeManager" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="category" && Canon(pars[1].ParameterType)=="HardlightProject.TimeCategory" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[6]).ReturnType)=="System.Single","HardlightProject.TimeExtensions 0x06002df3 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[7].GetParameters();
                    Check(members[7].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==2 && pars[0].Name=="timeManager" && Canon(pars[0].ParameterType)=="Hardlight.TimeManager" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="category" && Canon(pars[1].ParameterType)=="HardlightProject.TimeCategory" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[7]).ReturnType)=="System.Single","HardlightProject.TimeExtensions 0x06002df4 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[8].GetParameters();
                    Check(members[8].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==5 && pars[0].Name=="timeManager" && Canon(pars[0].ParameterType)=="Hardlight.TimeManager" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="timeScaled" && Canon(pars[1].ParameterType)=="Hardlight.ITimeScaled" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[2].Name=="category" && Canon(pars[2].ParameterType)=="HardlightProject.TimeCategory" && pars[2].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[3].Name=="updateOn" && Canon(pars[3].ParameterType)=="Hardlight.UpdateOn" && pars[3].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"Optional, HasDefault") && pars[4].Name=="path" && Canon(pars[4].ParameterType)=="System.String" && pars[4].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"Optional, HasDefault") && Canon(((MethodInfo)members[8]).ReturnType)=="System.Void","HardlightProject.TimeExtensions 0x06002df5 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[9].GetParameters();
                    Check(members[9].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && pars.Length==4 && pars[0].Name=="timeManager" && Canon(pars[0].ParameterType)=="Hardlight.TimeManager" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[1].Name=="timeScaled" && Canon(pars[1].ParameterType)=="Hardlight.ITimeScaled" && pars[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[2].Name=="category" && Canon(pars[2].ParameterType)=="HardlightProject.TimeCategory" && pars[2].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && pars[3].Name=="updateOn" && Canon(pars[3].ParameterType)=="Hardlight.UpdateOn" && pars[3].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"Optional, HasDefault") && Canon(((MethodInfo)members[9]).ReturnType)=="System.Void","HardlightProject.TimeExtensions 0x06002df6 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[10].GetParameters();
                    Check(members[10].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, Static, HideBySig") && pars.Length==1 && pars[0].Name=="category" && Canon(pars[0].ParameterType)=="HardlightProject.TimeCategory" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[10]).ReturnType)=="Hardlight.TimeCategoryObject","HardlightProject.TimeExtensions 0x06002df7 original signature/parameter names/flags");
                }
            }
            {
                Type type=typeof(HardlightProject.TimeSettings_SDT);
                Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1056769, "HardlightProject.TimeSettings_SDT original type flags/assembly");
                Check(Canon(type.BaseType)=="Hardlight.TimeSetting","HardlightProject.TimeSettings_SDT original base identity");
                Check(Attributes(type).SequenceEqual(new string[]{"Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(1|False)[]","Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(2|False)[]"}),"HardlightProject.TimeSettings_SDT ordered authored type attributes/values");
                Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_overrides"}),"HardlightProject.TimeSettings_SDT full ordered original fields");
                Check(Canon(Field(type,"m_overrides").FieldType)=="Hardlight.SerializableDictionary`2<HardlightProject.TimeCategory,System.Single>" && (int)Field(type,"m_overrides").Attributes==1,"HardlightProject.TimeSettings_SDT.m_overrides original field type/flags");
                Check(Attributes(Field(type,"m_overrides")).SequenceEqual(new string[]{"UnityEngine.SerializeField()[]"}),"HardlightProject.TimeSettings_SDT.m_overrides authored field attributes/values");
                MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
                Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"ApplyTimeCategoryObjectConversion",".ctor"}),"HardlightProject.TimeSettings_SDT full original method declaration order");
                {
                    ParameterInfo[] pars=members[0].GetParameters();
                    Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && pars.Length==1 && pars[0].Name=="lookup" && Canon(pars[0].ParameterType)=="System.Collections.Generic.IDictionary`2<HardlightProject.TimeCategory,Hardlight.TimeCategoryObject>" && pars[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && Canon(((MethodInfo)members[0]).ReturnType)=="System.Void","HardlightProject.TimeSettings_SDT 0x06002e70 original signature/parameter names/flags");
                }
                {
                    ParameterInfo[] pars=members[1].GetParameters();
                    Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName, RTSpecialName") && pars.Length==0,"HardlightProject.TimeSettings_SDT 0x06002e71 original signature/parameter names/flags");
                }
            }
        }

        private sealed class ReferenceKeys : IEqualityComparer<TimeCategoryObject>
        {
            internal TimeCategoryObject ThrowOn;
            public bool Equals(TimeCategoryObject a,TimeCategoryObject b) { return ReferenceEquals(a,b); }
            public int GetHashCode(TimeCategoryObject key)
            { if(ReferenceEquals(key,ThrowOn))throw new ApplicationException("fixture key hash"); return RuntimeHelpers.GetHashCode(key); }
        }
        private static SerializableDictionary<TimeCategoryObject,float> Dictionary(TimeSetting setting)
        { return (SerializableDictionary<TimeCategoryObject,float>)Read(setting,"m_overridesDictionary"); }
        private static TimeSetting Setting(params object[] entries)
        {
            var setting=new TimeSetting();
            var dictionary=new SerializableDictionary<TimeCategoryObject,float>(new ReferenceKeys());
            Write(setting,"m_overridesDictionary",dictionary);
            for(int i=0;i<entries.Length;i+=2)setting.SetCategory((TimeCategoryObject)entries[i],(float)entries[i+1]);
            return setting;
        }
        private static void SettingChecks()
        {
            var a=Raw<TimeCategoryObject>();var b=Raw<TimeCategoryObject>();var c=Raw<TimeCategoryObject>();
            Write(a,"m_guid","managed-a");Write(b,"m_guid","managed-b");Write(c,"m_guid","managed-c");
            var fresh=new TimeSetting();
            Check(fresh.OverrideDictionaryLookup.Count==0,"original setting dictionary initializes empty");
            Check(((IList)Read(fresh,"m_kvpsToModify")).Count==0,"original scratch list initializes empty");
            Check(ReferenceEquals(fresh.OverrideDictionaryLookup,Dictionary(fresh)),"readonly view preserves mutable dictionary identity");
            Check(fresh.GetOverrideWithDefault(a,3.5f)==3.5f,"missing category default forwards without mutation");
            Check(fresh.OverrideDictionaryLookup.Count==0,"fallback leaves dictionary empty");
            var from=Setting(a,2f,b,4f,c,99f);var to=Setting(a,6f);var target=Setting(a,-7f,b,-9f);
            target.Lerp(from,to,0.25f);
            Check(target.GetOverrideWithDefault(a,-1)==3f,"interpolate matching key");
            Check(target.GetOverrideWithDefault(b,-1)==3.25f,"missing endpoint defaults to one");
            Check(target.OverrideDictionaryLookup.Count==2,"endpoint-only keys are not introduced");
            Check(((IList)Read(target,"m_kvpsToModify")).Count==0,"normal interpolation drains scratch");
            target.Lerp(from,to,-5f);Check(target.GetOverrideWithDefault(a,-1)==2f && target.GetOverrideWithDefault(b,-1)==4f,"lower clamp");
            target.Lerp(from,to,5f);Check(target.GetOverrideWithDefault(a,-1)==6f && target.GetOverrideWithDefault(b,-1)==1f,"upper clamp");
            target.Lerp(from,to,float.NaN);Check(float.IsNaN(target.GetOverrideWithDefault(a,0)) && float.IsNaN(target.GetOverrideWithDefault(b,0)),"NaN interpolation follows native float path");
            var alias=Setting(a,2f,b,4f);alias.Lerp(alias,to,0.5f);
            Check(alias.GetOverrideWithDefault(a,0)==4f && alias.GetOverrideWithDefault(b,0)==2.5f,"alias endpoints are read before deferred writes");
            var empty=Setting();empty.Lerp(null,null,0.5f);Check(empty.OverrideDictionaryLookup.Count==0,"empty target does not touch null endpoints");
            target=Setting(a,3f,b,5f);Throws<NullReferenceException>(()=>target.Lerp(null,to,0.5f),"nonempty target dereferences missing endpoint");
            Check(target.GetOverrideWithDefault(a,0)==3f && ((IList)Read(target,"m_kvpsToModify")).Count==0,"failure before first add preserves target and scratch");
            from=Setting(a,2f,b,4f);var comparer=(ReferenceKeys)((Dictionary<TimeCategoryObject,float>)Read(Dictionary(from),"m_dictionary")).Comparer;
            comparer.ThrowOn=b;target=Setting(a,9f,b,8f);
            Throws<ApplicationException>(()=>target.Lerp(from,to,0.5f),"second endpoint key failure propagates");
            Check(((IList)Read(target,"m_kvpsToModify")).Count==1,"partial interpolation retains first staged entry");
            Check(target.GetOverrideWithDefault(a,0)==9f && target.GetOverrideWithDefault(b,0)==8f,"staged interpolation does not partially write target");
            Dictionary(target).Clear();comparer.ThrowOn=null;target.Lerp(from,to,0.5f);
            Check(target.OverrideDictionaryLookup.Count==1 && target.GetOverrideWithDefault(a,0)==4f,"retry with empty target writes stale staged key");
            Check(((IList)Read(target,"m_kvpsToModify")).Count==0,"retry normal completion clears retained scratch");
            target=Setting(c,17f);target.Copy(from);
            Check(target.OverrideDictionaryLookup.Count==2 && target.GetOverrideWithDefault(a,0)==2f,"copy clears stale keys and copies source values");
            Check(!ReferenceEquals(Dictionary(target),Dictionary(from)),"copy preserves destination dictionary identity");
            target.Copy(target);Check(target.OverrideDictionaryLookup.Count==0,"original self-copy clears before enumerating");
            target=Setting(c,17f);Throws<NullReferenceException>(()=>target.Copy(null),"null copy source propagates after clear");
            Check(target.OverrideDictionaryLookup.Count==0,"failed null copy leaves cleared destination");
            var cloned=TimeSetting.Clone(from);
            Check(!ReferenceEquals(cloned,from) && cloned.OverrideDictionaryLookup.Count==2,"clone allocates genuine independent setting");
            Check(cloned.OverrideDictionaryLookup.Values.OrderBy(v=>v).SequenceEqual(new[]{2f,4f}),"clone preserves all copied values");
            Throws<NullReferenceException>(()=>TimeSetting.Clone(null),"null clone uses original copy boundary");

            var translated=new TimeSettings_SDT();var enums=(SerializableDictionary<TimeCategory,float>)Read(translated,"m_overrides");
            Check(ReferenceEquals(((Dictionary<TimeCategory,float>)Read(enums,"m_dictionary")).Comparer,HardlightEnumComparers.TimeCategoryComparer),"original generated enum comparer is retained");
            Write(translated,"m_overridesDictionary",new SerializableDictionary<TimeCategoryObject,float>(new ReferenceKeys()));
            enums.Add(TimeCategory.PlayerMovement,0.4f);enums.Add(TimeCategory.PlayerPhysics,0.8f);
            var lookup=new Dictionary<TimeCategory,TimeCategoryObject>{{TimeCategory.PlayerMovement,a}};
            Throws<KeyNotFoundException>(()=>translated.ApplyTimeCategoryObjectConversion(lookup),"missing second lookup propagates after first conversion");
            Check(translated.OverrideDictionaryLookup.Count==1 && translated.GetOverrideWithDefault(a,0)==0.4f,"conversion preserves partial first write");
            lookup.Add(TimeCategory.PlayerPhysics,b);translated.ApplyTimeCategoryObjectConversion(lookup);
            Check(translated.OverrideDictionaryLookup.Count==1,"any prior entry suppresses retry of incomplete conversion");
            Dictionary(translated).Clear();translated.ApplyTimeCategoryObjectConversion(lookup);
            Check(translated.OverrideDictionaryLookup.Count==2 && translated.GetOverrideWithDefault(b,0)==0.8f,"explicitly empty conversion completes original mapping");
            translated.ApplyTimeCategoryObjectConversion(null);Check(translated.OverrideDictionaryLookup.Count==2,"nonempty base avoids even null lookup");
            var noEnums=new TimeSettings_SDT();noEnums.ApplyTimeCategoryObjectConversion(null);
            Check(noEnums.OverrideDictionaryLookup.Count==0,"empty enum input never dereferences missing lookup");
        }

        private static void ComparerChecks()
        {
            var u=EnumComparers.UpdateOnComparer;var l=EnumComparers.LeaderboardTypeComparer;
            Check(u!=null && l!=null && !ReferenceEquals(u,l),"ordered genuine comparer static initialization");
            Check(new EnumComparers()!=null,"original nonstatic comparer host constructor");
            foreach(UpdateOn value in new[]{UpdateOn.FixedUpdate,UpdateOn.LateUpdate,UpdateOn.None,UpdateOn.Update,(UpdateOn)0,(UpdateOn)int.MinValue,(UpdateOn)int.MaxValue})
            {
                Check(u.Equals(value,value),"update comparer reflexive for unknown literals too");
                Check(u.GetHashCode(value)==(int)value,"update hash is unchecked original identity");
                Check(!u.Equals(value,(UpdateOn)((int)value ^ 1)),"update equality distinguishes adjacent values");
            }
            foreach(long value in new[]{0L,1L,-1L,int.MinValue,(long)int.MaxValue})
            {
                Check(l.Equals((LeaderboardType)value,(LeaderboardType)value),"64-bit leaderboard equality");
                Check(l.GetHashCode((LeaderboardType)value)==(int)value,"original boxed Convert hash within range");
                Check(!l.Equals((LeaderboardType)value,(LeaderboardType)(value+(1L<<32))),"leaderboard equality preserves high bits");
            }
            Throws<OverflowException>(()=>l.GetHashCode((LeaderboardType)((long)int.MaxValue+1)),"boxed enum conversion rejects positive overflow");
            Throws<OverflowException>(()=>l.GetHashCode((LeaderboardType)((long)int.MinValue-1)),"boxed enum conversion rejects negative overflow");
        }

        // A real original ITimeScaled consumer used only as a callback fixture.
        private sealed class Tick : ITimeScaled
        {
            internal bool Paused;
            internal string Trace="";
            internal int Updates;
            internal float Delta;
            internal Action OnTick;
            internal string ThrowAt;
            public bool IsPaused { get { Trace+="g;";return Paused; } set { Trace+="s"+value+";";Paused=value; } }
            public void OnPause() { Trace+="p"+Paused+";";if(ThrowAt=="pause")throw new ApplicationException("pause"); }
            public void OnResume() { Trace+="r"+Paused+";";if(ThrowAt=="resume")throw new ApplicationException("resume"); }
            public void OnUpdate(float deltaTime) { Trace+="u;";Updates++;Delta=deltaTime;OnTick?.Invoke();if(ThrowAt=="update")throw new ApplicationException("update"); }
            public void OnFixedUpdate(float deltaTime) { Trace+="f;";Updates++;Delta=deltaTime; }
            public void OnLateUpdate(float deltaTime) { Trace+="l;";Updates++;Delta=deltaTime; }
            public override bool Equals(object value) { return value is Tick; }
            public override int GetHashCode() { return 1; }
        }
        private static Type Subscription => typeof(TimeManager).GetNestedType("TimeScaledSubscription",BindingFlags.NonPublic);
        private static IList List() { return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Subscription)); }
        private static object Sub(ITimeScaled tick,bool subscribed=true,string path="fixture")
        {
            object subscription=Activator.CreateInstance(Subscription);
            Write(subscription,"TimeScaledObject",tick);Write(subscription,"IsSubscribed",subscribed);
            Write(subscription,"TransformPath",path);return subscription;
        }
        private static TimeManager Manager()
        {
            var manager=Raw<TimeManager>();Write(manager,"m_pendingSubscriptions",List());
            Write(manager,"m_timeSettings",new StackableData());return manager;
        }
        private static IList Pending(TimeManager manager) { return (IList)Read(manager,"m_pendingSubscriptions"); }
        private static void Category(TimeManager manager,IList list,float delta,Action<ITimeScaled,float> update=null)
        { Call(manager,"UpdateCategory",list,delta,update ?? ((tick,time)=>tick.OnUpdate(time))); }
        private static void CategoryChecks()
        {
            foreach(float delta in new[]{0f,-0f,0.5f,-2f,float.Epsilon,float.NaN})
                foreach(bool paused in new[]{false,true})
                {
                    var manager=Manager();var list=List();var tick=new Tick{Paused=paused};list.Add(Sub(tick));
                    Category(manager,list,delta);
                    bool zero=delta==0f;
                    string expected=zero?(paused?"g;":"g;sTrue;pTrue;"):(paused?"g;sFalse;rFalse;u;":"g;u;");
                    Check(tick.Trace==expected,"pause flag/callback/update order at exact zero boundary");
                    Check(tick.Paused==zero && tick.Updates==(zero?0:1),"exact zero alone suppresses update");
                    Check(zero || (float.IsNaN(delta)?float.IsNaN(tick.Delta):tick.Delta==delta),"negative epsilon and NaN deltas are forwarded");
                }
            {
                var manager=Manager();var list=List();var disabled=new Tick{Paused=true};list.Add(Sub(disabled,false));
                Category(manager,list,2f);Check(disabled.Trace=="" && disabled.Paused && list.Count==1,"unsubscribed entries neither tick nor prune inside category");
                var first=new Tick();var second=new Tick();object later=Sub(second);list.Clear();list.Add(Sub(first));first.OnTick=()=>Pending(manager).Add(later);
                Category(manager,list,1f);Check(first.Updates==1 && second.Updates==0 && list.Count==2 && Pending(manager).Count==0,"new pending subscription drains after current iteration");
                first.OnTick=null;Category(manager,list,1f);Check(first.Updates==2 && second.Updates==1,"drained subscription begins next category pass");
                list.Clear();Pending(manager).Add(later);Category(manager,list,0f);
                Check(list.Count==1 && second.Updates==1 && Pending(manager).Count==0,"empty paused category still drains pending subscriptions");
            }
            foreach(string failure in new[]{"pause","resume","update"})
            {
                var manager=Manager();var list=List();var tick=new Tick{Paused=failure=="resume",ThrowAt=failure};list.Add(Sub(tick));Pending(manager).Add(Sub(new Tick()));
                Throws<ApplicationException>(()=>Category(manager,list,failure=="pause"?0f:1f),"callback exception propagates at "+failure);
                Check(Pending(manager).Count==1 && list.Count==1,"callback failure does not drain or clear pending list");
                Check(tick.Paused==(failure=="pause"),"pause flag remains changed before throwing callback");
            }
            {
                var manager=Manager();var list=List();var tick=new Tick();object later=Sub(new Tick());list.Add(Sub(tick));tick.OnTick=()=>list.Add(later);Pending(manager).Add(later);
                Throws<InvalidOperationException>(()=>Category(manager,list,1f),"direct live-list mutation retains enumerator failure");
                Check(list.Count==2 && Pending(manager).Count==1,"live mutation and undrained pending state remain visible");
                list=List();list.Add(Sub(new Tick()));Throws<NullReferenceException>(()=>Call(manager,"UpdateCategory",list,1f,null),"nonzero category dereferences missing update action");
            }
        }

        private static void ControlChecks()
        {
            var manager=Manager();manager.ClearUnityTimescaleOverride();
            foreach(float value in new[]{-1f,0f,0.000999f})
            { manager.OverrideUnityTimescale(value);Check(!manager.UnityTimescaleOverride && manager.GetUnityTimescale()==0f,"sub-threshold override is ignored"); }
            manager.OverrideUnityTimescale(TimeManager.TimeScaleMinDelta);Check(manager.UnityTimescaleOverride && manager.GetUnityTimescale()==0.001f,"threshold equality is accepted");
            manager.ClearUnityTimescaleOverride();Check(!manager.UnityTimescaleOverride && manager.GetUnityTimescale()==0.001f,"clear preserves stored override value");
            manager.OverrideUnityTimescale(-2f,true);Check(manager.UnityTimescaleOverride && manager.GetUnityTimescale()==-2f,"debug override accepts negative value");
            manager.OverrideUnityTimescale(float.NaN);Check(float.IsNaN(manager.GetUnityTimescale()) && manager.UnityTimescaleOverride,"original less-than guard accepts NaN");
            manager.OverrideUnityTimescale(float.PositiveInfinity);Check(float.IsPositiveInfinity(manager.GetUnityTimescale()),"positive infinity follows original guard");
            manager.UpdateGameSpeedOverride(-9f);Check((float)Read(manager,"m_overrideSpeed")==-9f,"speed setter stores without clamping");
            manager.Subscribe(null,null,UpdateOn.None,null);manager.Unsubscribe(null,null,UpdateOn.None);
            Check(Pending(manager).Count==0,"None skips phase lookup and null object/category/path");
            manager.UpdateTimeSetting(null,null);manager.RemoveTimeSetting(null);Check(true,"null override handles are ignored before setting access");
        }
        private static StackableData.ResultCarrier<TimeSetting>.Operation Multiply()
        { return (StackableData.ResultCarrier<TimeSetting>.Operation)typeof(TimeManager).GetMethod("TimeSettingMultiply",BindingFlags.NonPublic|BindingFlags.Static).CreateDelegate(typeof(StackableData.ResultCarrier<TimeSetting>.Operation)); }
        private static void MultiplyChecks()
        {
            var a=Raw<TimeCategoryObject>();var b=Raw<TimeCategoryObject>();var c=Raw<TimeCategoryObject>();
            Write(a,"m_guid","multiply-a");Write(b,"m_guid","multiply-b");Write(c,"m_guid","multiply-c");
            FieldInfo cache=Field(typeof(TimeManager),"s_cachedTimeSettingResult");object previous=cache.GetValue(null);
            IDictionary operations=(IDictionary)Field(typeof(StackableData),"Multiplies").GetValue(null);
            bool had=operations.Contains(typeof(TimeSetting));object old=operations[typeof(TimeSetting)];
            try
            {
                var cached=Setting(c,99f);cache.SetValue(null,cached);var op=Multiply();var result=default(StackableData.ResultCarrier<TimeSetting>);
                Check(op(new StackableData.StackableDataContainer<TimeSetting>(Setting(a,2f,b,3f)),ref result)==StackableData.OperationAction.Continue,"multiply always continues");
                Check(result.HasValue && ReferenceEquals(result.Value,cached),"first operation uses one real shared cached setting");
                Check(cached.OverrideDictionaryLookup.Count==2 && cached.GetOverrideWithDefault(a,0)==2f,"initial operation copies and clears stale cache keys");
                Check(op(new StackableData.StackableDataContainer<TimeSetting>(Setting(a,0.5f,c,4f)),ref result)==StackableData.OperationAction.Continue,"later operation also continues");
                Check(result.Value.GetOverrideWithDefault(a,0)==1f && result.Value.GetOverrideWithDefault(b,0)==3f && result.Value.GetOverrideWithDefault(c,0)==4f,"multiply preserves untouched keys and defaults new factors to one");
                var prior=result.Value;var other=default(StackableData.ResultCarrier<TimeSetting>);op(new StackableData.StackableDataContainer<TimeSetting>(Setting(b,7f)),ref other);
                Check(ReferenceEquals(prior,other.Value) && prior.OverrideDictionaryLookup.Count==1 && prior.GetOverrideWithDefault(b,0)==7f,"separate first retrieval rewrites earlier carrier through global cache alias");
                var presentNull=new StackableData.ResultCarrier<TimeSetting>(null);
                Throws<NullReferenceException>(()=>op(new StackableData.StackableDataContainer<TimeSetting>(Setting(a,2f)),ref presentNull),"present null result is dereferenced without fabricated default");
                StackableData.RegisterTypeOperations<TimeSetting>(multiply:op);cache.SetValue(null,Setting());
                var manager=Manager();var stack=(StackableData)Read(manager,"m_timeSettings");stack.SetBaseValue(0,Setting(a,2f,b,3f),StackableData.RetrievalOperation.Multiply);
                Check(manager.GetTimescale(a)==2f && manager.GetTimescale(b)==3f,"real stack base retrieval uses original multiply operation");
                var handle=manager.ApplyTimeSetting(Setting(a,0.5f));Check(handle!=null && manager.GetTimescale(a)==1f && manager.GetTimescale(b)==3f,"apply preserves multiplicative untouched categories");
                manager.UpdateTimeSetting(handle,Setting(a,4f));Check(manager.GetTimescale(a)==8f,"same override handle replacement invalidates genuine cache");
                manager.RemoveTimeSetting(handle);Check(manager.GetTimescale(a)==2f,"remove restores base multiplication");
                // Missing-key exception formatting calls Unity.Object.ToString on .NET10;
                // that genuine engine boundary is tested with actual objects in EngineChecks.
                manager.RemoveTimeSetting(handle);Check(manager.GetTimescale(a)==2f,"second removal uses original no-op absent handle path");
            }
            finally
            {
                cache.SetValue(null,previous);
                if(had)operations[typeof(TimeSetting)]=old;else operations.Remove(typeof(TimeSetting));
            }
        }
        public static int RunManaged()
        {
            checks=0;Shape();SettingChecks();ComparerChecks();CategoryChecks();ControlChecks();MultiplyChecks();
            return checks+DefaultInterfaceCompatibility.Run();
        }
        private static Action<ITimeScaled,float> PhaseAction(string method)
        {
            Type closure=typeof(TimeManager).GetNestedType("<>c",BindingFlags.NonPublic);
            object singleton=closure.GetField("<>9",Own).GetValue(null);
            return (Action<ITimeScaled,float>)closure.GetMethod(method,Own).CreateDelegate(typeof(Action<ITimeScaled,float>),singleton);
        }
        private static void Perform(TimeManager manager,UpdateOn phase,float delta)
        {
            string table=phase==UpdateOn.FixedUpdate?"m_totalFixedTimeLookup":phase==UpdateOn.LateUpdate?"m_totalLateUpdateTimeLookup":"m_totalTimeLookup";
            string callback=phase==UpdateOn.FixedUpdate?"<FixedUpdate>b__39_0":phase==UpdateOn.LateUpdate?"<LateUpdate>b__41_0":"<Update>b__40_0";
            Call(manager,"PerformUpdate",phase,delta,Read(manager,table),PhaseAction(callback));
        }
        private static IDictionary Phase(TimeManager manager,UpdateOn phase)
        { return (IDictionary)((IDictionary)Read(manager,"m_updateObjects"))[phase]; }
        // Native accumulator stores and timing returns round to binary32.
        // Materialize expectations so Mono expression precision cannot leak into equality.
        private static float SinglePrecision(float value)
        { return BitConverter.ToSingle(BitConverter.GetBytes(value),0); }
        private static int EngineChecks()
        {
            int start=checks;
            float priorScale=Time.timeScale,priorFixed=Time.fixedDeltaTime;
            FieldInfo cache=Field(typeof(TimeManager),"s_cachedTimeSettingResult"),enumConfig=Field(typeof(TimeExtensions),"s_config_Internal");
            object priorCache=cache.GetValue(null),priorEnum=enumConfig.GetValue(null);
            IDictionary multiplies=(IDictionary)Field(typeof(StackableData),"Multiplies").GetValue(null);
            bool hadMultiply=multiplies.Contains(typeof(TimeSetting));object priorMultiply=multiplies[typeof(TimeSetting)];
            bool hadSystem=!ProcessManager.IsSystemNull<SystemConfiguration>();
            TimeCategoryObject a=null,b=null,g=null,spare=null,equalA=null;
            TimeCategoryConfiguration config=null;TimeCategoryLookup lookup=null;GameObject host=null;TimeManager manager=null;
            SystemConfiguration system=null;StackableDataHandle configHandle=null,lookupHandle=null;
            var applied=new List<StackableDataHandle>();bool initialised=false,destroyed=false;
            try
            {
                a=ScriptableObject.CreateInstance<TimeCategoryObject>();b=ScriptableObject.CreateInstance<TimeCategoryObject>();g=ScriptableObject.CreateInstance<TimeCategoryObject>();spare=ScriptableObject.CreateInstance<TimeCategoryObject>();equalA=ScriptableObject.CreateInstance<TimeCategoryObject>();
                Check(a.GetGUID()=="" && b.GetGUID()=="","genuine category constructor preserves original empty GUID");
                Write(a,"m_guid","engine-a");Write(b,"m_guid","engine-b");Write(g,"m_guid","engine-global");Write(spare,"m_guid","engine-spare");Write(equalA,"m_guid","engine-a");
                a.name="A";b.name="B";g.name="Global";
                config=ScriptableObject.CreateInstance<TimeCategoryConfiguration>();
                Check(config.DefaultTimescale==1f && config.UnityGlobalTime==null && !config.PrioritisedCategories.Any(),"real config original constructor defaults");
                Check((string)Read(config,"m_autoGeneratedClassName")=="CLASS_NAME" && (string)Read(config,"m_autoGeneratedLookupFolder")=="PATH" && (string)Read(config,"m_autoGeneratedNamespace")=="NAMESPACE" && !(bool)Read(config,"m_generatesLookupStrings"),"real config generation-field defaults");
                config.AddCategory(a);config.AddCategory(equalA);config.AddCategory(b);
                Check(config.PrioritisedCategories.SequenceEqual(new[]{a,b}),"AddUnique respects genuine authored GUID equality");
                var live=config.PrioritisedCategories;((List<TimeCategoryObject>)live).Add(equalA);config.RemoveCategory(equalA);
                Check(ReferenceEquals(live,config.PrioritisedCategories) && live.SequenceEqual(new[]{b,equalA}),"remove affects first equal category and retains live priority list");
                Write(config,"m_categoriesPrioritised",new List<TimeCategoryObject>{a,b});Write(config,"m_unityGlobalTime",g);
                config.Validate();Check(config.PrioritisedCategories.Count()==2,"genuine empty validation does not rewrite categories");
                lookup=ScriptableObject.CreateInstance<TimeCategoryLookup>();
                Check(lookup.Dictionary.Count==0 && ReferenceEquals(((Dictionary<TimeCategory,TimeCategoryObject>)Read(lookup.Dictionary,"m_dictionary")).Comparer,HardlightEnumComparers.TimeCategoryComparer),"real enum lookup constructor retains original generated comparer");
                lookup.Dictionary.Add(TimeCategory.PlayerMovement,a);lookup.Dictionary.Add(TimeCategory.PlayerPhysics,b);lookup.Dictionary.Add(TimeCategory.UnityGlobal,g);lookup.Validate();
                if(!hadSystem)ProcessManager.RegisterSystem(new SystemConfiguration());
                system=ProcessManager.GetSystem<SystemConfiguration>();
                // Runtime config retrieval uses the original base-typed stack entries.
                configHandle=SystemConfiguration.AddConfig<SystemConfigurationAsset>(config);lookupHandle=SystemConfiguration.AddConfig<SystemConfigurationAsset>(lookup);
                Check(ReferenceEquals(SystemConfiguration.GetConfig<TimeCategoryConfiguration>(),config),"genuine config retrieval resolves fixture override");
                Time.fixedDeltaTime=0.02f;host=new GameObject("OriginalTimeSchedulerFixture");host.SetActive(false);
                // The Editor invokes the original Reset message before Awake; its
                // configuration is still null. Preserve and verify that exact failure.
                int resetExceptions=0;string resetStack=null;
                Application.LogCallback resetLog=(condition,stack,type)=>
                {
                    if(type==LogType.Exception && condition=="NullReferenceException: Object reference not set to an instance of an object")
                    {resetExceptions++;resetStack=stack;}
                };
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
                    new System.Text.RegularExpressions.Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
                Application.logMessageReceived+=resetLog;
                try {manager=host.AddComponent<TimeManager>();}
                finally {Application.logMessageReceived-=resetLog;}
                Check(resetExceptions==1 && resetStack!=null && resetStack.Contains("Hardlight.TimeManager.Reset"),"Editor component creation retains original Reset-before-Awake exception boundary");
                Check(manager.GameSpeedMin==0.2f && manager.GameSpeedMax==1f && manager.GameSpeedApplied && !manager.UnityTimescaleOverride && manager.GetUnityTimescale()==1f,"genuine MonoBehaviour constructor original field defaults");
                if(Read(manager,"m_timeCategoryConfiguration")==null)Call(manager,"Awake");initialised=true;
                Check(ReferenceEquals(Read(manager,"m_timeCategoryConfiguration"),config) && ReferenceEquals(Read(manager,"m_unityTimeCategory"),g),"Awake retains actual config/global category identities");
                Check(config.PrioritisedCategories.SequenceEqual(new[]{a,b,g}),"Awake adds global category after authored priority entries");
                Check(((IDictionary)Read(manager,"m_updateObjects")).Count==3 && (float)Read(manager,"m_fixedTimeSetting")==0.02f,"Awake establishes all phases and captures fixed timestep");
                Check(ProcessManager.GetSystem<TimeManager>()==manager,"Awake registers the genuine system");
                Check(manager.GetTotalTime(a)==0 && manager.GetTotalFixedTime(a)==0 && ((Dictionary<TimeCategoryObject,float>)Read(manager,"m_totalLateUpdateTimeLookup"))[a]==0,"Awake resets all phase totals");
                var defaults=TimeSetting.GetDefault(2.25f);
                Check(defaults.OverrideDictionaryLookup.Count==3 && defaults.OverrideDictionaryLookup.Values.All(v=>v==2.25f),"GetDefault uses actual priority/global list and supplied scale");
                Check(config.PrioritisedCategories.Count()==3,"repeated default requests keep unique global category");
                Check(manager.GetTimescale(a)==1f && manager.GetTimescale(b)==1f && manager.GetTimescale(g)==1f,"Awake original multiply base is default one");
                Throws<KeyNotFoundException>(()=>manager.Subscribe(null,null,(UpdateOn)0),"invalid zero phase fails before profiler creation");
                Throws<KeyNotFoundException>(()=>manager.Unsubscribe(null,null,(UpdateOn)0),"invalid zero phase preserves dictionary lookup failure");
                var first=new Tick();var later=new Tick();manager.Subscribe(first,a,UpdateOn.Update,"first-path");
                IList aList=(IList)Phase(manager,UpdateOn.Update)[a];object firstSubscription=aList[0];
                manager.Subscribe(first,a,UpdateOn.Update,"replacement-path");
                Check(aList.Count==1 && ReferenceEquals(firstSubscription,aList[0]) && (string)Read(firstSubscription,"TransformPath")=="first-path","duplicate subscribe reactivates original entry without replacing path or marker");
                manager.Unsubscribe(new Tick(),a);Check((bool)Read(firstSubscription,"IsSubscribed"),"subscription identity ignores consumer Equals override");
                first.OnTick=()=>{ manager.Subscribe(later,a);manager.Unsubscribe(later,a); };
                Perform(manager,UpdateOn.Update,0.25f);first.OnTick=null;
                Check(first.Updates==1 && later.Updates==0 && aList.Count==2 && Pending(manager).Count==0,"engine profiler pass drains new same-category subscription after dispatch");
                Check((bool)Read(aList[1],"IsSubscribed"),"pending entry cannot be immediately unsubscribed through existing-list lookup");
                Check(manager.GetTotalTime(a)==0.25f && manager.GetTotalTime(b)==0f,"only categories with phase lists accumulate totals");
                Check(Read(manager,"m_currentUpdateCategory")==null && (UpdateOn)Read(manager,"m_currentUpdateType")==0,"normal completion clears category and resets phase to zero rather than None");
                Perform(manager,UpdateOn.Update,0.5f);
                Check(first.Updates==2 && later.Updates==1 && manager.GetTotalTime(a)==0.75f,"pending consumer starts next original phase pass");
                var pause=new TimeSetting();pause.SetCategory(a,0f);var pauseHandle=manager.ApplyTimeSetting(pause);applied.Add(pauseHandle);
                Perform(manager,UpdateOn.Update,0.5f);
                Check(first.Paused && later.Paused && first.Updates==2 && later.Updates==1 && manager.GetTotalTime(a)==0.75f,"zero category pauses without updating or advancing total");
                string pausedTrace=first.Trace;Perform(manager,UpdateOn.Update,0.5f);
                Check(first.Trace==pausedTrace+"g;","repeated zero does not repeat pause callback");
                manager.RemoveTimeSetting(pauseHandle);Perform(manager,UpdateOn.Update,0.125f);
                Check(!first.Paused && !later.Paused && first.Trace.EndsWith("g;sFalse;rFalse;u;") && manager.GetTotalTime(a)==0.875f,"resume precedes actual callback and accumulation restarts");
                manager.Unsubscribe(first,a);Perform(manager,UpdateOn.Update,0.125f);
                Check(aList.Count==1 && ReferenceEquals(Read(aList[0],"TimeScaledObject"),later) && first.Updates==3,"original phase pruning removes unsubscribed entry before updates");
                manager.Unsubscribe(later,a);Perform(manager,UpdateOn.Update,0.125f);
                Check(aList.Count==0 && manager.GetTotalTime(a)==1.125f,"existing empty category list still accumulates time");
                var fixedTick=new Tick();var lateTick=new Tick();manager.Subscribe(fixedTick,a,UpdateOn.FixedUpdate);manager.Subscribe(lateTick,b,UpdateOn.LateUpdate);
                Perform(manager,UpdateOn.FixedUpdate,0.02f);Perform(manager,UpdateOn.LateUpdate,0.03f);
                Check(fixedTick.Trace=="g;f;" && fixedTick.Delta==0.02f && lateTick.Trace=="g;l;" && lateTick.Delta==0.03f,"real generated phase callbacks dispatch fixed and late methods");
                Check(manager.GetTotalFixedTime(a)==0.02f && ((Dictionary<TimeCategoryObject,float>)Read(manager,"m_totalLateUpdateTimeLookup"))[b]==0.03f,"phase totals remain independent");
                float fixedBefore=manager.GetTotalFixedTime(a),fixedDelta=Time.fixedDeltaTime;manager.FixedUpdate();
                Check(manager.GetTotalFixedTime(a)==SinglePrecision(fixedBefore+fixedDelta) && fixedTick.Trace.EndsWith("g;f;"),"original public fixed wrapper uses actual fixedDeltaTime and generated callback");
                float updateBefore=manager.GetTotalTime(a),updateDelta=Time.deltaTime;manager.Update();
                float updateActual=manager.GetTotalTime(a);
                Check(updateActual==SinglePrecision(updateBefore+updateDelta),"original public update wrapper accumulates actual deltaTime in existing empty category");
                float lateDelta=Time.unscaledDeltaTime;int lateUpdates=lateTick.Updates;manager.LateUpdate();
                Check(lateDelta==0f?(lateTick.Paused && lateTick.Updates==lateUpdates):(lateTick.Delta==lateDelta && lateTick.Updates==lateUpdates+1),"original late wrapper uses actual unscaledDeltaTime");
                var global=new TimeSetting();global.SetCategory(g,0.5f);var globalHandle=manager.ApplyTimeSetting(global);applied.Add(globalHandle);Perform(manager,UpdateOn.FixedUpdate,0.02f);
                Check(Time.timeScale==0.5f && Time.fixedDeltaTime==0.01f,"global setting controls engine scale and original fixed baseline");
                manager.OverrideUnityTimescale(3f);Perform(manager,UpdateOn.FixedUpdate,0.02f);
                Check(Time.timeScale==3f && Time.fixedDeltaTime==0.01f,"Unity timescale override leaves fixed-category scale calculation intact");
                global=new TimeSetting();global.SetCategory(g,0f);manager.UpdateTimeSetting(globalHandle,global);Perform(manager,UpdateOn.FixedUpdate,0.02f);
                Check(Time.timeScale==3f && Time.fixedDeltaTime==0.02f,"approximately zero global scale restores fixed baseline despite override");
                manager.RemoveTimeSetting(globalHandle);manager.ClearUnityTimescaleOverride();
                var speeds=(List<TimeCategoryObject>)Read(manager,"m_gameSpeedTimeCategories");speeds.Add(a);
                manager.InitialiseGameSpeedOverride();manager.UpdateGameSpeedOverride(-0.25f);manager.UpdateGameSpeedOverride();
                Check(manager.GetTimescale(a)==-0.25f,"game speed override applies unclamped stored value");
                manager.GameSpeedApplied=false;manager.UpdateGameSpeedOverride();Check(manager.GetTimescale(a)==1f,"disabled game speed applies original max rather than min");
                Call(manager,"RemoveGameSpeedOverride");Check(Read(manager,"m_gameSpeedOverrideHandle")==null && manager.GetTimescale(a)==1f,"removal clears speed handle after genuine stack removal");
                var enumSetting=new TimeSettings_SDT();((SerializableDictionary<TimeCategory,float>)Read(enumSetting,"m_overrides")).Add(TimeCategory.PlayerMovement,0.4f);
                enumConfig.SetValue(null,lookup);var enumHandle=manager.ApplyTimeSetting_SDT(enumSetting);applied.Add(enumHandle);
                Check(manager.GetTimescale(TimeCategory.PlayerMovement)==0.4f && enumSetting.OverrideDictionaryLookup.Count==1,"original game enum conversion integrates with genuine scheduler stack");
                Check(manager.GetTotalTime(TimeCategory.PlayerMovement)==manager.GetTotalTime(a) && manager.GetTotalFixedTime(TimeCategory.PlayerMovement)==manager.GetTotalFixedTime(a),"original enum extensions preserve timing lookup keys");
                Check(manager.GetDeltaTime(TimeCategory.PlayerMovement)==SinglePrecision(Time.deltaTime*0.4f) && manager.GetFixedDeltaTime(TimeCategory.PlayerMovement)==SinglePrecision(Time.fixedDeltaTime*0.4f),"original enum delta extensions use matching actual engine timestep");
                manager.RemoveTimeSetting(enumHandle);var enumTick=new Tick();manager.Subscribe(enumTick,TimeCategory.PlayerMovement,UpdateOn.Update,"enum-path");
                Check(((IList)Phase(manager,UpdateOn.Update)[a]).Count==1,"original enum subscribe forwards category and phase");
                manager.Unsubscribe(enumTick,TimeCategory.PlayerMovement);Check(!(bool)Read(((IList)Phase(manager,UpdateOn.Update)[a])[0],"IsSubscribed"),"original enum unsubscribe preserves underlying subscription flag behavior");
                var current=((StackableData)Read(manager,"m_timeSettings")).Get<TimeSetting>(0);current.SetCategory(spare,9f);
                foreach(string table in new[]{"m_totalTimeLookup","m_totalFixedTimeLookup","m_totalLateUpdateTimeLookup"})((Dictionary<TimeCategoryObject,float>)Read(manager,table))[spare]=99f;
                manager.OverrideUnityTimescale(2f);manager.Reset();
                Check(manager.GetTimescale(spare)==9f && manager.GetTotalTime(spare)==99f && manager.GetTotalFixedTime(spare)==99f,"Reset preserves old unprioritised setting and total keys");
                Check(manager.UnityTimescaleOverride && manager.GetUnityTimescale()==2f && manager.GetTotalTime(a)==0f,"Reset retains override state while resetting authored priority totals");
                Write(equalA,"m_guid","missing-engine");
                Throws<KeyNotFoundException>(()=>manager.GetTimescale(equalA),"genuine missing timescale key retains direct indexer failure");
                // Callback failures leave the original active category/type and pending entries.
                enumTick.ThrowAt="update";manager.Subscribe(enumTick,a);Pending(manager).Add(Sub(new Tick()));
                Throws<ApplicationException>(()=>Perform(manager,UpdateOn.Update,0.1f),"engine callback failure propagates");
                Check(ReferenceEquals(Read(manager,"m_currentUpdateCategory"),a) && (UpdateOn)Read(manager,"m_currentUpdateType")==UpdateOn.Update && Pending(manager).Count==1,"failing category leaves native active state and pending queue");
                enumTick.ThrowAt=null;Pending(manager).Clear();
                Call(manager,"OnDestroy");destroyed=true;
                Check(Time.timeScale==1f && !config.PrioritisedCategories.Contains(g) && ProcessManager.IsSystemNull<TimeManager>(),"original destruction resets global scale, removes one category and unregisters");
            }
            finally
            {
                if(manager!=null && initialised && !destroyed)Call(manager,"OnDestroy");
                if(host!=null)UnityEngine.Object.DestroyImmediate(host);
                if(system!=null)
                {
                    if(configHandle!=null)system.StackableData.RemoveOverrides(configHandle);
                    if(lookupHandle!=null)system.StackableData.RemoveOverrides(lookupHandle);
                }
                if(!hadSystem && system!=null)ProcessManager.UnregisterSystem(system);
                foreach(UnityEngine.Object item in new UnityEngine.Object[]{config,lookup,a,b,g,spare,equalA})if(item!=null)UnityEngine.Object.DestroyImmediate(item);
                cache.SetValue(null,priorCache);enumConfig.SetValue(null,priorEnum);
                if(hadMultiply)multiplies[typeof(TimeSetting)]=priorMultiply;else multiplies.Remove(typeof(TimeSetting));
                Time.timeScale=priorScale;Time.fixedDeltaTime=priorFixed;
            }
            return checks-start;
        }
        public static int Run() { int managed=RunManaged();return managed+EngineChecks(); }
    }
}
