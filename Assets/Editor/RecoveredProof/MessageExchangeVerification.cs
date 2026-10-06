using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid
{
    // Source/native-derived ordinary messaging proof. Fixture subclasses exercise the genuine
    // protected cache boundary; they do not replace any original runtime type or service.
    public static class MessageExchangeVerification
    {
        private static int checks;
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static void Check(bool value, string label) { ++checks; if (!value) throw new InvalidOperationException(label); }
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            try { action(); } catch (T) { ++checks; return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name + ": " + label);
        }
        private static FieldInfo Field(Type t, string n) => t.GetField(n, All);
        private static object Read(Type t, object o, string n) => Field(t, n).GetValue(o);
        private static IDictionary Infos<T>(MessageExchangeBase<T> e) => (IDictionary)Read(typeof(MessageExchangeBase<T>), e, "m_messageInfos");
        private static IList Invalid<T>(MessageExchangeBase<T> e) => (IList)Read(typeof(MessageExchangeBase<T>), e, "m_invalidMessageHashes");
        private sealed class CollisionKey : IEquatable<CollisionKey>
        {
            public readonly int Id;
            public CollisionKey(int id) { Id = id; }
            public bool Equals(CollisionKey other) => other != null && Id == other.Id;
            public override bool Equals(object other) => other is CollisionKey key && Equals(key);
            public override int GetHashCode() => 7;
        }
        private interface IMessageFixture { }
        private sealed class InterfaceKey : IMessageFixture { }
        private sealed class CacheFixture<T> : MessageExchangeBase<T>
        {
            public THandle Handle<TCallback, THandle>(in T message) where TCallback : Delegate where THandle : class, IExchangeHandle, new() => GetExchangeHandleInternal<TCallback, THandle>(in message);
        }
        private sealed class ReentrantHandle : IExchangeHandle
        {
            public static Action Construct;
            public static Action InvalidateAction;
            public bool Valid { get; private set; } = true;
            public ReentrantHandle() { Construct?.Invoke(); }
            public void Invalidate() { InvalidateAction?.Invoke(); Valid = false; }
        }
        private static void Shutdown(ISystem system) => system.ProcessSystemAction(SystemAction.Shutdown);

        private static void CheckMetadata()
        {
            Check(typeof(MessageExchange<>).Assembly.GetName().Name == "HLUnityCore.Runtime", "genuine original assembly identity");
            Check(typeof(MessageExchange<>).IsSealed && typeof(MessageExchangeBase<>).IsAbstract && typeof(ExchangeHandleBase<>).IsAbstract, "original class modifiers");
            Check(typeof(MessageExchangeBase<>).GetInterfaces().SequenceEqual(new[] { typeof(ISystem) }) && !typeof(IDisposable).IsAssignableFrom(typeof(MessageExchangeBase<>)), "base original ISystem only, no invented Dispose");
            Check(typeof(IExchangeHandle).GetMembers(All).Count(x => x is MethodInfo) == 2 && typeof(IExchangeHandle).GetProperties(All).Length == 1, "genuine two-method handle interface");
            Type[] types = { typeof(MessageExchange<>), typeof(MessageExchangeBase<>), typeof(ExchangeHandle), typeof(ExchangeHandle<>), typeof(ExchangeHandle<,>), typeof(ExchangeHandle<,,>), typeof(ExchangeHandle<,,,>), typeof(ExchangeHandle<,,,,>), typeof(ExchangeHandleBase<>) };
            int[][] order = { new[] { 2, 1 }, new[] { 1, 2 }, new[] { 1, 2 }, new[] { 2, 1 }, new[] { 1, 2 }, new[] { 2, 1 }, new[] { 1, 2 }, new[] { 2, 1 }, new[] { 2, 1 } };
            for (int i = 0; i < types.Length; ++i)
            {
                var attrs = types[i].GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Check(attrs.Length == 2 && attrs.Select(a => (int)a.Option).SequenceEqual(order[i]) && attrs.All(a => Equals(a.Value, false)), types[i].Name + " exact ordered original IL2CPP options");
                Check((types[i].Attributes & TypeAttributes.BeforeFieldInit) != 0, types[i].Name + " original BeforeFieldInit");
            }
            Check(typeof(MessageExchangeBase<>).GetFields(All).OrderBy(f => f.MetadataToken).Select(f => f.Name).SequenceEqual(new[] { "s_messageComparer", "m_invalidMessageHashes", "m_messageInfos" }), "complete3 base fields in original order");
            Check(typeof(ExchangeHandleBase<>).GetFields(All).OrderBy(f => f.MetadataToken).Select(f => f.Name).SequenceEqual(new[] { "<Valid>k__BackingField", "m_subscribers" }), "complete2 handle fields in original order");
            Check(typeof(ExchangeHandleBase<>).GetProperty("Subscribers", All).GetMethod.IsFamily && typeof(ExchangeHandleBase<>).GetProperty("Valid", All).SetMethod.IsPrivate, "protected subscribers/private validity setter");
            Check(typeof(MessageExchangeBase<>).GetMethod("OnShutdown", All).IsVirtual && typeof(MessageExchangeBase<>).GetMethod("GetExchangeHandleInternal", All).IsVirtual, "original protected virtual cache/shutdown hooks");
            var generic = typeof(MessageExchangeBase<>).GetMethod("GetExchangeHandleInternal", All).GetGenericArguments();
            Check(generic[0].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(Delegate) }), "callback Delegate constraint");
            Check(generic[1].GenericParameterAttributes == (GenericParameterAttributes.ReferenceTypeConstraint | GenericParameterAttributes.DefaultConstructorConstraint) && generic[1].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(IExchangeHandle) }), "handle class/new/real-interface constraints");
            foreach (var t in new[] { typeof(MessageCallback), typeof(MessageCallback<>), typeof(MessageCallback<,>), typeof(MessageCallback<,,>), typeof(MessageCallback<,,,>), typeof(MessageCallback<,,,,>) })
            {
                var ps = t.GetMethod("Invoke").GetParameters();
                Check(ps.All(x => x.ParameterType.IsByRef && x.IsIn), t.Name + " original delegate in arguments");
                Check(t.GetMethods(All).Count(m => m.Name == "Invoke" || m.Name == "BeginInvoke" || m.Name == "EndInvoke") == 3, t.Name + " genuine runtime delegate methods");
            }
            var p4 = typeof(ExchangeHandle<,,,>).GetMethod("PublishMessage").GetParameters();
            Check(p4.Take(3).All(x => x.ParameterType.IsByRef && x.IsIn) && !p4[3].ParameterType.IsByRef && !p4[3].IsIn, "original fourth handle argument by value");
            Check(typeof(MessageExchange<>).GetMethods(All).Where(m => m.Name == "PublishMessage").All(m => m.GetParameters().All(x => x.ParameterType.IsByRef && x.IsIn)), "outer publication retains all in arguments");
            Check(typeof(SubscribeHandle<>).GetFields(All).Single().IsInitOnly && typeof(SubscribeHandle<>).GetCustomAttributesData().Any(a => a.AttributeType == typeof(IsReadOnlyAttribute)), "original readonly subscribe handle");
            var nested = typeof(MessageExchangeBase<>).GetNestedTypes(All);
            Check(nested.Select(t => t.Name).SequenceEqual(new[] { "MessageHash`1", "MessageHashInfo", "MessageInfo" }), "complete natural nested struct names/order");
            foreach (Type t in nested) Check(t.IsNestedPrivate && t.IsValueType && t.GetCustomAttributesData().Any(a => a.AttributeType == typeof(IsReadOnlyAttribute)), t.Name + " original private readonly struct");
        }

        private static void CheckOrdinaryRoutes()
        {
            var exchange = new MessageExchange<string>();
            string key = "LoadingProgress"; int calls = 0; float observed = 0;
            MessageCallback<float> callback = (in float v) => { ++calls; observed = v; };
            var subscription = exchange.SubscribeToMessage<float>(in key, callback);
            MessageCallback<float> stored = subscription;
            Check(ReferenceEquals(stored, callback), "subscription holds exact callback identity");
            float value = .25f; exchange.PublishMessage<float>(in key, in value);
            Check(calls == 1 && observed == .25f, "original scene-loading progress path");
            var handle = exchange.GetExchangeHandle<float>(in key);
            Check(ReferenceEquals(handle, exchange.GetExchangeHandle<float>(in key)), "typed cache retains exact handle");
            var zero = exchange.GetExchangeHandle(in key);
            Check(!ReferenceEquals(zero, handle) && Infos(exchange).Count == 2, "same key different delegate signatures use distinct cache rows");
            exchange.SubscribeToMessage<float>(in key, callback);
            exchange.PublishMessage<float>(in key, in value); Check(calls == 3, "duplicates are invoked independently");
            Check(exchange.UnsubscribeFromMessage<float>(in key, callback), "first duplicate removed");
            exchange.PublishMessage<float>(in key, in value); Check(calls == 4, "one duplicate remains");
            Check(exchange.UnsubscribeFromMessage<float>(in key, callback) && !exchange.UnsubscribeFromMessage<float>(in key, callback), "remove exactly remaining duplicate then false");
            string unknown = "never published";
            Check(!exchange.UnsubscribeFromMessage<float>(in unknown, callback) && Infos(exchange).Count == 3, "unsubscribe on absent key creates original empty handle");
            string emptyPublish = "empty publication"; exchange.PublishMessage(in emptyPublish);
            Check(Infos(exchange).Count == 4, "publication on absent key creates cache row");
            handle.Invalidate();
            Check(!handle.Valid && ReferenceEquals(handle, exchange.GetExchangeHandle<float>(in key)), "invalid equal handle remains cached without validity check");
            handle.PublishMessage(in value); Check(calls == 4, "invalidated original empty list permits silent publication");
            Throws<NullReferenceException>(() => handle.SubscribeToMessage(null), "invalid null callback faults before diagnostic");
            Check(!handle.UnsubscribeFromMessage(callback), "invalid handle still calls List.Remove");
            exchange.ReleaseCollectedMessageInfos();
            Check(Infos(exchange).Count == 3 && Invalid(exchange).Count == 1, "explicit collection removes invalid row and retains hash");
            var newHandle = exchange.GetExchangeHandle<float>(in key);
            Check(newHandle.Valid && !ReferenceEquals(handle, newHandle) && Infos(exchange).Count == 4, "original released slot can be reused");
            exchange.ReleaseCollectedMessageInfos();
            Check(Infos(exchange).Count == 3 && newHandle.Valid && Invalid(exchange).Count == 1, "retained stale key removes valid reused row without invalidating it");
            Shutdown(exchange); Check(Infos(exchange).Count == 0 && !zero.Valid && Invalid(exchange).Count == 1, "shutdown invalidates live values/clears map but retains invalid-key list");
            Check(ProcessManager.IsSystemNull<MessageExchange<string>>(), "constructor does not register the exchange as a system");
        }

        private static void CheckArities()
        {
            var e = new MessageExchange<string>(); string key = "arity"; string trace = "";
            MessageCallback c0 = () => trace += "0";
            MessageCallback<int> c1 = (in int a) => trace += "1:" + a;
            MessageCallback<int, string> c2 = (in int a, in string b) => trace += "2:" + a + b;
            MessageCallback<int, string, float> c3 = (in int a, in string b, in float c) => trace += "3:" + a + b + c;
            MessageCallback<int, string, float, long> c4 = (in int a, in string b, in float c, in long d) => trace += "4:" + a + b + c + d;
            MessageCallback<int, string, float, long, bool> c5 = (in int a, in string b, in float c, in long d, in bool f) => trace += "5:" + a + b + c + d + f;
            Check(ReferenceEquals((MessageCallback)e.SubscribeToMessage(in key, c0), c0), "arity0 subscription identity");
            Check(ReferenceEquals((MessageCallback<int>)e.SubscribeToMessage<int>(in key, c1), c1), "arity1 subscription identity");
            Check(ReferenceEquals((MessageCallback<int, string>)e.SubscribeToMessage<int, string>(in key, c2), c2), "arity2 subscription identity");
            Check(ReferenceEquals((MessageCallback<int, string, float>)e.SubscribeToMessage<int, string, float>(in key, c3), c3), "arity3 subscription identity");
            Check(ReferenceEquals((MessageCallback<int, string, float, long>)e.SubscribeToMessage<int, string, float, long>(in key, c4), c4), "arity4 subscription identity");
            Check(ReferenceEquals((MessageCallback<int, string, float, long, bool>)e.SubscribeToMessage<int, string, float, long, bool>(in key, c5), c5), "arity5 subscription identity");
            int a1=3; string a2="X"; float a3=2; long a4=5; bool a5=true;
            e.PublishMessage(in key); Check(trace=="0", "arity0 publish"); trace="";
            e.PublishMessage<int>(in key,in a1); Check(trace=="1:3", "arity1 publish"); trace="";
            e.PublishMessage<int,string>(in key,in a1,in a2); Check(trace=="2:3X", "arity2 publish"); trace="";
            e.PublishMessage<int,string,float>(in key,in a1,in a2,in a3); Check(trace=="3:3X2", "arity3 publish"); trace="";
            e.PublishMessage<int,string,float,long>(in key,in a1,in a2,in a3,in a4); Check(trace=="4:3X25", "arity4 publish"); trace="";
            e.PublishMessage<int,string,float,long,bool>(in key,in a1,in a2,in a3,in a4,in a5); Check(trace=="5:3X25True", "arity5 publish");
            Check(Infos(e).Count==6, "all six typed signatures have exact distinct rows");
            Check(e.UnsubscribeFromMessage(in key,c0), "arity0 unsubscribe");
            Check(e.UnsubscribeFromMessage<int>(in key,c1), "arity1 unsubscribe");
            Check(e.UnsubscribeFromMessage<int,string>(in key,c2), "arity2 unsubscribe");
            Check(e.UnsubscribeFromMessage<int,string,float>(in key,c3), "arity3 unsubscribe");
            Check(e.UnsubscribeFromMessage<int,string,float,long>(in key,c4), "arity4 unsubscribe");
            Check(e.UnsubscribeFromMessage<int,string,float,long,bool>(in key,c5), "arity5 unsubscribe");
            trace="";e.PublishMessage<int,string,float,long,bool>(in key,in a1,in a2,in a3,in a4,in a5);Check(trace=="", "removed arity5 is no longer invoked");
            Check(e.GetExchangeHandle<int,string>(in key).Valid && e.GetExchangeHandle<int,string,float>(in key).Valid && e.GetExchangeHandle<int,string,float,long>(in key).Valid && e.GetExchangeHandle<int,string,float,long,bool>(in key).Valid, "all original typed get wrappers use cached valid handles");
            Shutdown(e);
        }

        private static void CheckLiveDispatch()
        {
            var h = new ExchangeHandle(); string trace=""; bool added=false;
            MessageCallback late=()=>trace+="L";
            h.SubscribeToMessage(()=>{trace+="A";if(!added){added=true;h.SubscribeToMessage(late);}});
            h.SubscribeToMessage(()=>trace+="B");h.PublishMessage();Check(trace=="AB", "append excluded by captured count");
            trace="";h.PublishMessage();Check(trace=="ABL", "appended callback joins next pass");
            h.Invalidate();Check(!h.Valid, "invalidation marks false after clearing");h.PublishMessage();Check(trace=="ABL", "empty invalid handle dispatch has no validity gate");
            var removing=new ExchangeHandle(); MessageCallback first=null;trace="";
            first=()=>{trace+="A";removing.UnsubscribeFromMessage(first);};removing.SubscribeToMessage(first);removing.SubscribeToMessage(()=>trace+="B");
            Throws<ArgumentOutOfRangeException>(()=>removing.PublishMessage(), "live removal faults later captured index");Check(trace=="A", "shifted callback skipped before index failure");
            var cleared=new ExchangeHandle();trace="";cleared.SubscribeToMessage(()=>{trace+="A";cleared.Invalidate();});cleared.SubscribeToMessage(()=>trace+="B");
            Throws<ArgumentOutOfRangeException>(()=>cleared.PublishMessage(), "live clear faults next index");Check(trace=="A"&&!cleared.Valid, "clear takes effect synchronously");
            var failed=new ExchangeHandle();trace="";failed.SubscribeToMessage(()=>{trace+="A";throw new FormatException("fixture");});failed.SubscribeToMessage(()=>trace+="B");
            Throws<FormatException>(()=>failed.PublishMessage(), "callback exception propagates");Check(trace=="A", "callback failure aborts remaining dispatch");
            var nulls=new ExchangeHandle();nulls.SubscribeToMessage(null);Throws<NullReferenceException>(()=>nulls.PublishMessage(), "valid null subscriber preserved and invocation faults");Check(nulls.UnsubscribeFromMessage(null), "null subscriber can be removed");
            var recursive=new ExchangeHandle();trace="";int depth=0;
            recursive.SubscribeToMessage(()=>{trace+="A"+depth;if(depth==0){++depth;recursive.PublishMessage();--depth;}});recursive.SubscribeToMessage(()=>trace+="B"+depth);
            recursive.PublishMessage();Check(trace=="A0A1B1B0", "recursive publication is synchronous and ordered");
        }

        private static void CheckCollisionsAndStorage()
        {
            var e=new MessageExchange<CollisionKey>();var k1=new CollisionKey(1);var k2=new CollisionKey(2);var equal=new CollisionKey(1);
            var h1=e.GetExchangeHandle(in k1);var h2=e.GetExchangeHandle(in k2);
            Check(!ReferenceEquals(h1,h2)&&Infos(e).Count==2,"same hash/unequal weak messages linearly probe");
            Check(ReferenceEquals(h1,e.GetExchangeHandle(in equal))&&Infos(e).Count==2,"equal weak target messages reuse existing cache row");
            h1.Invalidate();e.ReleaseCollectedMessageInfos();Check(!h1.Valid&&h2.Valid&&Infos(e).Count==1,"invalid collision row removed without touching neighbor");
            var revived=e.GetExchangeHandle(in k1);Check(revived.Valid&&Infos(e).Count==2,"released collision slot recreated");
            object abandoned=Infos(e).Values.Cast<object>().Single(x=>ReferenceEquals(Read(x.GetType(),x,"ExchangeHandle"),revived));
            e.ReleaseCollectedMessageInfos();
            Check(revived.Valid&&Infos(e).Count==1&&h2.Valid,"stale hash removes newly valid colliding slot");
            ((IDisposable)Read(abandoned.GetType(),abandoned,"MessageHashInfo")).Dispose();
            Shutdown(e);GC.KeepAlive(k1);GC.KeepAlive(k2);GC.KeepAlive(equal);
            // revived's original weak handle was removed without release by the stale-key quirk.
            // Fixture cleanup frees exactly that captured abandoned native-shaped info once.
            var stringE=new MessageExchange<string>();string nil=null;
            Check(ReferenceEquals(stringE.GetExchangeHandle(in nil),stringE.GetExchangeHandle(in nil)),"declared string null remains strongly stored/equal");Shutdown(stringE);
            var objectE=new MessageExchange<object>();object nullObject=null;
            var n1=objectE.GetExchangeHandle(in nullObject);var n2=objectE.GetExchangeHandle(in nullObject);
            Check(!ReferenceEquals(n1,n2)&&Infos(objectE).Count==2,"weak null temporary never passes Valid equality");objectE.ReleaseCollectedMessageInfos();
            Check(!n1.Valid&&!n2.Valid&&Infos(objectE).Count==0,"weak null target collection invalidates both rows");Shutdown(objectE);
            var ifaceE=new MessageExchange<IMessageFixture>();IMessageFixture iface=new InterfaceKey();ifaceE.GetExchangeHandle(in iface);
            object ifaceInfo=Infos(ifaceE).Values.Cast<object>().Single();object ifaceHash=Read(ifaceInfo.GetType(),ifaceInfo,"MessageHashInfo");
            Check(!( (GCHandle)Read(ifaceHash.GetType(),ifaceHash,"m_gcHandle")).IsAllocated,"declared interface uses strong storage though runtime object is class");
            Check(ReferenceEquals(Read(ifaceHash.GetType(),ifaceHash,"m_message"),iface),"strong interface message identity preserved");Shutdown(ifaceE);
            var weakE=new MessageExchange<object>();object runtimeString=new string(new[]{'S','T'});weakE.GetExchangeHandle(in runtimeString);
            object weakInfo=Infos(weakE).Values.Cast<object>().Single();object weakHash=Read(weakInfo.GetType(),weakInfo,"MessageHashInfo");
            Check(((GCHandle)Read(weakHash.GetType(),weakHash,"m_gcHandle")).IsAllocated,"declared object creates weak handle even for runtime string");
            Check(Read(weakHash.GetType(),weakHash,"m_message")==null,"weak branch stores default message");Shutdown(weakE);GC.KeepAlive(runtimeString);
        }

        private static void CheckFourthArgumentCopy()
        {
            int v1=1,v2=2,v3=3,v4=4,v5=5;
            (int,int,int,int) observed4=default;
            MessageCallback<int,int,int,int> mutate4=(in int a,in int b,in int c,in int d)=>{v1=9;v2=10;v3=11;v4=12;};
            MessageCallback<int,int,int,int> observe4=(in int a,in int b,in int c,in int d)=>observed4=(a,b,c,d);
            var direct=new ExchangeHandle<int,int,int,int>();direct.SubscribeToMessage(mutate4);direct.SubscribeToMessage(observe4);
            direct.PublishMessage(in v1,in v2,in v3,v4);
            Check(observed4==(9,10,11,4)&&v4==12,"native handle arity4 keeps fourth argument copy while first three retain live incoming references");
            var exchange=new MessageExchange<string>();string key="fourth argument value copy";
            exchange.SubscribeToMessage<int,int,int,int>(in key,mutate4);exchange.SubscribeToMessage<int,int,int,int>(in key,observe4);
            v1=1;v2=2;v3=3;v4=4;exchange.PublishMessage<int,int,int,int>(in key,in v1,in v2,in v3,in v4);
            Check(observed4==(9,10,11,4)&&v4==12,"native outer arity4 accepts all in references then makes the original handle fourth-argument copy");
            (int,int,int,int,int) observed5=default;
            var five=new ExchangeHandle<int,int,int,int,int>();
            five.SubscribeToMessage((in int a,in int b,in int c,in int d,in int e)=>{v1=9;v2=10;v3=11;v4=12;v5=13;});
            five.SubscribeToMessage((in int a,in int b,in int c,in int d,in int e)=>observed5=(a,b,c,d,e));
            v1=1;v2=2;v3=3;v4=4;v5=5;five.PublishMessage(in v1,in v2,in v3,in v4,in v5);
            Check(observed5==(9,10,11,12,13),"native arity5 retains all five live reference arguments, distinct from the original arity4 value quirk");
            Shutdown(exchange);
        }

        private static void CheckReentrancy()
        {
            var e=new CacheFixture<string>();string key="reentrant";ReentrantHandle inner=null;bool constructing=false;
            ReentrantHandle.Construct=()=>{if(!constructing){constructing=true;inner=e.Handle<MessageCallback,ReentrantHandle>(in key);constructing=false;}};
            var outer=e.Handle<MessageCallback,ReentrantHandle>(in key);ReentrantHandle.Construct=null;
            Check(inner!=null&&!ReferenceEquals(inner,outer)&&Infos(e).Count==1,"constructor reentrancy outer indexer overwrites inner cache row");
            Check(ReferenceEquals(outer,e.Handle<MessageCallback,ReentrantHandle>(in key))&&inner.Valid,"overwritten original inner handle remains valid/unreleased");
            Throws<InvalidCastException>(()=>e.Handle<MessageCallback,ExchangeHandle>(in key),"equal cached callback type retains checked handle cast");
            string key2="mutating invalidation";var h=e.Handle<MessageCallback,ReentrantHandle>(in key2);h.Invalidate();
            string inserted="inserted during release";ReentrantHandle.InvalidateAction=()=>e.Handle<MessageCallback<int>,ExchangeHandle<int>>(in inserted);
            Throws<InvalidOperationException>(()=>e.ReleaseCollectedMessageInfos(),"Invalidate reentrantly adding row invalidates live dictionary enumerator");
            Check(Infos(e).Count==3&&Invalid(e).Count==1,"failed first pass skips removals and retains appended invalid key");ReentrantHandle.InvalidateAction=null;
            e.ReleaseCollectedMessageInfos();Check(Infos(e).Count==2&&Invalid(e).Count==2,"retry appends same invalid key again before removing all accumulated hashes");
            Shutdown(e);Check(!outer.Valid&&inner.Valid,"shutdown affects currently mapped values only");
            var shutdownE=new CacheFixture<string>();string shutdownKey="shutdown mutation";shutdownE.Handle<MessageCallback,ReentrantHandle>(in shutdownKey);
            ReentrantHandle.InvalidateAction=()=>shutdownE.Handle<MessageCallback<int>,ExchangeHandle<int>>(in inserted);
            Throws<InvalidOperationException>(()=>Shutdown(shutdownE),"shutdown uses live Values and preserves map after reentrant failure");
            Check(Infos(shutdownE).Count==2,"failed shutdown skips final Clear");ReentrantHandle.InvalidateAction=null;Shutdown(shutdownE);Check(Infos(shutdownE).Count==0,"successful shutdown retry clears mapped values");
        }

        public static int RunManaged()
        {
            checks=0;
            var lookup=(Dictionary<SystemAction,Dictionary<ISystem,Action<object>>>)Field(typeof(ProcessManager),"s_systemActionLookup").GetValue(null);
            var original=lookup.Select(p=>(p.Key,p.Value,p.Value.ToArray())).ToArray();
            try { CheckMetadata();CheckOrdinaryRoutes();CheckArities();CheckLiveDispatch();CheckCollisionsAndStorage();CheckFourthArgumentCopy();CheckReentrancy();return checks; }
            finally
            {
                ReentrantHandle.Construct=null;ReentrantHandle.InvalidateAction=null;
                lookup.Clear();foreach(var row in original){row.Value.Clear();foreach(var entry in row.Item3)row.Value.Add(entry.Key,entry.Value);lookup.Add(row.Key,row.Value);}
            }
        }
        // Actual Unity object identity/lifetime boundary, authored but unrun outside
        // root's Editor window. These are real owned GameObjects, never fake runtime types.
        public static int RunActualUnityObjects()
        {
            checks=0;
            var lookup=(Dictionary<SystemAction,Dictionary<ISystem,Action<object>>>)Field(typeof(ProcessManager),"s_systemActionLookup").GetValue(null);
            var original=lookup.Select(p=>(p.Key,p.Value,p.Value.ToArray())).ToArray();
            UnityEngine.GameObject first=null,second=null;MessageExchange<UnityEngine.GameObject> exchange=null;
            try
            {
                first=new UnityEngine.GameObject("bounded original messaging first");second=new UnityEngine.GameObject("bounded original messaging second");
                exchange=new MessageExchange<UnityEngine.GameObject>();
                var firstHandle=exchange.GetExchangeHandle(in first);var secondHandle=exchange.GetExchangeHandle(in second);
                Check(!ReferenceEquals(firstHandle,secondHandle)&&Infos(exchange).Count==2,"actual distinct Unity object identities retain separate message handles");
                Check(ReferenceEquals(firstHandle,exchange.GetExchangeHandle(in first)),"actual same Unity object identity reuses its original cache row");
                object firstInfo=Infos(exchange).Values.Cast<object>().Single(v=>ReferenceEquals(Read(v.GetType(),v,"ExchangeHandle"),firstHandle));
                object hashInfo=Read(firstInfo.GetType(),firstInfo,"MessageHashInfo");
                var weak=(GCHandle)Read(hashInfo.GetType(),hashInfo,"m_gcHandle");
                Check(weak.IsAllocated&&ReferenceEquals(weak.Target,first)&&Read(hashInfo.GetType(),hashInfo,"m_message")==null,"actual declared Unity class uses original weak GC handle and default strong-message field");
                int calls=0;MessageCallback callback=()=>++calls;exchange.SubscribeToMessage(in first,callback);exchange.PublishMessage(in first);
                Check(calls==1,"actual Unity message subscription reaches the cached original callback");
                UnityEngine.Object.DestroyImmediate(first);
                Check(first==null&&!ReferenceEquals(first,null)&&ReferenceEquals(weak.Target,first)&&(bool)firstInfo.GetType().GetProperty("Valid",All).GetValue(firstInfo),"native raw managed weak target stays valid while its destroyed Unity wrapper remains rooted; no invented Unity null predicate");
                Check(ReferenceEquals(firstHandle,exchange.GetExchangeHandle(in first)),"actual destroyed but rooted same wrapper retains native Object.Equals cache identity");
                exchange.PublishMessage(in first);Check(calls==2,"actual destroyed Unity wrapper still publishes through its original mapped handle");
                firstHandle.Invalidate();exchange.ReleaseCollectedMessageInfos();
                Check(Infos(exchange).Count==1&&Invalid(exchange).Count==1&&secondHandle.Valid,"actual explicit invalidation releases weak row and retains native invalid-key history");
                Shutdown(exchange);Check(Infos(exchange).Count==0&&!secondHandle.Valid&&Invalid(exchange).Count==1,"actual original process-action shutdown clears surviving Unity row without clearing stale key history");
                GC.KeepAlive(first);GC.KeepAlive(second);return checks;
            }
            finally
            {
                try { if(exchange!=null)Shutdown(exchange); }
                finally
                {
                    try { if(first!=null)UnityEngine.Object.DestroyImmediate(first); }
                    finally
                    {
                        try { if(second!=null)UnityEngine.Object.DestroyImmediate(second); }
                        finally
                        {
                            lookup.Clear();foreach(var row in original){row.Value.Clear();foreach(var entry in row.Item3)row.Value.Add(entry.Key,entry.Value);lookup.Add(row.Key,row.Value);}
                        }
                    }
                }
            }
        }
        public static void Run()
        {
            int managed=RunManaged(),engine=RunActualUnityObjects();
            UnityEngine.Debug.Log("Project Lucid ordinary messaging checks="+(managed+engine)+"; managed="+managed+"; actualUnity="+engine+"; full App/authored startup remains unresolved");
        }
    }
}
