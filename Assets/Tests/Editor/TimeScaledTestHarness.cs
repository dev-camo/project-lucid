using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // A real TimeManager component that only holds the Environment category at scale 2, for the time-scaled
    // component fixtures. Descendant construction registers it through the genuine shared reference;
    // Awake (which would build its per-phase subscription
    // tables) never runs on its inactive host, so the tables are added here.
    internal static class TimeScaledTestHarness
    {
        internal const float Scale = 2f;
        private static readonly BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;

        internal sealed class Scene
        {
            internal Owned Owned;
            internal TimeManager Manager;
            internal TimeCategoryObject Category;
            internal TimeScaledManagerLease ManagerLease;
        }

        internal static void WithManager(Action<Scene> body) => Owned.RunAttemptAll(managerOwned =>
        {
            var lease = managerOwned.Lease(new TimeScaledManagerLease());
            TimeManager manager = NewTimeManager(managerOwned, TimeCategory.Environment, Scale); lease.AttachManager(manager);
            // Descendants release first, then public unregistration, then the
            // outer manager/dependencies. No readonly static is replaced.
            Owned.RunAttemptAll(owned =>
            {
                owned.Lease(new ReleaseAction(lease.Unregister));
                Scene scene = NewScene(owned, manager); scene.ManagerLease = lease; body(scene);
            });
        });

        // The same manager registered into its absent row, for components whose own field initialiser takes the typed
        // reference (the static TimeScaledComponent reference is left alone).
        internal static void WithRegisteredManager(Action<Scene> body) => Owned.Run(owned =>
            body(NewScene(owned, owned.Register(NewTimeManager(owned, TimeCategory.Environment, Scale)))));

        private static Scene NewScene(Owned owned, TimeManager manager)
        {
            var phases = (IDictionary)Get<object>(manager, "m_updateObjects");
            Type categoryLists = phases.GetType().GetGenericArguments()[1];
            foreach (UpdateOn phase in new[] { UpdateOn.FixedUpdate, UpdateOn.LateUpdate, UpdateOn.Update })
                phases.Add(phase, Activator.CreateInstance(categoryLists));
            var lookup = (TimeCategoryLookup)typeof(TimeExtensions).GetField("s_config_Internal", Statics).GetValue(null);
            return new Scene { Owned = owned, Manager = manager, Category = lookup.Dictionary[TimeCategory.Environment] };
        }

        // A time-scaled component added with the empty category lookup its editor callbacks need, then given the
        // manager's category object.
        internal static T Add<T>(Scene scene, GameObject host) where T : TimeScaledComponent_SDT
        {
            T component = scene.ManagerLease == null ? EnemyAbilityTestSupport.AddTimeScaled<T>(host) :
                scene.ManagerLease.Construct(host, () => EnemyAbilityTestSupport.AddTimeScaled<T>(host));
            component.TimeCategoryObject = scene.Category;
            return component;
        }

        // How many of the component's subscriptions to this update phase are live.
        internal static int Subscribed(Scene scene, UpdateOn phase)
        {
            var byCategory = (IDictionary)((IDictionary)Get<object>(scene.Manager, "m_updateObjects"))[phase];
            if (!byCategory.Contains(scene.Category)) return 0;
            return ((IEnumerable)byCategory[scene.Category]).Cast<object>()
                .Count(subscription => (bool)subscription.GetType().GetField("IsSubscribed").GetValue(subscription));
        }
    }
}
