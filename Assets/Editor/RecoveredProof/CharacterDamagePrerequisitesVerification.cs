using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Complete genuine data/contract types support the actual Character's damage,
    // collectable and hover callers. This bounded fixture does not replace those
    // callers or claim collision, gameplay, original assets or native layout.
    public static class CharacterDamagePrerequisitesVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static int checks;
        private static void Check(bool value,string message)
        { if(!value)throw new InvalidOperationException(message);checks++; }
        private static FieldInfo Field(Type t,string name) => t.GetField(name,Own) ?? throw new InvalidOperationException(name);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value),0);
        public static int RunManaged()
        {
            checks=0;
            var hover=new HoverData();
            Check(ReferenceEquals(hover.Origin,null) && ReferenceEquals(hover.DistanceSpeedCurve,null),"original hover references remain null");
            Check(Bits(hover.TargetHeight)==0 && Bits(hover.OscillateHeightBounds)==0 && Bits(hover.MoveSpeed)==0 && Bits(hover.SmoothTimeSeconds)==0,"original hover scalar fields remain binary32 zero");
            hover.TargetHeight=-4f;hover.OscillateHeightBounds=float.NaN;hover.MoveSpeed=float.PositiveInfinity;hover.SmoothTimeSeconds=-0f;
            Check(hover.TargetHeight==-4f && float.IsNaN(hover.OscillateHeightBounds) && float.IsPositiveInfinity(hover.MoveSpeed) && Bits(hover.SmoothTimeSeconds)==Bits(-0f),"hover fields store authored values without new clamps/defaults");
            var definition=new CharacterAbilityTypeGroup.AbilityTypeDefinition();
            Check((int)definition.Type==0 && Bits(definition.GracePeriod)==Bits(0.2f),"original ability type/grace constructor defaults");
            Field(typeof(CharacterAbilityTypeGroup.AbilityTypeDefinition),"m_type").SetValue(definition,(ActorAbilityType)123);
            Field(typeof(CharacterAbilityTypeGroup.AbilityTypeDefinition),"m_gracePeriod").SetValue(definition,float.NaN);
            Check(definition.Type==(ActorAbilityType)123 && float.IsNaN(definition.GracePeriod),"group entry accessors expose exact serialized values");
            var group=(CharacterAbilityTypeGroup)FormatterServices.GetUninitializedObject(typeof(CharacterAbilityTypeGroup));
            var entries=new List<CharacterAbilityTypeGroup.AbilityTypeDefinition>();Field(typeof(CharacterAbilityTypeGroup),"m_abilities").SetValue(group,entries);
            Check(ReferenceEquals(group.Abilities,entries),"group getter returns actual mutable authored list");entries.Add(definition);Check(group.Abilities.Count==1 && ReferenceEquals(group.Abilities[0],definition),"group live getter reflects list mutation");group.Abilities.Clear();Check(entries.Count==0,"group getter does not copy/filter");
            foreach(int fixedChange in new[]{int.MinValue,-100,-1,0,1,100,int.MaxValue})
                foreach(float fraction in new[]{-0f,0f,-1f,1f,float.Epsilon,-float.Epsilon,float.MinValue,float.MaxValue,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
                {
                    var value=new CollectableChangeData {Type=(CollectableType)987,FixedChange=fixedChange,FractionChange=fraction,Metadata=new CollectableChangeMetadata{Source=(CollectableSource)(-231)}};
                    bool expected=fixedChange!=0 || (!float.IsNaN(fraction) ? fraction!=0f : true);
                    Check(value.HasChange()==expected,"native fixed-first/nonzero fraction predicate including NaN");
                    Check(value.FixedChange==fixedChange && Bits(value.FractionChange)==Bits(fraction),"HasChange does not mutate change fields");
                }
            var zero=default(CollectableChangeData);Check(!zero.HasChange(),"default change struct is empty");zero.Type=CollectableType.Ring;zero.Metadata.Source=CollectableSource.DamageTaken;Check(!zero.HasChange(),"type/source alone do not make change");
            Type mod=typeof(IGameplayModifiable);Check(mod.IsInterface && mod.GetFields(Own).Length==0,"real modifier interface has no fields");
            var declarations=mod.GetMethods(Own).OrderBy(m=>m.MetadataToken).ToArray();Check(declarations.Select(m=>m.Name).SequenceEqual(new[]{"AddModifierOverride","RemoveModifierOverrides","AreModifiersBlocked"}),"complete modifier contract order");
            Check(declarations.All(m=>m.IsAbstract && m.GetMethodBody()==null),"three genuine contracts have no invented implementation");
            var generic=declarations[0].GetGenericArguments();Check(generic.Length==1 && generic[0].Name=="T" && generic[0].GenericParameterAttributes==GenericParameterAttributes.None && generic[0].GetGenericParameterConstraints().Length==0,"original modifier T stays unconstrained");
            Check(declarations[0].ReturnType==typeof(StackableDataHandle) && declarations[0].GetParameters()[0].Name=="modifierType" && declarations[0].GetParameters()[1].Name=="value","original generic modifier signature/names");
            var damage=typeof(IDamageable).GetMethods(Own);Check(typeof(IDamageable).IsInterface && damage.Length==1 && damage[0].Name=="TryTakeDamage" && damage[0].IsAbstract && damage[0].GetMethodBody()==null,"sole actual damage interface contract");
            Check(damage[0].ReturnType==typeof(DamageAction) && damage[0].GetParameters()[0].ParameterType==typeof(HazardDefinition) && damage[0].GetParameters()[1].ParameterType==typeof(Action<DamageAction,Collider>),"exact original damage definition/callback types");
            return checks;
        }
        private static int EngineChecks()
        {
            int before=checks;HazardDefinition hazard=null;CharacterAbilityTypeGroup group=null;
            try
            {
                hazard=ScriptableObject.CreateInstance<HazardDefinition>();group=ScriptableObject.CreateInstance<CharacterAbilityTypeGroup>();
                Check(hazard.AllowKnockback && !hazard.InstantDeath,"genuine hazard constructor knockback/death defaults");
                Check(hazard.CharacterInvulnerableFormType==ActorFormType.None && hazard.CharacterDestroyHazardFormType==ActorFormType.None,"both original None form hashes");
                Check(Bits(hazard.BoostCooldownDurationSeconds)==Bits(0.5f) && Bits(hazard.ZeroToFullVelocityScale)==Bits(1f),"hazard original cooldown/full velocity stores");
                Check(Bits(hazard.StumbleSpeedThreshold)==0 && Bits(hazard.StumbleSpeedReductionFraction)==0 && Bits(hazard.InvulnerableDurationSeconds)==0 && Bits(hazard.InitialToResultingVelocityScale)==0,"unwritten hazard floats stay zero");
                Check(hazard.DoNotKnockbackAbilities==null && hazard.InvulnerableAbilities==null && hazard.CharacterDestroyHazardAbilities==null,"hazard arrays/groups are originally null");
                Check(!hazard.CollectableCost.HasChange() && (int)hazard.Type==0,"hazard change/type initially zero");
                Check(group.Abilities!=null && group.Abilities.Count==0,"real group constructor creates empty owned list");
                var original=group.Abilities;group.Abilities.Add(new CharacterAbilityTypeGroup.AbilityTypeDefinition());Check(ReferenceEquals(group.Abilities,original) && group.Abilities.Count==1,"real engine group retains live list");
                string json=JsonUtility.ToJson(group);Check(json.Contains("m_abilities") && json.Contains("m_gracePeriod"),"original private serialized group fields emitted");
                JsonUtility.FromJsonOverwrite("{\"m_abilities\":[{\"m_type\":123,\"m_gracePeriod\":-0.25}]}",group);
                Check(group.Abilities.Count==1 && (int)group.Abilities[0].Type==123 && group.Abilities[0].GracePeriod==-0.25f,"actual group Json fields bind without facade");
                JsonUtility.FromJsonOverwrite("{\"AllowKnockback\":false,\"CharacterInvulnerableFormType\":321,\"BoostCooldownDurationSeconds\":-2,\"CollectableCost\":{\"FixedChange\":-7,\"FractionChange\":0.25,\"Metadata\":{\"Source\":123}}}",hazard);
                Check(!hazard.AllowKnockback && (int)hazard.CharacterInvulnerableFormType==321 && hazard.BoostCooldownDurationSeconds==-2f,"actual hazard Json preserves authored override values");
                Check(hazard.CollectableCost.FixedChange==-7 && hazard.CollectableCost.FractionChange==0.25f && (int)hazard.CollectableCost.Metadata.Source==123 && hazard.CollectableCost.HasChange(),"actual nested collectable struct serialization");
                var hover=JsonUtility.FromJson<HoverData>("{\"TargetHeight\":-4,\"OscillateHeightBounds\":0.25,\"MoveSpeed\":2,\"SmoothTimeSeconds\":0.5}");
                Check(hover.TargetHeight==-4f && hover.OscillateHeightBounds==0.25f && hover.MoveSpeed==2f && hover.SmoothTimeSeconds==0.5f && hover.Origin==null && hover.DistanceSpeedCurve==null,"actual hover field deserialization");
            }
            finally
            {
                try {if(hazard!=null)UnityEngine.Object.DestroyImmediate(hazard);}
                finally {if(group!=null)UnityEngine.Object.DestroyImmediate(group);}
            }
            return checks-before;
        }
        public static int Run() { int managed=RunManaged();return managed+EngineChecks(); }
    }
}
