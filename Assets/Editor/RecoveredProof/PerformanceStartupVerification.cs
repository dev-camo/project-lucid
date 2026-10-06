using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class PerformanceStartupVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Original performance startup verification: " + label); checks++; }
        private static FieldInfo Field(Type t, string name) => t.GetField(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly);
        private static void Set(object value, string name, object data) { Field(value.GetType(),name).SetValue(value,data); }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static void Throws<T>(Action action, string label) where T:Exception { bool thrown=false;try { action(); }catch(T){thrown=true;} Check(thrown,label); }
        private static void Validate(PerformanceProfile p) { try { typeof(PerformanceProfile).GetMethod("OnValidate",BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Invoke(p,null); }catch(TargetInvocationException e){throw e.InnerException;} }
        private static ScalableFeature Feature(string id) { var f=Raw<ScalableFeature>();f.ID=id;return f; }
        private static PerformanceProfile Profile(params PerformanceAttribute[] values) { var p=Raw<PerformanceProfile>();Set(p,"m_attributes",values==null?null:new List<PerformanceAttribute>(values));return p; }
        private static PerformanceAttribute Attribute(ScalableFeature f, int level) => new PerformanceAttribute { Feature=f,Level=(PerformanceProfile.QualityLevel)level };
        private static RuntimeIssueConfiguration Configuration(float threshold,bool maximum,string path=null) {var c=new RuntimeIssueConfiguration();Set(c,"m_path",path);Set(c,"m_value",threshold);Set(c,"m_isMaxThreshold",maximum);return c;}

        private static void Declarations()
        {
            var rows=new[]{
                (Type:typeof(PerformanceAttribute),Names:new[]{"Feature","Level"}),
                (Type:typeof(PerformanceProfile),Names:new[]{"m_attributes","OnProfileUpdated"}),
                (Type:typeof(ScalableFeature),Names:new[]{"ID"}),
                (Type:typeof(DevicePerformance),Names:new[]{"m_platform","m_fallback"}),
                (Type:typeof(RuntimeIssueAttribute),Names:new[]{"m_level","m_performanceAttribute","m_configurations"}),
                (Type:typeof(RuntimeIssueConfiguration),Names:new[]{"BytesPerMegabyte","m_path","m_value","m_isMaxThreshold"}),
                (Type:typeof(RuntimeIssueProfile),Names:new[]{"m_attributes"})};
            foreach(var r in rows)
            {
                Check(r.Type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(r.Names),r.Type.Name+" complete original ordered fields");
                Check((r.Type.Attributes&TypeAttributes.BeforeFieldInit)!=0,r.Type.Name+" original BeforeFieldInit");
                Check(r.Type.GetInterfaces().Length==0,r.Type.Name+" no invented contracts");
            }
            Check(typeof(PerformanceProfile).Assembly.GetName().Name=="HLUnityCore.Runtime" && typeof(DevicePerformance).Assembly.GetName().Name=="Game.Runtime","genuine two assembly identities");
            foreach(Type t in new[]{typeof(PerformanceProfile),typeof(RuntimeIssueProfile)}) Check(t.BaseType==typeof(ScriptableObjectWithGuid),t.Name+" genuine GUID base");
            foreach(Type t in new[]{typeof(ScalableFeature),typeof(DevicePerformance)}) Check(t.BaseType==typeof(ScriptableObject),t.Name+" genuine ScriptableObject base");
            foreach(Type t in new[]{typeof(PerformanceAttribute),typeof(RuntimeIssueAttribute),typeof(RuntimeIssueConfiguration)}) Check(t.IsSerializable,t.Name+" original Serializable");
            foreach(Type t in new[]{typeof(PerformanceProfile),typeof(DevicePerformance),typeof(RuntimeIssueAttribute),typeof(RuntimeIssueConfiguration),typeof(RuntimeIssueProfile)})
            {
                var attrs=t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Check(attrs.Select(a=>a.Option).SequenceEqual(new[]{Option.NullChecks,Option.ArrayBoundsChecks})&&attrs.All(a=>Equals(a.Value,false)),t.Name+" ordered original IL2CPP options");
            }
            Check(typeof(PerformanceAttribute).GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a=>a.Option).SequenceEqual(new[]{Option.ArrayBoundsChecks,Option.NullChecks}),"PerformanceAttribute reverse original options");
            Check(!typeof(ScalableFeature).IsDefined(typeof(Il2CppSetOptionAttribute),false),"ScalableFeature has no invented original IL2CPP options");
            Check(Field(typeof(PerformanceProfile),"m_attributes").IsFamily && Field(typeof(PerformanceProfile),"m_attributes").IsDefined(typeof(SerializeField),false),"protected serialized profile attributes");
            Check(Field(typeof(DevicePerformance),"m_platform").IsPrivate && !Field(typeof(DevicePerformance),"m_platform").IsDefined(typeof(SerializeField),false),"original private platform remains nonserialized");
            Check(Field(typeof(DevicePerformance),"m_fallback").IsFamily && Field(typeof(DevicePerformance),"m_fallback").IsDefined(typeof(SerializeField),false),"original protected serialized fallback");
            Check(typeof(DevicePerformance).GetMethod("GetMatch").IsVirtual && typeof(DevicePerformance).GetProperty("Platform").GetMethod.IsVirtual,"original virtual base matching APIs");
            Check(typeof(RuntimeIssueAttribute).GetProperty("Configurations").PropertyType==typeof(IReadOnlyCollection<RuntimeIssueConfiguration>),"original read-only collection API wraps same underlying list");
            Check(Field(typeof(RuntimeIssueConfiguration),"BytesPerMegabyte").IsLiteral && Equals(Field(typeof(RuntimeIssueConfiguration),"BytesPerMegabyte").GetRawConstantValue(),1048576f),"original Single megabyte constant");
            var levels=new[]{-200,-100,0,100,200};Check(Enum.GetValues(typeof(PerformanceProfile.QualityLevel)).Cast<PerformanceProfile.QualityLevel>().Select(x=>(int)x).OrderBy(x=>x).SequenceEqual(levels),"all signed original quality levels");
            var nested=typeof(PerformanceProfile).GetNestedTypes(BindingFlags.NonPublic).OrderBy(t=>t.Name).ToArray();
            Check(nested.Select(t=>t.Name).SequenceEqual(new[]{"<>c__DisplayClass5_0","<>c__DisplayClass6_0","<>c__DisplayClass7_0","<>c__DisplayClass8_0"}),"four natural compiler identities without padding");
            foreach(var t in nested) Check(t.IsSealed && t.GetFields(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Length==1,t.Name+" complete original capture field");
            var ev=typeof(PerformanceProfile).GetEvent("OnProfileUpdated");Check(ev!=null&&ev.EventHandlerType==typeof(Action)&&ev.AddMethod.IsPublic&&ev.RemoveMethod.IsPublic,"genuine public Action event");
            var menu=typeof(PerformanceProfile).GetCustomAttribute<CreateAssetMenuAttribute>();Check(menu.fileName=="NewPerformanceProfile"&&menu.menuName=="Hardlight/HLUnityCore/ScalablePerformance/PerformanceProfile","original profile authoring menu");
            menu=typeof(ScalableFeature).GetCustomAttribute<CreateAssetMenuAttribute>();Check(menu.fileName=="NewScalableFeature"&&menu.menuName=="Hardlight/HLUnityCore/ScalablePerformance/ScalableFeature","original scalable-feature authoring menu");
            menu=typeof(RuntimeIssueProfile).GetCustomAttribute<CreateAssetMenuAttribute>();Check(menu.fileName=="RuntimeIssueProfile"&&menu.menuName=="HardlightProject/Config/RuntimeIssueProfile","original runtime-issue authoring menu");
        }

        private static void Matching()
        {
            var feature=Feature("render");var equivalent=Feature("render");var different=Feature("Render");
            var first=Attribute(feature,100);var later=Attribute(equivalent,200);var distinct=Attribute(different,-100);var p=Profile(first,later,distinct);
            Check(ReferenceEquals(p.GetAttribute("render"),first),"first duplicate string-ID match wins");
            Check(ReferenceEquals(p.GetAttribute(equivalent),first),"different feature objects match by ID");
            Check(ReferenceEquals(p.GetAttribute("Render"),distinct),"IDs retain ordinal case sensitivity");
            Check(p.GetAttribute("absent")==null,"missing string returns null");
            Check(p.GetAttribute(Feature("absent"))==null,"missing feature ID returns null");
            var required=Attribute(equivalent,100);Check(p.IsSupported(required)&&p.IsExactlySupported(required),"first duplicate quality satisfies exact required quality");
            required.Level=PerformanceProfile.QualityLevel.VeryHigh;Check(!p.IsSupported(required)&&!p.IsExactlySupported(required),"later high duplicate does not override first low match");
            required.Level=PerformanceProfile.QualityLevel.Low;Check(p.IsSupported(required)&&!p.IsExactlySupported(required),"first level supports lower required signed quality");
            int[] levels={-200,-100,0,100,200};string[] support={"10000","11000","11100","11110","11111"};
            for(int row=0;row<levels.Length;row++)
            {
                first.Level=(PerformanceProfile.QualityLevel)levels[row];
                for(int col=0;col<levels.Length;col++)
                {
                    required.Level=(PerformanceProfile.QualityLevel)levels[col];
                    Check(p.IsSupported(required)==(support[row][col]=='1'),"native signed quality matrix "+row+":"+col);
                    Check(p.IsExactlySupported(required)==(row==col),"native exact-quality matrix "+row+":"+col);
                }
            }
            var missing=Profile();string missingSupport="00011",missingExact="00100";
            for(int i=0;i<levels.Length;i++)
            {
                required.Level=(PerformanceProfile.QualityLevel)levels[i];
                Check(missing.IsSupported(required)==(missingSupport[i]=='1'),"missing feature native support quirk "+levels[i]);
                Check(missing.IsExactlySupported(required)==(missingExact[i]=='1'),"missing feature exact Medium default "+levels[i]);
            }
            first.Level=(PerformanceProfile.QualityLevel)int.MinValue;required.Level=(PerformanceProfile.QualityLevel)int.MaxValue;
            Check(!p.IsSupported(required),"quality signed extreme minimum does not support maximum");
            first.Level=(PerformanceProfile.QualityLevel)int.MaxValue;required.Level=(PerformanceProfile.QualityLevel)int.MinValue;
            Check(p.IsSupported(required),"quality signed extreme maximum supports minimum");
            var nullId=Attribute(Feature(null),0);var nullProfile=Profile(nullId);
            Check(ReferenceEquals(nullProfile.GetAttribute((string)null),nullId)&&ReferenceEquals(nullProfile.GetAttribute(Feature(null)),nullId),"null ID strings match without normalization");
            Check(nullProfile.IsExactlySupported(Attribute(Feature(null),0)),"null ID exact profile matching");
            Check(missing.GetAttribute((ScalableFeature)null)==null,"empty list never dereferences missing captured feature");
            Throws<NullReferenceException>(()=>p.GetAttribute((ScalableFeature)null),"populated feature lookup reaches genuine null capture dereference");
            Throws<NullReferenceException>(()=>Profile((PerformanceAttribute)null).GetAttribute("x"),"null list element is not skipped");
            Throws<NullReferenceException>(()=>Profile(Attribute(null,0)).GetAttribute("x"),"null stored feature is not skipped");
            Throws<NullReferenceException>(()=>p.IsSupported(null),"missing required object remains invalid");
            Throws<NullReferenceException>(()=>missing.IsSupported(null),"empty list still reads missing required quality");
            Throws<NullReferenceException>(()=>Profile((PerformanceAttribute[])null).GetAttribute("x"),"null authored list is not initialized by lookup");
            Check(ReferenceEquals(((List<PerformanceAttribute>)Field(typeof(PerformanceProfile),"m_attributes").GetValue(p))[0],first),"queries retain authored list and original attributes");
        }

        private static void Notifications()
        {
            var p=Profile();var trace=new List<string>();Action late=()=>trace.Add("late"),second=()=>trace.Add("second");
            Action first=()=>{trace.Add("first");p.OnProfileUpdated-=second;p.OnProfileUpdated+=late;};
            p.OnProfileUpdated+=first;p.OnProfileUpdated+=second;Validate(p);Check(string.Join(",",trace)=="first,second","validation invokes captured multicast snapshot despite subscription mutation");
            trace.Clear();Validate(p);Check(string.Join(",",trace)=="first,late","next validation observes prior subscription changes");
            p.OnProfileUpdated-=first;p.OnProfileUpdated-=late;p.OnProfileUpdated-=late;trace.Clear();Validate(p);Check(trace.Count==0,"remove duplicates leaves genuinely empty event");
            Action duplicate=()=>trace.Add("duplicate");p.OnProfileUpdated+=duplicate;p.OnProfileUpdated+=duplicate;p.OnProfileUpdated-=duplicate;Validate(p);Check(trace.Count==1,"remove deletes one last matching delegate");p.OnProfileUpdated-=duplicate;
            var marker=new InvalidOperationException("original notification fault");Action broken=()=>{throw marker;};p.OnProfileUpdated+=broken;p.OnProfileUpdated+=second;
            try{Validate(p);throw new Exception("expected notification fault");}catch(InvalidOperationException e){Check(ReferenceEquals(e,marker),"original observer exception propagates without wrapper or swallowing");}
            Check(trace.Count==1,"later observer is skipped after throw");p.OnProfileUpdated-=broken;p.OnProfileUpdated-=second;
            int calls=0;Action[] handlers=Enumerable.Range(0,8).Select(_=>(Action)(()=>Interlocked.Increment(ref calls))).ToArray();
            var threads=handlers.Select(h=>new Thread(()=>p.OnProfileUpdated+=h)).ToArray();foreach(var t in threads)t.Start();foreach(var t in threads)t.Join();Validate(p);Check(calls==8,"genuine event compare-exchange retains concurrent subscriptions");
            threads=handlers.Select(h=>new Thread(()=>p.OnProfileUpdated-=h)).ToArray();foreach(var t in threads)t.Start();foreach(var t in threads)t.Join();Validate(p);Check(calls==8,"genuine event compare-exchange removes concurrent subscriptions");
            Check(p.GetGUID()==null,"override notification does not initialize genuine GUID-base data");
        }

        private static void Thresholds()
        {
            var c=new RuntimeIssueConfiguration();Check(c.Path==null&&c.Value==0,"configuration base-only constructor leaves original defaults");
            var a=new RuntimeIssueAttribute();Check(a.QualityLevel==0&&a.PerformanceAttribute==null&&a.Configurations==null,"runtime attribute constructor leaves original list null");
            var pa=new PerformanceAttribute();Check(pa.Feature==null&&pa.Level==0,"performance attribute constructor leaves original defaults");
            var list=new List<RuntimeIssueConfiguration>{c};Set(a,"m_configurations",list);Set(a,"m_level",PerformanceProfile.QualityLevel.Low);Set(a,"m_performanceAttribute",pa);
            Check(ReferenceEquals(a.Configurations,list)&&ReferenceEquals(a.PerformanceAttribute,pa)&&a.QualityLevel==PerformanceProfile.QualityLevel.Low,"original attribute getters expose retained fields");
            list.Add(Configuration(7,false));Check(a.Configurations.Count==2,"read-only collection is live authored list");
            float[] values={float.NegativeInfinity,-1f,0f,1f,float.PositiveInfinity,float.NaN};string min="001110",max="111000";
            foreach(bool maximum in new[]{false,true})
            {
                c=Configuration(0,maximum,"runtime:path");Check(c.Path=="runtime:path"&&c.Value==0,"direct path/threshold getters");
                for(int i=0;i<values.Length;i++)Check(c.IsValid(values[i])==((maximum?max:min)[i]=='1'),"inclusive ordered threshold "+maximum+":"+i);
                Set(c,"m_value",float.NaN);foreach(float value in values)Check(!c.IsValid(value),"NaN threshold does not qualify "+maximum+":"+value);
            }
            c=Configuration(1,false);Check(c.IsValidBytes(1048576f),"byte minimum includes exactly one binary megabyte");Check(!c.IsValidBytes(1048575f),"byte minimum rejects preceding byte");Check(c.IsValidBytes(1048577f),"byte minimum accepts following byte");
            c=Configuration(1,true);Check(c.IsValidBytes(1048576f),"byte maximum includes exact threshold");Check(c.IsValidBytes(1048575f),"byte maximum accepts preceding byte");Check(!c.IsValidBytes(1048577f),"byte maximum rejects following byte");
            Check(!c.IsValidBytes(float.NaN),"byte NaN fails ordered comparison");Check(c.IsValidBytes(float.NegativeInfinity)&&!c.IsValidBytes(float.PositiveInfinity),"byte infinities retain ordered native thresholds");
            c=Configuration(-1,false);Check(c.IsValidBytes(-1048576f)&&!c.IsValidBytes(-1048577f),"negative byte threshold retains signed boundary");
            c=Configuration(float.Epsilon,true);Check(c.IsValidBytes(float.Epsilon),"native reciprocal scaling underflows subnormal to zero");
            var device=Raw<DevicePerformance>();var fallback=Profile();Set(device,"m_platform",RuntimePlatform.LinuxPlayer);Set(device,"m_fallback",fallback);
            Check(device.Platform==RuntimePlatform.LinuxPlayer&&ReferenceEquals(device.GetMatch(),fallback),"base device returns original platform and fallback without hardware lookup");Set(device,"m_fallback",null);Check(device.GetMatch()==null,"null configured fallback remains null");
        }
        public static int RunManaged(){checks=0;Declarations();Matching();Notifications();Thresholds();return checks;}

        public static int RunEngine()
        {
            checks=0;ScalableFeature first=null,equal=null;PerformanceProfile profile=null;RuntimeIssueProfile issues=null;DevicePerformance device=null;
            try
            {
                first=ScriptableObject.CreateInstance<ScalableFeature>();equal=ScriptableObject.CreateInstance<ScalableFeature>();profile=ScriptableObject.CreateInstance<PerformanceProfile>();issues=ScriptableObject.CreateInstance<RuntimeIssueProfile>();device=ScriptableObject.CreateInstance<DevicePerformance>();
                Check(first.ID==null&&device.Platform==0&&device.GetMatch()==null,"genuine engine constructors retain original uninitialized fields");
                Check(((List<PerformanceAttribute>)Field(typeof(PerformanceProfile),"m_attributes").GetValue(profile)).Count==0&&issues.m_attributes.Count==0,"genuine engine profile constructors allocate authored lists");
                Check(profile.GetGUID()==""&&issues.GetGUID()=="","both real GUID-base constructors execute");
                first.ID="shared";equal.ID="shared";var entry=Attribute(first,100);Set(profile,"m_attributes",new List<PerformanceAttribute>{entry});Set(device,"m_fallback",profile);
                Check(ReferenceEquals(profile.GetAttribute(equal),entry),"actual distinct live feature objects match by ID");
                UnityEngine.Object.DestroyImmediate(first);Check(first==null,"owned feature actually destroyed in engine");
                Check(ReferenceEquals(profile.GetAttribute(equal),entry),"destroyed managed feature still exposes ID field without Unity null normalization");
                Check(ReferenceEquals(device.GetMatch(),profile),"actual base-device fallback retained by reference");
                var json=JsonUtility.ToJson(device);Check(json.Contains("m_fallback")&&!json.Contains("m_platform"),"actual Unity serializer preserves original fallback/platform eligibility");
                json=JsonUtility.ToJson(profile);Check(json.Contains("m_attributes")&&!json.Contains("OnProfileUpdated"),"actual Unity serializer excludes event backing delegate");
                var configuration=JsonUtility.FromJson<RuntimeIssueConfiguration>("{\"m_path\":\"budget\",\"m_value\":2,\"m_isMaxThreshold\":true}");Check(configuration.Path=="budget"&&configuration.Value==2&&configuration.IsValidBytes(2097152f)&&!configuration.IsValidBytes(2097153f),"genuine private-field JSON configuration drives native byte thresholds");
                var runtime=JsonUtility.FromJson<RuntimeIssueAttribute>("{\"m_level\":-100,\"m_configurations\":[{\"m_path\":\"nested\",\"m_value\":3}]}");Check(runtime.QualityLevel==PerformanceProfile.QualityLevel.Low&&runtime.Configurations.Count==1&&runtime.Configurations.First().Path=="nested","actual authored nested runtime-issue fields deserialize");
                int notificationCount=0;Set(profile,"OnProfileUpdated",(Action)(()=>notificationCount++));Validate(profile);Check(notificationCount==1,"actual engine original notification invoked once");Set(profile,"OnProfileUpdated",null);
                return checks;
            }
            finally
            {
                try{if((UnityEngine.Object)first != null)UnityEngine.Object.DestroyImmediate(first);}finally{try{if((UnityEngine.Object)equal != null)UnityEngine.Object.DestroyImmediate(equal);}finally{try{if((UnityEngine.Object)profile != null)UnityEngine.Object.DestroyImmediate(profile);}finally{try{if((UnityEngine.Object)issues != null)UnityEngine.Object.DestroyImmediate(issues);}finally{if((UnityEngine.Object)device != null)UnityEngine.Object.DestroyImmediate(device);}}}}
            }
        }
        public static int Run(){int managed=RunManaged();int engine=RunEngine();Debug.Log("Original performance startup bounded checks="+(managed+engine)+"; engine="+engine);return managed+engine;}
    }
}
