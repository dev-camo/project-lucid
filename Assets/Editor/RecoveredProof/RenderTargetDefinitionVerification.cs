using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;
using UnityEngine.AddressableAssets;
using HardlightEnumComparers = HardlightProject.HardlightEnumComparers;

namespace ProjectLucid
{
    public static class RenderTargetDefinitionVerification
    {
        private static int checks;
        private static void Check(bool value,string label){if(!value)throw new Exception("Original render/target definitions: "+label);checks++;}
        private static T Raw<T>()=>(T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type t,string n)=>t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
        private static void Set(object o,string n,object v)=>Field(o.GetType(),n).SetValue(o,v);
        private static object Read(object o,string n)=>Field(o.GetType(),n).GetValue(o);
        private static object Comparer(object o)=>o.GetType().GetMethod("GetKeyComparer",BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Invoke(o,null);
        private static void Throws<T>(Action a,string label)where T:Exception{try{a();throw new Exception("Expected fault absent:"+label);}catch(T){Check(true,label);}}
        private static DefinitionDataType<K,T>.DefinitionElement<T> Row<K,T>(T value)where T:ScriptableObject{var r=Raw<DefinitionDataType<K,T>.DefinitionElement<T>>();r.Data=value;return r;}
        private static void Group<K,T>(DefinitionDataType<K,T> group,T value,K key,IEqualityComparer<K> comparer)where T:ScriptableObject
        {
            Check(ReferenceEquals(Comparer(group),comparer),"exact original registry comparer "+typeof(T).Name);
            group.m_elements=Array.Empty<DefinitionDataType<K,T>.DefinitionElement<T>>();var empty=group.GetData();Check(empty.Count==0&&ReferenceEquals(empty.Comparer,comparer),"empty real dictionary/comparer");
            group.m_elements=new[]{Row<K,T>(value)};Check(ReferenceEquals(group.GetData()[key],value),"own getter/field key retains exact authored reference");
            group.m_elements=new[]{Row<K,T>(value),Row<K,T>(value)};Throws<ArgumentException>(()=>group.GetData(),"duplicate native key not overwritten");
            group.m_elements=new DefinitionDataType<K,T>.DefinitionElement<T>[]{null};Throws<NullReferenceException>(()=>group.GetData(),"null row not skipped");
            group.m_elements=new[]{Row<K,T>(null)};Throws<NullReferenceException>(()=>group.GetData(),"null data getter/field fault preserved");
        }
        public static int RunManaged()
        {
            checks=0;var targeting=Raw<TargetingTypePriorityDefinition>();
            Check((int)targeting.TargetType==0&&targeting.Priority==0,"target native field-zero defaults");
            Set(targeting,"m_targetType",(HomingTargetType)(-81));Set(targeting,"m_priority",int.MinValue);Check((int)targeting.TargetType==-81&&targeting.Priority==int.MinValue,"target getters preserve negative enum/extreme priority");
            Set(targeting,"m_priority",int.MaxValue);Check(targeting.Priority==int.MaxValue,"target priority is not clamped");
            var cascade=Raw<CascadeObjectDefinition>();cascade.Type=(CascadeObjectType)82;cascade.PrefabPoolType=Raw<PrefabPoolType>();cascade.MinLaunchVelocity=new Vector3(-1,2,-3);cascade.MaxLaunchVelocity=new Vector3(4,-5,6);cascade.LifetimeSeconds=-7;cascade.MaximumSpawnedObjects=-8;cascade.MaxRandomDirection=new Vector3(9,10,11);cascade.MinRandomDirection=new Vector3(12,13,14);
            object pool=cascade.PrefabPoolType;
            typeof(CascadeObjectDefinition).GetMethod("OnValidate",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Invoke(cascade,null);
            Check(cascade.LifetimeSeconds==-7&&cascade.MaximumSpawnedObjects==-8&&ReferenceEquals(cascade.PrefabPoolType,pool)&&cascade.MinLaunchVelocity.x==-1&&cascade.MaxRandomDirection.y==10,"native RET validation does not normalize public authored fields");
            var material=Raw<Material>();var item=new ChallengeZoneMaterialDefinition.ChallengeZoneMaterial();
            Check((int)item.Type==0&&ReferenceEquals(Read(item,"m_materialAsset"),null),"real nested constructor leaves native zero/null fields");
            Set(item,"m_type",(ChallengeZoneMaterialType)(-83));var reference=new AssetReferenceT<Material>("0123456789abcdef0123456789abcdef");Set(item,"m_materialAsset",reference);
            Check((int)item.Type==-83&&ReferenceEquals(Read(item,"m_materialAsset"),reference),"nested authored type/reference preserved");
            Check(!item.IsMatch(material)&&!item.IsMatch(null),"native IsMatch always false and never loads authored AA reference");
            Material retained=material;item.ApplyMaterial(ref retained);Check(ReferenceEquals(retained,material),"native RET ApplyMaterial preserves non-null ref");
            retained=null;item.ApplyMaterial(ref retained);Check(ReferenceEquals(retained,null),"native RET ApplyMaterial preserves null ref");
            var zone=Raw<ChallengeZoneMaterialDefinition>();Set(zone,"m_identifier",(ChallengeZoneIdentifier)84);
            Check((int)zone.Identifier==84,"zone direct Identifier getter");ChallengeZoneMaterialType matched=(ChallengeZoneMaterialType)(-999);
            Check(!zone.TryGetTypeMatch(material,out matched)&&(int)matched==0,"type-match always clears out/defaultfalse even with null array");
            matched=(ChallengeZoneMaterialType)999;Check(!zone.TryGetTypeMatch(null,out matched)&&(int)matched==0,"null material ignored by native constant false type-match");
            Set(zone,"m_materials",Array.Empty<ChallengeZoneMaterialDefinition.ChallengeZoneMaterial>());retained=material;
            Check(!zone.TryGetMaterial((ChallengeZoneMaterialType)(-83),out retained)&&ReferenceEquals(retained,null),"empty array clears out before false");
            Set(zone,"m_materials",new[]{item});retained=material;Check(zone.TryGetMaterial((ChallengeZoneMaterialType)(-83),out retained)&&ReferenceEquals(retained,null),"matching type returns true while material stays null in supplied player");
            retained=material;Check(!zone.TryGetMaterial((ChallengeZoneMaterialType)83,out retained)&&ReferenceEquals(retained,null),"missing type clears out and returns false");
            var later=new ChallengeZoneMaterialDefinition.ChallengeZoneMaterial();Set(later,"m_type",(ChallengeZoneMaterialType)85);Set(zone,"m_materials",new[]{item,later});
            Check(zone.TryGetMaterial((ChallengeZoneMaterialType)85,out retained)&&ReferenceEquals(retained,null),"later array type match true/null");
            Set(zone,"m_materials",new ChallengeZoneMaterialDefinition.ChallengeZoneMaterial[]{item,null});Check(zone.TryGetMaterial((ChallengeZoneMaterialType)(-83),out retained),"first match returns before later null row");
            retained=material;Throws<NullReferenceException>(()=>zone.TryGetMaterial((ChallengeZoneMaterialType)85,out retained),"earlier unmatched then null row preserves fault");Check(ReferenceEquals(retained,null),"out cleared before null-row fault");
            Set(zone,"m_materials",null);retained=material;Throws<NullReferenceException>(()=>zone.TryGetMaterial((ChallengeZoneMaterialType)0,out retained),"null array fault preserved");Check(ReferenceEquals(retained,null),"out cleared before null-array fault");
            Group(Raw<TargetingTypePriorityDefinitionGroup>(),targeting,(HomingTargetType)(-81),HardlightEnumComparers.HomingTargetTypeComparer);
            Group(Raw<CascadeObjectDefinitionGroup>(),cascade,(CascadeObjectType)82,HardlightEnumComparers.CascadeObjectTypeComparer);
            Group(Raw<ChallengeZoneMaterialDefinitionGroup>(),zone,(ChallengeZoneIdentifier)84,HardlightEnumComparers.ChallengeZoneIdentifierComparer);
            return checks;
        }
        private static T Own<T>(List<UnityEngine.Object> owned)where T:ScriptableObject
        {
            T value=ScriptableObject.CreateInstance<T>();
            if(ReferenceEquals(value,null))throw new Exception("Original render/target fixture failed to construct genuine concrete "+typeof(T).FullName);
            owned.Add(value);return value;
        }
        private static void Release(List<UnityEngine.Object> owned,int index)
        {
            if(index<0)return;
            try{if(!ReferenceEquals(owned[index],null))UnityEngine.Object.DestroyImmediate(owned[index]);}
            finally{Release(owned,index-1);}
        }
        private static bool Near(Vector3 actual,Vector3 expected)=>(actual-expected).sqrMagnitude<1e-10f;
        public static int RunEngine()
        {
            checks=0;var owned=new List<UnityEngine.Object>();UnityEngine.Random.State oldRandom=UnityEngine.Random.state;
            try
            {
                var pool=Own<PrefabPoolType>(owned);
                Check(pool.GetType()==typeof(PrefabPoolType),"actual original fieldless pool concrete ScriptableObject; no invented enum/subclass");
                var targeting=Own<TargetingTypePriorityDefinition>(owned);var targetCopy=Own<TargetingTypePriorityDefinition>(owned);
                Check((int)targeting.TargetType==0&&targeting.Priority==0,"actual target native constructor field defaults");
                Set(targeting,"m_targetType",(HomingTargetType)(-81));Set(targeting,"m_priority",int.MinValue);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(targeting),targetCopy);
                Check((int)targetCopy.TargetType==-81&&targetCopy.Priority==int.MinValue,"actual serialized target own fields/getters roundtrip");
                var cascade=Own<CascadeObjectDefinition>(owned);var cascadeCopy=Own<CascadeObjectDefinition>(owned);
                Check((int)cascade.Type==0&&ReferenceEquals(cascade.PrefabPoolType,null)&&cascade.LifetimeSeconds==0f&&cascade.MaximumSpawnedObjects==0&&cascade.MinLaunchVelocity==Vector3.zero&&cascade.MaxLaunchVelocity==Vector3.zero,"actual cascade fields not assigned by native constructor stay zero/null");
                Check(cascade.LaunchVelocityBySpawnedNumberCurve!=null&&Mathf.Approximately(cascade.LaunchVelocityBySpawnedNumberCurve.Evaluate(.25f),.25f)&&Mathf.Approximately(cascade.LaunchVelocityBySpawnedNumberCurve.Evaluate(.75f),.75f),"actual original AnimationCurve.Linear(0,0,1,1) default");
                Check(cascade.MaxRandomDirection==Vector3.one&&cascade.MinRandomDirection==-Vector3.one,"actual native max-one/min-negative-one defaults");
                cascade.MinRandomDirection=Vector3.zero;cascade.MaxRandomDirection=Vector3.zero;
                Check(cascade.GetRandomDirection()==Vector3.up,"actual zero samples normalize to native up fallback");
                cascade.MinRandomDirection=new Vector3(3,4,0);cascade.MaxRandomDirection=cascade.MinRandomDirection;
                Check(Near(cascade.GetRandomDirection(),new Vector3(.6f,.8f,0f)),"actual fixed nonzero samples normalize without fallback");
                cascade.MinRandomDirection=new Vector3(-7,-5,-3);cascade.MaxRandomDirection=new Vector3(2,4,8);
                UnityEngine.Random.InitState(13579);
                var expected=new Vector3(UnityEngine.Random.Range(-7f,2f),UnityEngine.Random.Range(-5f,4f),UnityEngine.Random.Range(-3f,8f)).normalized;
                if(expected.sqrMagnitude==0f)expected=Vector3.up;
                float expectedNext=UnityEngine.Random.value;
                UnityEngine.Random.InitState(13579);Vector3 actual=cascade.GetRandomDirection();float actualNext=UnityEngine.Random.value;
                Check(Near(actual,expected),"actual seeded native x/y/z draw ordering and normalization");
                Check(actualNext==expectedNext,"actual native draws exactly three float Random.Range calls before return");
                cascade.MinRandomDirection=new Vector3(1e-6f,0,0);cascade.MaxRandomDirection=cascade.MinRandomDirection;
                Check(cascade.GetRandomDirection()==Vector3.up,"actual original normalization threshold small nonzero direction falls back up");
                cascade.MinRandomDirection=new Vector3(2e-5f,0,0);cascade.MaxRandomDirection=cascade.MinRandomDirection;
                Check(cascade.GetRandomDirection()==Vector3.right,"actual above original normalization threshold preserves positive direction");
                cascade.Type=(CascadeObjectType)82;cascade.PrefabPoolType=pool;cascade.LifetimeSeconds=-7;cascade.MaximumSpawnedObjects=-8;
                cascade.MinLaunchVelocity=new Vector3(-1,2,-3);cascade.MaxLaunchVelocity=new Vector3(4,-5,6);
                typeof(CascadeObjectDefinition).GetMethod("OnValidate",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Invoke(cascade,null);
                Check(cascade.LifetimeSeconds==-7&&cascade.MaximumSpawnedObjects==-8&&ReferenceEquals(cascade.PrefabPoolType,pool)&&cascade.MinLaunchVelocity.x==-1&&cascade.MaxLaunchVelocity.y==-5,"actual genuine OnValidate RET leaves negative authored values unchanged");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(cascade),cascadeCopy);
                Check((int)cascadeCopy.Type==82&&ReferenceEquals(cascadeCopy.PrefabPoolType,pool)&&cascadeCopy.LifetimeSeconds==-7&&cascadeCopy.MaximumSpawnedObjects==-8&&cascadeCopy.MinLaunchVelocity==cascade.MinLaunchVelocity&&cascadeCopy.MaxLaunchVelocity==cascade.MaxLaunchVelocity&&cascadeCopy.MinRandomDirection==cascade.MinRandomDirection&&Mathf.Approximately(cascadeCopy.LaunchVelocityBySpawnedNumberCurve.Evaluate(.25f),.25f),"actual public cascade field graph/pool reference/curve JSON roundtrip");
                var zone=Own<ChallengeZoneMaterialDefinition>(owned);var zoneCopy=Own<ChallengeZoneMaterialDefinition>(owned);
                Check((int)zone.Identifier==0&&ReferenceEquals(Read(zone,"m_materials"),Array.Empty<ChallengeZoneMaterialDefinition.ChallengeZoneMaterial>()),"actual zone ctor installs exact shared empty nested array before SO base");
                var entry=new ChallengeZoneMaterialDefinition.ChallengeZoneMaterial();
                Check((int)entry.Type==0&&ReferenceEquals(Read(entry,"m_materialAsset"),null),"actual complete nested material constructor leaves native defaults");
                Shader shader=Shader.Find("Hidden/InternalErrorShader");if(ReferenceEquals(shader,null))throw new Exception("Genuine Material fixture shader unavailable");
                var material=new Material(shader);owned.Add(material);
                Check(!entry.IsMatch(material)&&!entry.IsMatch(null),"actual Material and null retain original native false IsMatch");
                Material retained=material;entry.ApplyMaterial(ref retained);
                Check(ReferenceEquals(retained,material)&&material.GetInstanceID()!=0,"actual native RET preserves owned engine Material reference");
                ChallengeZoneMaterialType match=(ChallengeZoneMaterialType)999;
                Check(!zone.TryGetTypeMatch(material,out match)&&(int)match==0,"actual native TryGetTypeMatch ignores Material and clears output");
                Set(entry,"m_type",(ChallengeZoneMaterialType)(-83));Set(entry,"m_materialAsset",new AssetReferenceT<Material>("0123456789abcdef0123456789abcdef"));Set(zone,"m_identifier",(ChallengeZoneIdentifier)84);Set(zone,"m_materials",new[]{entry});
                Check(zone.TryGetMaterial((ChallengeZoneMaterialType)(-83),out retained)&&ReferenceEquals(retained,null),"actual matching authored type returns true/null and does not load material");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(zone),zoneCopy);
                var copied=(ChallengeZoneMaterialDefinition.ChallengeZoneMaterial[])Read(zoneCopy,"m_materials");
                Check((int)zoneCopy.Identifier==84&&copied.Length==1&&(int)copied[0].Type==-83&&((AssetReferenceT<Material>)Read(copied[0],"m_materialAsset")).AssetGUID=="0123456789abcdef0123456789abcdef","actual private nested typed Material AA graph JSON roundtrip; no supplied asset load");
                Check(zoneCopy.TryGetMaterial((ChallengeZoneMaterialType)(-83),out retained)&&ReferenceEquals(retained,null),"actual deserialized matching row preserves original true/null behavior");
                var targetGroup=Own<TargetingTypePriorityDefinitionGroup>(owned);targetGroup.m_elements=new[]{new DefinitionDataType<HomingTargetType,TargetingTypePriorityDefinition>.DefinitionElement<TargetingTypePriorityDefinition>(targeting)};
                Check(ReferenceEquals(targetGroup.GetData()[(HomingTargetType)(-81)],targeting)&&ReferenceEquals(targetGroup.GetData().Comparer,HardlightEnumComparers.HomingTargetTypeComparer),"actual target group retains own key/comparer/data references");
                var cascadeGroup=Own<CascadeObjectDefinitionGroup>(owned);cascadeGroup.m_elements=new[]{new DefinitionDataType<CascadeObjectType,CascadeObjectDefinition>.DefinitionElement<CascadeObjectDefinition>(cascade)};
                Check(ReferenceEquals(cascadeGroup.GetData()[(CascadeObjectType)82],cascade)&&ReferenceEquals(cascadeGroup.GetData().Comparer,HardlightEnumComparers.CascadeObjectTypeComparer),"actual cascade group retains public Type key/comparer/data references");
                var zoneGroup=Own<ChallengeZoneMaterialDefinitionGroup>(owned);zoneGroup.m_elements=new[]{new DefinitionDataType<ChallengeZoneIdentifier,ChallengeZoneMaterialDefinition>.DefinitionElement<ChallengeZoneMaterialDefinition>(zone)};
                Check(ReferenceEquals(zoneGroup.GetData()[(ChallengeZoneIdentifier)84],zone)&&ReferenceEquals(zoneGroup.GetData().Comparer,HardlightEnumComparers.ChallengeZoneIdentifierComparer),"actual material group retains Identifier/comparer/data references");
                return checks;
            }
            finally{try{UnityEngine.Random.state=oldRandom;}finally{Release(owned,owned.Count-1);}}
        }
        public static void Run()
        {
            int managed=RunManaged();if(managed!=39)throw new Exception("Original render/target managed assertion count changed");
            int engine=RunEngine();if(engine!=25)throw new Exception("Original render/target engine assertion count changed: "+engine);
            Debug.Log("Bounded original render/target checks="+(managed+engine)+" (managed="+managed+", actual Unity="+engine+"); supplied assets/native invalid-input faults/token/scope/layout/owners/fullgame remain unapproved.");
        }
    }
}
