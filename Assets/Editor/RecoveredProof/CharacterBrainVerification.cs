using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight.Utils;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded original input accumulator proof. Managed callback invocation and a
    // missing coroutine-host failure are separate from real engine frame scheduling.
    public static class CharacterBrainVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); ++checks; }
        private static FieldInfo Field(Type t, string name) { return t.GetField(name, Own); }
        private static string Canon(Type t)
        {
            if (t == null) return null;
            if (t.IsGenericParameter) return (t.DeclaringMethod == null ? "!" : "!!") + t.GenericParameterPosition;
            if (t.IsArray) return Canon(t.GetElementType()) + "[]";
            if (t.IsByRef) return Canon(t.GetElementType()) + "&";
            if (t.IsGenericType) return t.GetGenericTypeDefinition().FullName + "<" + string.Join(",", t.GetGenericArguments().Select(Canon)) + ">";
            return t.FullName;
        }
        private static T Get<T>(CharacterBrain brain, string name) { return (T)Field(typeof(CharacterBrain), name).GetValue(brain); }
        private static void Put(CharacterBrain brain, string name, object value) { Field(typeof(CharacterBrain), name).SetValue(brain, value); }
        private static void Callback(CharacterBrain brain)
        {
            // This invokes the genuine naturally emitted original lambda body. It
            // does not imply a CoroutineUtils frame was run in this managed host.
            typeof(CharacterBrain).GetMethod("<SetEnabled>b__52_0", Own).Invoke(brain, null);
        }
        private sealed class Brain : CharacterBrain
        {
            public int closeCalls;
            public override void Close() { ++closeCalls; }
            public void Input(GameAction action, bool value) { SetState(action, value); }
            public void Move(Vector2 direction, float amount) { AddMovement(direction, amount); }
        }
        private enum Ordinal { Zero=0, Two=2, AliasTwo=2, Sign=31, Wrapped=33, Negative=-1 }
        [Flags] private enum Masks { Zero=0, One=1, Four=4, Five=5 }
        private enum Huge : ulong { TooLarge = ulong.MaxValue }
        private static bool Argument(Action action, string text)
        {
            try { action(); return false; }
            catch (ArgumentException e) { return e.GetType() == typeof(ArgumentException) && e.Message == text && e.ParamName == null; }
        }
        private static bool Overflow(Action action) { try { action(); return false; } catch (OverflowException) { return true; } }
        private static bool ActionError(Action action, GameAction value)
        {
            try { action(); return false; }
            catch (ArgumentOutOfRangeException e) { return e.ParamName == "action" && e.ActualValue is GameAction && (GameAction)e.ActualValue == value; }
        }
        public static int RunManaged()
        {
            checks = 0;
            {
            Type type=typeof(Hardlight.Utils.BitFlags32);
            Check(type.Assembly.GetName().Name=="HLUnityCore.Runtime" && (int)type.Attributes==1057033,"Hardlight.Utils.BitFlags32 exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.ValueType","Hardlight.Utils.BitFlags32 original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_flags"}),"Hardlight.Utils.BitFlags32 original complete field order");
            Check(Canon(Field(type,"m_flags").FieldType)=="System.Int32" && (int)Field(type,"m_flags").Attributes==1,"Hardlight.Utils.BitFlags32.m_flags original field type/flags");
            Check(Field(type,"m_flags").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"UnityEngine.SerializeField"}),"Hardlight.Utils.BitFlags32.m_flags original attribute sequence");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"AllSelectedNonFlags","AllSelected","SelectFlag","op_Implicit","IsSet","IsAnySet","IsAnySet","IsAllSet","Set","IsIndexSet","SetIndex","Clear","ToInt"}),"Hardlight.Utils.BitFlags32 complete original method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Int32","Hardlight.Utils.BitFlags32 0x0600115a original signature/flags/parameters");
            Check(((MethodInfo)members[0]).GetGenericArguments()[0].Name=="T" && ((MethodInfo)members[0]).GetGenericArguments()[0].GenericParameterAttributes==GenericParameterAttributes.None && ((MethodInfo)members[0]).GetGenericArguments()[0].GetGenericParameterConstraints().Length==0,"Hardlight.Utils.BitFlags32 original unconstrained T");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && members[1].GetParameters().Length==0 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Int32","Hardlight.Utils.BitFlags32 0x0600115b original signature/flags/parameters");
            Check(((MethodInfo)members[1]).GetGenericArguments()[0].Name=="T" && ((MethodInfo)members[1]).GetGenericArguments()[0].GenericParameterAttributes==GenericParameterAttributes.None && ((MethodInfo)members[1]).GetGenericArguments()[0].GetGenericParameterConstraints().Length==0,"Hardlight.Utils.BitFlags32 original unconstrained T");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && members[2].GetParameters().Length==1 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Int32" && members[2].GetParameters()[0].Name=="val" && Canon(members[2].GetParameters()[0].ParameterType)=="!!0" && members[2].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"Hardlight.Utils.BitFlags32 0x0600115c original signature/flags/parameters");
            Check(((MethodInfo)members[2]).GetGenericArguments()[0].Name=="T" && ((MethodInfo)members[2]).GetGenericArguments()[0].GenericParameterAttributes==GenericParameterAttributes.None && ((MethodInfo)members[2]).GetGenericArguments()[0].GetGenericParameterConstraints().Length==0,"Hardlight.Utils.BitFlags32 original unconstrained T");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig, SpecialName") && members[3].GetParameters().Length==1 && Canon(((MethodInfo)members[3]).ReturnType)=="Hardlight.Utils.BitFlags32" && members[3].GetParameters()[0].Name=="val" && Canon(members[3].GetParameters()[0].ParameterType)=="System.Int32" && members[3].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"Hardlight.Utils.BitFlags32 0x0600115d original signature/flags/parameters");
            Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[4].GetParameters().Length==1 && Canon(((MethodInfo)members[4]).ReturnType)=="System.Boolean" && members[4].GetParameters()[0].Name=="mask" && Canon(members[4].GetParameters()[0].ParameterType)=="System.Int32" && members[4].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"Hardlight.Utils.BitFlags32 0x0600115e original signature/flags/parameters");
            Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[5].GetParameters().Length==1 && Canon(((MethodInfo)members[5]).ReturnType)=="System.Boolean" && members[5].GetParameters()[0].Name=="mask" && Canon(members[5].GetParameters()[0].ParameterType)=="System.Int32" && members[5].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"Hardlight.Utils.BitFlags32 0x0600115f original signature/flags/parameters");
            Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[6].GetParameters().Length==0 && Canon(((MethodInfo)members[6]).ReturnType)=="System.Boolean","Hardlight.Utils.BitFlags32 0x06001160 original signature/flags/parameters");
            Check(members[7].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[7].GetParameters().Length==1 && Canon(((MethodInfo)members[7]).ReturnType)=="System.Boolean" && members[7].GetParameters()[0].Name=="flags" && Canon(members[7].GetParameters()[0].ParameterType)=="System.Int32" && members[7].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"Hardlight.Utils.BitFlags32 0x06001161 original signature/flags/parameters");
            Check(members[8].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[8].GetParameters().Length==2 && Canon(((MethodInfo)members[8]).ReturnType)=="System.Void" && members[8].GetParameters()[0].Name=="mask" && Canon(members[8].GetParameters()[0].ParameterType)=="System.Int32" && members[8].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && members[8].GetParameters()[1].Name=="val" && Canon(members[8].GetParameters()[1].ParameterType)=="System.Boolean" && members[8].GetParameters()[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"Hardlight.Utils.BitFlags32 0x06001162 original signature/flags/parameters");
            Check(members[9].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[9].GetParameters().Length==1 && Canon(((MethodInfo)members[9]).ReturnType)=="System.Boolean" && members[9].GetParameters()[0].Name=="index" && Canon(members[9].GetParameters()[0].ParameterType)=="System.Int32" && members[9].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"Hardlight.Utils.BitFlags32 0x06001163 original signature/flags/parameters");
            Check(members[10].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[10].GetParameters().Length==2 && Canon(((MethodInfo)members[10]).ReturnType)=="System.Void" && members[10].GetParameters()[0].Name=="index" && Canon(members[10].GetParameters()[0].ParameterType)=="System.Int32" && members[10].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && members[10].GetParameters()[1].Name=="val" && Canon(members[10].GetParameters()[1].ParameterType)=="System.Boolean" && members[10].GetParameters()[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"Hardlight.Utils.BitFlags32 0x06001164 original signature/flags/parameters");
            Check(members[11].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[11].GetParameters().Length==0 && Canon(((MethodInfo)members[11]).ReturnType)=="System.Void","Hardlight.Utils.BitFlags32 0x06001165 original signature/flags/parameters");
            Check(members[12].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[12].GetParameters().Length==0 && Canon(((MethodInfo)members[12]).ReturnType)=="System.Int32","Hardlight.Utils.BitFlags32 0x06001166 original signature/flags/parameters");
            }
            {
            Type type=typeof(HardlightProject.CharacterActionFlags);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1048841,"HardlightProject.CharacterActionFlags exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.ValueType","HardlightProject.CharacterActionFlags original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"m_value","<ActionCount>k__BackingField","m_actionIndices","m_actionMapping"}),"HardlightProject.CharacterActionFlags original complete field order");
            Check(Canon(Field(type,"m_value").FieldType)=="Hardlight.Utils.BitFlags32" && (int)Field(type,"m_value").Attributes==1,"HardlightProject.CharacterActionFlags.m_value original field type/flags");
            Check(Field(type,"m_value").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterActionFlags.m_value original attribute sequence");
            Check(Canon(Field(type,"<ActionCount>k__BackingField").FieldType)=="System.Int32" && (int)Field(type,"<ActionCount>k__BackingField").Attributes==17,"HardlightProject.CharacterActionFlags.<ActionCount>k__BackingField original field type/flags");
            Check(Field(type,"<ActionCount>k__BackingField").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"System.Runtime.CompilerServices.CompilerGeneratedAttribute"}),"HardlightProject.CharacterActionFlags.<ActionCount>k__BackingField original attribute sequence");
            Check(Canon(Field(type,"m_actionIndices").FieldType)=="HardlightProject.GameAction[]" && (int)Field(type,"m_actionIndices").Attributes==17,"HardlightProject.CharacterActionFlags.m_actionIndices original field type/flags");
            Check(Field(type,"m_actionIndices").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterActionFlags.m_actionIndices original attribute sequence");
            Check(Canon(Field(type,"m_actionMapping").FieldType)=="System.Collections.Generic.Dictionary`2<HardlightProject.GameAction,System.Int32>" && (int)Field(type,"m_actionMapping").Attributes==49,"HardlightProject.CharacterActionFlags.m_actionMapping original field type/flags");
            Check(Field(type,"m_actionMapping").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterActionFlags.m_actionMapping original attribute sequence");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_ActionCount","set_ActionCount","Initialise","GetAction","GetValue","SetValue","ToInt","Clear","op_Implicit",".cctor"}),"HardlightProject.CharacterActionFlags complete original method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig, SpecialName") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Int32","HardlightProject.CharacterActionFlags 0x060012d4 original signature/flags/parameters");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, Static, HideBySig, SpecialName") && members[1].GetParameters().Length==1 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Void" && members[1].GetParameters()[0].Name=="value" && Canon(members[1].GetParameters()[0].ParameterType)=="System.Int32" && members[1].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterActionFlags 0x060012d5 original signature/flags/parameters");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && members[2].GetParameters().Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="System.Void","HardlightProject.CharacterActionFlags 0x060012d6 original signature/flags/parameters");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig") && members[3].GetParameters().Length==1 && Canon(((MethodInfo)members[3]).ReturnType)=="HardlightProject.GameAction" && members[3].GetParameters()[0].Name=="actionIndex" && Canon(members[3].GetParameters()[0].ParameterType)=="System.Int32" && members[3].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterActionFlags 0x060012d7 original signature/flags/parameters");
            Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[4].GetParameters().Length==1 && Canon(((MethodInfo)members[4]).ReturnType)=="System.Boolean" && members[4].GetParameters()[0].Name=="action" && Canon(members[4].GetParameters()[0].ParameterType)=="HardlightProject.GameAction" && members[4].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterActionFlags 0x060012d8 original signature/flags/parameters");
            Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[5].GetParameters().Length==2 && Canon(((MethodInfo)members[5]).ReturnType)=="System.Void" && members[5].GetParameters()[0].Name=="action" && Canon(members[5].GetParameters()[0].ParameterType)=="HardlightProject.GameAction" && members[5].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && members[5].GetParameters()[1].Name=="value" && Canon(members[5].GetParameters()[1].ParameterType)=="System.Boolean" && members[5].GetParameters()[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterActionFlags 0x060012d9 original signature/flags/parameters");
            Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[6].GetParameters().Length==0 && Canon(((MethodInfo)members[6]).ReturnType)=="System.Int32","HardlightProject.CharacterActionFlags 0x060012da original signature/flags/parameters");
            Check(members[7].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[7].GetParameters().Length==0 && Canon(((MethodInfo)members[7]).ReturnType)=="System.Void","HardlightProject.CharacterActionFlags 0x060012db original signature/flags/parameters");
            Check(members[8].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Static, HideBySig, SpecialName") && members[8].GetParameters().Length==1 && Canon(((MethodInfo)members[8]).ReturnType)=="HardlightProject.CharacterActionFlags" && members[8].GetParameters()[0].Name=="value" && Canon(members[8].GetParameters()[0].ParameterType)=="System.Int32" && members[8].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterActionFlags 0x060012dc original signature/flags/parameters");
            Check(members[9].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, Static, HideBySig, SpecialName, RTSpecialName") && members[9].GetParameters().Length==0,"HardlightProject.CharacterActionFlags 0x060012dd original signature/flags/parameters");
            }
            {
            Type type=typeof(HardlightProject.CharacterBrain);
            Check(type.Assembly.GetName().Name=="Game.Runtime" && (int)type.Attributes==1048705,"HardlightProject.CharacterBrain exact type flags/assembly");
            Check(Canon(type.BaseType)=="System.Object","HardlightProject.CharacterBrain original base");
            Check(type.GetFields(Own).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(new string[]{"<Enabled>k__BackingField","m_actionStateQueued","m_actionStateApplied","m_actionStateRaw","m_ignoreEnableActions","m_actionStateDeferred","m_actionStateActivatedThisFrame","m_actionStateTimestamps","m_movementAggregate","m_movementState","m_cacheLockTimestamp","m_actionStateCache"}),"HardlightProject.CharacterBrain original complete field order");
            Check(Canon(Field(type,"<Enabled>k__BackingField").FieldType)=="System.Boolean" && (int)Field(type,"<Enabled>k__BackingField").Attributes==1,"HardlightProject.CharacterBrain.<Enabled>k__BackingField original field type/flags");
            Check(Field(type,"<Enabled>k__BackingField").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{"System.Runtime.CompilerServices.CompilerGeneratedAttribute"}),"HardlightProject.CharacterBrain.<Enabled>k__BackingField original attribute sequence");
            Check(Canon(Field(type,"m_actionStateQueued").FieldType)=="HardlightProject.CharacterActionFlags" && (int)Field(type,"m_actionStateQueued").Attributes==1,"HardlightProject.CharacterBrain.m_actionStateQueued original field type/flags");
            Check(Field(type,"m_actionStateQueued").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_actionStateQueued original attribute sequence");
            Check(Canon(Field(type,"m_actionStateApplied").FieldType)=="HardlightProject.CharacterActionFlags" && (int)Field(type,"m_actionStateApplied").Attributes==1,"HardlightProject.CharacterBrain.m_actionStateApplied original field type/flags");
            Check(Field(type,"m_actionStateApplied").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_actionStateApplied original attribute sequence");
            Check(Canon(Field(type,"m_actionStateRaw").FieldType)=="HardlightProject.CharacterActionFlags" && (int)Field(type,"m_actionStateRaw").Attributes==1,"HardlightProject.CharacterBrain.m_actionStateRaw original field type/flags");
            Check(Field(type,"m_actionStateRaw").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_actionStateRaw original attribute sequence");
            Check(Canon(Field(type,"m_ignoreEnableActions").FieldType)=="HardlightProject.CharacterActionFlags" && (int)Field(type,"m_ignoreEnableActions").Attributes==1,"HardlightProject.CharacterBrain.m_ignoreEnableActions original field type/flags");
            Check(Field(type,"m_ignoreEnableActions").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_ignoreEnableActions original attribute sequence");
            Check(Canon(Field(type,"m_actionStateDeferred").FieldType)=="HardlightProject.CharacterActionFlags" && (int)Field(type,"m_actionStateDeferred").Attributes==1,"HardlightProject.CharacterBrain.m_actionStateDeferred original field type/flags");
            Check(Field(type,"m_actionStateDeferred").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_actionStateDeferred original attribute sequence");
            Check(Canon(Field(type,"m_actionStateActivatedThisFrame").FieldType)=="System.Boolean" && (int)Field(type,"m_actionStateActivatedThisFrame").Attributes==1,"HardlightProject.CharacterBrain.m_actionStateActivatedThisFrame original field type/flags");
            Check(Field(type,"m_actionStateActivatedThisFrame").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_actionStateActivatedThisFrame original attribute sequence");
            Check(Canon(Field(type,"m_actionStateTimestamps").FieldType)=="System.Collections.Generic.Dictionary`2<HardlightProject.GameAction,System.Single>" && (int)Field(type,"m_actionStateTimestamps").Attributes==33,"HardlightProject.CharacterBrain.m_actionStateTimestamps original field type/flags");
            Check(Field(type,"m_actionStateTimestamps").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_actionStateTimestamps original attribute sequence");
            Check(Canon(Field(type,"m_movementAggregate").FieldType)=="UnityEngine.Vector2" && (int)Field(type,"m_movementAggregate").Attributes==1,"HardlightProject.CharacterBrain.m_movementAggregate original field type/flags");
            Check(Field(type,"m_movementAggregate").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_movementAggregate original attribute sequence");
            Check(Canon(Field(type,"m_movementState").FieldType)=="UnityEngine.Vector2" && (int)Field(type,"m_movementState").Attributes==1,"HardlightProject.CharacterBrain.m_movementState original field type/flags");
            Check(Field(type,"m_movementState").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_movementState original attribute sequence");
            Check(Canon(Field(type,"m_cacheLockTimestamp").FieldType)=="System.Single" && (int)Field(type,"m_cacheLockTimestamp").Attributes==1,"HardlightProject.CharacterBrain.m_cacheLockTimestamp original field type/flags");
            Check(Field(type,"m_cacheLockTimestamp").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_cacheLockTimestamp original attribute sequence");
            Check(Canon(Field(type,"m_actionStateCache").FieldType)=="HardlightProject.CharacterActionFlags" && (int)Field(type,"m_actionStateCache").Attributes==1,"HardlightProject.CharacterBrain.m_actionStateCache original field type/flags");
            Check(Field(type,"m_actionStateCache").GetCustomAttributesData().Select(a=>a.AttributeType.FullName).SequenceEqual(new string[]{}),"HardlightProject.CharacterBrain.m_actionStateCache original attribute sequence");
            MethodBase[] members=type.GetMethods(Own).Cast<MethodBase>().Concat(type.GetConstructors(Own)).OrderBy(m=>m.MetadataToken).ToArray();
            Check(members.Select(m=>m.Name).SequenceEqual(new string[]{"get_Enabled","set_Enabled","get_ActionStateQueued","get_Jump","get_RailTargeting","get_HomingAttackTarget","get_HomingAttackOneShot","get_HomingAttackLightspeedDash","get_AirActive","get_AirCancel","get_Boost","get_BurstAttack","get_AirStompAttack","get_SpinDashCharge","get_Roll","AddMovement","GetMovement","CacheMovement","UpdateTimestamps","GetActionTimestamp","ApplyActions","GetAppliedState","GetRawState","LockCache","SetState","EndAction","SetEnabled","EndImpulses","Initialise","Update","Close",".ctor","<SetEnabled>b__52_0"}),"HardlightProject.CharacterBrain complete original method order");
            Check(members[0].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[0].GetParameters().Length==0 && Canon(((MethodInfo)members[0]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012de original signature/flags/parameters");
            Check(members[1].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig, SpecialName") && members[1].GetParameters().Length==1 && Canon(((MethodInfo)members[1]).ReturnType)=="System.Void" && members[1].GetParameters()[0].Name=="value" && Canon(members[1].GetParameters()[0].ParameterType)=="System.Boolean" && members[1].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012df original signature/flags/parameters");
            Check(members[2].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[2].GetParameters().Length==0 && Canon(((MethodInfo)members[2]).ReturnType)=="HardlightProject.CharacterActionFlags","HardlightProject.CharacterBrain 0x060012e0 original signature/flags/parameters");
            Check(members[3].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[3].GetParameters().Length==0 && Canon(((MethodInfo)members[3]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012e1 original signature/flags/parameters");
            Check(members[4].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[4].GetParameters().Length==0 && Canon(((MethodInfo)members[4]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012e2 original signature/flags/parameters");
            Check(members[5].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[5].GetParameters().Length==0 && Canon(((MethodInfo)members[5]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012e3 original signature/flags/parameters");
            Check(members[6].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[6].GetParameters().Length==0 && Canon(((MethodInfo)members[6]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012e4 original signature/flags/parameters");
            Check(members[7].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[7].GetParameters().Length==0 && Canon(((MethodInfo)members[7]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012e5 original signature/flags/parameters");
            Check(members[8].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[8].GetParameters().Length==0 && Canon(((MethodInfo)members[8]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012e6 original signature/flags/parameters");
            Check(members[9].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[9].GetParameters().Length==0 && Canon(((MethodInfo)members[9]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012e7 original signature/flags/parameters");
            Check(members[10].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[10].GetParameters().Length==0 && Canon(((MethodInfo)members[10]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012e8 original signature/flags/parameters");
            Check(members[11].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[11].GetParameters().Length==0 && Canon(((MethodInfo)members[11]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012e9 original signature/flags/parameters");
            Check(members[12].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[12].GetParameters().Length==0 && Canon(((MethodInfo)members[12]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012ea original signature/flags/parameters");
            Check(members[13].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[13].GetParameters().Length==0 && Canon(((MethodInfo)members[13]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012eb original signature/flags/parameters");
            Check(members[14].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig, SpecialName") && members[14].GetParameters().Length==0 && Canon(((MethodInfo)members[14]).ReturnType)=="System.Boolean","HardlightProject.CharacterBrain 0x060012ec original signature/flags/parameters");
            Check(members[15].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"FamORAssem, HideBySig") && members[15].GetParameters().Length==2 && Canon(((MethodInfo)members[15]).ReturnType)=="System.Void" && members[15].GetParameters()[0].Name=="direction" && Canon(members[15].GetParameters()[0].ParameterType)=="UnityEngine.Vector2" && members[15].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && members[15].GetParameters()[1].Name=="amount" && Canon(members[15].GetParameters()[1].ParameterType)=="System.Single" && members[15].GetParameters()[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012ed original signature/flags/parameters");
            Check(members[16].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[16].GetParameters().Length==0 && Canon(((MethodInfo)members[16]).ReturnType)=="UnityEngine.Vector2","HardlightProject.CharacterBrain 0x060012ee original signature/flags/parameters");
            Check(members[17].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[17].GetParameters().Length==0 && Canon(((MethodInfo)members[17]).ReturnType)=="System.Void","HardlightProject.CharacterBrain 0x060012ef original signature/flags/parameters");
            Check(members[18].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && members[18].GetParameters().Length==2 && Canon(((MethodInfo)members[18]).ReturnType)=="System.Void" && members[18].GetParameters()[0].Name=="actions" && Canon(members[18].GetParameters()[0].ParameterType)=="HardlightProject.CharacterActionFlags" && members[18].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && members[18].GetParameters()[1].Name=="timestamp" && Canon(members[18].GetParameters()[1].ParameterType)=="System.Single" && members[18].GetParameters()[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012f0 original signature/flags/parameters");
            Check(members[19].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[19].GetParameters().Length==1 && Canon(((MethodInfo)members[19]).ReturnType)=="System.Single" && members[19].GetParameters()[0].Name=="action" && Canon(members[19].GetParameters()[0].ParameterType)=="HardlightProject.GameAction" && members[19].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012f1 original signature/flags/parameters");
            Check(members[20].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[20].GetParameters().Length==2 && Canon(((MethodInfo)members[20]).ReturnType)=="System.Void" && members[20].GetParameters()[0].Name=="actions" && Canon(members[20].GetParameters()[0].ParameterType)=="HardlightProject.CharacterActionFlags" && members[20].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && members[20].GetParameters()[1].Name=="timestamp" && Canon(members[20].GetParameters()[1].ParameterType)=="System.Single" && members[20].GetParameters()[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012f2 original signature/flags/parameters");
            Check(members[21].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[21].GetParameters().Length==1 && Canon(((MethodInfo)members[21]).ReturnType)=="System.Boolean" && members[21].GetParameters()[0].Name=="action" && Canon(members[21].GetParameters()[0].ParameterType)=="HardlightProject.GameAction" && members[21].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012f3 original signature/flags/parameters");
            Check(members[22].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[22].GetParameters().Length==2 && Canon(((MethodInfo)members[22]).ReturnType)=="System.Boolean" && members[22].GetParameters()[0].Name=="action" && Canon(members[22].GetParameters()[0].ParameterType)=="HardlightProject.GameAction" && members[22].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && members[22].GetParameters()[1].Name=="movementThreshold" && Canon(members[22].GetParameters()[1].ParameterType)=="System.Single" && members[22].GetParameters()[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"Optional, HasDefault"),"HardlightProject.CharacterBrain 0x060012f4 original signature/flags/parameters");
            Check(members[23].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[23].GetParameters().Length==1 && Canon(((MethodInfo)members[23]).ReturnType)=="System.Void" && members[23].GetParameters()[0].Name=="lockTimestamp" && Canon(members[23].GetParameters()[0].ParameterType)=="System.Single" && members[23].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012f5 original signature/flags/parameters");
            Check(members[24].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"FamORAssem, HideBySig") && members[24].GetParameters().Length==2 && Canon(((MethodInfo)members[24]).ReturnType)=="System.Void" && members[24].GetParameters()[0].Name=="action" && Canon(members[24].GetParameters()[0].ParameterType)=="HardlightProject.GameAction" && members[24].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && members[24].GetParameters()[1].Name=="value" && Canon(members[24].GetParameters()[1].ParameterType)=="System.Boolean" && members[24].GetParameters()[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012f6 original signature/flags/parameters");
            Check(members[25].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[25].GetParameters().Length==1 && Canon(((MethodInfo)members[25]).ReturnType)=="System.Void" && members[25].GetParameters()[0].Name=="action" && Canon(members[25].GetParameters()[0].ParameterType)=="HardlightProject.GameAction" && members[25].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012f7 original signature/flags/parameters");
            Check(members[26].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[26].GetParameters().Length==2 && Canon(((MethodInfo)members[26]).ReturnType)=="System.Void" && members[26].GetParameters()[0].Name=="value" && Canon(members[26].GetParameters()[0].ParameterType)=="System.Boolean" && members[26].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None") && members[26].GetParameters()[1].Name=="timestamp" && Canon(members[26].GetParameters()[1].ParameterType)=="System.Single" && members[26].GetParameters()[1].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012f8 original signature/flags/parameters");
            Check(members[27].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[27].GetParameters().Length==1 && Canon(((MethodInfo)members[27]).ReturnType)=="System.Void" && members[27].GetParameters()[0].Name=="timestamp" && Canon(members[27].GetParameters()[0].ParameterType)=="System.Single" && members[27].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012f9 original signature/flags/parameters");
            Check(members[28].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask") && members[28].GetParameters().Length==0 && Canon(((MethodInfo)members[28]).ReturnType)=="System.Void","HardlightProject.CharacterBrain 0x060012fa original signature/flags/parameters");
            Check(members[29].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, HideBySig") && members[29].GetParameters().Length==1 && Canon(((MethodInfo)members[29]).ReturnType)=="System.Void" && members[29].GetParameters()[0].Name=="timestamp" && Canon(members[29].GetParameters()[0].ParameterType)=="System.Single" && members[29].GetParameters()[0].Attributes==(ParameterAttributes)Enum.Parse(typeof(ParameterAttributes),"None"),"HardlightProject.CharacterBrain 0x060012fb original signature/flags/parameters");
            Check(members[30].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Public, Virtual, HideBySig, VtableLayoutMask, Abstract") && members[30].GetParameters().Length==0 && Canon(((MethodInfo)members[30]).ReturnType)=="System.Void","HardlightProject.CharacterBrain 0x060012fc original signature/flags/parameters");
            Check(members[31].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Family, HideBySig, SpecialName, RTSpecialName") && members[31].GetParameters().Length==0,"HardlightProject.CharacterBrain 0x060012fd original signature/flags/parameters");
            Check(members[32].Attributes==(MethodAttributes)Enum.Parse(typeof(MethodAttributes),"Private, HideBySig") && members[32].GetParameters().Length==0 && Canon(((MethodInfo)members[32]).ReturnType)=="System.Void","HardlightProject.CharacterBrain 0x060012fe original signature/flags/parameters");
            }
            int[] values = { 0, 1, 5, int.MinValue, -1, unchecked((int)0xaaaaaaaa) };
            int[] masks = { 0, 1, 3, 5, int.MinValue, -1, unchecked((int)0x55555555) };
            foreach (int value in values)
            {
                BitFlags32 f=value;
                Check(f.ToInt()==value, "bits survive implicit conversion");
                Check(f.IsAnySet()==(value!=0), "empty versus any bit");
                foreach (int mask in masks)
                {
                    Check(f.IsSet(mask)==((mask & ~value)==0), "all requested mask bits including empty mask");
                    Check(f.IsAnySet(mask)==((mask & value)!=0), "mask overlap");
                    Check(f.IsAllSet(mask)==(mask==value), "IsAllSet exact equality rather than containment");
                    BitFlags32 changed=f; changed.Set(mask,true);
                    Check(changed.ToInt()==(value|mask), "mask union"); changed.Set(mask,false);
                    Check(changed.ToInt()==(value&~mask), "mask removal preserves unrelated bits");
                }
                foreach(int index in new[]{-33,-32,-1,0,1,31,32,33,63,64,int.MinValue,int.MaxValue})
                {
                    int bit=unchecked(1 << (index & 31));
                    Check(f.IsIndexSet(index)==((value&bit)!=0), "index low-five-bit wrapping");
                    BitFlags32 changed=f;changed.SetIndex(index,true);
                    Check(changed.ToInt()==(value|bit), "wrapped index insertion"); changed.SetIndex(index,false);
                    Check(changed.ToInt()==(value&~bit), "wrapped index clearing");
                }
                f.Clear();Check(f.ToInt()==0 && f.IsSet(0) && !f.IsAnySet(), "clear and empty-mask containment");
            }
            Check(BitFlags32.AllSelected<Masks>()==5, "OR values includes composite aliases");
            Check(BitFlags32.AllSelectedNonFlags<Masks>()==51, "nonflags treats composite values as indices");
            Check(BitFlags32.AllSelected<Ordinal>()==-1, "signed enum literal OR");
            Check(BitFlags32.AllSelectedNonFlags<Ordinal>()==unchecked((int)0x80000007), "ordinal negative/large shifts wrap");
            Check(BitFlags32.SelectFlag(Ordinal.Negative)==int.MinValue, "select negative enum bit");
            Check(BitFlags32.SelectFlag(Masks.Five)==32, "select does not special-case FlagsAttribute");
            Check(BitFlags32.SelectFlag(33)==2 && BitFlags32.SelectFlag(-33)==int.MinValue, "unconstrained convertible integers");
            Check(BitFlags32.SelectFlag("2")==4 && BitFlags32.SelectFlag((object)null)==1, "unconstrained convertible string/null");
            Check(Argument(()=>BitFlags32.AllSelected<int>(), "T must be an enumerated type"), "nonenum explicit original message");
            Check(Argument(()=>BitFlags32.AllSelectedNonFlags<string>(), "T must be an enumerated type"), "nonenum nonflags explicit original message");
            Check(Overflow(()=>BitFlags32.AllSelected<Huge>()), "enum conversion overflow remains visible");
            Check(Overflow(()=>BitFlags32.AllSelectedNonFlags<Huge>()), "enum-index conversion overflow remains visible");
            Check(Overflow(()=>BitFlags32.SelectFlag(Huge.TooLarge)), "single enum conversion overflow remains visible");

            Dictionary<GameAction,int> map=(Dictionary<GameAction,int>)Field(typeof(CharacterActionFlags),"m_actionMapping").GetValue(null);
            Dictionary<GameAction,int> previous=new Dictionary<GameAction,int>(map);
            object oldIndices=Field(typeof(CharacterActionFlags),"m_actionIndices").GetValue(null);
            object oldCount=Field(typeof(CharacterActionFlags),"<ActionCount>k__BackingField").GetValue(null);
            object host=Field(typeof(CoroutineUtils),"s_instance").GetValue(null);
            try
            {
                // Genuine host stays absent here; no fake MonoBehaviour is introduced.
                Field(typeof(CoroutineUtils),"s_instance").SetValue(null,null);
                CharacterActionFlags.Initialise(); GameAction[] order=(GameAction[])Enum.GetValues(typeof(GameAction));
                Check(CharacterActionFlags.ActionCount==19 && order.Length==19, "all nineteen original game actions");
                Check(order.Select(x=>unchecked((uint)(int)x)).SequenceEqual(order.Select(x=>unchecked((uint)(int)x)).OrderBy(x=>x)), "Enum.GetValues uses unsigned ordering");
                CharacterActionFlags all=0;
                for(int i=0;i<order.Length;++i)
                {
                    Check(CharacterActionFlags.GetAction(i)==order[i], "original action index ordering");
                    CharacterActionFlags one=0;one.SetValue(order[i],true);
                    Check(one.ToInt()==(1<<i) && one.GetValue(order[i]), "hashed action selects ordinal bit");
                    one.SetValue(order[i],false); Check(one.ToInt()==0, "action bit removal");all.SetValue(order[i],true);
                }
                Check(all.ToInt()==(1<<19)-1, "all action bits");all.Clear();Check(all.ToInt()==0, "action clear");
                GameAction absent=(GameAction)123456789;
                Check(ActionError(()=>all.GetValue(absent),absent), "unknown read exact error payload");
                Check(ActionError(()=>all.SetValue(absent,true),absent), "unknown write exact error payload");
                map[absent]=31; CharacterActionFlags.Initialise();
                CharacterActionFlags extra=0;extra.SetValue(absent,true);
                Check(extra.ToInt()==int.MinValue && extra.GetValue(absent), "reinitialise retains existing mapping entries");map.Remove(absent);
                Check(((CharacterActionFlags)(-1)).ToInt()==-1, "conversion retains extra unused bits");
                Brain b=new Brain(); Check(b.Enabled && b.ActionStateQueued.ToInt()==0, "constructor enabled and empty states");
                Check(ReferenceEquals(Get<Dictionary<GameAction,float>>(b,"m_actionStateTimestamps").Comparer,HardlightEnumComparers.GameActionComparer), "genuine generated comparer identity");
                b.Initialise();Check(Get<CharacterActionFlags>(b,"m_ignoreEnableActions").ToInt()==Mask(GameAction.CharacterHomingAttack,GameAction.CharacterHomingAttackOneShot,GameAction.CharacterLightspeedDash,GameAction.CharacterRailTarget).ToInt(), "four original ignored-enable actions");
                Check(b.GetActionTimestamp(absent)==0f, "unseen action timestamp defaults to zero without flag validation");
                b.Input(GameAction.CharacterJump,true);
                Check(b.GetRawState(GameAction.CharacterJump) && !b.Jump && b.ActionStateQueued.GetValue(GameAction.CharacterJump), "raw queue precedes applied state");
                b.Update(2f); Check(b.Jump && b.GetActionTimestamp(GameAction.CharacterJump)==2f, "press applies and timestamps");
                b.Update(3f);Check(b.GetActionTimestamp(GameAction.CharacterJump)==2f, "held state leaves timestamp unchanged");
                b.EndAction(GameAction.CharacterJump);
                Check(!b.GetRawState(GameAction.CharacterJump) && b.Jump && !b.ActionStateQueued.GetValue(GameAction.CharacterJump), "release raw queue before applied");
                b.Update(4f);Check(!b.Jump && b.GetActionTimestamp(GameAction.CharacterJump)==4f, "release also timestamps");
                b.ApplyActions(Mask(GameAction.CharacterBoost,GameAction.CharacterSpinDashCharge),5f);
                Check(b.Boost && b.SpinDashCharge && b.GetActionTimestamp(GameAction.CharacterBoost)==5f, "external applied actions use original edge updater");
                Check(!b.GetRawState(GameAction.CharacterBoost), "external application does not mutate raw");
                b.Input(GameAction.CharacterBoost,true);b.Input(GameAction.CharacterSpinDashCharge,true);
                b.Input(GameAction.CharacterHomingAttackOneShot,true);b.Input(GameAction.CharacterRoll,true);
                foreach(GameAction action in new[]{GameAction.CharacterJump,GameAction.CharacterRailTarget,GameAction.CharacterHomingAttack,GameAction.CharacterLightspeedDash,GameAction.CharacterAirActivate,GameAction.CharacterAirCancel,GameAction.CharacterBurstAttack,GameAction.CharacterAirStompAttack,GameAction.CharacterSpinDashCharge})
                    b.Input(action,true);
                b.Update(6f);b.EndImpulses(7f);
                Check(!b.Jump && b.Boost && !b.SpinDashCharge && b.Roll && b.HomingAttackOneShot, "impulse reset releases spin-dash charge and retains boost/roll/one-shot");
                Check(b.GetActionTimestamp(GameAction.CharacterJump)==7f, "impulse release timestamp");
                foreach(GameAction action in new[]{GameAction.CharacterRailTarget,GameAction.CharacterHomingAttack,GameAction.CharacterLightspeedDash,GameAction.CharacterAirActivate,GameAction.CharacterAirCancel,GameAction.CharacterBurstAttack,GameAction.CharacterAirStompAttack,GameAction.CharacterSpinDashCharge})
                    Check(!b.GetAppliedState(action), "original cleared impulse membership");
                foreach(GameAction action in new[]{GameAction.CharacterJump,GameAction.CharacterRailTarget,GameAction.CharacterHomingAttack,GameAction.CharacterLightspeedDash,GameAction.CharacterAirActivate,GameAction.CharacterAirCancel,GameAction.CharacterBurstAttack,GameAction.CharacterAirStompAttack,GameAction.CharacterSpinDashCharge})
                {
                    Check(!b.GetRawState(action) && !b.ActionStateQueued.GetValue(action), "native nine-action impulse reset clears raw and queued states");
                    Check(b.GetActionTimestamp(action)==7f, "native nine-action impulse release timestamps changed applied state");
                }
                Check(b.GetActionTimestamp(GameAction.CharacterRoll)==6f, "retained roll timestamp is unchanged");
                Check(b.GetActionTimestamp(GameAction.CharacterBoost)==5f, "retained boost timestamp is unchanged");
                Check(b.GetActionTimestamp(GameAction.CharacterHomingAttackOneShot)==6f, "retained one-shot timestamp is unchanged");
                Brain disabled=new Brain();disabled.Initialise();
                foreach(GameAction action in new[]{GameAction.CharacterSpinDashCharge,GameAction.CharacterRoll,GameAction.CharacterBoost,GameAction.CharacterHomingAttackOneShot}) disabled.Input(action,true);
                disabled.Update(20f);disabled.SetEnabled(false,21f);
                Check(!disabled.Enabled && !disabled.SpinDashCharge && disabled.Roll && disabled.Boost && disabled.HomingAttackOneShot, "disable invokes original impulse reset before disabling while retaining continuous actions");
                Check(!disabled.GetRawState(GameAction.CharacterSpinDashCharge) && disabled.GetRawState(GameAction.CharacterRoll), "disable clears charge raw input while retaining roll raw input");
                Check(!disabled.ActionStateQueued.GetValue(GameAction.CharacterSpinDashCharge) && disabled.ActionStateQueued.GetValue(GameAction.CharacterRoll), "disable clears charge queued input while retaining roll queue");
                Check(disabled.GetActionTimestamp(GameAction.CharacterSpinDashCharge)==21f && disabled.GetActionTimestamp(GameAction.CharacterRoll)==20f, "disable timestamps released charge and retains roll press timestamp");
                b.ApplyActions(0,8f);b.SetEnabled(false,9f);Check(!b.Enabled, "disable after synchronous impulse update");
                b.Input(GameAction.CharacterJump,true);
                Check(b.GetRawState(GameAction.CharacterJump) && !b.ActionStateQueued.GetValue(GameAction.CharacterJump) && Get<CharacterActionFlags>(b,"m_actionStateDeferred").GetValue(GameAction.CharacterJump), "disabled action is deferred but raw stays true");
                b.Input(GameAction.CharacterHomingAttack,true);
                Check(b.ActionStateQueued.GetValue(GameAction.CharacterHomingAttack), "ignored-enable action still queues while disabled");
                b.Update(10f); Check(b.HomingAttackTarget && !b.Jump, "disabled Update still applies queue");
                bool missingHost=false;try{b.SetEnabled(true,11f);}catch(NullReferenceException){missingHost=true;}
                Check(missingHost && !b.Enabled && Get<bool>(b,"m_actionStateActivatedThisFrame"), "missing genuine host fails after activation latch");
                b.Input(GameAction.CharacterBoost,true);
                Check(b.GetRawState(GameAction.CharacterBoost) && !b.ActionStateQueued.GetValue(GameAction.CharacterBoost) && !Get<CharacterActionFlags>(b,"m_actionStateDeferred").GetValue(GameAction.CharacterBoost), "activation latch discards new pressed state from queue and deferred");
                Callback(b);Check(b.Enabled, "original callback only sets Enabled");
                b.Update(12f);Check(b.Jump && !b.HomingAttackTarget && !Get<bool>(b,"m_actionStateActivatedThisFrame"), "first enabled application consumes saved deferred state once");
                b.Update(13f);Check(b.Jump && b.GetActionTimestamp(GameAction.CharacterJump)==12f, "deferred press timestamp is not repeated");
                b.SetEnabled(true,14f);
                Check(!Get<bool>(b,"m_actionStateActivatedThisFrame"), "already enabled call returns without callback/latch");
                b.EndAction(GameAction.CharacterJump);Check(!Get<CharacterActionFlags>(b,"m_actionStateDeferred").GetValue(GameAction.CharacterJump), "release clears deferred");
                b.Update(15f);Check(!b.Jump, "no-op enabling leaves ordinary release application");
                b.Close();Check(b.closeCalls==1, "genuine abstract Close supplied only by test subtype");

                Brain c=new Brain();c.Initialise();c.Input(GameAction.CharacterJump,true);c.LockCache(20f);
                c.Input(GameAction.CharacterJump,false);c.Input(GameAction.CharacterBoost,true);
                Check(c.GetRawState(GameAction.CharacterJump) && !c.GetRawState(GameAction.CharacterBoost), "lock raw snapshot differs from live raw");
                c.Update(19f);Check(c.Jump && !c.Boost && Get<float>(c,"m_cacheLockTimestamp")==20f, "lock snapshot survives earlier update");
                c.LockCache(18f);Check(Get<float>(c,"m_cacheLockTimestamp")==20f, "lower lock timestamp preserves maximum but refreshes snapshot");
                c.Update(20f);Check(!c.Jump && c.Boost && Get<float>(c,"m_cacheLockTimestamp")==0f && Get<CharacterActionFlags>(c,"m_actionStateCache").ToInt()==0, "expiry applies refreshed snapshot then clears lock/cache");
                c.Input(GameAction.CharacterBoost,false);c.Update(21f);Check(!c.Boost, "postexpiry live queue resumes");
                c.LockCache(float.NaN);Check(float.IsNaN(Get<float>(c,"m_cacheLockTimestamp")), "NaN lock argument preserved by ordered maximum");
                c.Update(22f);Check(Get<float>(c,"m_cacheLockTimestamp")==0f, "unordered lock cleans up");
                c.Input(GameAction.CharacterBoost,true);c.LockCache(0f);int zeroCache=Get<CharacterActionFlags>(c,"m_actionStateCache").ToInt();c.Update(-1f);Check(Get<float>(c,"m_cacheLockTimestamp")==0f && zeroCache!=0 && Get<CharacterActionFlags>(c,"m_actionStateCache").ToInt()==zeroCache, "zero lock preserves cache even for negative update time");
                c.LockCache(-2f);c.Update(-3f);Check(Get<float>(c,"m_cacheLockTimestamp")==0f && Get<CharacterActionFlags>(c,"m_actionStateCache").ToInt()==zeroCache, "maximum zero remains zero and cache retained for negative request");
                Brain m=new Brain();m.Initialise();m.Move(new Vector2(1f,0f),.75f);m.Move(new Vector2(0f,-1f),.5f);m.CacheMovement();
                Check(m.GetMovement().x==.75f && m.GetMovement().y==-.5f, "subunit aggregate retains magnitude");
                Check(m.GetRawState(GameAction.CharacterMovementRight) && !m.GetRawState(GameAction.CharacterMovementLeft), "positive horizontal direction");
                Check(!m.GetRawState(GameAction.CharacterMovementDown) && !m.GetRawState(GameAction.CharacterMovementUp), "movement threshold is strict");
                Check(m.GetRawState(GameAction.CharacterMovementDown,.49f) && m.GetRawState(GameAction.CharacterMovementVertical,.49f), "negative vertical versus absolute axis");
                Check(m.GetRawState(GameAction.CharacterMovementHorizontal,.74f) && !m.GetRawState(GameAction.CharacterMovementHorizontal,.75f), "axis threshold strict edge");
                Check(!m.GetRawState(GameAction.CharacterMovementRight,float.NaN) && !m.GetRawState(GameAction.CharacterMovementLeft,float.NaN), "unordered movement thresholds inactive");
                m.CacheMovement();Check(m.GetMovement().x==0 && m.GetMovement().y==0, "cache consumes aggregate once");
                m.Move(new Vector2(3f,4f),1f);m.CacheMovement();Check(Math.Abs(m.GetMovement().x-.6f)<1e-6 && Math.Abs(m.GetMovement().y-.8f)<1e-6, "superunit input normalized");
                m.Move(new Vector2(float.NaN,1f),1f);m.CacheMovement();Check(float.IsNaN(m.GetMovement().x) && m.GetMovement().y==1f, "NaN magnitude skips normalization");
                m.Move(new Vector2(float.PositiveInfinity,1f),1f);m.CacheMovement();Check(float.IsNaN(m.GetMovement().x) && m.GetMovement().y==0f, "infinite aggregate preserves original normalization result");
                Brain getters=new Brain();getters.Initialise(); getters.ApplyActions(Mask(order),1f);
                Check(getters.Jump && getters.RailTargeting && getters.HomingAttackTarget && getters.HomingAttackOneShot && getters.HomingAttackLightspeedDash && getters.AirActive && getters.AirCancel && getters.Boost && getters.BurstAttack && getters.AirStompAttack && getters.SpinDashCharge && getters.Roll, "all twelve named applied getters");
                foreach(GameAction action in order)Check(getters.GetAppliedState(action), "complete original applied action set");
            }
            finally
            {
                Field(typeof(CoroutineUtils),"s_instance").SetValue(null,host);
                map.Clear();foreach(var pair in previous)map[pair.Key]=pair.Value;
                Field(typeof(CharacterActionFlags),"m_actionIndices").SetValue(null,oldIndices);
                Field(typeof(CharacterActionFlags),"<ActionCount>k__BackingField").SetValue(null,oldCount);
            }
            return checks;
        }
        private static CharacterActionFlags Mask(params GameAction[] actions)
        {
            CharacterActionFlags flags=0;foreach(GameAction action in actions)flags.SetValue(action,true);return flags;
        }
        public static int RunEngineJson()
        {
            checks=0;BitFlags32 bits=unchecked((int)0x81234567);
            string json=JsonUtility.ToJson(bits);
            Check(json=="{\"m_flags\":-2128394905}", "original serialized private flag field");
            BitFlags32 restored=JsonUtility.FromJson<BitFlags32>(json);
            Check(restored.ToInt()==bits.ToInt(), "genuine JsonUtility signed bits roundtrip");
            restored=JsonUtility.FromJson<BitFlags32>("{\"m_flags\":-1}");
            Check(restored.ToInt()==-1 && restored.IsSet(int.MinValue), "genuine JsonUtility negative bits");
            restored=JsonUtility.FromJson<BitFlags32>("{}");Check(restored.ToInt()==0, "genuine JsonUtility absent field defaults");
            return checks;
        }
    }
}
