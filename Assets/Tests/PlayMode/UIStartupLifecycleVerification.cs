using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEngine;
using UnityEngine.Events;
using UObject = UnityEngine.Object;
namespace ProjectLucid
{
    public static class UIStartupLifecycleVerification
    {
        private static int checks;
        private static void Check(bool value,string label) {if(!value)throw new Exception("Genuine owned UI startup lifecycle: "+label);checks++;}
        private static FieldInfo Field(Type type,string name) => type.GetField(name,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
        private static void Set(object target,string name,object value) => Field(target.GetType(),name).SetValue(target,value);
        private static object Invoke(object target,string name,params object[] args) {try{return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException;}}
        private static void Throws<T>(Action action,string label) where T:Exception {bool caught=false;try{action();}catch(T){caught=true;}Check(caught,label);}
        private static void Guid(ScriptableObjectWithGuid value,string guid) => Field(typeof(ScriptableObjectWithGuid),"m_guid").SetValue(value,guid);
        private static GameObject Owner(List<UObject> allocations,string name,bool active=false) {var owner=new GameObject(name);allocations.Add(owner);owner.SetActive(active);return owner;}
        private static T Definition<T>(List<UObject> allocations,string guid) where T:ScriptableObjectWithGuid {T definition=ScriptableObject.CreateInstance<T>();allocations.Add(definition);Guid(definition,guid);return definition;}
        private static UIContainerSimple Template(List<UObject> allocations,UIContainerIdentifier identifier,UIContainerBehaviour behaviour)
        {
            var owner=Owner(allocations,"Lucid owned real UI template");var canvas=owner.AddComponent<Canvas>();var template=owner.AddComponent<UIContainerSimple>();
            Field(typeof(UIContainer),"m_identifier").SetValue(template,identifier);Field(typeof(UIContainer),"m_behaviour").SetValue(template,behaviour);Field(typeof(UIContainer),"m_canvas").SetValue(template,canvas);
            identifier.RegisterContainer(template);return template;
        }
        private static void DestroyAll<T>(IList<T> objects,int index) where T:UObject
        {
            if(index<0)return;
            try {if(objects[index]!=null)UObject.DestroyImmediate(objects[index]);}
            finally {DestroyAll(objects,index-1);}
        }
        // Root executes this in an actual Unity frame test. Fresh registry and
        // typed combiner leases are restored by exact reference, including on faults.
        // The prefab cache is filled through the original RegisterContainer API;
        // no Addressable provider, catalog or IUIScreenTransition substitute is used.
        public static IEnumerator RunOwnedRuntime()
        {
            int managed=0;checks=0;
            FieldInfo registryField=Field(typeof(ProcessManager),"s_systemDictionary");object previousRegistry=registryField.GetValue(null);
            var logicalAnds=(IDictionary)Field(typeof(StackableData),"LogicalAnds").GetValue(null);Type visibilityType=typeof(Dictionary<string,bool>);bool hadOperation=logicalAnds.Contains(visibilityType);object previousOperation=hadOperation?logicalAnds[visibilityType]:null;
            var allocations=new List<UObject>();var clones=new List<GameObject>();UIManager manager=null;UIScreenTransitionManager transitionHost=null;
            try
            {
                registryField.SetValue(null,Activator.CreateInstance(registryField.FieldType));
                UIVisibilityGroupDefinition hidden=Definition<UIVisibilityGroupDefinition>(allocations,"lucid-owned-initially-hidden");Set(hidden,"m_initialVisibility",false);
                UIVisibilityGroupDefinition shown=Definition<UIVisibilityGroupDefinition>(allocations,"lucid-owned-initially-visible");Set(shown,"m_initialVisibility",true);
                var waitingOwner=Owner(allocations,"Lucid owned waiting visibility member");var waitingMember=waitingOwner.AddComponent<UIVisibilityGroupMember>();Set(waitingMember,"m_visibilityGroupDefinition",hidden);
                Check((bool)Field(typeof(UIVisibilityGroupMember),"m_setGameObjectActive").GetValue(waitingMember),"actual component constructor retains original activation=true");
                int invisibleEvents=0,visibleEvents=0;var invisible=new UnityEvent();var visible=new UnityEvent();
                invisible.AddListener(()=>{invisibleEvents++;Check(!(bool)Field(typeof(UIVisibilityGroupMember),"m_isVisible").GetValue(waitingMember)&&(bool)Field(typeof(UIVisibilityGroupMember),"m_initialVisibilitySet").GetValue(waitingMember),"invisible UnityEvent sees both state flags already published");});
                visible.AddListener(()=>{visibleEvents++;Check((bool)Field(typeof(UIVisibilityGroupMember),"m_isVisible").GetValue(waitingMember),"visible UnityEvent sees state before activation");Set(waitingMember,"m_setGameObjectActive",false);});
                Set(waitingMember,"m_onBecomeInvisible",invisible);Set(waitingMember,"m_onBecomeVisible",visible);
                waitingOwner.SetActive(true);
                Check(invisibleEvents==1&&!waitingOwner.activeSelf,"missing manager applies initial false event and deactivates original owner during Awake");
                var memberRef=(SystemRef<UIManager>)Field(typeof(UIVisibilityGroupMember),"m_uiManagerSystemRef").GetValue(waitingMember);
                Check(!memberRef.IsValid()&&ReferenceEquals(Field(typeof(UIVisibilityGroupMember),"m_gameObject").GetValue(waitingMember),waitingOwner),"deactivated member retains actual cached GameObject and waiting registry reference");
                var observedRef=ProcessManager.GetSystemRef<UIManager>();bool observedCompleteHost=false;
                observedRef.InvokeOnValid(value=>{var managers=(IDictionary)Field(typeof(UIManager),"m_containerManagers").GetValue(value);var stack=(StackableData)Field(typeof(UIManager),"m_visibilityGroupsStack").GetValue(value);observedCompleteHost=managers.Count==6&&!stack.Get<Dictionary<string,bool>>(0)[hidden.GetGUID()]&&stack.Get<Dictionary<string,bool>>(0)[shown.GetGUID()];});
                var managerOwner=Owner(allocations,"Lucid owned original UI host");manager=managerOwner.AddComponent<UIManager>();Set(manager,"m_visibilityGroupDefinitions",new List<UIVisibilityGroupDefinition>{hidden,shown});
                var cameraOwner=Owner(allocations,"Lucid owned UI camera",true);var camera=cameraOwner.AddComponent<Camera>();Set(manager,"m_guiCamera",camera);
                foreach(string name in new[]{"m_freeParent","m_linkedParent","m_singleParent","m_stackParent","m_transitionParent","m_debugParent"}){var parent=Owner(allocations,"Lucid owned "+name,true);Set(manager,name,parent.transform);}
                managerOwner.SetActive(true);
                Check(observedCompleteHost&&memberRef.IsValid()&&ReferenceEquals(ProcessManager.GetSystem<UIManager>(),manager),"actual original Awake publishes initialized six-manager/base-data host before registration callbacks");
                var managersMap=(Dictionary<UIContainerBehaviour,UIContainerManager>)Field(typeof(UIManager),"m_containerManagers").GetValue(manager);
                Check(managersMap.Keys.SequenceEqual(new[]{UIContainerBehaviour.Free,UIContainerBehaviour.Linked,UIContainerBehaviour.Single,UIContainerBehaviour.Stack,UIContainerBehaviour.Transition,UIContainerBehaviour.Debug}),"actual engine host inserts all six original manager families in native order");
                var members=(Dictionary<string,List<UIVisibilityGroupMember>>)Field(typeof(UIManager),"m_uiVisibilityGroupMembers").GetValue(manager);
                Check(members[hidden.GetGUID()].Count==1&&ReferenceEquals(members[hidden.GetGUID()][0],waitingMember)&&!waitingOwner.activeSelf&&invisibleEvents==1,"late valid-host callback registers original inactive member without repeating initialized visibility");
                manager.RegisterUIVisibilityGroupMember(hidden,waitingMember);Check(members[hidden.GetGUID()].Count==1,"real AddUnique retains single registered member");
                StackableDataHandle show=manager.AddVisibilityOverrides(new Dictionary<string,bool>{{hidden.GetGUID(),true}});
                Check(visibleEvents==1&&!waitingOwner.activeSelf&&(bool)Field(typeof(UIVisibilityGroupMember),"m_isVisible").GetValue(waitingMember),"visible event can change activation flag before original post-event field reload");
                Set(waitingMember,"m_setGameObjectActive",true);waitingMember.OnVisibilityChanged(true);Check(!waitingOwner.activeSelf&&visibleEvents==1,"same initialized true value still returns before activation after caller changes flag");
                manager.RemoveVisibilityOverrides(show);Check(invisibleEvents==2&&!waitingOwner.activeSelf,"removing original visibility handle restores false and dispatches real invisible event");
                var lateOwner=Owner(allocations,"Lucid owned registered visibility member");var late=lateOwner.AddComponent<UIVisibilityGroupMember>();Set(late,"m_visibilityGroupDefinition",shown);lateOwner.SetActive(true);
                Check(lateOwner.activeSelf&&(bool)Field(typeof(UIVisibilityGroupMember),"m_isVisible").GetValue(late)&&members[shown.GetGUID()].Count==1,"member Awake with existing host applies original base visibility through direct registration");
                var alternateOwner=Owner(allocations,"Lucid owned event activation target",true);var mutateInvisible=new UnityEvent();mutateInvisible.AddListener(()=>Set(late,"m_gameObject",alternateOwner));Set(late,"m_onBecomeInvisible",mutateInvisible);
                late.OnVisibilityChanged(false);Check(lateOwner.activeSelf&&!alternateOwner.activeSelf,"original invisible event can replace cached GameObject before SetActive receiver reload");late.OnVisibilityChanged(true);Check(alternateOwner.activeSelf,"later original visibility toggles the live replaced cached GameObject");
                var missing=Definition<UIVisibilityGroupDefinition>(allocations,"lucid-owned-missing-base");Throws<KeyNotFoundException>(()=>manager.RegisterUIVisibilityGroupMember(missing,late),"missing original default GUID faults after list insertion");Check(members.ContainsKey(missing.GetGUID())&&members[missing.GetGUID()].Count==1,"missing visibility failure retains preceding native member-list mutation");
                var first=Definition<UIContainerIdentifier>(allocations,"lucid-owned-host-free");var second=Definition<UIContainerIdentifier>(allocations,"lucid-owned-host-transition");var defaultId=Definition<UIContainerIdentifier>(allocations,"lucid-owned-default-transition");var liveNullGuid=Definition<UIContainerIdentifier>(allocations,null);
                var freeTemplate=Template(allocations,first,UIContainerBehaviour.Free);var transitionTemplate=Template(allocations,second,UIContainerBehaviour.Transition);Template(allocations,defaultId,UIContainerBehaviour.Free);
                int creations=0,closes=0;manager.OnContainerCreated+=container=>{creations++;clones.Add(container.gameObject);Check(container.GetComponent<Canvas>().worldCamera==camera,"original host assigns real Canvas camera before live creation callback");};manager.OnContainerClosed+=identifier=>closes++;
                Check(ReferenceEquals(manager.GetOrCreate(first,null),null),"null parameters preserve original null return after opening real container");UIContainerSimple opened;Check(manager.TryGetAs(first,out opened)&&opened!=null&&!ReferenceEquals(opened,freeTemplate)&&creations==1,"null-parameter opening retains real instantiated/registered manager entry and notification");
                Check(ReferenceEquals(manager.GetOrCreate<UIContainerSimple>(first),opened)&&creations==2,"original generic lookup reuses real Free container and repeats creation callback");manager.SetSelectedWidget(opened,13);int index;Check(manager.TryGetSelectedWidget(opened,out index)&&index==13,"actual original widget selection map is keyed by container identifier");
                manager.SetEnabled(first,false);Check(!opened.GetComponent<Canvas>().enabled,"real host dispatch disables only owned matching Canvas");manager.SetEnabled(first,true);Check(opened.GetComponent<Canvas>().enabled,"real host dispatch restores matching Canvas");
                manager.Register(opened);Check(ReferenceEquals(first.LoadedUIContainer,opened),"original host Register routes exact real identifier cache");manager.Unregister(opened);Check(ReferenceEquals(first.LoadedUIContainer,null),"original host Unregister clears identifier cache despite manager-owned clone");first.RegisterContainer(freeTemplate);
                var transitionContainer=manager.GetOrCreate(second);Check(transitionContainer!=null&&manager.IsOpen(first)&&manager.IsOpen(second)&&!manager.IsOnlyContainerOpen(first),"original host exclusivity sees other manager families");
                Check(manager.AreExclusivelyOpen(new[]{first,second}),"all original families apply genuine requested membership subset");manager.CloseAllContainers();Check(!manager.IsOpen(first)&&manager.IsOpen(second)&&closes==0,"bulk native host close excludes Transition family and emits no per-ID close event");
                Check(opened!=null,"original host bulk destruction remains deferred in calling frame");yield return null;Check(opened==null&&transitionContainer!=null,"real frame destroys bulk-owned Free clone while retaining Transition clone");manager.Close(second);Check(closes==1&&!manager.IsOpen(second),"individual host Close visits original Transition family and notifies after all families");yield return null;Check(transitionContainer==null,"original individual close has deferred engine destruction");
                var unsupported=Definition<UIContainerIdentifier>(allocations,"lucid-owned-unmatched-behaviour");Template(allocations,unsupported,(UIContainerBehaviour)int.MaxValue);int acquired=(int)Field(typeof(UIContainerIdentifier),"m_refCount").GetValue(unsupported);int previousCreations=creations;Check(ReferenceEquals(manager.GetOrCreate(unsupported),null)&&(int)Field(typeof(UIContainerIdentifier),"m_refCount").GetValue(unsupported)==acquired+1&&creations==previousCreations,"unsupported original behavior still acquires supplied cached prefab before failing manager lookup");
                var transitionOwner=Owner(allocations,"Lucid owned original transition host");var transitionRef=ProcessManager.GetSystemRef<UIScreenTransitionManager>();bool observedBase=false;transitionRef.InvokeOnValid(value=>observedBase=((StackableData)Field(typeof(UIScreenTransitionManager),"m_transitionStackableData").GetValue(value)).Get<bool>(0));transitionHost=transitionOwner.AddComponent<UIScreenTransitionManager>();Set(transitionHost,"m_defaultTransitionIdentifier",defaultId);transitionOwner.SetActive(true);
                Check(observedBase&&ReferenceEquals(transitionRef.Get(),transitionHost)&&(int)Field(typeof(UIScreenTransitionManager),"m_maximumTimeoutSeconds").GetValue(transitionHost)==15,"original transition host initializes integer timeout/ref/true stack before registry publication");
                Check(((SystemRef<UIManager>)Field(typeof(UIScreenTransitionManager),"m_uiManagerRef").GetValue(transitionHost)).IsValid()&&!transitionHost.IsTransitionActive(),"original transition constructor captures genuine live UI registry dependency");
                Check(!ReferenceEquals(liveNullGuid,null)&&liveNullGuid!=null,"original live null-GUID identifier is not equal to an actual null identifier");
                int callbacks=0;previousCreations=creations;transitionHost.QueueTransition((UIContainerIdentifier)null,()=>callbacks++,()=>callbacks++,()=>callbacks++);
                Check(creations==previousCreations&&!transitionHost.IsTransitionActive(),"Queue with actual null identifier falls back yet always waits before original start");yield return null;yield return null;yield return null;
                Check(creations==previousCreations+1&&manager.IsOpen(defaultId)&&!transitionHost.IsTransitionActive()&&callbacks==0,"original deferred queue opens actual default container then retains absent real IUIScreenTransition boundary without invented callbacks");
                Set(transitionHost,"m_maximumTimeoutSeconds",0);var timeout=(IEnumerator)Invoke(transitionHost,"OnTransitionMaximumTimeout",(Action)(()=>callbacks++));var timeoutHandle=transitionHost.StartCoroutine(timeout);Set(transitionHost,"m_maximumTimeoutCoroutine",timeoutHandle);Check(timeoutHandle!=null&&ReferenceEquals(Field(typeof(UIScreenTransitionManager),"m_maximumTimeoutCoroutine").GetValue(transitionHost),timeoutHandle),"actual engine starts original inactive realtime timeout wrapper and stores owned handle");yield return null;yield return null;
                Check(ReferenceEquals(Field(typeof(UIScreenTransitionManager),"m_maximumTimeoutCoroutine").GetValue(transitionHost),null)&&callbacks==0,"actual original realtime timeout clears owner handle before inactive guard without callback");
                var wait=transitionHost.AddWaitForMidpointOverride();Invoke(transitionHost,"OnReachMidpoint",(Action)(()=>callbacks++));Check(callbacks==1&&!((StackableData)Field(typeof(UIScreenTransitionManager),"m_transitionStackableData").GetValue(transitionHost)).Get<bool>(0),"real midpoint override waits after original caller notification");Throws<NullReferenceException>(()=>transitionHost.ReleaseWaitForMidpointHandle(wait),"released midpoint still requires actual transition component and preserves original missing-component failure");
                manager.Close(defaultId);yield return null;
                UObject.DestroyImmediate(lateOwner);Check(members[shown.GetGUID()].Count==0&&members.ContainsKey(shown.GetGUID()),"actual OnDestroy unregisters member while retaining empty original GUID row");UObject.DestroyImmediate(waitingOwner);Check(members[hidden.GetGUID()].Count==0,"original inactive member OnDestroy unregisters through its now-valid registry reference");
                Debug.Log("PASS genuine owned UI startup lifecycle checks="+checks+"; managed="+managed+"; no authored prefab/catalog, concrete transition/App or boot approval.");
            }
            finally
            {
                try {if(transitionHost!=null)transitionHost.StopAllCoroutines();}
                finally {try {DestroyAll(allocations,allocations.Count-1);}
                finally {try {DestroyAll(clones,clones.Count-1);}
                finally {try {if(hadOperation)logicalAnds[visibilityType]=previousOperation;else logicalAnds.Remove(visibilityType);}
                finally {registryField.SetValue(null,previousRegistry);}}}}
            }
        }
    }
}
