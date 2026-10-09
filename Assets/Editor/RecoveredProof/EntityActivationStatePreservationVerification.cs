using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HardlightProject;

namespace ProjectLucid.Verification
{
    // Rebuilt managed state proof. Instance/Manager constructors, entity factories,
    // ProcessManager registration and Unity object lifecycle are deliberately bypassed.
    public static class EntityActivationStatePreservationVerification
    {
        private sealed class Observations
        {
            internal int Count;
            internal void Check(bool condition, string message)
            {
                ++Count;
                if (!condition) throw new InvalidOperationException(message + " (observation " + Count + ")");
            }
        }

        // A fixture collaborator implementing the authentic abstract contract. It is
        // never a reconstructed provider or added gameplay source. Finalizers are
        // suppressed at construction; cleanup does not invoke throwing delegates.
        private sealed class ProbeLogic : EntityActivationLogic
        {
            internal Action Started, Ended, Stopped;
            internal Func<bool> Predicate;
            internal ProbeLogic() { GC.SuppressFinalize(this); }
            public override void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity)
            { throw new NotSupportedException("This fixture does not construct entity factories."); }
            public override void OnStart() { base.OnStart(); if (Started != null) Started(); }
            public override void OnEnd() { base.OnEnd(); if (Ended != null) Ended(); }
            public override void Shutdown() { if (Stopped != null) Stopped(); }
            public override bool CanTrigger() { return Predicate == null ? base.CanTrigger() : Predicate(); }
            internal void Release()
            { GC.SuppressFinalize(this); Started = Ended = Stopped = null; Predicate = null; }
        }

        private sealed class OwnedState : IDisposable
        {
            internal readonly List<ProbeLogic> Logics = new List<ProbeLogic>();
            internal readonly List<EntityActivationInstance> Instances = new List<EntityActivationInstance>();
            internal readonly List<EntityActivationManager> Managers = new List<EntityActivationManager>();
            internal ProbeLogic Logic() { var value = new ProbeLogic(); Logics.Add(value); return value; }
            internal EntityActivationInstance Instance(List<EntityActivationLogic> activate,
                List<EntityActivationLogic> deactivate, Action onActivate = null, Action onDeactivate = null)
            {
                var value = (EntityActivationInstance)FormatterServices.GetUninitializedObject(typeof(EntityActivationInstance));
                Instances.Add(value);
                Set(value, "m_activateLogics", activate); Set(value, "m_deactivateLogics", deactivate);
                Set(value, "m_onActivateCallback", onActivate); Set(value, "m_onDeactivateCallback", onDeactivate);
                return value;
            }
            internal EntityActivationManager Manager(Dictionary<EntityActivationType, List<EntityActivationInstance>> active,
                Dictionary<EntityActivationType, List<EntityActivationInstance>> inactive)
            {
                var value = (EntityActivationManager)FormatterServices.GetUninitializedObject(typeof(EntityActivationManager));
                Managers.Add(value); Set(value, "m_active", active); Set(value, "m_inactive", inactive); return value;
            }
            public void Dispose()
            {
                foreach (var logic in Logics) logic.Release();
                foreach (var instance in Instances)
                { Set(instance, "m_onActivateCallback", null); Set(instance, "m_onDeactivateCallback", null); }
                Logics.Clear(); Instances.Clear(); Managers.Clear();
            }
        }

