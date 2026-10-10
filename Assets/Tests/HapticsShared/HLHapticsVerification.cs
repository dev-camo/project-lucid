using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;
namespace ProjectLucid.Verification
{
 public static partial class HLHapticsVerification
 {
  sealed class ObjectSnapshot
  {
   readonly List<Action> checks=new List<Action>();readonly HashSet<object> seen=new HashSet<object>(ReferenceComparer.Instance);readonly HashSet<string> excluded;
   public ObjectSnapshot(object root,params string[] excludedFields){excluded=new HashSet<string>(excludedFields);Capture(root);}
   void Capture(object value)
   {
    if(value==null||!seen.Add(value))return;
    if(value is IList list)
    {object[] copy=new object[list.Count];list.CopyTo(copy,0);checks.Add(()=>{Require(list.Count==copy.Length,"owned array/list length");for(int i=0;i<copy.Length;i++)Require(SameOwnedValue(list[i],copy[i]),"owned ordered array/list identity/value");});foreach(object entry in copy)if(entry is VibrationEventData.Vibrations)Capture(entry);return;}
    Type type=value.GetType();Require(type==typeof(HLHapticsConfigurationAsset)||type==typeof(VibrationEventData)||type==typeof(VibrationEventData.Vibrations)||type==typeof(VibrationManager),"exact actual owned original type");
    for(Type owner=type;owner==type||owner==typeof(SystemConfigurationAsset);owner=owner.BaseType)
     foreach(FieldInfo field in owner.GetFields(Declared).Where(x=>!x.IsStatic))
     {if(excluded.Contains(field.Name))continue;object original=field.GetValue(value);checks.Add(()=>Require(SameOwnedValue(field.GetValue(value),original),"owned exact field "+field.Name));if(original is IList)Capture(original);}
   }
   public void Check(){foreach(Action check in checks)check();}
  }
  static bool SameOwnedValue(object a,object b)
  {if(a is float fa&&b is float fb)return BitConverter.ToInt32(BitConverter.GetBytes(fa),0)==BitConverter.ToInt32(BitConverter.GetBytes(fb),0);return Same(a,b);}
  static T OwnAsset<T>(List<UnityEngine.Object> objects) where T:ScriptableObject
  {T value=ScriptableObject.CreateInstance<T>();Require(value!=null,"genuine preconstructed owned asset");objects.Add(value);return value;}
  static void WithAssets(Action<List<UnityEngine.Object>> body)
  {
   CheckWholeContract();var objects=new List<UnityEngine.Object>();var errors=new List<Exception>();try{body(objects);}catch(Exception e){errors.Add(e);}foreach(UnityEngine.Object value in objects)try{UnityEngine.Object.DestroyImmediate(value);}catch(Exception e){errors.Add(e);}if(errors.Count!=0)throw new AggregateException("Original owned asset body and independent releases",errors);
  }
  static void Expect<T>(Action action) where T:Exception
  {Exception observed=null;try{action();}catch(Exception e){observed=e;}Require(observed!=null&&observed.GetType()==typeof(T),"prospective genuine CLR exact fault "+typeof(T)+"; observed "+(observed==null?"none":observed.GetType().ToString()));}
  static FieldInfo ConfigField(string name)=>typeof(HLHapticsConfigurationAsset).GetField(name,Declared);
  static void CheckFileSet(HLHapticsConfigurationAsset asset,string[] values)
  {
   var before=new ObjectSnapshot(asset);IEnumerable<string> result=asset.GetIncludedNativeFiles();Require(result!=null&&result.GetType()==typeof(HashSet<string>),"genuine new HashSet result");var actual=(HashSet<string>)result;Require(actual.Count==values.Length&&values.All(actual.Contains),"exact file set membership/count without enumeration order claim");before.Check();IEnumerable<string> other=asset.GetIncludedNativeFiles();Require(!ReferenceEquals(result,other),"independent returned collections");before.Check();
  }
  public static void ConfigurationDefaultsAndLiteralNativeFiles()
  {
   WithAssets(objects=>{HLHapticsConfigurationAsset asset=OwnAsset<HLHapticsConfigurationAsset>(objects);string[] required=(string[])ConfigField("m_requiredNativeFiles").GetValue(asset),included=(string[])ConfigField("m_includedNativeFiles").GetValue(asset);Require(required.Length==2&&required[0]=="VibrationController.h"&&required[1]=="VibrationController.mm"&&included!=null&&included.Length==0&&asset.GetNativeCodePath()=="/NativeiOS\\~/","original configuration constructor fields/literal path");var snapshot=new ObjectSnapshot(asset);asset.Validate();snapshot.Check();CheckFileSet(asset,new[]{"VibrationController.h","VibrationController.mm"});snapshot.Check();});
  }
  public static void ConfigurationJsonDuplicatesAndPrivateSerializedPath()
  {
   WithAssets(objects=>{HLHapticsConfigurationAsset asset=OwnAsset<HLHapticsConfigurationAsset>(objects);string[] required=(string[])ConfigField("m_requiredNativeFiles").GetValue(asset);string first=required[0],second=required[1];
    // Genuine Unity 2022.3 JSON maps these null strings to empty strings; original null-source behavior remains held.
    JsonUtility.FromJsonOverwrite("{\"m_includedNativeFiles\":[\"VibrationController.h\",\"Extra\",\"Extra\",null,\"VibrationController.mm\"],\"m_nativeCodePath\":\"/owned\\\\mixed/path\"}",asset);
    string[] included=(string[])ConfigField("m_includedNativeFiles").GetValue(asset);Require(included!=null&&included.Length==5&&included[0]=="VibrationController.h"&&included[1]=="Extra"&&included[2]=="Extra"&&included[3]==""&&included[4]=="VibrationController.mm","genuine observed Unity 2022.3 JSON private array representation/order/duplicates");Require(asset.GetNativeCodePath()=="/owned\\mixed/path","authentic mixed path JSON");CheckFileSet(asset,new[]{"VibrationController.h","VibrationController.mm","Extra",""});
    foreach(string json in new[]{"{\"m_nativeCodePath\":\"\"}","{\"m_nativeCodePath\":null}"})
    {JsonUtility.FromJsonOverwrite(json,asset);var snap=new ObjectSnapshot(asset);Require(asset.GetNativeCodePath()=="","genuine observed Unity 2022.3 JSON empty/null string path representation");asset.Validate();snap.Check();}
    Require(ReferenceEquals(ConfigField("m_requiredNativeFiles").GetValue(asset),required)&&ReferenceEquals(required[0],first)&&ReferenceEquals(required[1],second),"readonly required array/string identities unchanged");
    // Whole null included-array calls HLOutput.LogError: no ownership-qualified shared logger lease exists, so that branch stays held.
   });
  }
  public static void EventDataAndNestedVibrationsJsonRoundTrip()
  {
   WithAssets(objects=>{VibrationEventData data=OwnAsset<VibrationEventData>(objects),copy=OwnAsset<VibrationEventData>(objects);Require(data.m_vibrationEvent==VibrationEvent.None&&data.m_vibrationEvents==null,"original real asset defaults");var zero=new VibrationEventData.Vibrations();Require(zero.m_delay==0f&&zero.m_repeat==0f&&(int)zero.m_vibrationType==0,"original reference-entry constructor defaults");
    data.m_vibrationEvent=VibrationEvent.LongPressSelection;data.m_vibrationEvents=new[]{new VibrationEventData.Vibrations{m_delay=0.03f,m_repeat=1.5f,m_vibrationType=VibrationType.ImpactLight},new VibrationEventData.Vibrations{m_delay=-1f,m_repeat=-1f,m_vibrationType=VibrationType.None}};var snapshot=new ObjectSnapshot(data);string json=JsonUtility.ToJson(data);snapshot.Check();JsonUtility.FromJsonOverwrite(json,copy);Require(copy.m_vibrationEvent==data.m_vibrationEvent&&copy.m_vibrationEvents!=null&&copy.m_vibrationEvents.Length==2&&!ReferenceEquals(copy.m_vibrationEvents,data.m_vibrationEvents),"genuine preconstructed asset JSON enum/array order");
    for(int i=0;i<2;i++){VibrationEventData.Vibrations a=data.m_vibrationEvents[i],b=copy.m_vibrationEvents[i];Require(b!=null&&!ReferenceEquals(a,b)&&SameOwnedValue(a.m_delay,b.m_delay)&&SameOwnedValue(a.m_repeat,b.m_repeat)&&a.m_vibrationType==b.m_vibrationType,"authentic serialized reference entries/float bits/enum");}snapshot.Check();
    data.m_vibrationEvents[1]=null;new ObjectSnapshot(data).Check();
    // Null inline-class and NaN JSON representations remain pending actual Unity observation; neither is invented or passed via a replacement DTO.
   });
  }
  public static void InactiveManagerOwnedLifecycleDefaults()
  {
   WithManager(lease=>{var stable=new ObjectSnapshot(lease.Manager,"m_vibrationEnabledInSettings");lease.Manager.SetVibrationFromSettings(true);Require(lease.Manager.VibrationEnabledInSettings,"genuine public settings setter true");stable.Check();lease.CheckShared();lease.Manager.SetVibrationFromSettings(false);Require(!lease.Manager.VibrationEnabledInSettings,"genuine public settings setter false");stable.Check();lease.CheckShared();var all=new ObjectSnapshot(lease.Manager);lease.Manager.TriggerEvent(VibrationEvent.None);all.Check();lease.CheckShared();Expect<NullReferenceException>(()=>lease.Manager.TriggerEvent((VibrationEventData)null));all.Check();lease.CheckShared();});
  }
  public static void ManagerAwakeDebugSnapshotAndSafeTriggerOrder()
  {
   WithManager(lease=>{lease.Activate(true);var stable=new ObjectSnapshot(lease.Manager);lease.Manager.TriggerEvent(VibrationEvent.None);lease.Manager.TriggerEvent(VibrationEvent.DefaultSelection);stable.Check();lease.CheckShared();lease.Manager.SetVibrationFromSettings(true);stable=new ObjectSnapshot(lease.Manager);lease.Manager.TriggerEvent(VibrationEvent.None);Expect<NullReferenceException>(()=>lease.Manager.TriggerEvent(VibrationEvent.DefaultSelection));Expect<NullReferenceException>(()=>lease.Manager.TriggerEvent((VibrationEventData)null));stable.Check();lease.CheckShared();
    WithAssets(objects=>{VibrationEventData data=OwnAsset<VibrationEventData>(objects);data.m_vibrationEvent=VibrationEvent.DefaultSelection;data.m_vibrationEvents=new[]{new VibrationEventData.Vibrations{m_repeat=2f,m_delay=0f,m_vibrationType=VibrationType.Selection}};var ds=new ObjectSnapshot(data);Expect<NullReferenceException>(()=>lease.Manager.TriggerEvent(data));ds.Check();stable.Check();lease.CheckShared();});
   });
   WithManager(lease=>{lease.Activate(false);lease.Manager.SetVibrationFromSettings(true);var stable=new ObjectSnapshot(lease.Manager);lease.Manager.TriggerEvent(VibrationEvent.DefaultSelection);Require(!lease.Manager.IsAvailable,"settings cannot change original unavailable feature");stable.Check();lease.CheckShared();});
  }
  static void CheckFiniteInput(VibrationEventData.Vibrations[] values)
  {
   Require(values!=null&&values.Length<=4,"bounded finite owned array");foreach(VibrationEventData.Vibrations value in values)Require(value!=null&&!float.IsInfinity(value.m_repeat)&&!(value.m_repeat>3f)&&!(value.m_repeat<-2f)&&!float.IsInfinity(value.m_delay)&&!(value.m_delay>0.1f),"exclude infinite/huge/overflow input before natural iterator allocation");
  }
  static IEnumerator NaturalIterator(ManagerLease lease,VibrationEventData.Vibrations[] values)
  {
   MethodInfo method=typeof(VibrationManager).GetMethods(Declared).Single(x=>x.Name=="Vibrate"&&x.GetParameters().Length==1&&x.GetParameters()[0].ParameterType==typeof(VibrationEventData.Vibrations[]));IEnumerator iterator=(IEnumerator)method.Invoke(lease.Manager,new object[]{values});Require(iterator!=null&&iterator.GetType().FullName.Replace('+','/')=="Hardlight.VibrationManager/<Vibrate>d__13"&&iterator.GetType().Assembly==typeof(VibrationManager).Assembly,"actual original wrapper allocated complete natural iterator");lease.Track(iterator);Require(ReferenceEquals(iterator.GetType().GetField("vibrations",Declared).GetValue(iterator),values)&&ReferenceEquals(iterator.GetType().GetField("<>4__this",Declared).GetValue(iterator),lease.Manager),"exact owned natural argument/manager identities");return iterator;
  }
  public static void NaturalIteratorNonpositiveAndNullBoundaries()
  {
   WithManager(lease=>{var manager=new ObjectSnapshot(lease.Manager);foreach(float repeat in new[]{-1f,float.NaN,0f,1f,1.5f})foreach(float delay in new[]{0f,-1f,float.NaN})
    {var values=new[]{new VibrationEventData.Vibrations{m_repeat=repeat,m_delay=delay,m_vibrationType=VibrationType.Selection}};CheckFiniteInput(values);var snapshot=new ObjectSnapshot(values);IEnumerator iterator=NaturalIterator(lease,values);Require(iterator.Current==null&&((IEnumerator<object>)iterator).Current==null,"actual both Current defaults");Expect<NotSupportedException>(()=>iterator.Reset());Require(!iterator.MoveNext()&&!iterator.MoveNext()&&iterator.Current==null,"bounded original no-positive-wait termination; no pulse count claim");snapshot.Check();manager.Check();lease.CheckShared();}
    IEnumerator empty=NaturalIterator(lease,new VibrationEventData.Vibrations[0]);Require(!empty.MoveNext(),"empty original array terminal");IEnumerator missing=NaturalIterator(lease,null);Expect<NullReferenceException>(()=>missing.MoveNext());IEnumerator entry=NaturalIterator(lease,new VibrationEventData.Vibrations[]{null});Expect<NullReferenceException>(()=>entry.MoveNext());manager.Check();lease.CheckShared();
   });
  }
  sealed class Observation {public bool Completed;public Exception Error;public int Yields,Resumed;public readonly List<int> Indices=new List<int>(),Repeats=new List<int>();}
  static bool ObserveStep(ManagerLease lease,IEnumerator iterator,VibrationEventData.Vibrations[] values,Observation state)
  {
   try
   {
    lease.CheckShared();var manager=new ObjectSnapshot(lease.Manager);var array=new ObjectSnapshot(values);bool more=iterator.MoveNext();manager.Check();array.Check();Require(ReferenceEquals(iterator.GetType().GetField("vibrations",Declared).GetValue(iterator),values),"captured original array retained across actual resumes");
    if(!more){state.Completed=true;return false;}Require(state.Yields<8&&iterator.Current is Coroutine,"bounded exact actual child Coroutine yield");lease.Track((Coroutine)iterator.Current);state.Yields++;state.Indices.Add((int)iterator.GetType().GetField("<j>5__2",Declared).GetValue(iterator));state.Repeats.Add((int)iterator.GetType().GetField("<repeatCount>5__3",Declared).GetValue(iterator));return true;
   }catch(Exception error){state.Error=error;state.Completed=true;return false;}
  }
  static IEnumerator ObserveOriginal(ManagerLease lease,IEnumerator iterator,VibrationEventData.Vibrations[] values,Observation state,Action<int> afterWait)
  {
   while(ObserveStep(lease,iterator,values,state))
   {
    float started=Time.realtimeSinceStartup;object current=iterator.Current;
    yield return current; // The owned manager outer coroutine yields the exact original child handle. The UnityTest polls with its own genuine real-time yield.
    try{Require(Time.realtimeSinceStartup>started,"genuine positive wait elapsed; no exact frame/tolerance claim");state.Resumed++;afterWait?.Invoke(state.Resumed);lease.CheckShared();}catch(Exception error){state.Error=error;state.Completed=true;}
    if(state.Completed)yield break;
   }
  }
  static IEnumerator WatchOwned(ManagerLease lease,IEnumerator iterator,VibrationEventData.Vibrations[] values,Action<int> afterWait,Action<Observation> verify)
  {
   var state=new Observation();Exception primary=null;float deadline=Time.realtimeSinceStartup+5f;int frames=0;
   try
   {
    try{lease.Track(lease.Manager.StartCoroutine(ObserveOriginal(lease,iterator,values,state,afterWait)));}catch(Exception e){primary=e;}
    while(primary==null&&!state.Completed)
    {
     try{Require(Application.isPlaying&&Time.realtimeSinceStartup<=deadline&&++frames<=600,"independent genuine PlayMode watchdog deadline/frame bound");lease.CheckShared();}catch(Exception e){primary=e;break;}
     yield return new WaitForSecondsRealtime(0.01f);
    }
    if(primary==null)try{Require(Application.isPlaying&&Time.realtimeSinceStartup<=deadline,"independent genuine PlayMode terminal deadline bound");verify(state);lease.CheckShared();}catch(Exception e){primary=e;}
   }
   finally{lease.CloseWith(primary);}
  }
  static IEnumerator DeferredOwnedCase(Func<ManagerLease,IEnumerator> factory)
  {
   ManagerLease lease=null;IEnumerator inner=null;Exception primary=null;
   try
   {
    try{CheckWholeContract();Require(Application.isPlaying,"SETUP HOLD: original positive Coroutine waits require an already genuine PlayMode host; current EditMode is not qualified");lease=ManagerLease.Acquire();inner=factory(lease);Require(inner!=null,"actual owned case iterator");}catch(Exception error){primary=error;}
    while(primary==null&&inner!=null)
    {
     bool more=false;object current=null;
     try{more=inner.MoveNext();if(more)current=inner.Current;}catch(Exception error){primary=error;}
     if(primary!=null||!more)break;
     yield return current;
    }
   }
   finally
   {
    if(inner is IDisposable disposable)try{disposable.Dispose();}catch(Exception error){primary=primary==null?error:new AggregateException("First owned case and inner-disposal observations",primary,error);}
    if(lease!=null)lease.CloseWith(primary);else if(primary!=null)throw primary;
   }
  }
  public static IEnumerator NaturalIteratorFinitePositiveWaitRepeatCounts() => DeferredOwnedCase(lease=>
  {
   lease.Activate(true);var values=new[]{new VibrationEventData.Vibrations{m_repeat=0f,m_delay=0.03f,m_vibrationType=VibrationType.ImpactLight},new VibrationEventData.Vibrations{m_repeat=1f,m_delay=0.03f,m_vibrationType=VibrationType.ImpactMedium},new VibrationEventData.Vibrations{m_repeat=1.5f,m_delay=0.03f,m_vibrationType=VibrationType.ImpactHeavy}};CheckFiniteInput(values);IEnumerator iterator=NaturalIterator(lease,values);return WatchOwned(lease,iterator,values,null,state=>Require(state.Error==null&&state.Completed&&state.Yields==6&&state.Resumed==6&&state.Indices.SequenceEqual(new[]{0,1,1,2,2,2})&&state.Repeats.SequenceEqual(new[]{0,0,1,0,1,2}),"complete finite/fractional actual positive-wait repeat sequence"));
  });
  public static IEnumerator NaturalIteratorLiveOwnedArrayAfterPositiveWait() => DeferredOwnedCase(lease=>
  {
   lease.Activate(true);VibrationEventData data=OwnAsset<VibrationEventData>(lease.Assets);var values=new[]{new VibrationEventData.Vibrations{m_repeat=1f,m_delay=0.03f,m_vibrationType=VibrationType.Selection},new VibrationEventData.Vibrations{m_repeat=-1f,m_delay=0f},new VibrationEventData.Vibrations{m_repeat=-1f,m_delay=0f}};data.m_vibrationEvents=values;CheckFiniteInput(values);IEnumerator iterator=NaturalIterator(lease,values);
   return WatchOwned(lease,iterator,values,resumed=>{if(resumed==1){values[0].m_repeat=-1f;values[0].m_delay=0f;values[1]=new VibrationEventData.Vibrations{m_repeat=0f,m_delay=0.03f,m_vibrationType=VibrationType.ImpactLight};data.m_vibrationEvents=new[]{new VibrationEventData.Vibrations{m_repeat=-1f,m_delay=0f}};Require(!ReferenceEquals(data.m_vibrationEvents,values)&&ReferenceEquals(iterator.GetType().GetField("vibrations",Declared).GetValue(iterator),values),"public asset replacement cannot retarget captured argument");}else if(resumed==2){values[2]=null;}else throw new InvalidOperationException("Unexpected owned mutation step");},state=>Require(state.Completed&&state.Error!=null&&state.Error.GetType()==typeof(NullReferenceException)&&state.Yields==2&&state.Resumed==2&&state.Indices.SequenceEqual(new[]{0,1})&&state.Repeats.SequenceEqual(new[]{0,0}),"live reread ordering and prospective managed null-entry fault after real waits; original IL2CPP malformed behavior remains held"));
  });
 }
}
