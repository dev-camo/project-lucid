using System;using System.Collections;using System.Collections.Generic;using System.Reflection;using Hardlight;using HardlightProject;using NUnit.Framework;using UnityEngine;using UnityEngine.Pool;using UnityEngine.TestTools;using UObj=UnityEngine.Object;
namespace ProjectLucid.Tests
{
    // Bounded original pooling prerequisites only; no LevelSetup/DataManager/scene/gameplay claim.
    public sealed class InstancedPoolingLifecycleTests
    {
        static void Check(bool ok,string name,ref int checks){if(!ok)throw new InvalidOperationException(name);checks++;}
        static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)??throw new MissingFieldException(type.FullName,name);
        static Dictionary<int,ObjectPool<Mesh>> Pools(InstancedObjectPool<Mesh> p)=>(Dictionary<int,ObjectPool<Mesh>>)Field(typeof(InstancedObjectPool<Mesh>),"m_pools").GetValue(p);
        [UnityTest]
        public IEnumerator OriginalInstancedPool_CloneDestroyAndManagerLifecycle()

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
                original.Clear();yield return null;Check(clone==null,"retained pool still owns original destroy callback",ref n);Assert.AreEqual(12,n,"bounded original pool frame checks");
            }
            finally
            {
                try{if(go!=null)UObj.DestroyImmediate(go);}finally{try{if(clone!=null)UObj.DestroyImmediate(clone);}finally{try{if(template!=null)UObj.DestroyImmediate(template);}finally{registry.SetValue(null,prior);}}}
            }
        }
    }
}
