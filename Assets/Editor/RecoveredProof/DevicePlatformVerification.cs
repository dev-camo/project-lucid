using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
            CompiledMacMatcherAgreesWithNativeTable();
            return checks;
        }

        // Result immediates of the five native op_Equality blocks in
        // MatchMacToDeviceGeneration (0x5f4340); unmatched models reach 0x5f4edf.
        private static readonly MacDeviceGeneration NativeUltra=(MacDeviceGeneration)unchecked((int)0xfa0f6760);   // 0x5f4db2
        private static readonly MacDeviceGeneration NativeHigh=(MacDeviceGeneration)unchecked((int)0x386ff61e);    // 0x5f4ea6, default 0x5f4edf
        private static readonly MacDeviceGeneration NativeMedium=(MacDeviceGeneration)unchecked((int)0xc9747dce);  // 0x5f4ec7
        private static readonly MacDeviceGeneration NativeLow=(MacDeviceGeneration)unchecked((int)0xfbd0c5d4);     // 0x5f4e52
        private static readonly MacDeviceGeneration NativeVeryLow=(MacDeviceGeneration)unchecked((int)0x72bb5869); // 0x5f4e7c

        // All 72 native model literals with the result block each one reaches,
        // in native branch order (address of the lea that loads the literal).
        private static readonly KeyValuePair<string,MacDeviceGeneration>[] NativeModelTable=
        {
            new KeyValuePair<string,MacDeviceGeneration>("iMac13,2",NativeVeryLow), // 0x5f4717
            new KeyValuePair<string,MacDeviceGeneration>("iMac21,2",NativeHigh), // 0x5f4765
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro18,4",NativeUltra), // 0x5f47a8
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro13,3",NativeVeryLow), // 0x5f47eb
            new KeyValuePair<string,MacDeviceGeneration>("iMac14,3",NativeVeryLow), // 0x5f4823
            new KeyValuePair<string,MacDeviceGeneration>("MacBookAir7,2",NativeVeryLow), // 0x5f485b
            new KeyValuePair<string,MacDeviceGeneration>("Macmini9,1",NativeMedium), // 0x5f4893
            new KeyValuePair<string,MacDeviceGeneration>("iMac17,1",NativeLow), // 0x5f48cb
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro14,3",NativeLow), // 0x5f48f8
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro11,5",NativeVeryLow), // 0x5f4920
            new KeyValuePair<string,MacDeviceGeneration>("iMac18,2",NativeMedium), // 0x5f494d
            new KeyValuePair<string,MacDeviceGeneration>("MacBookAir9,1",NativeVeryLow), // 0x5f497a
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro16,3",NativeMedium), // 0x5f49a7
            new KeyValuePair<string,MacDeviceGeneration>("Mac14,9",NativeHigh), // 0x5f49d4
            new KeyValuePair<string,MacDeviceGeneration>("Macmini7,1",NativeVeryLow), // 0x5f4a01
            new KeyValuePair<string,MacDeviceGeneration>("Mac14,12",NativeHigh), // 0x5f4a2e
            new KeyValuePair<string,MacDeviceGeneration>("iMac15,1",NativeVeryLow), // 0x5f4a50
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro11,4",NativeVeryLow), // 0x5f4a72
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro18,2",NativeUltra), // 0x5f4a94
            new KeyValuePair<string,MacDeviceGeneration>("MacBook8,1",NativeVeryLow), // 0x5f4ab6
            new KeyValuePair<string,MacDeviceGeneration>("iMac14,4",NativeVeryLow), // 0x5f4ad8
            new KeyValuePair<string,MacDeviceGeneration>("Mac14,6",NativeUltra), // 0x5f4afa
            new KeyValuePair<string,MacDeviceGeneration>("MacBookAir6,1",NativeVeryLow), // 0x5f4b1c
            new KeyValuePair<string,MacDeviceGeneration>("iMac19,2",NativeMedium), // 0x5f4b3e
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro14,1",NativeLow), // 0x5f4b6b
            new KeyValuePair<string,MacDeviceGeneration>("Mac14,3",NativeHigh), // 0x5f4b98
            new KeyValuePair<string,MacDeviceGeneration>("iMac18,1",NativeMedium), // 0x5f4bc5
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro15,1",NativeMedium), // 0x5f4bf2
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro16,4",NativeHigh), // 0x5f4c1f
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro13,2",NativeVeryLow), // 0x5f4c4c
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro10,2",NativeVeryLow), // 0x5f4c79
            new KeyValuePair<string,MacDeviceGeneration>("iMac20,1",NativeMedium), // 0x5f4ca6
            new KeyValuePair<string,MacDeviceGeneration>("iMac13,1",NativeVeryLow), // 0x5f4cb2
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro10,1",NativeVeryLow), // 0x5f4cbe
            new KeyValuePair<string,MacDeviceGeneration>("Macmini8,1",NativeLow), // 0x5f4cca
            new KeyValuePair<string,MacDeviceGeneration>("MacBookAir8,2",NativeVeryLow), // 0x5f4cd6
            new KeyValuePair<string,MacDeviceGeneration>("iMac14,2",NativeVeryLow), // 0x5f4ce2
            new KeyValuePair<string,MacDeviceGeneration>("MacBookAir7,1",NativeVeryLow), // 0x5f4cee
            new KeyValuePair<string,MacDeviceGeneration>("IMac16,2",NativeLow), // 0x5f4cfa
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro15,4",NativeMedium), // 0x5f4d06
            new KeyValuePair<string,MacDeviceGeneration>("MacPro7,1",NativeHigh), // 0x5f4d12
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro11,2",NativeVeryLow), // 0x5f4d1e
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro18,1",NativeHigh), // 0x5f4d2a
            new KeyValuePair<string,MacDeviceGeneration>("MacBook10,1",NativeLow), // 0x5f4d36
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro16,1",NativeMedium), // 0x5f4d42
            new KeyValuePair<string,MacDeviceGeneration>("Mac14,5",NativeUltra), // 0x5f4d4e
            new KeyValuePair<string,MacDeviceGeneration>("Macmini6,2",NativeVeryLow), // 0x5f4d57
            new KeyValuePair<string,MacDeviceGeneration>("iMac19,1",NativeMedium), // 0x5f4d63
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro12,1",NativeVeryLow), // 0x5f4d6f
            new KeyValuePair<string,MacDeviceGeneration>("iMac21,1",NativeHigh), // 0x5f4d7b
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro18,3",NativeHigh), // 0x5f4d87
            new KeyValuePair<string,MacDeviceGeneration>("MacBookAir8,1",NativeVeryLow), // 0x5f4d93
            new KeyValuePair<string,MacDeviceGeneration>("iMac14,1",NativeVeryLow), // 0x5f4d9f
            new KeyValuePair<string,MacDeviceGeneration>("Mac14,7",NativeUltra), // 0x5f4dab
            new KeyValuePair<string,MacDeviceGeneration>("Macmini6,1",NativeVeryLow), // 0x5f4dd3
            new KeyValuePair<string,MacDeviceGeneration>("iMac16,1",NativeLow), // 0x5f4ddf
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro14,2",NativeLow), // 0x5f4de8
            new KeyValuePair<string,MacDeviceGeneration>("MacBook9,1",NativeLow), // 0x5f4df1
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro11,3",NativeVeryLow), // 0x5f4dfa
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro11,1",NativeVeryLow), // 0x5f4e03
            new KeyValuePair<string,MacDeviceGeneration>("MacBookAir5,2",NativeVeryLow), // 0x5f4e0c
            new KeyValuePair<string,MacDeviceGeneration>("iMac18,3",NativeMedium), // 0x5f4e15
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro15,2",NativeMedium), // 0x5f4e21
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro15,3",NativeMedium), // 0x5f4e2d
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro16,2",NativeHigh), // 0x5f4e39
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro17,1",NativeHigh), // 0x5f4e42
            new KeyValuePair<string,MacDeviceGeneration>("MacPro6,1",NativeLow), // 0x5f4e4b
            new KeyValuePair<string,MacDeviceGeneration>("MacBookPro13,1",NativeVeryLow), // 0x5f4e6c
            new KeyValuePair<string,MacDeviceGeneration>("MacBookAir6,2",NativeVeryLow), // 0x5f4e75
            new KeyValuePair<string,MacDeviceGeneration>("iMacPro1,1",NativeMedium), // 0x5f4e96
            new KeyValuePair<string,MacDeviceGeneration>("Mac14,10",NativeHigh), // 0x5f4e9f
            new KeyValuePair<string,MacDeviceGeneration>("iMac20,2",NativeMedium), // 0x5f4ec0
        };
        private static readonly Dictionary<string,MacDeviceGeneration> NativeModels=NativeModelTable.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);

        // Expected model data comes from the original native equality branches.
        // This fixture does not supply a fake engine model or add an original
        // runtime provider/helper.
        private static MacDeviceGeneration NativeExpectedModel(string model)
        {
            return model!=null && NativeModels.TryGetValue(model,out var value) ? value : NativeHigh;
        }

        private struct ILInstruction { public int Offset;public OpCode Code;public object Operand;public int Next; }
        private static readonly Dictionary<ushort,OpCode> OpCodeTable=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(c=>unchecked((ushort)c.Value));
        private static List<ILInstruction> DecodeIL(MethodBase method)
        {
            byte[] data=method.GetMethodBody().GetILAsByteArray();var result=new List<ILInstruction>();
            for(int p=0;p<data.Length;)
            {
                int offset=p;ushort value=data[p++];if(value==0xfe)value=(ushort)(0xfe00|data[p++]);
                OpCode code=OpCodeTable[value];object operand=null;
                switch(code.OperandType)
                {
                    case OperandType.InlineNone:break;
                    case OperandType.ShortInlineI:operand=(int)(sbyte)data[p++];break;
                    case OperandType.ShortInlineVar:p++;break;
                    case OperandType.InlineVar:p+=2;break;
                    case OperandType.ShortInlineBrTarget:operand=p+1+(sbyte)data[p];p++;break;
                    case OperandType.InlineBrTarget:operand=p+4+BitConverter.ToInt32(data,p);p+=4;break;
                    case OperandType.InlineI:operand=BitConverter.ToInt32(data,p);p+=4;break;
                    case OperandType.InlineI8:p+=8;break;
                    case OperandType.ShortInlineR:p+=4;break;
                    case OperandType.InlineR:p+=8;break;
                    case OperandType.InlineSwitch:p+=4+BitConverter.ToInt32(data,p)*4;break;
                    case OperandType.InlineMethod:operand=method.Module.ResolveMethod(BitConverter.ToInt32(data,p));p+=4;break;
                    case OperandType.InlineString:operand=method.Module.ResolveString(BitConverter.ToInt32(data,p));p+=4;break;
                    case OperandType.InlineField:case OperandType.InlineType:case OperandType.InlineTok:case OperandType.InlineSig:p+=4;break;
                    default:throw new Exception("Original device platform verification: unsupported matcher IL operand "+code.OperandType);
                }
                result.Add(new ILInstruction{Offset=offset,Code=code,Operand=operand,Next=p});
            }
            return result;
        }
        private static int? ILConstant(ILInstruction i)
        {
            if(i.Code==OpCodes.Ldc_I4||i.Code==OpCodes.Ldc_I4_S)return (int)i.Operand;
            if(i.Code==OpCodes.Ldc_I4_M1)return -1;
            OpCode[] small={OpCodes.Ldc_I4_0,OpCodes.Ldc_I4_1,OpCodes.Ldc_I4_2,OpCodes.Ldc_I4_3,OpCodes.Ldc_I4_4,OpCodes.Ldc_I4_5,OpCodes.Ldc_I4_6,OpCodes.Ldc_I4_7,OpCodes.Ldc_I4_8};
            for(int n=0;n<small.Length;n++)if(i.Code==small[n])return n;
            return null;
        }
        private static bool StoresOrReturns(OpCode code) => code==OpCodes.Ret||code==OpCodes.Stloc||code==OpCodes.Stloc_S||code==OpCodes.Stloc_0||code==OpCodes.Stloc_1||code==OpCodes.Stloc_2||code==OpCodes.Stloc_3;

        // The engine model cannot be injected, so the compiled matcher's own CIL
        // is read instead: each model literal's ordinal op_Equality branch leads
        // to the constant that case returns. Compared with the native table, not
        // with the maintained source.
        private static void CompiledMacMatcherAgreesWithNativeTable()
        {
            var il=DecodeIL(typeof(DevicePerformanceMatch_macOS).GetMethod("MatchMacToDeviceGeneration",BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly));
            var at=new Dictionary<int,int>();for(int i=0;i<il.Count;i++)at[il[i].Offset]=i;
            Check(il.Count(i=>i.Operand is MethodInfo m&&m.DeclaringType==typeof(SystemInfo)&&m.Name=="get_deviceModel")==1,"compiled Mac matcher queries the engine model exactly once");
            var compiled=new List<KeyValuePair<string,MacDeviceGeneration>>();var caseBodies=new HashSet<int>();
            for(int i=0;i<il.Count;i++)
            {
                if(il[i].Code!=OpCodes.Ldstr)continue;
                string model=(string)il[i].Operand;
                Check(i+2<il.Count&&il[i+1].Operand is MethodInfo eq&&eq.DeclaringType==typeof(string)&&eq.Name=="op_Equality"&&eq.GetParameters().Length==2,"compiled literal "+model+" uses ordinal string equality");
                ILInstruction branch=il[i+2];int target;
                if(branch.Code==OpCodes.Brtrue||branch.Code==OpCodes.Brtrue_S)target=(int)branch.Operand;
                else if(branch.Code==OpCodes.Brfalse||branch.Code==OpCodes.Brfalse_S)target=branch.Next;
                else throw new Exception("Original device platform verification: unexpected branch after literal "+model+": "+branch.Code);
                int body=at[target];while(il[body].Code==OpCodes.Nop)body++;
                int? value=ILConstant(il[body]);
                Check(value.HasValue&&StoresOrReturns(il[body+1].Code),"compiled literal "+model+" reaches a constant result");
                caseBodies.Add(body);compiled.Add(new KeyValuePair<string,MacDeviceGeneration>(model,(MacDeviceGeneration)value.Value));
            }
            Check(compiled.Count==NativeModelTable.Length&&compiled.Select(p=>p.Key).Distinct(StringComparer.Ordinal).Count()==NativeModelTable.Length,"compiled Mac matcher has exactly the 72 distinct native model literals");
            foreach(var expected in NativeModelTable)
                Check(compiled.Count(p=>p.Key==expected.Key)==1&&compiled.First(p=>p.Key==expected.Key).Value==expected.Value,"compiled "+expected.Key+" returns its native block result "+expected.Value);
            var defaults=il.Where((x,i)=>ILConstant(x).HasValue&&!caseBodies.Contains(i)&&i+1<il.Count&&StoresOrReturns(il[i+1].Code)).ToList();
            Check(defaults.Count==1&&(MacDeviceGeneration)ILConstant(defaults[0]).Value==NativeHigh,"compiled unmatched model default is the native 0x5f4edf High result");
            // Ordinal-only negatives: case variants of original literals and an
            // unlisted model fall to the default; the original 'IMac16,2' is kept.
            foreach(string model in new[]{"imac16,2","iMac16,2","macbookpro18,4","MACBOOKPRO18,2","Mac14,8",""})
                Check(!compiled.Any(p=>p.Key==model)&&NativeExpectedModel(model)==NativeHigh,"unlisted or case-variant model "+model+" reaches the High default");
            Check(NativeExpectedModel(null)==NativeHigh&&NativeExpectedModel("IMac16,2")==NativeLow&&NativeExpectedModel("MacBookPro18,4")==NativeUltra,"native table positive and null controls");
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
                Check(probe.MatchMacToDeviceGeneration()==expected,"genuine engine Mac query agrees with the 72-literal native table for the actual model");
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