        private static void Set(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.IsStatic || !field.IsInitOnly ||
                (value != null && !field.FieldType.IsInstanceOfType(value)))
                throw new InvalidOperationException("Unexpected original instance field: " + name);
            field.SetValue(owner, value);
        }
        private static Exception Capture(Action action)
        { try { action(); } catch (Exception exception) { return exception; } return null; }
        private static List<EntityActivationLogic> Rules(params EntityActivationLogic[] values)
        { return new List<EntityActivationLogic>(values); }
        private static string Trace(List<string> trace) { return string.Join("|", trace.ToArray()); }

        public static int TransitionOrderAndFirstTime()
        {
            var o = new Observations();
            using (var s = new OwnedState())
            {
                var trace = new List<string>();
                var a = s.Logic(); var b = s.Logic(); var c = s.Logic(); var d = s.Logic();
                a.Ended = () => trace.Add("a.end"); b.Ended = () => trace.Add("b.end");
                c.Ended = () => trace.Add("c.end"); d.Ended = () => trace.Add("d.end");
                a.Started = () => trace.Add("a.start"); b.Started = () => trace.Add("b.start");
                c.Started = () => trace.Add("c.start"); d.Started = () => trace.Add("d.start");
                a.OnStart(); b.OnStart(); trace.Clear();
                var instance = s.Instance(Rules(a,b), Rules(c,d), () => trace.Add("activate"), () => trace.Add("deactivate"));
                instance.OnActivate();
                o.Check(Trace(trace) == "a.end|b.end|activate|c.start|d.start", "activation ordering");
                o.Check(!a.CanTrigger(), "first activation rule ended"); o.Check(!b.CanTrigger(), "second activation rule ended");
                o.Check(c.CanTrigger(), "first opposite rule started"); o.Check(d.CanTrigger(), "second opposite rule started");
                trace.Clear(); instance.OnDeactivate();
                o.Check(Trace(trace) == "c.end|d.end|deactivate|a.start|b.start", "deactivation ordering");
                o.Check(a.CanTrigger(), "first activation rule restarted"); o.Check(b.CanTrigger(), "second activation rule restarted");
                o.Check(!c.CanTrigger(), "first deactivation rule ended"); o.Check(!d.CanTrigger(), "second deactivation rule ended");
                trace.Clear(); instance.OnActivate(true);
                o.Check(Trace(trace) == "activate|c.start|d.start", "first-time activation skips only ending");
                o.Check(a.CanTrigger(), "first-time retains activation state one"); o.Check(b.CanTrigger(), "first-time retains activation state two");
                o.Check(c.CanTrigger(), "first-time starts opposite one"); o.Check(d.CanTrigger(), "first-time starts opposite two");
                trace.Clear(); instance.OnDeactivate(true);
                o.Check(Trace(trace) == "deactivate|a.start|b.start", "first-time deactivation skips only ending");
                o.Check(c.CanTrigger(), "first-time retains deactivation state one"); o.Check(d.CanTrigger(), "first-time retains deactivation state two");
                var empty = s.Instance(Rules(),Rules());
                o.Check(!empty.CanActivate(), "empty activation false"); o.Check(!empty.CanDeactivate(), "empty deactivation false");
            }
            return o.Count;
        }

        public static int CallbackFaultAndLiveOppositeList()
        {
            var o = new Observations();
            using (var s = new OwnedState())
            {
                var trace = new List<string>(); var sentinel = new ApplicationException("owned callback fault");
                var a = s.Logic(); var d = s.Logic(); a.OnStart(); a.Ended = () => trace.Add("a.end");
                var first = s.Instance(Rules(a), Rules(d), () => { trace.Add("callback"); throw sentinel; });
                o.Check(ReferenceEquals(Capture(() => first.OnActivate()),sentinel), "callback fault identity");
                o.Check(Trace(trace) == "a.end|callback", "callback fault stops opposite start");
                o.Check(!d.CanTrigger(), "opposite not started after fault"); o.Check(!a.CanTrigger(), "ending before callback retained");
                trace.Clear(); var x = s.Logic(); var y = s.Logic(); y.OnStart(); y.Ended = () => trace.Add("y.end");
                var mirror = s.Instance(Rules(x),Rules(y),null,() => { trace.Add("callback"); throw sentinel; });
                o.Check(ReferenceEquals(Capture(() => mirror.OnDeactivate()),sentinel), "deactivate fault identity");
                o.Check(Trace(trace) == "y.end|callback", "deactivate callback fault prefix");
                o.Check(!x.CanTrigger(), "activation not restarted after fault"); o.Check(!y.CanTrigger(), "deactivation ending retained");
                trace.Clear(); var e = s.Logic(); var opposite = Rules(d);
                d.Started = () => trace.Add("d.start"); e.Started = () => trace.Add("e.start");
                var live = s.Instance(Rules(a),opposite,() => { trace.Add("callback"); opposite.Add(e); });
                live.OnActivate();
                o.Check(Trace(trace) == "a.end|callback|d.start|e.start", "callback-added opposite rule is observed");
                o.Check(d.CanTrigger(), "existing opposite started"); o.Check(e.CanTrigger(), "new opposite started");
                o.Check(opposite.Count == 2, "callback list change retained");
                trace.Clear(); d.OnEnd(); trace.Clear(); var removed = Rules(d);
                var remove = s.Instance(Rules(a),removed,() => { trace.Add("callback"); removed.Clear(); }); remove.OnActivate();
                o.Check(Trace(trace) == "a.end|callback", "callback-cleared opposite is skipped");
                o.Check(!d.CanTrigger(), "removed opposite remains ended"); o.Check(removed.Count == 0, "callback clear retained");
                var nullRule = s.Instance(Rules(a),Rules((EntityActivationLogic)null));
                o.Check(Capture(() => nullRule.OnActivate()) is NullReferenceException, "null opposite faults after ending");
                o.Check(!a.CanTrigger(), "null opposite does not undo ending");
                trace.Clear(); var firstTime = s.Instance(Rules((EntityActivationLogic)null),Rules(d)); firstTime.OnActivate(true);
                o.Check(Trace(trace) == "d.start", "first-time skips null ending rule");
            }
            return o.Count;
        }

        public static int PredicateShortCircuitAndLiveMutation()
        {
            var o = new Observations();
            using (var s = new OwnedState())
            {
                var empty=s.Instance(Rules(),Rules());
                o.Check(!empty.CanActivate(),"empty activate predicate"); o.Check(!empty.CanDeactivate(),"empty deactivate predicate");
                int falseCalls=0,trueCalls=0; var no=s.Logic(); var yes=s.Logic();
                no.Predicate=()=>{++falseCalls;return false;}; yes.Predicate=()=>{++trueCalls;return true;};
                var value=s.Instance(Rules(no,yes,null),Rules(no,yes,null));
                o.Check(value.CanActivate(),"first true skips later null"); o.Check(falseCalls==1,"false predicate once"); o.Check(trueCalls==1,"true predicate once");
                o.Check(value.CanDeactivate(),"deactivate first true skips later null"); o.Check(falseCalls==2,"deactivate false once"); o.Check(trueCalls==2,"deactivate true once");
                var nullFirst=s.Instance(Rules(null,yes),Rules(null,yes));
                o.Check(Capture(()=>nullFirst.CanActivate()) is NullReferenceException,"null before true faults activate");
                o.Check(Capture(()=>nullFirst.CanDeactivate()) is NullReferenceException,"null before true faults deactivate");
                int mutatingCalls=0,laterCalls=0; var mutation=s.Logic(); var later=s.Logic(); var list=Rules(mutation,later);
                mutation.Predicate=()=>{++mutatingCalls;list.Add(yes);return false;};later.Predicate=()=>{++laterCalls;return true;};
                var changing=s.Instance(list,Rules());
                o.Check(Capture(()=>changing.CanActivate()) is InvalidOperationException,"mutation detected by next MoveNext");
                o.Check(mutatingCalls==1,"mutating predicate once"); o.Check(laterCalls==0,"later rule not read after invalidation"); o.Check(list.Count==3,"mutation persists after fault");
                // A true predicate returns through the genuine finally before another MoveNext.
                mutation.Predicate=()=>{list.Add(yes);return true;};
                o.Check(changing.CanActivate(),"true return survives collection mutation"); o.Check(list.Count==4,"true-return mutation retained");
            }
            return o.Count;
        }

        public static int ShutdownAndManagerClearFaultPrefix()
        {
            var o=new Observations();
            using(var s=new OwnedState())
            {
                var trace=new List<string>();var a=s.Logic();var b=s.Logic();var d=s.Logic();
                a.Stopped=()=>trace.Add("a.shutdown");b.Stopped=()=>trace.Add("b.shutdown");d.Stopped=()=>trace.Add("d.shutdown");
                int callbacks=0;var instance=s.Instance(Rules(a,b),Rules(d),()=>++callbacks,()=>++callbacks);
                instance.Shutdown();o.Check(Trace(trace)=="a.shutdown|b.shutdown|d.shutdown","shutdown order");o.Check(callbacks==0,"shutdown does not call transitions");
                trace.Clear();var fault=new ApplicationException("owned shutdown fault");a.Stopped=()=>{trace.Add("a.shutdown");throw fault;};
                o.Check(ReferenceEquals(Capture(instance.Shutdown),fault),"shutdown fault identity");o.Check(Trace(trace)=="a.shutdown","shutdown stops at first fault");
                var activeList=new List<EntityActivationInstance>{instance};var inactiveInstance=s.Instance(Rules(),Rules());
                var inactiveList=new List<EntityActivationInstance>{inactiveInstance};
                var active=new Dictionary<EntityActivationType,List<EntityActivationInstance>>{{default(EntityActivationType),activeList}};
                var inactive=new Dictionary<EntityActivationType,List<EntityActivationInstance>>{{default(EntityActivationType),inactiveList}};
                var manager=s.Manager(active,inactive);trace.Clear();
                o.Check(ReferenceEquals(Capture(manager.Clear),fault),"active shutdown fault propagates");
                o.Check(active.Count==1,"active registry retained before clear");o.Check(inactive.Count==1,"inactive registry retained before clear");
                o.Check(activeList.Count==1,"active list retained on fault");
                a.Stopped=()=>trace.Add("a.shutdown");var second=s.Logic();second.Stopped=()=>{trace.Add("inactive.shutdown");throw fault;};
                inactiveList[0]=s.Instance(Rules(second),Rules());trace.Clear();
                o.Check(ReferenceEquals(Capture(manager.Clear),fault),"inactive shutdown fault propagates");
                o.Check(Trace(trace)=="a.shutdown|b.shutdown|d.shutdown|inactive.shutdown","active pass precedes inactive fault");
                o.Check(active.Count==1,"both dictionaries untouched until passes complete");o.Check(inactive.Count==1,"inactive dictionary untouched on its fault");
                second.Stopped=null;manager.Clear();
                o.Check(active.Count==0,"successful active clear");o.Check(inactive.Count==0,"successful inactive clear");
                o.Check(activeList.Count==1 && inactiveList.Count==1,"clear retains separately owned list references");
                o.Check(callbacks==0,"clear never invokes transition callbacks");
            }
            return o.Count;
        }
    }
}
