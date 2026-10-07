using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // These fixtures verify original boundaries using genuine restored owners.
    // Their outcomes cover the waypoint subsystem, not a complete level route.
    public static class OriginalWaypointVerification
    {
        private static int checks;
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Original waypoint verification: " + description);
            checks++;
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
        private static void Invoke(object owner, string name, params object[] arguments)
        {
            try { owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(owner, arguments); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static T Throws<T>(Action action, string description) where T : Exception
        {
            try { action(); }
            catch (T error) { checks++; return error; }
            throw new InvalidOperationException("Original waypoint expected " + typeof(T).Name + ": " + description);
        }
        public static int RunOriginalDeclarations()
        {
            checks = 0;
            Type manager = typeof(WaypointManager), target = typeof(WaypointTarget);
            Check(manager.FullName == "HardlightProject.WaypointManager" && target.FullName == "HardlightProject.WaypointTarget", "two original type identities");
            Check(manager.Assembly.GetName().Name == "Game.Runtime" && target.Assembly == manager.Assembly, "original Game assembly ownership");
            Check(manager.BaseType == typeof(object) && manager.GetInterfaces().SequenceEqual(new[] { typeof(ISystem) }), "manager original Object and memberless ISystem");
            Check(target.BaseType == typeof(MonoBehaviour) && target.GetInterfaces().Length == 0, "target original engine base");
            foreach (Type type in new[] { manager, target })
            {
                Check(type.IsPublic && !type.IsSealed && !type.IsAbstract && !type.IsGenericType && (type.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original public nonsealed class flags " + type.Name);
                var attributes = type.GetCustomAttributesData().Where(a => a.AttributeType == typeof(Il2CppSetOptionAttribute)).ToArray();
                Check(attributes.Length == 2 && attributes[0].ConstructorArguments[0].Value.Equals((int)Option.ArrayBoundsChecks) && attributes[1].ConstructorArguments[0].Value.Equals((int)Option.NullChecks), "ordered original option identities " + type.Name);
                Check(attributes.All(a => Equals(a.ConstructorArguments[1].Value, false) && a.NamedArguments.Count == 0), "both original disabled options " + type.Name);
                Check(type.GetConstructors().Length == 1 && type.GetConstructors()[0].GetParameters().Length == 0, "original public zero-parameter constructor " + type.Name);
            }
            FieldInfo[] managerFields = manager.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(managerFields.Select(f => f.Name).SequenceEqual(new[] { "WaypointsUpdated", "m_availableTargets", "m_overrideTargets" }), "all three manager fields in native order");
            Check(managerFields[0].IsPublic && managerFields[0].FieldType == typeof(Action) && !managerFields[0].IsInitOnly, "original public mutable notification delegate");
            Check(managerFields.Skip(1).All(f => f.IsPrivate && f.IsInitOnly && f.FieldType == typeof(List<WaypointTarget>)), "two private readonly real target lists");
            Check(managerFields.All(f => !f.IsStatic && f.GetCustomAttributesData().Count == 0), "no invented manager field attributes");
            FieldInfo[] targetFields = target.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(targetFields.Select(f => f.Name).SequenceEqual(new[] { "m_type", "m_waypointManagerRef" }), "two original target fields in native order");
            Check(targetFields[0].IsPrivate && !targetFields[0].IsInitOnly && targetFields[0].FieldType == typeof(WaypointType), "authored original target enum field");
            Check(targetFields[0].GetCustomAttributesData().Select(a => a.AttributeType).SequenceEqual(new[] { typeof(SerializeField) }), "only original enum serialization attribute");
            Check(targetFields[1].IsPrivate && targetFields[1].IsInitOnly && targetFields[1].FieldType == typeof(SystemRef<WaypointManager>) && targetFields[1].GetCustomAttributesData().Count == 0, "genuine original readonly system reference");
            var managerMethods = manager.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(m => m.MetadataToken).ToArray();
            Check(managerMethods.Select(m => m.Name).SequenceEqual(new[] { "RegisterWaypoint", "RemoveWaypoint", "SetOverrideTargets", "GetTargets" }), "all original manager methods in order");
            for (int i = 0; i < managerMethods.Length; i++)
            {
                var method = managerMethods[i]; var parameters = method.GetParameters();
                Check(method.IsPublic && !method.IsStatic && !method.IsVirtual && !method.IsGenericMethod && method.GetCustomAttributesData().Count == 0, "original manager method flags " + method.Name);
                Check(method.ReturnType == (i == 3 ? typeof(IReadOnlyList<WaypointTarget>) : typeof(void)), "original manager return " + method.Name);
                Check(parameters.Length == (i == 3 ? 0 : 1), "original manager parameter count " + method.Name);
                if (i != 3)
                    Check(parameters[0].Name == (i == 2 ? "waypoints" : "waypointTarget") && parameters[0].ParameterType == (i == 2 ? typeof(IEnumerable<WaypointTarget>) : typeof(WaypointTarget)) && !parameters[0].IsOptional && !parameters[0].IsOut && !parameters[0].ParameterType.IsByRef, "original explicit parameter identity " + method.Name);
            }
            PropertyInfo property = target.GetProperty("Type");
            Check(target.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 1 && property.PropertyType == typeof(WaypointType) && property.SetMethod == null && property.GetMethod.IsPublic, "sole original readonly enum property");
            var targetMethods = target.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(m => m.MetadataToken).ToArray();
            Check(targetMethods.Length == 4 && targetMethods.Take(3).Select(m => m.Name).SequenceEqual(new[] { "get_Type", "OnEnable", "OnDisable" }), "all original target APIs plus one natural callback");
            Check(targetMethods.Take(3).All(m => m.GetParameters().Length == 0 && !m.IsStatic && !m.IsVirtual) && targetMethods.Skip(1).Take(2).All(m => m.IsPrivate && m.ReturnType == typeof(void)), "original target lifecycle flags");
            MethodInfo callback = targetMethods[3];
            Check(callback.Name == "<OnEnable>b__4_0" && callback.IsPrivate && callback.IsDefined(typeof(CompilerGeneratedAttribute), false) && callback.ReturnType == typeof(void) && callback.GetParameters().Length == 1 && callback.GetParameters()[0].Name == "manager" && callback.GetParameters()[0].ParameterType == manager, "genuine natural callback contract without manual generated method");
            return checks;
        }
        private static IEnumerable<WaypointTarget> FailingSequence(WaypointTarget target, Exception marker, Action disposed)
        {
            try { yield return target; throw marker; }
            finally { disposed(); }
        }
        private static IEnumerable<WaypointTarget> MutatingSequence(WaypointManager manager, WaypointTarget next)
        {
            yield return null;
            manager.RegisterWaypoint(next);
            yield return next;
        }
        public static int RunOriginalEngineBoundaries()
        {
            checks = 0;
            var registry = (IDictionary)Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null);
            var registrySnapshot = new List<DictionaryEntry>(); foreach (DictionaryEntry row in registry) registrySnapshot.Add(row);
            // Isolate the actual process row used by genuine MonoBehaviour ctor
            //initializers; restore every preexisting row even if a callback faults.
            string name = ProcessManager.GetDefaultName<WaypointManager>();
            registry.Remove(name);
            var firstObject = new GameObject("Project Lucid original waypoint first fixture"); firstObject.SetActive(false);
            var secondObject = new GameObject("Project Lucid original waypoint second fixture"); secondObject.SetActive(false);
            var absentObject = new GameObject("Project Lucid original waypoint absent fixture"); absentObject.SetActive(false);
            try
            {
                var first = firstObject.AddComponent<WaypointTarget>();
                var second = secondObject.AddComponent<WaypointTarget>();
                var absent = absentObject.AddComponent<WaypointTarget>();
                Check(first.Type.Equals(default(WaypointType)), "target original zero enum default");
                Field(typeof(WaypointTarget), "m_type").SetValue(first, (WaypointType)int.MinValue);
                Check((int)first.Type == int.MinValue, "authored out-of-range enum read preserved");
                var manager = new WaypointManager();
                var available = (List<WaypointTarget>)Field(typeof(WaypointManager), "m_availableTargets").GetValue(manager);
                var overrides = (List<WaypointTarget>)Field(typeof(WaypointManager), "m_overrideTargets").GetValue(manager);
                Check(manager.WaypointsUpdated == null && available.Count == 0 && overrides.Count == 0 && !ReferenceEquals(available, overrides), "original distinct empty lists and null notification");
                Check(ReferenceEquals(manager.GetTargets(), available), "no override returns original available list identity");
                int notifications = 0; manager.WaypointsUpdated = () => notifications++;
                manager.RegisterWaypoint(first); manager.RegisterWaypoint(first); manager.RegisterWaypoint(null); manager.RegisterWaypoint(null); manager.RegisterWaypoint(second);
                Check(notifications == 5 && available.SequenceEqual(new[] { first, null, second }), "duplicate and null registration still notify, distinct list ordering");
                IReadOnlyList<WaypointTarget> live = manager.GetTargets();
                manager.RemoveWaypoint(absent); manager.RemoveWaypoint(null);
                Check(notifications == 7 && ReferenceEquals(live, available) && live.SequenceEqual(new[] { first, second }), "absent removal notifies and prior available view remains live");
                manager.SetOverrideTargets(new[] { second, absent, second, null, first });
                Check(notifications == 8 && overrides.SequenceEqual(new[] { second, absent, second, null, first }), "override preserves duplicates nulls and absent authored targets");
                IReadOnlyList<WaypointTarget> filtered = manager.GetTargets();
                Check(!ReferenceEquals(filtered, available) && !ReferenceEquals(filtered, overrides) && filtered.SequenceEqual(new[] { second, second, first }), "nonempty override ordered duplicate intersection");
                manager.RemoveWaypoint(second);
                Check(filtered.SequenceEqual(new[] { second, second, first }) && manager.GetTargets().SequenceEqual(new[] { first }), "override result snapshots while fresh reads see removal");
                manager.RegisterWaypoint(null);
                Check(manager.GetTargets().SequenceEqual(new[] { null, first }), "null membership retained in override intersection");
                manager.SetOverrideTargets(new[] { absent });
                Check(manager.GetTargets().Count == 0 && !ReferenceEquals(manager.GetTargets(), available) && !ReferenceEquals(manager.GetTargets(), manager.GetTargets()), "nonempty unmatched override yields fresh empty list");
                manager.SetOverrideTargets(Array.Empty<WaypointTarget>());
                Check(ReferenceEquals(manager.GetTargets(), available), "empty override resumes live available identity");
                var marker = new InvalidOperationException("original waypoint callback marker");
                manager.WaypointsUpdated = () => { throw marker; };
                Check(ReferenceEquals(Throws<InvalidOperationException>(() => manager.RegisterWaypoint(second), "register callback fault"), marker) && available.Contains(second), "register mutation precedes unwrapped callback fault");
                Check(ReferenceEquals(Throws<InvalidOperationException>(() => manager.RemoveWaypoint(first), "remove callback fault"), marker) && !available.Contains(first), "remove mutation precedes callback fault");
                Check(ReferenceEquals(Throws<InvalidOperationException>(() => manager.SetOverrideTargets(new[] { second }), "override callback fault"), marker) && overrides.SequenceEqual(new[] { second }), "replacement precedes callback fault");
                notifications = 0; manager.WaypointsUpdated = () => notifications++;
                Throws<ArgumentNullException>(() => manager.SetOverrideTargets(null), "null original sequence");
                Check(overrides.Count == 0 && notifications == 0 && ReferenceEquals(manager.GetTargets(), available), "null sequence clears old overrides before fault, no notification");
                int disposal = 0;
                Check(ReferenceEquals(Throws<InvalidOperationException>(() => manager.SetOverrideTargets(FailingSequence(first, marker, () => disposal++)), "enumeration failure"), marker), "original sequence fault unchanged");
                Check(overrides.SequenceEqual(new[] { first }) && notifications == 0 && disposal == 1, "partial replacement retained, failing enumerator disposed, callback skipped");
                manager.SetOverrideTargets(overrides);
                Check(overrides.Count == 0 && notifications == 1, "self replacement clears the supplied live list first");
                manager.SetOverrideTargets(MutatingSequence(manager, first));
                Check(overrides.SequenceEqual(new[] { null, first }) && notifications == 3, "reentrant registration during override enumeration and final notification");
                manager.WaypointsUpdated = () => manager.WaypointsUpdated = null;
                manager.RegisterWaypoint(first); manager.RegisterWaypoint(first);
                Check(manager.WaypointsUpdated == null, "callback replacement takes effect on following registration");
                var pending = new SystemRef<WaypointManager>("isolated pending waypoint", null);
                Field(typeof(WaypointTarget), "m_waypointManagerRef").SetValue(first, pending);
                Invoke(first, "OnEnable"); Invoke(first, "OnDisable");
                Check(pending.IsNull() && Field(typeof(SystemRef<WaypointManager>), "m_actionOnSystemValid").GetValue(pending) != null, "disable retains original deferred enable callback for missing system");
                var deferredManager = new WaypointManager(); int deferredNotifications = 0; deferredManager.WaypointsUpdated = () => deferredNotifications++;
                Invoke(pending, "Hardlight.ISystemRef.InternalReplaceSystem", deferredManager);
                Check(deferredNotifications == 1 && deferredManager.GetTargets().SequenceEqual(new[] { first }), "later real system replacement registers already disabled target");
                Invoke(first, "OnDisable");
                Check(deferredNotifications == 2 && deferredManager.GetTargets().Count == 0, "valid disable removes and notifies");
                Invoke(first, "OnEnable"); Invoke(first, "OnEnable");
                Check(deferredNotifications == 4 && deferredManager.GetTargets().SequenceEqual(new[] { first }), "immediate duplicate enables still notify through genuine manager");
                deferredManager.WaypointsUpdated = () => { throw marker; };
                Check(ReferenceEquals(Throws<InvalidOperationException>(() => Invoke(first, "OnDisable"), "lifecycle callback fault"), marker) && deferredManager.GetTargets().Count == 0, "actual lifecycle retains mutation and callback fault");
                deferredManager.WaypointsUpdated = null;
                manager.WaypointsUpdated = null; manager.SetOverrideTargets(Array.Empty<WaypointTarget>()); manager.RegisterWaypoint(second);
                UnityEngine.Object.DestroyImmediate(secondObject); secondObject = null;
                Check(second == null && !ReferenceEquals(second, null) && available.Any(value => ReferenceEquals(value, second)), "destroyed Unity wrapper remains in managed original available list");
            }
            finally
            {
                // All fixture GameObjects are inactive; explicit lifecycle calls
                //above do not rely on Editor scheduling or scene startup.
                UnityEngine.Object.DestroyImmediate(firstObject);
                if (secondObject != null) UnityEngine.Object.DestroyImmediate(secondObject);
                UnityEngine.Object.DestroyImmediate(absentObject);
                registry.Clear(); foreach (DictionaryEntry row in registrySnapshot) registry.Add(row.Key, row.Value);
            }
            return checks;
        }
    }
}
