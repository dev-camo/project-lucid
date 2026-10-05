using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid
{
    public static class StateMachinesGroupVerification
    {
        private static int s_checks;
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static void Check(bool condition,string message) { ++s_checks;if(!condition)throw new InvalidOperationException(message); }
        private static void Throws<T>(Action action,string message) where T:Exception
        {
            bool correct=false;try{action();}catch(Exception e){correct=(e is TargetInvocationException?e.InnerException:e)is T;}Check(correct,message);
        }
        public static int RunManaged()
        {
            s_checks=0;var type=typeof(StateMachinesGroup);
            Check(type.Assembly.GetName().Name=="Game.Runtime", "original Game.Runtime assembly");
            Check(type.FullName=="HardlightProject.StateMachinesGroup", "original type identity");
            Check((uint)type.Attributes==0x100001, "original type flags");
            Check(type.BaseType==typeof(DefinitionDataType<string,FiniteStateMachineScriptableObject>), "actual complete generic definition base");
            Check(type.GetFields(Own).Length==0, "original zeroown fields");
            Check(type.GetMethods(Own).Length==2 && type.GetConstructors(Own).Length==1, "exact three own methods");
            var key=type.GetMethod("GetElementKey",Own);var comparer=type.GetMethod("GetKeyComparer",Own);
            Check(key.IsFamily && key.IsVirtual && !key.IsFinal && key.ReturnType==typeof(string), "original protected key override");
            Check(key.GetParameters().Single().ParameterType==typeof(FiniteStateMachineScriptableObject), "actual FSM ScriptableObject input");
            Check(comparer.IsFamily && comparer.IsVirtual && !comparer.IsFinal && comparer.ReturnType==typeof(IEqualityComparer<string>), "original comparer contract");
            Check(comparer.GetMethodBody().GetILAsByteArray().SequenceEqual(new byte[]{0x14,0x2a}), "original CLRnull comparer body");
            byte[] il=key.GetMethodBody().GetILAsByteArray();Check(il.Length==7 && il[0]==0x03 && il[1]==0x6f && il[6]==0x2a, "key direct supplied-argument callvirt/return");
            var call=(MethodInfo)key.Module.ResolveMethod(BitConverter.ToInt32(il,2));Check(call.DeclaringType==typeof(UnityEngine.Object) && call.Name=="get_name", "genuine UnityObject name callee");
            var ctor=type.GetConstructor(Type.EmptyTypes);il=ctor.GetMethodBody().GetILAsByteArray();Check(il.Length==7 && il[0]==0x02 && il[1]==0x28 && il[6]==0x2a, "only actual base ctor in native-derived constructor");
            Check(ctor.Module.ResolveMethod(BitConverter.ToInt32(il,2)).DeclaringType==type.BaseType, "real closed generic base ctor");
            var menu=type.GetCustomAttribute<CreateAssetMenuAttribute>();Check(menu.fileName=="StateMachinesGroup" && menu.menuName=="HardlightProject/DefinitionData/Groups/StateMachinesGroup" && menu.order==0, "original authored menu values");
            var options=type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();Check(options.Length==2 && options[0].Option==Option.NullChecks && options[1].Option==Option.ArrayBoundsChecks && options.All(o=>o.Value is bool b && !b), "two ordered original IL2CPP options");
            return s_checks;
        }
        // Actual-engine suite. No FSM JSON acquire, provider replacement or authored asset load.
        public static void Run()
        {
            RunManaged();var group=ScriptableObject.CreateInstance<StateMachinesGroup>();var fsm=ScriptableObject.CreateInstance<FiniteStateMachineScriptableObject>();var other=ScriptableObject.CreateInstance<FiniteStateMachineScriptableObject>();
            try
            {
                Check(group.m_elements==null, "original base ctor leaves arraynull");
                Throws<NullReferenceException>(()=>group.GetData(), "original default array fails on enumeration");
                fsm.name="AuthoredAssetObject";other.name="SecondObject";
                typeof(FiniteStateMachineScriptableObject).GetField("m_name",Own).SetValue(fsm,"DifferentFSMSelection");
                var element=new DefinitionDataType<string,FiniteStateMachineScriptableObject>.DefinitionElement<FiniteStateMachineScriptableObject>(fsm);
                group.m_elements=new[]{element};var first=group.GetData();
                Check(first.Count==1 && ReferenceEquals(first["AuthoredAssetObject"],fsm) && !first.ContainsKey("DifferentFSMSelection"), "key is actual UnityObject name, not authored FSM selection");
                Check(ReferenceEquals(first.Comparer,EqualityComparer<string>.Default), "native null comparer requests actual default");
                fsm.name="Renamed";var second=group.GetData();Check(second.ContainsKey("Renamed") && first.ContainsKey("AuthoredAssetObject") && !ReferenceEquals(first,second), "new dictionary rebuild followscurrent Unityname");
                group.m_elements=new[]{element,new DefinitionDataType<string,FiniteStateMachineScriptableObject>.DefinitionElement<FiniteStateMachineScriptableObject>(other)};
                Check(group.GetData().Count==2, "two actual objects map without acquiring FSM");
                other.name=fsm.name;Throws<ArgumentException>(()=>group.GetData(), "duplicate actual Object names retain original Add failure");
                group.m_elements=new[]{new DefinitionDataType<string,FiniteStateMachineScriptableObject>.DefinitionElement<FiniteStateMachineScriptableObject>(null)};
                Throws<NullReferenceException>(()=>group.GetData(), "null data keeps original object-name dereference");
                group.m_elements=Array.Empty<DefinitionDataType<string,FiniteStateMachineScriptableObject>.DefinitionElement<FiniteStateMachineScriptableObject>>();Check(group.GetData().Count==0,"genuine explicit empty array yields empty dictionary");
                group.m_elements=new[]{element};UnityEngine.Object.DestroyImmediate(fsm);
                Throws<MissingReferenceException>(()=>group.GetData(),"destroyed original FSM data reaches actual Unity getname failure");
            }
            finally { UnityEngine.Object.DestroyImmediate(group);if(fsm!=null)UnityEngine.Object.DestroyImmediate(fsm);if(other!=null)UnityEngine.Object.DestroyImmediate(other); }
            Debug.Log("StateMachinesGroupVerification passed "+s_checks+" checks; original authored definition loading remains unverified.");
        }
    }
}
