using System;using System.Collections.Generic;using System.Reflection;using System.Runtime.Serialization;
using Hardlight;using HardlightProject;using UnityEngine;using UnityEngine.AddressableAssets;using UnityEngine.ResourceManagement.AsyncOperations;
using HardlightEnumComparers=HardlightProject.HardlightEnumComparers;
namespace ProjectLucid
{
 public static class UIBackedDefinitionVerification
 {
  static int checks;static void Check(bool v,string name){if(!v)throw new Exception("Original UI-backed definitions: "+name);checks++;}
  static T Raw<T>()=>(T)FormatterServices.GetUninitializedObject(typeof(T));
  static FieldInfo Field(Type t,string name)=>t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);
  static void Set(object value,string name,object data)=>Field(value.GetType(),name).SetValue(value,data);
  static object Read(object value,string name)=>Field(value.GetType(),name).GetValue(value);
  static void Enable(CollectableDefinition value)=>typeof(CollectableDefinition).GetMethod("OnEnable",BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Invoke(value,null);
  static object Asset(ManagedAddressableAsset<Sprite> value)=>Field(typeof(ManagedAddressableAsset<Sprite>),"m_assetReference").GetValue(value);
  static void Throws<T>(Action action,string name)where T:Exception{try{action();throw new Exception("Expected fault absent:"+name);}catch(T){Check(true,name);}}
  static DefinitionDataType<K,T>.DefinitionElement<T> Row<K,T>(T value)where T:ScriptableObject{var row=Raw<DefinitionDataType<K,T>.DefinitionElement<T>>();row.Data=value;return row;}
  static void Group<K,T>(DefinitionDataType<K,T> group,T value,K key,IEqualityComparer<K> comparer)where T:ScriptableObject
  {
   var own=group.GetType().GetMethod("GetKeyComparer",BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);Check(ReferenceEquals(own.Invoke(group,null),comparer),"exact original registry comparer "+typeof(T).Name);
   group.m_elements=Array.Empty<DefinitionDataType<K,T>.DefinitionElement<T>>();var empty=group.GetData();Check(empty.Count==0&&ReferenceEquals(empty.Comparer,comparer),"empty real dictionary/comparer");
   group.m_elements=new[]{Row<K,T>(value)};Check(ReferenceEquals(group.GetData()[key],value),"own getter key retains exact reference");
   group.m_elements=new[]{Row<K,T>(value),Row<K,T>(value)};Throws<ArgumentException>(()=>group.GetData(),"duplicate native key faults");
   group.m_elements=new DefinitionDataType<K,T>.DefinitionElement<T>[]{null};Throws<NullReferenceException>(()=>group.GetData(),"null row not skipped");
   group.m_elements=new[]{Row<K,T>(null)};Throws<NullReferenceException>(()=>group.GetData(),"null data getter fault");
   group.m_elements=null;Throws<NullReferenceException>(()=>group.GetData(),"null array fault");
  }
  public static int RunManaged()
  {
   checks=0;var ability=Raw<CharacterAbilityUIDefinition>();Check((int)ability.Type==0&&ReferenceEquals(ability.ContainerIdentifier,null),"uninitialized native fields zero/null; no constructor claim");var container=Raw<UIContainerIdentifier>();Set(ability,"m_type",(CharacterAbilityUIType)(-71));Set(ability,"m_containerIdentifier",container);Check((int)ability.Type==-71&&ReferenceEquals(ability.ContainerIdentifier,container),"direct original UI type/container getters");
   var collectable=Raw<CollectableDefinition>();Check((int)collectable.Type==0&&ReferenceEquals(collectable.Icon,null)&&ReferenceEquals(collectable.ProgressionUnlockWidget,null)&&ReferenceEquals(collectable.DisplayFormattedString,null)&&!collectable.HasTotal,"uninitialized own field values; constructor format literal proven separately");
   var icon=new AssetReferenceAtlasedSprite("0123456789abcdef0123456789abcdef");var alternate=new AssetReferenceAtlasedSprite("fedcba9876543210fedcba9876543210");var widget=Raw<UIWidgetProgression>();Set(collectable,"m_type",(CollectableType)72);Set(collectable,"m_icon",icon);Set(collectable,"m_iconAlt",alternate);Set(collectable,"m_progressionUnlockWidget",widget);Set(collectable,"m_displayFormattedString","{1} / {0}");Set(collectable,"m_hasTotal",true);
   Check((int)collectable.Type==72&&ReferenceEquals(collectable.Icon,icon)&&ReferenceEquals(collectable.ProgressionUnlockWidget,widget)&&collectable.DisplayFormattedString=="{1} / {0}"&&collectable.HasTotal,"authored values/reference getters are not normalized");
   Sprite held=Raw<Sprite>();Set(collectable,"m_loadedSprite",held);Set(collectable,"m_refCount",17);Enable(collectable);
   Check(collectable.IconAsset!=null&&collectable.IconAltAsset!=null&&!ReferenceEquals(collectable.IconAsset,collectable.IconAltAsset),"primary/alternate genuine managed wrappers created separately");
   Check(ReferenceEquals(Asset(collectable.IconAsset),icon)&&ReferenceEquals(Asset(collectable.IconAltAsset),alternate),"wrappers retain exact original atlas references");
   Check(ReferenceEquals(Read(collectable,"m_loadedSprite"),held)&&(int)Read(collectable,"m_refCount")==17&&!((AsyncOperationHandle<Sprite>)Read(collectable,"m_operationHandle")).IsValid(),"OnEnable does not clear cache/count or load any handle");
   var first=collectable.IconAsset;var second=collectable.IconAltAsset;Enable(collectable);Check(!ReferenceEquals(first,collectable.IconAsset)&&!ReferenceEquals(second,collectable.IconAltAsset),"repeated OnEnable replaces original wrappers without reuse");
   Set(collectable,"m_icon",alternate);Set(collectable,"m_iconAlt",null);Enable(collectable);Check(ReferenceEquals(Asset(collectable.IconAsset),alternate)&&ReferenceEquals(Asset(collectable.IconAltAsset),null),"each enable reloads both current authored fields including null alternate");
   Check(ReferenceEquals(Asset(first),icon)&&ReferenceEquals(Asset(second),alternate),"original detached wrappers retain their captured refs; no unload/rewrite");
   foreach(int initial in new[]{5,1,0,-8,int.MinValue})
   {
    Set(collectable,"m_refCount",initial);Set(collectable,"m_loadedSprite",held);Set(collectable,"m_operationHandle",default(AsyncOperationHandle<Sprite>));collectable.UnloadIcon();int after=unchecked(initial-1);bool keep=after>0;
    Check((int)Read(collectable,"m_refCount")== (keep?after:0)&&ReferenceEquals(Read(collectable,"m_loadedSprite"),keep?held:null)&&((AsyncOperationHandle<Sprite>)Read(collectable,"m_operationHandle")).Equals(default(AsyncOperationHandle<Sprite>)),"native unchecked decrement/positive reuse/clear-clamp/default handle retained "+initial);
   }
   Group(Raw<CharacterAbilityUIDefinitionGroup>(),ability,(CharacterAbilityUIType)(-71),HardlightEnumComparers.CharacterAbilityUITypeComparer);
   Group(Raw<CollectableDefinitionGroup>(),collectable,(CollectableType)72,HardlightEnumComparers.CollectableTypeComparer);
   return checks;
  }
  static T Own<T>(List<UnityEngine.Object> owned)where T:ScriptableObject
  {
   T value=ScriptableObject.CreateInstance<T>();if(ReferenceEquals(value,null))throw new Exception("Genuine UI definition concrete creation failed: "+typeof(T).FullName);owned.Add(value);return value;
  }
  static void ReleaseObjects(List<UnityEngine.Object> owned,int index)
  {
   if(index<0)return;try{if(!ReferenceEquals(owned[index],null))UnityEngine.Object.DestroyImmediate(owned[index]);}finally{ReleaseObjects(owned,index-1);}
  }
  static void ReleaseHandles(List<AsyncOperationHandle<Sprite>> handles,int index)
  {
   if(index<0)return;try{if(handles[index].IsValid())Addressables.Release(handles[index]);}finally{ReleaseHandles(handles,index-1);}
  }
  public static int RunEngine()
  {
   checks=0;var owned=new List<UnityEngine.Object>();var handles=new List<AsyncOperationHandle<Sprite>>();
   try
   {
    var ability=Own<CharacterAbilityUIDefinition>(owned);var abilityCopy=Own<CharacterAbilityUIDefinition>(owned);var container=Own<UIContainerIdentifier>(owned);
    Check((int)ability.Type==0&&ReferenceEquals(ability.ContainerIdentifier,null),"actual native ability constructor leaves own fields zero/null");
    Set(ability,"m_type",(CharacterAbilityUIType)(-71));Set(ability,"m_containerIdentifier",container);
    Check((int)ability.Type==-71&&ReferenceEquals(ability.ContainerIdentifier,container),"actual direct UI definition references and enum values");
    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(ability),abilityCopy);
    Check((int)abilityCopy.Type==-71&&ReferenceEquals(abilityCopy.ContainerIdentifier,container),"actual private ability fields/concrete UI identifier JSON roundtrip");
    var collectable=Own<CollectableDefinition>(owned);var copy=Own<CollectableDefinition>(owned);
    Check(collectable.DisplayFormattedString=="{0}/{1}"&&(int)collectable.Type==0&&!collectable.HasTotal&&ReferenceEquals(collectable.Icon,null)&&ReferenceEquals(collectable.ProgressionUnlockWidget,null),"actual literal initializer and native defaults");
    Check(collectable.IconAsset!=null&&collectable.IconAltAsset!=null&&!ReferenceEquals(collectable.IconAsset,collectable.IconAltAsset)&&ReferenceEquals(Asset(collectable.IconAsset),null)&&ReferenceEquals(Asset(collectable.IconAltAsset),null),"actual Unity OnEnable builds both genuine wrappers with initial null atlas references");
    var go=new GameObject("ProjectLucid.UIBackedDefinitionVerification");owned.Add(go);go.SetActive(false);var widget=go.AddComponent<UIWidgetProgression>();
    Check(widget!=null&&widget.MaskedTexture!=null,"actual inactive genuine progression widget with maintained constructor dependency");
    var icon=new AssetReferenceAtlasedSprite("0123456789abcdef0123456789abcdef"){SubObjectName="main-icon"};var alternate=new AssetReferenceAtlasedSprite("fedcba9876543210fedcba9876543210"){SubObjectName="alternate-icon"};
    Set(collectable,"m_type",(CollectableType)72);Set(collectable,"m_icon",icon);Set(collectable,"m_iconAlt",alternate);Set(collectable,"m_progressionUnlockWidget",widget);Set(collectable,"m_displayFormattedString","{1} / {0}");Set(collectable,"m_hasTotal",true);Enable(collectable);
    Check((int)collectable.Type==72&&ReferenceEquals(collectable.Icon,icon)&&ReferenceEquals(collectable.ProgressionUnlockWidget,widget)&&collectable.DisplayFormattedString=="{1} / {0}"&&collectable.HasTotal,"actual authored reference/value getters");
    Check(ReferenceEquals(Asset(collectable.IconAsset),icon)&&ReferenceEquals(Asset(collectable.IconAltAsset),alternate),"actual OnEnable retains separate current primary/alternate atlas references");
    var texture=new Texture2D(4,4);owned.Add(texture);var sprite=Sprite.Create(texture,new Rect(0,0,4,4),new Vector2(.5f,.5f));if(ReferenceEquals(sprite,null))throw new Exception("Genuine sprite allocation failed");owned.Add(sprite);
    Set(collectable,"m_loadedSprite",sprite);Set(collectable,"m_refCount",2);
    Check(ReferenceEquals(collectable.LoadIcon(),sprite)&&(int)Read(collectable,"m_refCount")==3,"actual live cached sprite reused after acquisition without catalog load");
    Set(collectable,"m_refCount",int.MaxValue);Check(ReferenceEquals(collectable.LoadIcon(),sprite)&&(int)Read(collectable,"m_refCount")==int.MinValue,"actual cached acquisition preserves unchecked Int32 overflow");
    Set(collectable,"m_refCount",2);collectable.UnloadIcon();Check(ReferenceEquals(Read(collectable,"m_loadedSprite"),sprite)&&(int)Read(collectable,"m_refCount")==1,"actual positive count retains live cache");
    var first=collectable.IconAsset;var second=collectable.IconAltAsset;Enable(collectable);
    Check(!ReferenceEquals(first,collectable.IconAsset)&&!ReferenceEquals(second,collectable.IconAltAsset)&&ReferenceEquals(Read(collectable,"m_loadedSprite"),sprite)&&(int)Read(collectable,"m_refCount")==1,"actual repeated enable replaces wrappers without clearing active cached sprite/count");
    collectable.UnloadIcon();Check(ReferenceEquals(Read(collectable,"m_loadedSprite"),null)&&(int)Read(collectable,"m_refCount")==0&&!((AsyncOperationHandle<Sprite>)Read(collectable,"m_operationHandle")).IsValid(),"actual zero-count path clears cache/clamps count with invalid untouched handle");
    Set(collectable,"m_loadedSprite",sprite);Set(collectable,"m_refCount",int.MinValue);collectable.UnloadIcon();Check(ReferenceEquals(Read(collectable,"m_loadedSprite"),sprite)&&(int)Read(collectable,"m_refCount")==int.MaxValue,"actual release decrement overflow retains live cache");
    var handle=Addressables.ResourceManager.CreateCompletedOperation<Sprite>(sprite,null);handles.Add(handle);
    Check(handle.IsValid()&&handle.IsDone&&ReferenceEquals(handle.Result,sprite),"genuine owned ResourceManager completed Sprite operation, no substitute asset provider");
    Set(collectable,"m_operationHandle",handle);Set(collectable,"m_refCount",2);Set(collectable,"m_loadedSprite",sprite);collectable.UnloadIcon();
    Check(handle.IsValid()&&((AsyncOperationHandle<Sprite>)Read(collectable,"m_operationHandle")).Equals(handle)&&ReferenceEquals(Read(collectable,"m_loadedSprite"),sprite)&&(int)Read(collectable,"m_refCount")==1,"actual positive count does not release or replace genuine operation");
    collectable.UnloadIcon();var retained=(AsyncOperationHandle<Sprite>)Read(collectable,"m_operationHandle");
    Check(!handle.IsValid()&&!retained.IsValid()&&retained.Equals(handle)&&!retained.Equals(default(AsyncOperationHandle<Sprite>)),"actual Addressables release invalidates operation but retains original stale handle field");
    Check(ReferenceEquals(Read(collectable,"m_loadedSprite"),null)&&(int)Read(collectable,"m_refCount")==0,"actual release clears sprite/count without destroying owned sprite");
    Check(sprite!=null&&ReferenceEquals(sprite.texture,texture),"real completed operation release does not replace/destroy owned fixture sprite");
    collectable.UnloadIcon();Check(((AsyncOperationHandle<Sprite>)Read(collectable,"m_operationHandle")).Equals(handle)&&(int)Read(collectable,"m_refCount")==0,"actual repeated invalid-handle unload clamps and retains old value");
    Set(collectable,"m_loadedSprite",sprite);Set(collectable,"m_refCount",11);string json=JsonUtility.ToJson(collectable);
    Check(!json.Contains("m_loadedSprite")&&!json.Contains("m_operationHandle")&&!json.Contains("m_refCount"),"actual original NonSerialized cache/count/handle fields excluded from JSON");
    JsonUtility.FromJsonOverwrite(json,copy);
    var copyAlternate=(AssetReferenceAtlasedSprite)Read(copy,"m_iconAlt");
    Check((int)copy.Type==72&&copy.HasTotal&&copy.DisplayFormattedString=="{1} / {0}"&&ReferenceEquals(copy.ProgressionUnlockWidget,widget),"actual authored scalar/progression widget graph JSON roundtrip");
    Check(copy.Icon.AssetGUID==icon.AssetGUID&&copy.Icon.SubObjectName=="main-icon"&&copyAlternate.AssetGUID==alternate.AssetGUID&&copyAlternate.SubObjectName=="alternate-icon","actual full atlas primary/alternate GUID and subobject strings roundtrip without load");
    Check(ReferenceEquals(Read(copy,"m_loadedSprite"),null)&&(int)Read(copy,"m_refCount")==0&&!((AsyncOperationHandle<Sprite>)Read(copy,"m_operationHandle")).IsValid(),"actual new copy retains its own native cache defaults after JSON");
    Enable(copy);Check(ReferenceEquals(Asset(copy.IconAsset),copy.Icon)&&ReferenceEquals(Asset(copy.IconAltAsset),copyAlternate),"actual explicit original OnEnable rebuilds wrappers from deserialized references");
    var abilityGroup=Own<CharacterAbilityUIDefinitionGroup>(owned);abilityGroup.m_elements=new[]{new DefinitionDataType<CharacterAbilityUIType,CharacterAbilityUIDefinition>.DefinitionElement<CharacterAbilityUIDefinition>(ability)};
    Check(ReferenceEquals(abilityGroup.GetData()[(CharacterAbilityUIType)(-71)],ability)&&ReferenceEquals(abilityGroup.GetData().Comparer,HardlightEnumComparers.CharacterAbilityUITypeComparer),"actual original ability group key/comparer/data reference");
    var collectableGroup=Own<CollectableDefinitionGroup>(owned);collectableGroup.m_elements=new[]{new DefinitionDataType<CollectableType,CollectableDefinition>.DefinitionElement<CollectableDefinition>(collectable)};
    Check(ReferenceEquals(collectableGroup.GetData()[(CollectableType)72],collectable)&&ReferenceEquals(collectableGroup.GetData().Comparer,HardlightEnumComparers.CollectableTypeComparer),"actual original collectable group key/comparer/data reference");
    return checks;
   }
   finally{try{ReleaseHandles(handles,handles.Count-1);}finally{ReleaseObjects(owned,owned.Count-1);}}
  }
  public static void Run()
  {
   int managed=RunManaged();if(managed!=29)throw new Exception("Original UI-backed managed assertion count changed");int engine=RunEngine();if(engine!=27)throw new Exception("Original UI-backed engine assertion count changed: "+engine);
   Debug.Log("Bounded original UI-backed definition checks="+(managed+engine)+" (managed="+managed+", actual Unity="+engine+"); uncached/supplied Addressable assets, original owners/layout/scopes/App/startup/fullgame remain unapproved.");
  }
 }
}
