using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Original definitions and inspector predicates only. Raw managed objects are
    // restricted to field/accessor checks; engine lifecycle and Unity fake-null
    // behavior are exercised separately with owned genuine ScriptableObjects.
    public static class CharacterCameraDefinitionsVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static int checks;
        private static void Check(bool value,string message) { if(!value)throw new InvalidOperationException(message);checks++; }
        private static FieldInfo Field(Type t,string name) => t.GetField(name,Own) ?? throw new InvalidOperationException(name);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value),0);
        private static object Get(object value,string name) => Field(value.GetType(),name).GetValue(value);
        private static void Put(object value,string name,object fieldValue) => Field(value.GetType(),name).SetValue(value,fieldValue);
        private static float SingleRadians(float degrees) => BitConverter.ToSingle(BitConverter.GetBytes(degrees*Mathf.Deg2Rad),0);
        private static bool CosineEquals(float actual,float degrees)
        {
            float expected=(float)Math.Cos((double)SingleRadians(degrees));
            return float.IsNaN(expected)?float.IsNaN(actual):Math.Abs(actual-expected)<=0.000001f;
        }
        private static void Invoke(object value,string method) => value.GetType().GetMethod(method,Own).Invoke(value,null);
        public static int RunManaged()
        {
            checks=0;
            var heading=new CameraHeadingOverride();
            Check(ReferenceEquals(heading.Target,null) && heading.Settings==null,"original heading references remain null");
            var a=new CameraHeadingOverride.CameraHeadingOverrideSettings();
            var b=new CameraHeadingOverride.CameraHeadingOverrideSettings();
            Check(a.UseTargetAsXAxisOverride && b.UseTargetAsXAxisOverride,"genuine heading x override default");
            Check(Bits(a.XAxisRecenteringTime)==Bits(.5f) && Bits(a.YAxisValue)==Bits(.5f) && Bits(a.YAxisSpeed)==Bits(.5f),"three original half defaults");
            Check(!a.OverrideXAxisRecenteringTime && !a.OverrideYAxisValue && !a.RecenterToCameraTargetOnCameraInput && !a.RequireMinimumAngle && !a.OverrideTargetingHoming,"unwritten heading flags remain false");
            Check(Bits(a.ResetAfterCameraInputTimeSeconds)==0 && Bits(a.MaximumAngleThreshold)==0 && Bits(a.MaximumAngleThresholdCosine)==0,"original scalar/cache zero before update");
            a.XAxisRecenteringTime=-3f;a.YAxisValue=float.NaN;a.YAxisSpeed=float.PositiveInfinity;a.ResetAfterCameraInputTimeSeconds=-0f;a.MaximumAngleThreshold=float.NegativeInfinity;
            Check(a.XAxisRecenteringTime==-3f && float.IsNaN(a.YAxisValue) && float.IsPositiveInfinity(a.YAxisSpeed) && Bits(a.ResetAfterCameraInputTimeSeconds)==Bits(-0f) && float.IsNegativeInfinity(a.MaximumAngleThreshold),"settings retain raw authored scalars without clamps");
            Check(b.XAxisRecenteringTime==.5f && b.YAxisValue==.5f && b.MaximumAngleThreshold==0f,"settings instances have independent fields");
            heading.Settings=a;Check(ReferenceEquals(heading.Settings,a),"outer retains actual settings reference");
            var raw=(CameraRecenterHeadingDefinition_FreeLook)FormatterServices.GetUninitializedObject(typeof(CameraRecenterHeadingDefinition_FreeLook));
            Type derived=typeof(CameraRecenterHeadingDefinition_FreeLook);Type parent=typeof(CameraRecenterHeadingDefinition);
            foreach(float v in new[]{-3f,-0f,0f,.1f,1f,90f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            {
                foreach(var pair in new[]{new[]{"m_recenterToHeadingReEnableAngle","RecenterToHeadingReEnableAngle"},new[]{"m_recenterToHeadingDisableAngle","RecenterToHeadingDisableAngle"},new[]{"m_minimumRequiredVelocityForRecenter","MinimumRequiredVelocityForRecenter"},new[]{"m_snapRecenterTimeSeconds","SnapRecenterTimeSeconds"},new[]{"m_recenterTimeSmoothingValue","RecenterTimeSmoothingValue"},new[]{"m_stoppedInputDisableRecenterSeconds","StoppedInputDisableRecenterSeconds"},new[]{"<StoppedInputDisableRecenterCosineAngleThreshold>k__BackingField","StoppedInputDisableRecenterCosineAngleThreshold"},new[]{"<VelocityToTargetUpCosine>k__BackingField","VelocityToTargetUpCosine"}})
                {
                    Field(derived,pair[0]).SetValue(raw,v);
                    Check(Bits((float)derived.GetProperty(pair[1],Own).GetValue(raw))==Bits(v),"original direct FreeLook getter retains exact binary32 "+pair[1]);
                }
                Field(parent,"m_timeoutSeconds").SetValue(raw,v);Field(parent,"m_forwardAngleExitSnapThreshold").SetValue(raw,v);
                Check(Bits(raw.TimeoutSeconds)==Bits(v) && Bits(raw.ForwardAngleExitSnapThreshold)==Bits(v),"base getter values without normalization");
            }
            foreach(int value in new[]{-1,0,1,2,3,int.MinValue,int.MaxValue})
            {
                Put(raw,"m_recenterMode",(CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)value);
                Put(raw,"m_headingDefinitionType",(CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition)value);
                Check((int)raw.RecenterMode==value && (int)raw.HeadingDefinitionType==value,"authored enum values are not restricted to known literals");
            }
            Field(parent,"m_clearStickyControlsOnSnapEnd").SetValue(raw,true);Check(raw.ClearStickyControlsOnSnapEnd,"base boolean direct getter true");Field(parent,"m_clearStickyControlsOnSnapEnd").SetValue(raw,false);Check(!raw.ClearStickyControlsOnSnapEnd,"base boolean direct getter false");
            Check(raw.VelocityToRecenteringTimeCurve==null && raw.AngleRecenteringTimeMultiplierCurve==null && raw.AngleMultiplierLimitByVelocityCurve==null,"raw curve getters do not allocate/fabricate curves");
            object marker=new object();object[] values={marker,null,123};string[] names={"first",null,"last"};
            foreach(var comparison in new[]{InspectorConditionalAttribute.ComparisonType.All,InspectorConditionalAttribute.ComparisonType.Any})
            {
                var hide=new HideIfAttribute(comparison,values,names);
                Check(hide.Comparison==comparison && hide.ConditionalFields.Length==9,"HideIf exact array overload/base Cartesian size");
                for(int f=0;f<names.Length;f++)for(int v=0;v<values.Length;v++)Check(hide.ConditionalFields[f*values.Length+v].FieldName==names[f] && ReferenceEquals(hide.ConditionalFields[f*values.Length+v].ComparisonValue,values[v]),"HideIf field-outer value-inner order and same objects");
                var single=new HideIfAttribute(comparison,marker,names);
                Check(single.Comparison==comparison && single.ConditionalFields.Length==3,"HideIf exact scalar overload/base size");
                for(int f=0;f<names.Length;f++)Check(single.ConditionalFields[f].FieldName==names[f] && ReferenceEquals(single.ConditionalFields[f].ComparisonValue,marker),"HideIf single value identity on each named field");
                Check(hide.ShouldShow(false) && !hide.ShouldShow(true),"original HideIf Boolean negation");
                Check(hide.ShouldEnable(false) && hide.ShouldEnable(true),"original HideIf enable returns true in both states");
            }
            var defaultHide=new HideIfAttribute(null);Check(defaultHide.Comparison==InspectorConditionalAttribute.ComparisonType.All && defaultHide.ConditionalFields.Length==1 && defaultHide.ConditionalFields[0].FieldName==null && defaultHide.ConditionalFields[0].ComparisonValue==null,"genuine single-field default/null arguments");
            Check(new HideIfAttribute(InspectorConditionalAttribute.ComparisonType.Any,Array.Empty<object>(),names).ConditionalFields.Length==0,"HideIf empty value Cartesian product");
            Check(new HideIfAttribute(InspectorConditionalAttribute.ComparisonType.All,values,Array.Empty<string>()).ConditionalFields.Length==0,"HideIf empty fields Cartesian product");
            var enumHide=(HideIfAttribute)Field(derived,"m_headingDefinitionType").GetCustomAttribute(typeof(HideIfAttribute));
            Check(enumHide.Comparison==InspectorConditionalAttribute.ComparisonType.All && enumHide.ConditionalFields.Length==1 && enumHide.ConditionalFields[0].FieldName=="m_recenterMode" && enumHide.ConditionalFields[0].ComparisonValue.GetType()==typeof(CameraRecenterHeadingDefinition_FreeLook.RecenteringMode) && (int)(CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)enumHide.ConditionalFields[0].ComparisonValue==2,"real typed boxed enum HideIf argument");
            var scalarHide=(HideIfAttribute)Field(typeof(CameraHeadingOverride.CameraHeadingOverrideSettings),"RecenterToCameraTargetOnCameraInput").GetCustomAttribute(typeof(HideIfAttribute));
            Check(scalarHide.ConditionalFields[0].ComparisonValue.GetType()==typeof(float) && Bits((float)scalarHide.ConditionalFields[0].ComparisonValue)==0,"real boxed Single zero HideIf argument");
            var nullShow=(ShowIfAttribute)Field(typeof(CameraHeadingOverride.CameraHeadingOverrideSettings),"OverrideXAxisRecenteringTime").GetCustomAttribute(typeof(ShowIfAttribute));
            Check(nullShow.ConditionalFields[0].ComparisonValue==null && nullShow.ConditionalFields[0].FieldName=="UseTargetAsXAxisOverride","real typed String-null ShowIf argument");
            var overrides=(CameraRecenterHeadingOverrides)FormatterServices.GetUninitializedObject(typeof(CameraRecenterHeadingOverrides));
            var dictionary=new SerializableDictionary<CameraRecenterHeadingType,CameraRecenterHeadingDefinition>(HardlightProject.HardlightEnumComparers.CameraRecenterHeadingTypeComparer);Put(overrides,"m_overrides",dictionary);
            Check(ReferenceEquals(((Dictionary<CameraRecenterHeadingType,CameraRecenterHeadingDefinition>)typeof(SerializableDictionary<CameraRecenterHeadingType,CameraRecenterHeadingDefinition>).BaseType.GetField("m_dictionary",Own).GetValue(dictionary)).Comparer,HardlightProject.HardlightEnumComparers.CameraRecenterHeadingTypeComparer),"genuine maintained original comparer boundary");
            foreach(int key in new[]{0,1,-1,123,int.MinValue,int.MaxValue})
            {
                CameraRecenterHeadingDefinition_FreeLook output=raw;
                Check(!overrides.TryGetValue((CameraRecenterHeadingType)key,out output) && ReferenceEquals(output,null),"missing key clears preexisting typed out before returning false");
                dictionary[(CameraRecenterHeadingType)key]=null;output=raw;
                Check(!overrides.TryGetValue((CameraRecenterHeadingType)key,out output) && ReferenceEquals(output,null),"present null clears out and returns false via genuine Unity null/null operator");
                dictionary.Remove((CameraRecenterHeadingType)key);
            }
            Put(overrides,"m_overrides",null);CameraRecenterHeadingDefinition_FreeLook cleared=raw;bool threw=false;
            try {overrides.TryGetValue((CameraRecenterHeadingType)1,out cleared);}catch(NullReferenceException){threw=true;}
            Check(threw && ReferenceEquals(cleared,null),"compiled CLR-null dictionary failure occurs only after clearing out; no optimized native exception-parity claim");
            return checks;
        }
        private static int EngineChecks()
        {
            int before=checks;CameraRecenterHeadingDefinition_FreeLook value=null;CameraRecenterHeadingOverrides overrides=null;
            try
            {
                value=ScriptableObject.CreateInstance<CameraRecenterHeadingDefinition_FreeLook>();
                overrides=ScriptableObject.CreateInstance<CameraRecenterHeadingOverrides>();
                Check(value.TimeoutSeconds==3f && value.ForwardAngleExitSnapThreshold==0f && !value.ClearStickyControlsOnSnapEnd,"real original base constructor defaults");
                Check(value.RecenterMode==CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Default && value.RecenterToHeadingReEnableAngle==90f && value.RecenterToHeadingDisableAngle==100f,"real packed native FreeLook mode/angle defaults");
                Check(value.MinimumRequiredVelocityForRecenter==1f && value.SnapRecenterTimeSeconds==.1f && value.RecenterTimeSmoothingValue==0f && value.StoppedInputDisableRecenterSeconds==0f,"real scalar constructor defaults");
                Check((int)value.HeadingDefinitionType==0 && value.VelocityToRecenteringTimeCurve==null && value.AngleRecenteringTimeMultiplierCurve==null && value.AngleMultiplierLimitByVelocityCurve==null,"real heading/default curve fields");
                Check((float)Get(value,"m_stoppedInputDisableRecenterAngleThreshold")==45f && (float)Get(value,"m_angleFromVelocityToTargetUp")==5f,"second genuine packed pair of constructor defaults");
                Invoke(value,"Awake");Check(CosineEquals(value.StoppedInputDisableRecenterCosineAngleThreshold,45f) && CosineEquals(value.VelocityToTargetUpCosine,5f),"actual original Awake cached cosines");
                var settings=new CameraHeadingOverride.CameraHeadingOverrideSettings();var heading=new CameraHeadingOverride{Settings=settings};
                foreach(float angle in new[]{0f,45f,90f,180f,-90f,360f,10000000f,float.NaN,float.PositiveInfinity})
                {
                    settings.MaximumAngleThreshold=angle;heading.UpdateCachedValues();Check(CosineEquals(settings.MaximumAngleThresholdCosine,angle),"actual heading delegated cosine with binary32 radians");
                    Put(value,"m_stoppedInputDisableRecenterAngleThreshold",angle);Put(value,"m_angleFromVelocityToTargetUp",angle);Invoke(value,"OnValidate");Check(CosineEquals(value.StoppedInputDisableRecenterCosineAngleThreshold,angle) && CosineEquals(value.VelocityToTargetUpCosine,angle),"actual original OnValidate refreshes both caches without new clamp");
                }
                Put(value,"m_stoppedInputDisableRecenterAngleThreshold",45f);Put(value,"m_angleFromVelocityToTargetUp",5f);
                Put(value,"m_recenterToHeadingReEnableAngle",float.NaN);Invoke(value,"Awake");Check(CosineEquals(value.StoppedInputDisableRecenterCosineAngleThreshold,45f) && CosineEquals(value.VelocityToTargetUpCosine,5f),"actual default-mode unordered re-enable angle skips diagnostic then refreshes caches");
                Put(value,"m_recenterToHeadingReEnableAngle",90f);Put(value,"m_recenterToHeadingDisableAngle",float.NaN);Invoke(value,"OnValidate");Check(CosineEquals(value.StoppedInputDisableRecenterCosineAngleThreshold,45f) && CosineEquals(value.VelocityToTargetUpCosine,5f),"actual default-mode unordered disable angle skips diagnostic then refreshes caches");
                Put(value,"m_recenterMode",CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never);Put(value,"m_recenterToHeadingReEnableAngle",float.NaN);Invoke(value,"Awake");Check(float.IsNaN(value.RecenterToHeadingReEnableAngle),"Never mode preserves invalid angle without evaluating default-mode diagnostic");
                Put(value,"m_recenterToHeadingReEnableAngle",90f);Put(value,"m_recenterToHeadingDisableAngle",100f);
                var authoredSettings=JsonUtility.FromJson<CameraHeadingOverride.CameraHeadingOverrideSettings>("{\"UseTargetAsXAxisOverride\":false,\"OverrideXAxisRecenteringTime\":true,\"XAxisRecenteringTime\":-2,\"OverrideYAxisValue\":true,\"YAxisValue\":0.25,\"YAxisSpeed\":-3,\"RequireMinimumAngle\":true,\"MaximumAngleThreshold\":75}");
                Check(!authoredSettings.UseTargetAsXAxisOverride && authoredSettings.OverrideXAxisRecenteringTime && authoredSettings.XAxisRecenteringTime==-2f && authoredSettings.OverrideYAxisValue && authoredSettings.YAxisValue==.25f && authoredSettings.YAxisSpeed==-3f && authoredSettings.RequireMinimumAngle && authoredSettings.MaximumAngleThreshold==75f,"actual Json binds heading settings public authored fields");
                string settingsJson=JsonUtility.ToJson(authoredSettings);Check(settingsJson.Contains("MaximumAngleThreshold") && !settingsJson.Contains("MaximumAngleThresholdCosine"),"actual authored heading angle serializes while private cached backing field does not");
                string json=JsonUtility.ToJson(value);Check(json.Contains("m_timeoutSeconds") && json.Contains("m_recenterMode") && json.Contains("m_headingDefinitionType"),"actual inherited and own serialized field names");
                JsonUtility.FromJsonOverwrite("{\"m_timeoutSeconds\":7,\"m_forwardAngleExitSnapThreshold\":-2,\"m_clearStickyControlsOnSnapEnd\":true,\"m_recenterMode\":1,\"m_recenterToHeadingReEnableAngle\":25,\"m_recenterToHeadingDisableAngle\":30,\"m_headingDefinitionType\":2}",value);
                Check(value.TimeoutSeconds==7f && value.ForwardAngleExitSnapThreshold==-2f && value.ClearStickyControlsOnSnapEnd && (int)value.RecenterMode==1 && value.RecenterToHeadingReEnableAngle==25f && value.RecenterToHeadingDisableAngle==30f && (int)value.HeadingDefinitionType==2,"actual Json binds full original base/derived fields");
                var dictionary=(SerializableDictionary<CameraRecenterHeadingType,CameraRecenterHeadingDefinition>)Get(overrides,"m_overrides");
                Check(dictionary!=null && dictionary.Count==0 && ReferenceEquals(((Dictionary<CameraRecenterHeadingType,CameraRecenterHeadingDefinition>)typeof(SerializableDictionary<CameraRecenterHeadingType,CameraRecenterHeadingDefinition>).BaseType.GetField("m_dictionary",Own).GetValue(dictionary)).Comparer,HardlightProject.HardlightEnumComparers.CameraRecenterHeadingTypeComparer),"real original dictionary comparer initializer");
                dictionary[(CameraRecenterHeadingType)123]=value;
                Check(overrides.TryGetValue((CameraRecenterHeadingType)123,out CameraRecenterHeadingDefinition_FreeLook output) && ReferenceEquals(output,value),"genuine positive typed dictionary lookup");
                Check(overrides.TryGetValue((CameraRecenterHeadingType)123,out CameraRecenterHeadingDefinition baseOutput) && ReferenceEquals(baseOutput,value),"genuine original base generic lookup");
                UnityEngine.Object.DestroyImmediate(value);Check(!overrides.TryGetValue((CameraRecenterHeadingType)123,out output) && !ReferenceEquals(output,null) && output==null,"destroyed original object retained in out yet false Unity fake-null result");value=null;
                dictionary[(CameraRecenterHeadingType)123]=null;Check(!overrides.TryGetValue((CameraRecenterHeadingType)123,out output) && ReferenceEquals(output,null),"real present null lookup");
            }
            finally
            {
                try {if(!ReferenceEquals(value,null))UnityEngine.Object.DestroyImmediate(value);}
                finally {if(!ReferenceEquals(overrides,null))UnityEngine.Object.DestroyImmediate(overrides);}
            }
            return checks-before;
        }
        public static int Run() { int managed=RunManaged();return managed+EngineChecks(); }
    }
}
