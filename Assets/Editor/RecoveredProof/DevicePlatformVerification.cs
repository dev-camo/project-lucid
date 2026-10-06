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
    public static class DevicePlatformVerification
    {
        private static int checks;
        private static void Check(bool value,string label) { if(!value)throw new Exception("Original device platform verification: "+label);checks++; }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type type,string name) => type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
        private static void SetName(DevicePerformanceMatch value,string name) { Field(typeof(DevicePerformanceMatch),"name").SetValue(value,name); }
        private static string Name(DevicePerformanceMatch value) => (string)Field(typeof(DevicePerformanceMatch),"name").GetValue(value);
        private static void Throws<T>(Action action,string label) where T:Exception { try { action();throw new Exception("Missing expected fault: "+label); } catch(T) { Check(true,label); } }
        public static int RunManaged()
        {
            checks=0;
            var profile=Raw<PerformanceProfile>();var basic=new DevicePerformanceMatch(profile);
            Check(ReferenceEquals(basic.Profile,profile) && Name(basic)==null,"base constructor retains exact profile and default name");
            SetName(basic,"untouched");basic.OnBeforeSerialize();Check(Name(basic)=="untouched" && ReferenceEquals(basic.Profile,profile),"genuine empty base before callback");
            basic.OnAfterDeserialize();Check(Name(basic)=="untouched" && ReferenceEquals(basic.Profile,profile),"genuine empty base after callback");
            var empty=new DevicePerformanceMatch(null);Check(ReferenceEquals(empty.Profile,null),"constructor preserves null profile");
            foreach(DevicePerformanceMatch_iOS.DeviceGeneration value in Enum.GetValues(typeof(DevicePerformanceMatch_iOS.DeviceGeneration)))
            {
                var match=new DevicePerformanceMatch_iOS(value,profile);
                Check(match.Device==value && ReferenceEquals(match.Profile,profile) && Name(match)==null,"iOS genuine full constructor fields");
                SetName(match,"old-before");match.OnBeforeSerialize();Check(Name(match)==value.ToString() && match.Device==value,"iOS original before conversion and retained device");
                SetName(match,"old-after");match.OnAfterDeserialize();Check(Name(match)==value.ToString() && ReferenceEquals(match.Profile,profile),"iOS original after conversion and retained profile");
            }
            foreach(DevicePerformanceMatch_tvOS.DeviceGeneration value in Enum.GetValues(typeof(DevicePerformanceMatch_tvOS.DeviceGeneration)))
            {
                var match=new DevicePerformanceMatch_tvOS(value,profile);
                Check(match.Device==value && ReferenceEquals(match.Profile,profile) && Name(match)==null,"tvOS constructor exact fields");
                match.OnBeforeSerialize();Check(Name(match)==value.ToString(),"tvOS callback preserves native enum conversion including aliases");
                SetName(match,"different");match.OnAfterDeserialize();Check(Name(match)==value.ToString() && match.Device==value && ReferenceEquals(match.Profile,profile),"tvOS after callback only name changes");
            }
            foreach(MacDeviceGeneration value in Enum.GetValues(typeof(MacDeviceGeneration)))
            {
                var match=new DevicePerformanceMatch_macOS(value,profile);
                Check(match.MacDevice==value && ReferenceEquals(match.Profile,profile) && Name(match)==null,"Mac constructor exact genuine enum/profile");
                match.OnBeforeSerialize();Check(Name(match)==value.ToString(),"Mac before callback original enum conversion");
                SetName(match,"different");match.OnAfterDeserialize();Check(Name(match)==value.ToString() && match.MacDevice==value && ReferenceEquals(match.Profile,profile),"Mac after callback retains profile/device");
            }
            var iosUnknown=new DevicePerformanceMatch_iOS((DevicePerformanceMatch_iOS.DeviceGeneration)int.MinValue,null);iosUnknown.OnBeforeSerialize();Check(Name(iosUnknown)==int.MinValue.ToString() && ReferenceEquals(iosUnknown.Profile,null),"unknown signed iOS enum is not normalized");
            var tvUnknown=new DevicePerformanceMatch_tvOS((DevicePerformanceMatch_tvOS.DeviceGeneration)int.MaxValue,null);tvUnknown.OnAfterDeserialize();Check(Name(tvUnknown)==int.MaxValue.ToString(),"unknown signed tvOS enum is not normalized");
            var macUnknown=new DevicePerformanceMatch_macOS((MacDeviceGeneration)int.MinValue,null);macUnknown.OnAfterDeserialize();Check(Name(macUnknown)==int.MinValue.ToString(),"unknown Mac enum remains numeric");
            DevicePerformance[] platforms={Raw<DevicePerformance_iOS>(),Raw<DevicePerformance_macOS>(),Raw<DevicePerformance_tvOS>()};RuntimePlatform[] expected={RuntimePlatform.IPhonePlayer,RuntimePlatform.OSXPlayer,RuntimePlatform.tvOS};
            var group=Raw<DevicePerformanceGroup>();MethodInfo key=typeof(DevicePerformanceGroup).GetMethod("GetElementKey",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);MethodInfo comparer=typeof(DevicePerformanceGroup).GetMethod("GetKeyComparer",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            Check(ReferenceEquals(comparer.Invoke(group,null),null),"whole original group comparer returns null");
            for(int i=0;i<platforms.Length;i++)
            {
                Field(typeof(DevicePerformance),"m_platform").SetValue(platforms[i],RuntimePlatform.WebGLPlayer);
                Check(platforms[i].Platform==expected[i],"original override ignores inherited platform field");
                Check((RuntimePlatform)key.Invoke(group,new object[]{platforms[i]})==expected[i],"group invokes genuine virtual Platform getter");
            }
            Check(typeof(DevicePerformance_iOS).GetMethod("GetMatch",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)==null && typeof(DevicePerformance_tvOS).GetMethod("GetMatch",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)==null,"no invented platform matching overrides");
            Field(typeof(DevicePerformance),"m_fallback").SetValue(platforms[0],profile);Field(typeof(DevicePerformance),"m_fallback").SetValue(platforms[2],profile);
            Check(ReferenceEquals(platforms[0].GetMatch(),profile) && ReferenceEquals(platforms[2].GetMatch(),profile),"iOS/tvOS consume original inherited fallback");
            Check(typeof(DevicePerformanceMatch).GetInterfaces().SequenceEqual(new[]{typeof(ISerializationCallbackReceiver)}) && !typeof(DevicePerformanceMatch_macOS).GetMethod("MatchMacToDeviceGeneration").IsStatic,"genuine base callback contract and instance Mac matcher");
            Check(Field(typeof(DevicePerformance_iOS),"m_deviceFallbacks").IsInitOnly && !Field(typeof(DevicePerformance_iOS),"m_deviceFallbacks").IsDefined(typeof(SerializeField),false),"original private readonly nonserialized fallback dictionary");
            Check(typeof(DevicePerformanceMatch_tvOS.DeviceGeneration).GetField("AppleTV1Gen").GetCustomAttribute<ObsoleteAttribute>().Message=="AppleTV1Gen has been renamed. Use AppleTVHD instead (UnityUpgradable) -> AppleTVHD" && !typeof(DevicePerformanceMatch_tvOS.DeviceGeneration).GetField("AppleTV1Gen").GetCustomAttribute<ObsoleteAttribute>().IsError,"original obsolete alias metadata one");
            Check(typeof(DevicePerformanceMatch_tvOS.DeviceGeneration).GetField("AppleTV2Gen").GetCustomAttribute<ObsoleteAttribute>().Message=="AppleTV2Gen has been renamed. Use AppleTV4K instead (UnityUpgradable) -> AppleTV4K" && !typeof(DevicePerformanceMatch_tvOS.DeviceGeneration).GetField("AppleTV2Gen").GetCustomAttribute<ObsoleteAttribute>().IsError,"original obsolete alias metadata two");
            return checks;
        }

        // Expected model data comes directly from the independently captured57
        // original native equality branches. This fixture does not supply a fake
        // engine model or add an original runtime provider/helper.
        private static MacDeviceGeneration NativeExpectedModel(string model)
        {
            var originalCases=new Dictionary<string,MacDeviceGeneration>(StringComparer.Ordinal)
            {
                {"iMac13,2", (MacDeviceGeneration)(1924880489)},
                {"MacBookPro13,3", (MacDeviceGeneration)(1924880489)},
                {"iMac14,3", (MacDeviceGeneration)(1924880489)},
                {"MacBookAir7,2", (MacDeviceGeneration)(1924880489)},
                {"Macmini9,1", (MacDeviceGeneration)(-915112498)},
                {"iMac17,1", (MacDeviceGeneration)(-70203948)},
                {"MacBookPro14,3", (MacDeviceGeneration)(-70203948)},
                {"MacBookPro11,5", (MacDeviceGeneration)(1924880489)},
                {"iMac18,2", (MacDeviceGeneration)(-915112498)},
                {"MacBookAir9,1", (MacDeviceGeneration)(1924880489)},
                {"MacBookPro16,3", (MacDeviceGeneration)(-915112498)},
                {"Macmini7,1", (MacDeviceGeneration)(1924880489)},
                {"iMac15,1", (MacDeviceGeneration)(1924880489)},
                {"MacBookPro11,4", (MacDeviceGeneration)(1924880489)},
                {"MacBook8,1", (MacDeviceGeneration)(1924880489)},
                {"iMac14,4", (MacDeviceGeneration)(1924880489)},
                {"MacBookAir6,1", (MacDeviceGeneration)(1924880489)},
                {"iMac19,2", (MacDeviceGeneration)(-915112498)},
                {"MacBookPro14,1", (MacDeviceGeneration)(-70203948)},
                {"iMac18,1", (MacDeviceGeneration)(-915112498)},
                {"MacBookPro15,1", (MacDeviceGeneration)(-915112498)},
                {"MacBookPro13,2", (MacDeviceGeneration)(1924880489)},
                {"MacBookPro10,2", (MacDeviceGeneration)(1924880489)},
                {"iMac20,1", (MacDeviceGeneration)(-915112498)},
                {"iMac13,1", (MacDeviceGeneration)(1924880489)},
                {"MacBookPro10,1", (MacDeviceGeneration)(1924880489)},
                {"Macmini8,1", (MacDeviceGeneration)(-70203948)},
                {"MacBookAir8,2", (MacDeviceGeneration)(1924880489)},
                {"iMac14,2", (MacDeviceGeneration)(1924880489)},
                {"MacBookAir7,1", (MacDeviceGeneration)(1924880489)},
                {"IMac16,2", (MacDeviceGeneration)(-70203948)},
                {"MacBookPro15,4", (MacDeviceGeneration)(-915112498)},
                {"MacBookPro11,2", (MacDeviceGeneration)(1924880489)},
                {"MacBook10,1", (MacDeviceGeneration)(-70203948)},
                {"MacBookPro16,1", (MacDeviceGeneration)(-915112498)},
                {"Macmini6,2", (MacDeviceGeneration)(1924880489)},
                {"iMac19,1", (MacDeviceGeneration)(-915112498)},
                {"MacBookPro12,1", (MacDeviceGeneration)(1924880489)},
                {"MacBookAir8,1", (MacDeviceGeneration)(1924880489)},
                {"iMac14,1", (MacDeviceGeneration)(1924880489)},
                {"Mac14,7", (MacDeviceGeneration)(-99653792)},
                {"Macmini6,1", (MacDeviceGeneration)(1924880489)},
                {"iMac16,1", (MacDeviceGeneration)(-70203948)},
                {"MacBookPro14,2", (MacDeviceGeneration)(-70203948)},
                {"MacBook9,1", (MacDeviceGeneration)(-70203948)},
                {"MacBookPro11,3", (MacDeviceGeneration)(1924880489)},
                {"MacBookPro11,1", (MacDeviceGeneration)(1924880489)},
                {"MacBookAir5,2", (MacDeviceGeneration)(1924880489)},
                {"iMac18,3", (MacDeviceGeneration)(-915112498)},
                {"MacBookPro15,2", (MacDeviceGeneration)(-915112498)},
                {"MacBookPro15,3", (MacDeviceGeneration)(-915112498)},
                {"MacPro6,1", (MacDeviceGeneration)(-70203948)},
                {"MacBookPro13,1", (MacDeviceGeneration)(1924880489)},
                {"MacBookAir6,2", (MacDeviceGeneration)(1924880489)},
                {"iMacPro1,1", (MacDeviceGeneration)(-915112498)},
                {"Mac14,10", (MacDeviceGeneration)(946861598)},
                {"iMac20,2", (MacDeviceGeneration)(-915112498)},
            };
            return model!=null && originalCases.TryGetValue(model,out var value) ? value : MacDeviceGeneration.High;
        }
        public static int RunEngine()
        {
            checks=0;DevicePerformance_iOS ios=null,secondIos=null;DevicePerformance_macOS mac=null;DevicePerformance_tvOS tv=null;DevicePerformanceGroup group=null;PerformanceProfile first=null,second=null;
            try
            {
                ios=ScriptableObject.CreateInstance<DevicePerformance_iOS>();secondIos=ScriptableObject.CreateInstance<DevicePerformance_iOS>();mac=ScriptableObject.CreateInstance<DevicePerformance_macOS>();tv=ScriptableObject.CreateInstance<DevicePerformance_tvOS>();group=ScriptableObject.CreateInstance<DevicePerformanceGroup>();first=ScriptableObject.CreateInstance<PerformanceProfile>();second=ScriptableObject.CreateInstance<PerformanceProfile>();
                Check(ios!=null&&secondIos!=null&&mac!=null&&tv!=null&&group!=null&&first!=null&&second!=null,"genuine concrete engine instances are allocated");
                Check(ios.Platform==RuntimePlatform.IPhonePlayer&&mac.Platform==RuntimePlatform.OSXPlayer&&tv.Platform==RuntimePlatform.tvOS,"actual engine original platform constants");
                Check(Field(typeof(DevicePerformance_iOS),"m_devicePerformanceMatches").GetValue(ios)==null&&Field(typeof(DevicePerformance_macOS),"m_devicePerformanceMatches").GetValue(mac)==null&&Field(typeof(DevicePerformance_tvOS),"m_devicePerformanceMatches").GetValue(tv)==null&&group.m_elements==null,"actual whole constructors leave original authored lists and element array null");
                var cache=(Dictionary<string,DevicePerformanceMatch_iOS.DeviceGeneration>)Field(typeof(DevicePerformance_iOS),"m_deviceFallbacks").GetValue(ios);
                Check(cache.Count==2&&cache["iPad"]==DevicePerformanceMatch_iOS.DeviceGeneration.iPadUnknown&&cache["iPod"]==DevicePerformanceMatch_iOS.DeviceGeneration.iPodTouchUnknown,"actual constructor builds the exact two native fallback entries");
                Check(!ReferenceEquals(cache,Field(typeof(DevicePerformance_iOS),"m_deviceFallbacks").GetValue(secondIos)),"separate real iOS instances own distinct readonly dictionaries");
                Check(!cache.ContainsKey("ipad")&&!cache.ContainsKey("iPhone"),"original ordinal sparse device fallbacks stay unchanged");
                Check(ReferenceEquals(ios.GetMatch(),null)&&ReferenceEquals(tv.GetMatch(),null),"actual iOS/tvOS inherited default fallback stays null");
                Field(typeof(DevicePerformance),"m_fallback").SetValue(ios,first);Field(typeof(DevicePerformance),"m_fallback").SetValue(tv,first);Field(typeof(DevicePerformance),"m_fallback").SetValue(mac,second);
                Check(ReferenceEquals(ios.GetMatch(),first)&&ReferenceEquals(tv.GetMatch(),first),"actual iOS/tvOS inherited configured fallback remains exact reference");
                string model=SystemInfo.deviceModel;var expected=NativeExpectedModel(model);var probe=new DevicePerformanceMatch_macOS(MacDeviceGeneration.Future,first);
                Check(probe.MatchMacToDeviceGeneration()==expected,"genuine engine Mac query agrees with original57-case native table for the actual model");
                Check(probe.MatchMacToDeviceGeneration()==expected&&SystemInfo.deviceModel==model,"repeated genuine engine query stable during bounded fixture");
                var rows=new List<DevicePerformanceMatch_macOS>();Field(typeof(DevicePerformance_macOS),"m_devicePerformanceMatches").SetValue(mac,rows);
                Check(ReferenceEquals(mac.GetMatch(),second),"actual empty Mac rows select configured fallback after initial engine query");
                rows.Add(new DevicePerformanceMatch_macOS((MacDeviceGeneration)int.MinValue,second));rows.Add(new DevicePerformanceMatch_macOS(expected,first));rows.Add(new DevicePerformanceMatch_macOS(expected,second));
                Check(ReferenceEquals(mac.GetMatch(),first),"actual first matching Mac row selected after genuine mismatched row");
                rows[1]=new DevicePerformanceMatch_macOS(expected,null);Check(ReferenceEquals(mac.GetMatch(),null),"actual matched null profile remains null and never normalizes to fallback");
                rows[1]=new DevicePerformanceMatch_macOS(expected,first);UnityEngine.Object.DestroyImmediate(first);Check(first==null,"owned profile truly destroyed in engine");
                Check(ReferenceEquals(mac.GetMatch(),first),"destroyed matched profile is retained as original managed reference");
                rows.Clear();rows.Add(new DevicePerformanceMatch_macOS((MacDeviceGeneration)int.MinValue,second));Check(ReferenceEquals(mac.GetMatch(),second),"actual all-mismatched Mac rows select fallback");
                rows.Clear();rows.Add(null);Throws<NullReferenceException>(()=>mac.GetMatch(),"actual null row remains invalid rather than skipped");
                Field(typeof(DevicePerformance_macOS),"m_devicePerformanceMatches").SetValue(mac,null);Throws<NullReferenceException>(()=>mac.GetMatch(),"actual null authored list remains invalid after original query");
                group.m_elements=new[]{new DefinitionDataType<RuntimePlatform,DevicePerformance>.DefinitionElement<DevicePerformance>(ios),new DefinitionDataType<RuntimePlatform,DevicePerformance>.DefinitionElement<DevicePerformance>(mac),new DefinitionDataType<RuntimePlatform,DevicePerformance>.DefinitionElement<DevicePerformance>(tv)};
                var definitions=group.GetData();Check(definitions.Count==3&&ReferenceEquals(definitions[RuntimePlatform.IPhonePlayer],ios)&&ReferenceEquals(definitions[RuntimePlatform.OSXPlayer],mac)&&ReferenceEquals(definitions[RuntimePlatform.tvOS],tv),"genuine whole definition group uses real virtual platform keys and default comparer");
                group.m_elements=new[]{new DefinitionDataType<RuntimePlatform,DevicePerformance>.DefinitionElement<DevicePerformance>(ios),new DefinitionDataType<RuntimePlatform,DevicePerformance>.DefinitionElement<DevicePerformance>(secondIos)};Throws<ArgumentException>(()=>group.GetData(),"genuine duplicate platform definition key is not silently replaced");
                Field(typeof(DevicePerformance_iOS),"m_devicePerformanceMatches").SetValue(ios,new List<DevicePerformanceMatch_iOS>{new DevicePerformanceMatch_iOS(DevicePerformanceMatch_iOS.DeviceGeneration.iPadUnknown,second)});
                string json=JsonUtility.ToJson(ios);Check(json.Contains("m_devicePerformanceMatches")&&json.Contains("m_deviceGeneration")&&json.Contains("m_fallback")&&!json.Contains("m_deviceFallbacks")&&!json.Contains("m_platform"),"actual iOS serializer includes original authored shape and excludes original readonly cache/platform");
                JsonUtility.FromJsonOverwrite("{\"m_devicePerformanceMatches\":[{\"m_deviceGeneration\":10003}]}",ios);var loadedIos=(List<DevicePerformanceMatch_iOS>)Field(typeof(DevicePerformance_iOS),"m_devicePerformanceMatches").GetValue(ios);
                Check(loadedIos.Count==1&&loadedIos[0].Device==DevicePerformanceMatch_iOS.DeviceGeneration.iPodTouchUnknown,"actual private inherited match graph accepts original iOS numeric enum data");
                Check(ReferenceEquals(cache,Field(typeof(DevicePerformance_iOS),"m_deviceFallbacks").GetValue(ios))&&cache.Count==2,"actual JSON overwrite retains nonserialized constructor fallback dictionary");
                JsonUtility.FromJsonOverwrite("{\"m_devicePerformanceMatches\":[{\"m_deviceGeneration\":-100}]}",tv);var loadedTv=(List<DevicePerformanceMatch_tvOS>)Field(typeof(DevicePerformance_tvOS),"m_devicePerformanceMatches").GetValue(tv);
                Check(loadedTv.Count==1&&(int)loadedTv[0].Device==-100,"actual tvOS unknown signed enum value preserved by serializer");
                loadedTv[0].OnBeforeSerialize();Check(Name(loadedTv[0])=="-100","real deserialized tvOS callback preserves enum numeric conversion");
                JsonUtility.FromJsonOverwrite("{\"m_devicePerformanceMatches\":[{\"m_deviceGeneration\":946861598}]}",mac);var loadedMac=(List<DevicePerformanceMatch_macOS>)Field(typeof(DevicePerformance_macOS),"m_devicePerformanceMatches").GetValue(mac);
                Check(loadedMac.Count==1&&loadedMac[0].MacDevice==MacDeviceGeneration.High,"actual macOS private match field deserializes genuine generated enum");
                loadedMac[0].OnAfterDeserialize();Check(Name(loadedMac[0])==MacDeviceGeneration.High.ToString(),"real deserialized Mac callback retains exact enum conversion");
                return checks;
            }
            finally
            {
                // Owned objects only; no scene, registry, global save or input state.
                if(!ReferenceEquals(ios,null))UnityEngine.Object.DestroyImmediate(ios);if(!ReferenceEquals(secondIos,null))UnityEngine.Object.DestroyImmediate(secondIos);if(!ReferenceEquals(mac,null))UnityEngine.Object.DestroyImmediate(mac);if(!ReferenceEquals(tv,null))UnityEngine.Object.DestroyImmediate(tv);if(!ReferenceEquals(group,null))UnityEngine.Object.DestroyImmediate(group);if(!ReferenceEquals(first,null)&&first!=null)UnityEngine.Object.DestroyImmediate(first);if(!ReferenceEquals(second,null))UnityEngine.Object.DestroyImmediate(second);
            }
        }
        public static int Run() => RunManaged()+RunEngine();
    }
}
