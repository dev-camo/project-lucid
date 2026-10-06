using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // The concrete Probe is a verification-only descendant of the genuine
    // abstract base, not a replacement runtime Actor or manager. Managed checks
    // allocate raw genuine objects only on paths without Unity internal calls.
    public static class TimeScaledComponentVerification
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static int checks;
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); checks++; }
        private static FieldInfo Field(Type type, string name)
        {
            for (Type t=type;t!=null;t=t.BaseType)
            { FieldInfo field=t.GetField(name,Flags);if(field!=null)return field; }
            throw new InvalidOperationException(name);
        }
        private static object Read(object value,string name) => Field(value.GetType(),name).GetValue(value);
        private static void Write(object value,string name,object data) => Field(value.GetType(),name).SetValue(value,data);
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static SystemRef<TimeManager> Shared => (SystemRef<TimeManager>)Field(typeof(TimeScaledComponent),"m_timeManagerRef").GetValue(null);
        public sealed class Probe : TimeScaledComponent
        {
            internal List<float> Updates = new List<float>();
            internal List<string> Trace = new List<string>();
            internal bool ThrowUpdate, ThrowInitialise;
            internal TimeCategoryObject ResetCategory;
            internal UpdateOn ResetUpdate;
            protected override void InternalUpdate(float value)
            { Updates.Add(value);if(ThrowUpdate)throw new ApplicationException("update"); }
            protected override void Initialise(TimeManager manager)
            { Trace.Add(ReferenceEquals(manager,null)?"init-null":"init");if(ThrowInitialise)throw new ApplicationException("initialise"); }
            protected override TimeCategoryObject GetDefaultTimeCategory()
            { Trace.Add("category");return ResetCategory; }
            protected override UpdateOn GetDefaultUpdateOn()
            { Trace.Add(ReferenceEquals(TimeCategoryObject,ResetCategory)?"update-after-category":"update-before-category");return ResetUpdate; }
            internal void DoAwake() => base.Awake();
            internal void DoEnable() => base.OnEnable();
            internal void DoDisable() => base.OnDisable();
            internal void DoReset() => base.Reset();
            internal void DoValidate() => base.OnValidate();
            internal void DoInitialise(TimeManager manager) => base.Initialise(manager);
            internal TimeCategoryObject DefaultCategory() => base.GetDefaultTimeCategory();
            internal UpdateOn DefaultUpdate() => base.GetDefaultUpdateOn();
            internal float Total() => base.GetTotalTime();
            internal float Delta() => base.GetDeltaTime();
            internal float Scale() => base.GetTimescale();
            internal void Replace(StackableDataHandle handle,TimeSetting setting) => base.UpdateTimeSetting(handle,setting);
            internal void Remove(StackableDataHandle handle) => base.RemoveTimeSetting(handle);
        }
        private static void ReplaceSystem(SystemRef<TimeManager> shared,TimeManager manager)
        { shared.GetType().GetMethod("Hardlight.ISystemRef.InternalReplaceSystem",Flags).Invoke(shared,new object[]{manager}); }
        private static void RevokeSystem(SystemRef<TimeManager> shared)
        { shared.GetType().GetMethod("Hardlight.ISystemRef.InternalRevokeSystem",Flags).Invoke(shared,null); }
        private static Probe NewProbe()
        { var p=Raw<Probe>();p.Updates=new List<float>();p.Trace=new List<string>();return p; }
        private static TimeCategoryObject Category(string guid)
        { var c=Raw<TimeCategoryObject>();Write(c,"m_guid",guid);return c; }
        private sealed class ReferenceKeys : IEqualityComparer<TimeCategoryObject>
        {
            public bool Equals(TimeCategoryObject a,TimeCategoryObject b) => ReferenceEquals(a,b);
            public int GetHashCode(TimeCategoryObject key) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key);
        }
        // Reference keys isolate raw objects from Unity fake-null; no authored
        // object equality, constructor or native hash parity is asserted here.
        private static TimeSetting Setting(TimeCategoryObject category,float scale)
        { var value=new TimeSetting();Write(value,"m_overridesDictionary",new SerializableDictionary<TimeCategoryObject,float>(new ReferenceKeys()));value.SetCategory(category,scale);return value; }
        private static TimeManager Manager(TimeCategoryObject category)
        {
            var value=Raw<TimeManager>();Write(value,"m_timeSettings",new StackableData());
            Write(value,"m_totalTimeLookup",new Dictionary<TimeCategoryObject,float>(new ReferenceKeys()){{category,7f}});
            Write(value,"m_totalFixedTimeLookup",new Dictionary<TimeCategoryObject,float>(new ReferenceKeys()){{category,13f}});
            Write(value,"m_totalLateUpdateTimeLookup",new Dictionary<TimeCategoryObject,float>(new ReferenceKeys()){{category,19f}});
            var type=Field(typeof(TimeManager),"m_updateObjects").FieldType;
            var phases=(IDictionary)Activator.CreateInstance(type);
            foreach(var phase in new[]{UpdateOn.Update,UpdateOn.FixedUpdate,UpdateOn.LateUpdate})
                phases.Add(phase,Activator.CreateInstance(type.GetGenericArguments()[1],new ReferenceKeys()));
            Write(value,"m_updateObjects",phases);return value;
        }
        private static object AddSubscription(TimeManager manager,TimeCategoryObject category,UpdateOn phase,Probe probe)
        {
            var phases=(IDictionary)Read(manager,"m_updateObjects");var categories=(IDictionary)phases[phase];
            var list=(IList)Activator.CreateInstance(categories.GetType().GetGenericArguments()[1]);
            var sub=Activator.CreateInstance(list.GetType().GetGenericArguments()[0]);
            Write(sub,"TimeScaledObject",probe);Write(sub,"IsSubscribed",true);list.Add(sub);categories.Add(category,list);return sub;
        }
        private static void Throws<T>(Action action,string name) where T:Exception
        { try {action();}catch(T){checks++;return;}throw new InvalidOperationException(name); }
        private static List<KeyValuePair<IDictionary,List<DictionaryEntry>>> RegistrySnapshot()
        {
            var result=new List<KeyValuePair<IDictionary,List<DictionaryEntry>>>();
            foreach(string name in new[]{"s_systemDictionary","s_systemActionLookup"})
            {
                var dictionary=(IDictionary)Field(typeof(ProcessManager),name).GetValue(null);
                var rows=new List<DictionaryEntry>();foreach(DictionaryEntry row in dictionary)rows.Add(row);
                result.Add(new KeyValuePair<IDictionary,List<DictionaryEntry>>(dictionary,rows));
                foreach(var row in rows)
                {
                    var child=name=="s_systemDictionary"?(IDictionary)Read(row.Value,"SystemRefDictionary"):(IDictionary)row.Value;
                    var children=new List<DictionaryEntry>();foreach(DictionaryEntry entry in child)children.Add(entry);
                    result.Add(new KeyValuePair<IDictionary,List<DictionaryEntry>>(child,children));
                }
            }
            return result;
        }
        private static void RegistryUnchanged(List<KeyValuePair<IDictionary,List<DictionaryEntry>>> state)
        {
            foreach(var pair in state)
            {
                if(pair.Key.Count!=pair.Value.Count)throw new InvalidOperationException("ProcessManager registry changed");
                foreach(var row in pair.Value)
                    if(!pair.Key.Contains(row.Key) || !ReferenceEquals(pair.Key[row.Key],row.Value))throw new InvalidOperationException("ProcessManager reference/action row changed");
            }
        }
        public static int RunManaged()
        {
            checks=0;var shared=Shared;
            // Force the genuine readonly cctor before the baseline. Its retained
            // registry row belongs to that shared static reference; deleting it
            // would leave a stale readonly cache. No registration API is called
            // by the fixture after this point; exact registry rows are guarded.
            var registry=RegistrySnapshot();var saved=new Dictionary<FieldInfo,object>();
            foreach(var name in new[]{"m_system","m_isystem","m_actionOnSystemValid","OnSystemStartup","OnSystemShutdown"})
            {var f=Field(shared.GetType(),name);saved.Add(f,f.GetValue(shared));}
            var cache=Field(typeof(TimeManager),"s_cachedTimeSettingResult");object oldCache=cache.GetValue(null);
            var multiplies=(IDictionary)Field(typeof(StackableData),"Multiplies").GetValue(null);
            bool had=multiplies.Contains(typeof(TimeSetting));object oldMultiply=multiplies[typeof(TimeSetting)];
            try
            {
                foreach(var pair in saved)pair.Key.SetValue(shared,null);
                var category=Category("component-check");var other=Category("component-other");var manager=Manager(category);
                var probe=NewProbe();Check(ReferenceEquals(Shared,shared),"static genuine reference retained");
                Check(probe.DefaultCategory()==null && probe.DefaultUpdate()==UpdateOn.FixedUpdate,"original default hooks");
                Check(probe.CheckIsValid(),"genuine unconditional validity hook");
                probe.TimeCategoryObject=category;probe.UpdateOn=UpdateOn.LateUpdate;
                Check(ReferenceEquals(probe.TimeCategoryObject,category) && probe.UpdateOn==UpdateOn.LateUpdate,"direct authored properties");
                foreach(bool paused in new[]{false,true})
                {
                    probe.IsPaused=paused;probe.OnPause();Check(probe.IsPaused==paused,"pause hook leaves manager-owned pause flag");
                    probe.OnResume();Check(probe.IsPaused==paused,"resume hook leaves flag");
                    probe.DoValidate();Check(probe.IsPaused==paused,"genuine validation hook does not alter pause");
                }
                foreach(float delta in new[]{0f,-0f,0.125f,-2f,float.NaN,float.PositiveInfinity})
                {
                    int before=probe.Updates.Count;probe.OnUpdate(delta);probe.OnFixedUpdate(delta);probe.OnLateUpdate(delta);
                    Check(probe.Updates.Count==before+3,"all three phases dispatch exactly once");
                    for(int i=before;i<before+3;i++)Check(BitConverter.ToInt32(BitConverter.GetBytes(probe.Updates[i]),0)==BitConverter.ToInt32(BitConverter.GetBytes(delta),0),"phase forwards exact single bits");
                }
                probe.ThrowUpdate=true;
                Throws<ApplicationException>(()=>probe.OnUpdate(1f),"update exception propagates");
                Throws<ApplicationException>(()=>probe.OnFixedUpdate(1f),"fixed exception propagates");
                Throws<ApplicationException>(()=>probe.OnLateUpdate(1f),"late exception propagates");probe.ThrowUpdate=false;
                probe.ResetCategory=other;probe.ResetUpdate=(UpdateOn)12345;probe.DoReset();
                Check(string.Join(";",probe.Trace)=="category;update-after-category","default category assigned before next virtual callback");
                Check(ReferenceEquals(probe.TimeCategoryObject,other) && probe.UpdateOn==(UpdateOn)12345,"Reset preserves returned unknown phase/category");probe.Trace.Clear();
                Write(probe,"m_enabledByActivator",true);probe.DoAwake();Check((bool)Read(probe,"m_ignoreEnable"),"Awake copies activator");
                Write(probe,"m_enabledByActivator",false);probe.DoEnable();Check(!(bool)Read(probe,"m_ignoreEnable") && probe.Trace.Count==0,"one ignored enable clears latch without registration");
                probe.DoEnable();Check(probe.Trace.Count==0 && Read(shared,"m_actionOnSystemValid")!=null,"next enable queues actual SystemRef callback");
                ReplaceSystem(shared, manager);Check(string.Join(";",probe.Trace)=="init" && Read(shared,"m_actionOnSystemValid")==null,"real replacement completes pending callback once");
                probe.Trace.Clear();probe.DoEnable();Check(string.Join(";",probe.Trace)=="init","valid system invokes virtual Initialise immediately");
                probe.Trace.Clear();probe.ThrowInitialise=true;Throws<ApplicationException>(()=>probe.DoEnable(),"initialise exception propagates");
                Check(probe.Trace.Count==1,"throwing initialisation ran exactly once");probe.ThrowInitialise=false;
                probe.TimeCategoryObject=category;
                foreach(var phase in new[]{UpdateOn.Update,UpdateOn.FixedUpdate,UpdateOn.LateUpdate,UpdateOn.None,(UpdateOn)999})
                {probe.UpdateOn=phase;Check(probe.Total()==(phase==UpdateOn.FixedUpdate?13f:7f),"only exact FixedUpdate selects fixed total");}
                var op=(StackableData.ResultCarrier<TimeSetting>.Operation)typeof(TimeManager).GetMethod("TimeSettingMultiply",Flags).CreateDelegate(typeof(StackableData.ResultCarrier<TimeSetting>.Operation));
                StackableData.RegisterTypeOperations<TimeSetting>(multiply:op);cache.SetValue(null,Setting(category,0f));
                ((StackableData)Read(manager,"m_timeSettings")).SetBaseValue(0,Setting(category,2f),StackableData.RetrievalOperation.Multiply);
                Check(probe.Scale()==2f,"real base stack scale");var handle=manager.ApplyTimeSetting(Setting(category,0.5f));
                Check(probe.Scale()==1f,"real override multiplication");probe.Replace(handle,Setting(category,4f));Check(probe.Scale()==8f,"base replaces genuine handle");
                probe.Replace(null,null);Check(probe.Scale()==8f,"null handle skips setting access after required receiver resolution");
                probe.Remove(handle);Check(probe.Scale()==2f,"base removes override");probe.Remove(handle);probe.Remove(null);Check(probe.Scale()==2f,"repeat/null removal preserves genuine stack");
                probe.UpdateOn=UpdateOn.Update;var sub=AddSubscription(manager,category,UpdateOn.Update,probe);probe.DoDisable();Check(!(bool)Read(sub,"IsSubscribed"),"disable delegates real Shutdown/unsubscription");
                Write(sub,"IsSubscribed",true);probe.OnDestroy();Check(!(bool)Read(sub,"IsSubscribed"),"destroy delegates real Shutdown");
                RevokeSystem(shared);Write(sub,"IsSubscribed",true);probe.DoDisable();probe.OnDestroy();Check((bool)Read(sub,"IsSubscribed"),"invalid reference leaves prior subscription unchanged");
                Write(probe,"m_ignoreEnable",true);Write(probe,"m_enabledByActivator",false);probe.DoAwake();Check(!(bool)Read(probe,"m_ignoreEnable"),"Awake false overwrites prior latch");
                return checks;
            }
            finally
            {
                foreach(var pair in saved)pair.Key.SetValue(shared,pair.Value);
                cache.SetValue(null,oldCache);if(had)multiplies[typeof(TimeSetting)]=oldMultiply;else multiplies.Remove(typeof(TimeSetting));
                RegistryUnchanged(registry);
            }
        }
        // Live Unity constructor/time-getter checks deliberately use the same
        // real manager's pure stack, not a synthetic callback or timer. Full
        // TimeManager Awake/registration is covered separately by its fixture;
        // this does not claim Actor lifecycle or asset/startup/gameplay parity.
        private static int EngineChecks()
        {
            int before=checks;var shared=Shared;var systemField=Field(shared.GetType(),"m_system");var untypedField=Field(shared.GetType(),"m_isystem");
            object oldSystem=systemField.GetValue(shared),oldUntyped=untypedField.GetValue(shared);
            var cache=Field(typeof(TimeManager),"s_cachedTimeSettingResult");object oldCache=cache.GetValue(null);
            var operations=(IDictionary)Field(typeof(StackableData),"Multiplies").GetValue(null);bool had=operations.Contains(typeof(TimeSetting));object prior=operations[typeof(TimeSetting)];
            GameObject host=null;TimeCategoryObject category=null;
            try
            {
                category=ScriptableObject.CreateInstance<TimeCategoryObject>();Write(category,"m_guid","component-engine");
                host=new GameObject("OriginalTimeScaledComponentFixture");host.SetActive(false);var probe=host.AddComponent<Probe>();probe.Updates=new List<float>();probe.Trace=new List<string>();
                // Editor Reset may have used the virtual fields' null/zero
                // defaults. Constructor default is checked separately via Cecil;
                // reset here invokes the original base default directly.
                Check(probe.DefaultUpdate()==UpdateOn.FixedUpdate && probe.DefaultCategory()==null,"live genuine default hooks");
                Check(probe.CheckIsValid() && !probe.IsPaused,"owned actual MonoBehaviour initial pause/validity");
                var manager=Manager(category);systemField.SetValue(shared,manager);untypedField.SetValue(shared,manager);
                var op=(StackableData.ResultCarrier<TimeSetting>.Operation)typeof(TimeManager).GetMethod("TimeSettingMultiply",Flags).CreateDelegate(typeof(StackableData.ResultCarrier<TimeSetting>.Operation));
                StackableData.RegisterTypeOperations<TimeSetting>(multiply:op);cache.SetValue(null,Setting(category,0f));((StackableData)Read(manager,"m_timeSettings")).SetBaseValue(0,Setting(category,3f),StackableData.RetrievalOperation.Multiply);
                probe.TimeCategoryObject=category;
                foreach(var phase in new[]{UpdateOn.FixedUpdate,UpdateOn.Update,UpdateOn.LateUpdate,(UpdateOn)987})
                {
                    probe.UpdateOn=phase;float expected=BitConverter.ToSingle(BitConverter.GetBytes((phase==UpdateOn.FixedUpdate?Time.fixedDeltaTime:Time.deltaTime)*3f),0);
                    Check(probe.Delta()==expected,"live engine getter exact fixed-vs-frame branch with single expectation");
                    Check(probe.Total()==(phase==UpdateOn.FixedUpdate?13f:7f),"owned category real totals branch");
                }
                probe.UpdateOn=UpdateOn.None;probe.DoInitialise(manager);Check(((IDictionary)Read(manager,"m_updateObjects")).Count==3,"None initialisation delegates genuine early return");
            }
            finally
            {
                // Clear callback-visible refs before destruction so a destroyed
                // category is never traversed by the original Shutdown boundary.
                systemField.SetValue(shared,null);untypedField.SetValue(shared,null);
                try {if(host!=null)UnityEngine.Object.DestroyImmediate(host);}
                finally
                {try {if(category!=null)UnityEngine.Object.DestroyImmediate(category);}
                 finally {systemField.SetValue(shared,oldSystem);untypedField.SetValue(shared,oldUntyped);cache.SetValue(null,oldCache);if(had)operations[typeof(TimeSetting)]=prior;else operations.Remove(typeof(TimeSetting));}}
            }
            return checks-before;
        }
        public static int Run() { int managed=RunManaged();return managed+EngineChecks(); }
    }
}
