using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;
using UnityEngine.Pool;
using Unity.IL2CPP.CompilerServices;
using UObj = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    public static class InstancedPoolingVerification
    {
        static void Check(bool ok, string name, ref int checks) { if (!ok) throw new InvalidOperationException(name); checks++; }
        static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) ?? throw new MissingFieldException(type.FullName, name);
        static Dictionary<int,ObjectPool<Mesh>> Pools(InstancedObjectPool<Mesh> p) => (Dictionary<int,ObjectPool<Mesh>>)Field(typeof(InstancedObjectPool<Mesh>), "m_pools").GetValue(p);
        static void Throws<T>(Action action, string name, ref int checks) where T : Exception
        {
            try { action(); } catch(T) { checks++; return; } throw new InvalidOperationException(name);
        }
        // This path exercises only managed routing and the real installed Unity ObjectPool.
        // Uninitialized Mesh instances are identity-only test values; original registration/cloning/destruction stays in engine fixtures.
        public static int RunManaged()
        {
            int n=0; var p=new InstancedObjectPool<Mesh>(); var dict=Pools(p);
            Check(dict!=null && dict.Count==0,"constructor owns an empty real typed dictionary",ref n);
            Check(Field(typeof(InstancedObjectPool<Mesh>),"m_pools").IsInitOnly,"pool dictionary retains readonly declaration",ref n);
            Mesh marker=(Mesh)FormatterServices.GetUninitializedObject(typeof(Mesh)); Mesh other=(Mesh)FormatterServices.GetUninitializedObject(typeof(Mesh)); Mesh output=marker;
            Check(!p.Get(9,out output),"missing get false",ref n); Check(ReferenceEquals(output,null),"missing get clears out",ref n);
            p.Release(9,marker); Check(dict.Count==0,"unknown release never creates pool",ref n);
            object[] args={12,null}; Check(!(bool)typeof(InstancedObjectPool<Mesh>).GetMethod("TryGetPool",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,args),"private missing lookup false",ref n); Check(args[1]==null,"private lookup null out",ref n);
            var trace=new List<string>(); int creates=0;
            var pool=new ObjectPool<Mesh>(()=>{creates++;trace.Add("create");return marker;},x=>trace.Add("get"),x=>trace.Add("release"),x=>trace.Add("destroy"),true,0,2);dict.Add(1,pool);
            Check(p.Get(1,out output),"registered get true",ref n); Check(ReferenceEquals(output,marker),"get exact factory output",ref n);Check(creates==1 && string.Join(",",trace)=="create,get","real factory precedes get callback",ref n);
            Check(pool.CountActive==1 && pool.CountInactive==0,"real pool active counters",ref n);
            p.Release(1,output); Check(pool.CountInactive==1,"release routed to actual pool",ref n);Check(string.Join(",",trace)=="create,get,release","release callback order",ref n);
            Check(p.Get(1,out output)&&ReferenceEquals(output,marker),"inactive value reused",ref n);Check(creates==1,"reused get avoids factory",ref n);
            p.Release(99,output);Check(pool.CountActive==1,"wrong key release leaves actual pool alone",ref n);
            p.Release(1,output);Throws<InvalidOperationException>(()=>p.Release(1,output),"duplicate release preserves collection check",ref n);
            var nullPool=new ObjectPool<Mesh>(()=>null,null,null,null,true,0,2);dict.Add(2,nullPool);
            output=marker;Check(p.Get(2,out output),"factory null still successful found get",ref n);Check(ReferenceEquals(output,null),"factory null assigned directly",ref n);
            var boom=new InvalidOperationException("factory sentinel");dict.Add(3,new ObjectPool<Mesh>(()=>throw boom,null,null,null,true,0,2));output=other;
            Exception actual=null;try{p.Get(3,out output);}catch(Exception e){actual=e;}
            Check(ReferenceEquals(actual,boom),"factory exception passes unchanged",ref n);Check(ReferenceEquals(output,other),"failed factory leaves caller out untouched",ref n);
            dict.Add(4,null);output=marker;Throws<NullReferenceException>(()=>p.Get(4,out output),"null stored pool get faults",ref n);Check(ReferenceEquals(output,marker),"null stored pool fault before out assignment",ref n);Throws<NullReferenceException>(()=>p.Release(4,marker),"null stored pool release faults",ref n);
            var callbackBoom=new InvalidOperationException("release sentinel");dict.Add(5,new ObjectPool<Mesh>(()=>marker,null,x=>throw callbackBoom,null,true,0,2));actual=null;try{p.Release(5,marker);}catch(Exception e){actual=e;}Check(ReferenceEquals(actual,callbackBoom),"release callback failure passes unchanged",ref n);
            var destroyTrace=new List<Mesh>();var bounded=new ObjectPool<Mesh>(()=>marker,null,null,x=>destroyTrace.Add(x),false,0,1);dict.Add(6,bounded);p.Release(6,marker);p.Release(6,other);Check(bounded.CountInactive==1,"real maximum retains first value",ref n);Check(destroyTrace.Count==1 && ReferenceEquals(destroyTrace[0],other),"real maximum sends second value to destroy callback",ref n);
            var manager=(InstancedObjectPoolManager)FormatterServices.GetUninitializedObject(typeof(InstancedObjectPoolManager));Field(typeof(InstancedObjectPoolManager),"m_meshPool").SetValue(manager,p);
            Check(manager.GetMesh(1,out output)&&ReferenceEquals(output,marker),"manager get forwards actual id and out",ref n);manager.ReleaseMesh(1,output);Check(pool.CountInactive==1,"manager release forwards actual id and value",ref n);output=marker;Check(!manager.GetMesh(-99,out output)&&ReferenceEquals(output,null),"manager missing get forwards failure/out",ref n);
            Check(typeof(ISystem).IsAssignableFrom(typeof(InstancedObjectPoolManager)),"original marker contract retained",ref n);
            Check(Field(typeof(InstancedObjectPoolManager),"m_meshPool").IsInitOnly,"manager pool readonly declaration",ref n);
            var capacity=Field(typeof(InstancedObjectPoolManager),"m_defaultMeshCapacity");Check(capacity.GetCustomAttribute<SerializeField>()!=null,"capacity SerializeField",ref n);Check(capacity.GetCustomAttribute<MinAttribute>().min==0,"capacity original Min0",ref n);
            foreach(Type type in new[]{typeof(InstancedObjectPool<>),typeof(InstancedObjectPoolManager)})
            {
                var attrs=type.GetCustomAttributes<Il2CppSetOptionAttribute>();int a=0;foreach(var v in attrs){Check((v.Option==Option.ArrayBoundsChecks || v.Option==Option.NullChecks) && (bool)v.Value==false,"exact disabled original IL2CPP option",ref n);a++;}Check(a==2,"two original IL2CPP options",ref n);
            }
            var typeArg=typeof(InstancedObjectPool<>).GetGenericArguments()[0];Check(typeArg.GenericParameterAttributes==GenericParameterAttributes.None,"original generic flags0",ref n);Check(typeArg.GetGenericParameterConstraints().Length==1&&typeArg.GetGenericParameterConstraints()[0]==typeof(UObj),"original Unity Object constraint",ref n);
            return n;
        }
        // All objects and the ProcessManager dictionary lease belong to this fixture.
        // Editor fixtures invoke original lifecycle entries explicitly; automatic Awake/OnDestroy requires the frame fixture.
        // Original Destroy is validated separately on a genuine PlayMode frame, not replaced for Editor tests.
        public static int RunEngine()
        {
            int n=0; var registry=typeof(ProcessManager).GetField("s_systemDictionary",BindingFlags.Static|BindingFlags.NonPublic);object prior=registry.GetValue(null);object fresh=Activator.CreateInstance(registry.FieldType);
            GameObject go=null;Mesh template=null,clone=null,second=null,invalidCapacityTemplate=null;registry.SetValue(null,fresh);
            try
            {
                go=new GameObject("Project Lucid instanced pooling verification");var manager=go.AddComponent<InstancedObjectPoolManager>();
                typeof(InstancedObjectPoolManager).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
                Check(Field(typeof(InstancedObjectPoolManager),"m_defaultMeshCapacity").GetValue(manager).Equals(10),"native manager default capacity10",ref n);
                var p=(InstancedObjectPool<Mesh>)Field(typeof(InstancedObjectPoolManager),"m_meshPool").GetValue(manager);Check(p!=null&&Pools(p).Count==0,"native manager constructor owns empty pool",ref n);
                Check(ReferenceEquals(ProcessManager.GetSystemRef<InstancedObjectPoolManager>().Get(),manager),"original Awake entry registers genuine manager",ref n);
                template=new Mesh {name="Original pooling template",vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2}};
                int id=manager.RegisterMesh(template);Check(id==template.GetInstanceID(),"Register returns actual instanceID",ref n);Check(Pools(p).Count==1,"first registration adds exactly one pool",ref n);
                var firstPool=Pools(p)[id];
                Check((bool)Field(firstPool.GetType(),"m_CollectionCheck").GetValue(firstPool),"registration enables real collection check",ref n);
                Check((int)Field(firstPool.GetType(),"m_MaxSize").GetValue(firstPool)==10000,"registration original maximum10000",ref n);
                Check(Field(firstPool.GetType(),"m_ActionOnGet").GetValue(firstPool)==null&&Field(firstPool.GetType(),"m_ActionOnRelease").GetValue(firstPool)==null,"registration original absent get/release hooks",ref n);
                var destroy=(Action<Mesh>)Field(firstPool.GetType(),"m_ActionOnDestroy").GetValue(firstPool);Check(destroy.Target==null&&destroy.Method.DeclaringType==typeof(UObj)&&destroy.Method.Name=="Destroy"&&destroy.Method.GetParameters().Length==1&&destroy.Method.GetParameters()[0].ParameterType==typeof(UObj),"original static Destroy(Object) action",ref n);
                Field(typeof(InstancedObjectPoolManager),"m_defaultMeshCapacity").SetValue(manager,-1);Check(manager.RegisterMesh(template)==id,"duplicate registration returns same key even with invalid new capacity",ref n);Check(ReferenceEquals(firstPool,Pools(p)[id]),"duplicate preserves first pool",ref n);
                invalidCapacityTemplate=new Mesh();Throws<ArgumentOutOfRangeException>(()=>manager.RegisterMesh(invalidCapacityTemplate),"new negative capacity follows actual List capacity failure",ref n);Check(Pools(p).Count==1&&!Pools(p).ContainsKey(invalidCapacityTemplate.GetInstanceID()),"failed pool construction never registers a row",ref n);
                Check(manager.GetMesh(id,out clone),"engine get succeeds",ref n);Check(clone!=null&&!ReferenceEquals(clone,template),"real factory clones a Mesh",ref n);Check(clone.vertexCount==3&&clone.triangles.Length==3,"clone retains source geometry",ref n);
                clone.name="Changed clone";Check(template.name=="Original pooling template","clone independent source identity",ref n);
                manager.ReleaseMesh(id,clone);Check(firstPool.CountInactive==1,"released clone retained",ref n);Mesh reused;Check(manager.GetMesh(id,out reused)&&ReferenceEquals(clone,reused),"same released clone reused",ref n);manager.ReleaseMesh(id,reused);
                Check(manager.GetMesh(id,out reused)&&ReferenceEquals(clone,reused),"repeated reuse same clone",ref n);Check(manager.GetMesh(id,out second)&&second!=null&&!ReferenceEquals(second,clone),"concurrent get creates second genuine clone",ref n);manager.ReleaseMesh(id,reused);manager.ReleaseMesh(id,second);
                Throws<InvalidOperationException>(()=>manager.ReleaseMesh(id,clone),"engine duplicate release keeps collection check",ref n);
                Mesh absent=clone;Check(!manager.GetMesh(int.MinValue,out absent)&&ReferenceEquals(absent,null),"engine unknown key false/null",ref n);manager.ReleaseMesh(int.MinValue,clone);Check(firstPool.CountInactive==2,"engine unknown release leaves two inactive clones",ref n);
                typeof(InstancedObjectPoolManager).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
                UObj.DestroyImmediate(go);go=null;Check(ProcessManager.GetSystemRef<InstancedObjectPoolManager>().GetSafe()==null,"original OnDestroy entry unregisters manager",ref n);Check(Pools(p).Count==1&&firstPool.CountInactive==2,"OnDestroy does not dispose or clear pools",ref n);Check(clone!=null&&second!=null,"OnDestroy leaves pooled clones alive",ref n);
                return n;
            }
            finally
            {
                try{if(go!=null)UObj.DestroyImmediate(go);}finally{try{if(clone!=null)UObj.DestroyImmediate(clone);}finally{try{if(second!=null)UObj.DestroyImmediate(second);}finally{try{if(template!=null)UObj.DestroyImmediate(template);}finally{try{if(invalidCapacityTemplate!=null)UObj.DestroyImmediate(invalidCapacityTemplate);}finally{registry.SetValue(null,prior);}}}}}
            }
        }
        // Genuine PlayMode validation: the original Object.Destroy delegate is deferred,
        // and manager destruction never disposes the captured pool. Root owns execution.
        public static IEnumerator RunFrames(Action<int> result)
        {
            int n=0;var registry=typeof(ProcessManager).GetField("s_systemDictionary",BindingFlags.Static|BindingFlags.NonPublic);object prior=registry.GetValue(null);registry.SetValue(null,Activator.CreateInstance(registry.FieldType));
            GameObject go=null;Mesh template=null,clone=null;ObjectPool<Mesh> original=null;
            try
            {
                go=new GameObject("Project Lucid original pooled destruction");var manager=go.AddComponent<InstancedObjectPoolManager>();template=new Mesh {name="Deferred pool template"};
                int id=manager.RegisterMesh(template);var pool=(InstancedObjectPool<Mesh>)Field(typeof(InstancedObjectPoolManager),"m_meshPool").GetValue(manager);original=Pools(pool)[id];
                Check(original.CountAll==0,"Register defers original Instantiate factory",ref n);
                template.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.forward};Check(manager.GetMesh(id,out clone)&&clone!=null&&clone.vertexCount==4,"factory clones latest captured source data",ref n);
                manager.ReleaseMesh(id,clone);Check(original.CountInactive==1,"real clone returned before Clear",ref n);original.Clear();Check(original.CountInactive==0,"shared pool Clear invokes original destroy delegate",ref n);Check(clone!=null,"Object.Destroy remains deferred until frame",ref n);
                yield return null;
                Check(clone==null,"original Object.Destroy removes clone on actual frame",ref n);Check(template!=null,"destroy callback never destroys captured source",ref n);
                Check(manager.GetMesh(id,out clone)&&clone!=null,"pool remains reusable after Clear",ref n);manager.ReleaseMesh(id,clone);
                UObj.Destroy(go);Check(ReferenceEquals(ProcessManager.GetSystemRef<InstancedObjectPoolManager>().GetSafe(),manager),"manager stays registered until deferred destruction",ref n);
                yield return null;
                go=null;Check(ProcessManager.GetSystemRef<InstancedObjectPoolManager>().GetSafe()==null,"deferred OnDestroy unregisters real manager",ref n);Check(original.CountInactive==1&&clone!=null,"manager destruction leaves pool and clone alive",ref n);
                original.Clear();yield return null;Check(clone==null,"retained pool still owns original destroy callback",ref n);result(n);
            }
            finally
            {
                try{if(go!=null)UObj.DestroyImmediate(go);}finally{try{if(clone!=null)UObj.DestroyImmediate(clone);}finally{try{if(template!=null)UObj.DestroyImmediate(template);}finally{registry.SetValue(null,prior);}}}
            }
        }
    }
}
