using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;

namespace ProjectLucid.Editor
{
    public static class LevelManagerSystemsVerification
    {
        // Test-only systems exercise the genuine empty ISystem contract and real
        // ProcessManager callbacks. They do not replace an original runtime type.
        private class Probe : ISystem
        {
            public static int Creations;
            public Probe() { Creations++; }
        }
        private sealed class Other : Probe { }
        private sealed class CallbackFailure : Exception { }
        private static int checks;
        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Level managed systems verification: " + label);
            checks++;
        }
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            try { action(); }
            catch (T) { Check(true, label); return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name + ": " + label);
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        private static Dictionary<Type, ISystem> Rows(LevelManagerSystems owner) =>
            (Dictionary<Type, ISystem>)Field(typeof(LevelManagerSystems), "m_managedSystems").GetValue(owner);

        private static DictionaryEntry[] Snapshot(IDictionary dictionary)
        {
            var rows = new List<DictionaryEntry>();
            IDictionaryEnumerator iterator = dictionary.GetEnumerator();
            while (iterator.MoveNext()) rows.Add(iterator.Entry);
            return rows.ToArray();
        }

        public static int RunManaged()
        {
            checks = 0;
            FieldInfo[] state = new[] { "s_systemDictionary", "s_systemActionLookup", "s_actionList", "s_systemActionInProgress" }
                .Select(name => Field(typeof(ProcessManager), name)).ToArray();
            object[] previous = state.Select(field => field.GetValue(null)).ToArray();
            var registryRows = Snapshot((IDictionary)previous[0]);
            var actionRows = Snapshot((IDictionary)previous[1]);
            var actionList = ((IList)previous[2]).Cast<object>().ToArray();
            int previousCreations = Probe.Creations;
            try
            {
                ((IDictionary)previous[0]).Clear();
                ((IDictionary)previous[1]).Clear();
                ((IList)previous[2]).Clear();
                state[3].SetValue(null, false);
                var owner = new LevelManagerSystems(null);
                Check(Rows(owner).Count == 0, "constructor starts with empty system ownership");
                Check(((IDictionary)Field(typeof(LevelManagerSystems), "m_managedGameObjects").GetValue(owner)).Count == 0,
                    "constructor starts with empty object ownership");
                Check(Field(typeof(LevelManagerSystems), "m_parent").GetValue(owner) == null, "null parent is retained without an engine call");
                Check(typeof(LevelManagerSystems).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .All(field => field.IsInitOnly), "all three original fields are readonly");
                CheckDeclarations();
                Check(!owner.HasSystemOfType<Probe>(), "absent row is not owned");
                Probe first = new Probe();
                int initialisations = 0, shutdowns = 0;
                first.SubscribeToAction(SystemAction.Initialise, context =>
                {
                    Check(context == null, "initialise receives default null context");
                    Check(ReferenceEquals(ProcessManager.GetSystemSafe<Probe>(), first), "registration precedes initialise");
                    Check(!owner.HasSystemOfType<Probe>(), "ownership publication follows initialise");
                    initialisations++;
                });
                first.SubscribeToAction(SystemAction.Shutdown, context =>
                {
                    Check(context == null, "shutdown receives default null context");
                    Check(ReferenceEquals(ProcessManager.GetSystemSafe<Probe>(), first), "shutdown callback precedes unregistration");
                    Check(ReferenceEquals(Rows(owner)[typeof(Probe)], first), "shutdown callback precedes ownership removal");
                    shutdowns++;
                });
                owner.AddManagedSystem(first);
                Check(initialisations == 1 && owner.HasSystemOfType<Probe>(), "successful add publishes after one initialise");
                Check(ReferenceEquals(Rows(owner)[typeof(Probe)], first), "ownership keeps the exact supplied object");
                int count = Probe.Creations;
                owner.AddManagedSystem<Probe>();
                Check(Probe.Creations == count && initialisations == 1, "valid existing system suppresses construction and initialise");
                owner.AddManagedSystem<Probe>(true);
                Probe replacement = ProcessManager.GetSystemSafe<Probe>();
                Check(shutdowns == 1, "constructor replacement dispatches old shutdown exactly once");
                Check(Probe.Creations == count + 1 && !ReferenceEquals(first, replacement), "new instance is constructed after removal");
                Check(ReferenceEquals(Rows(owner)[typeof(Probe)], replacement), "constructor replacement publishes the new instance");
                Check(!ProcessManager.UnsubscribeFromAction(first, SystemAction.Initialise), "unregistration removes old initialise callback");
                Check(!ProcessManager.UnsubscribeFromAction(first, SystemAction.Shutdown), "unregistration removes old shutdown callback");

                Probe supplied = new Probe();
                bool suppliedShutdown = false;
                supplied.SubscribeToAction(SystemAction.Shutdown, _ => suppliedShutdown = true);
                Throws<InvalidOperationException>(() => owner.ReplaceManagedSystem(supplied), "different replacement does not silently remove the registered old instance");
                Check(suppliedShutdown, "ReplaceManagedSystem shuts down the supplied new instance");
                Check(ReferenceEquals(ProcessManager.GetSystemSafe<Probe>(), replacement), "failed replacement leaves old registry entry valid");
                Check(!owner.HasSystemOfType<Probe>(), "supplied-object removal removes the ownership key before registration fails");
                owner.AddManagedSystem(replacement);
                replacement.SubscribeToAction(SystemAction.Shutdown, _ =>
                {
                    Check(owner.HasSystemOfType<Probe>(), "faulting shutdown sees retained ownership");
                    throw new CallbackFailure();
                });
                Throws<CallbackFailure>(() => owner.RemoveManagedSystem(replacement), "shutdown failure propagates");
                Check(owner.HasSystemOfType<Probe>() && ReferenceEquals(ProcessManager.GetSystemSafe<Probe>(), replacement),
                    "faulting removal retains registry and ownership");
                replacement.UnsubscribeFromAction(SystemAction.Shutdown);
                owner.RemoveManagedSystem(replacement);
                Check(!owner.HasSystemOfType<Probe>() && ProcessManager.GetSystemRef<Probe>().IsNull(), "successful removal revokes registry and ownership");

                Probe failed = new Probe();
                failed.SubscribeToAction(SystemAction.Initialise, _ => { throw new CallbackFailure(); });
                Throws<CallbackFailure>(() => owner.AddManagedSystem(failed), "initialise failure propagates");
                Check(!owner.HasSystemOfType<Probe>() && ReferenceEquals(ProcessManager.GetSystemSafe<Probe>(), failed),
                    "failed initialise retains prior registration without ownership publication");
                failed.UnsubscribeFromAction(SystemAction.Initialise);
                ProcessManager.UnregisterSystem(failed);
                owner.AddManagedSystem(failed);

                var other = new Other();
                int otherInitialise = 0;
                other.SubscribeToAction(SystemAction.Initialise, _ => otherInitialise++);
                owner.AddManagedSystem<Probe>(other, true);
                Check(otherInitialise == 1 && ReferenceEquals(ProcessManager.GetSystemSafe<Other>(), other), "registry default name uses the concrete supplied type");
                Check(ReferenceEquals(Rows(owner)[typeof(Probe)], other) && !owner.HasSystemOfType<Other>(),
                    "ownership key uses the generic type rather than the concrete instance type");
                Check(ReferenceEquals(ProcessManager.GetSystemSafe<Probe>(), failed), "generic ownership replacement does not unregister a separately named old registry row");
                owner.RemoveManagedSystem<Probe>(null);
                Check(!owner.HasSystemOfType<Probe>(), "null removal still removes the generic ownership key");
                Check(ReferenceEquals(ProcessManager.GetSystemSafe<Other>(), other), "null removal has no shutdown or registry side effect");
                Rows(owner)[typeof(Probe)] = null;
                Check(owner.HasSystemOfType<Probe>(), "a retained null-valued row counts as present");
                owner.RemoveManagedSystem<Probe>(null);
                Check(!owner.HasSystemOfType<Probe>(), "null-valued ownership row can be removed");
                ProcessManager.UnregisterSystem(failed);
                ProcessManager.UnregisterSystem(other);

                var events = new List<string>();
                first = new Probe();
                other = new Other();
                first.SubscribeToAction(SystemAction.Shutdown, _ =>
                {
                    Check(owner.HasSystemOfType<Probe>(), "Shutdown retains a concrete ownership key during its interface-generic call");
                    events.Add("first");
                });
                other.SubscribeToAction(SystemAction.Shutdown, _ =>
                {
                    Check(owner.HasSystemOfType<Probe>() && owner.HasSystemOfType<Other>(), "live Values enumeration precedes final Clear");
                    events.Add("other");
                });
                owner.AddManagedSystem(first);
                owner.AddManagedSystem(other);
                owner.Shutdown();
                Check(events.SequenceEqual(new[] { "first", "other" }), "shutdown walks the genuine dictionary Values without a reverse or snapshot order");
                Check(!owner.HasSystemOfType<Probe>() && !owner.HasSystemOfType<Other>(), "both concrete rows clear after enumeration");
                Check(ProcessManager.GetSystemRef<Probe>().IsNull() && ProcessManager.GetSystemRef<Other>().IsNull(), "shutdown revokes both registered systems");
                owner.Shutdown();
                Check(events.Count == 2, "empty shutdown is repeatable without redispatch");

                first = new Probe();
                other = new Other();
                first.SubscribeToAction(SystemAction.Shutdown, _ => { throw new CallbackFailure(); });
                other.SubscribeToAction(SystemAction.Shutdown, _ => events.Add("unexpected"));
                owner.AddManagedSystem(first);
                owner.AddManagedSystem(other);
                Throws<CallbackFailure>(owner.Shutdown, "whole shutdown aborts at its first failing callback");
                Check(events.Count == 2 && Rows(owner).Count == 2, "failed shutdown retains all rows and skips later callbacks");
                Check(ReferenceEquals(ProcessManager.GetSystemSafe<Probe>(), first) && ReferenceEquals(ProcessManager.GetSystemSafe<Other>(), other),
                    "failed shutdown leaves both original registry entries live");
                first.UnsubscribeFromAction(SystemAction.Shutdown);
                owner.Shutdown();
                Check(Rows(owner).Count == 0 && events.Last() == "unexpected", "successful retry resumes normal teardown");
                return checks;
            }
            finally
            {
                IDictionary registry = (IDictionary)previous[0], actions = (IDictionary)previous[1];
                registry.Clear();
                foreach (DictionaryEntry row in registryRows) registry.Add(row.Key, row.Value);
                actions.Clear();
                foreach (DictionaryEntry row in actionRows) actions.Add(row.Key, row.Value);
                IList list = (IList)previous[2];
                list.Clear();
                foreach (object callback in actionList) list.Add(callback);
                state[3].SetValue(null, previous[3]);
                Probe.Creations = previousCreations;
            }
        }

        private static void CheckDeclarations()
        {
            Type type = typeof(LevelManagerSystems);
            Check(type.IsPublic && !type.IsSealed && type.BaseType == typeof(object), "original public unsealed object-derived type");
            Check((type.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original BeforeFieldInit marker");
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Check(fields.Length == 3 && fields.Select(field => field.Name).SequenceEqual(new[] { "m_parent", "m_managedSystems", "m_managedGameObjects" }),
                "exact original field names and declaration order");
            Check(fields.All(field => field.Attributes == (FieldAttributes.Private | FieldAttributes.InitOnly)), "exact private readonly field flags");
            Check(fields[0].FieldType == typeof(UnityEngine.Transform) && fields[1].FieldType == typeof(Dictionary<Type, ISystem>) &&
                fields[2].FieldType == typeof(Dictionary<Type, UnityEngine.GameObject>), "genuine field type graph");
            CustomAttributeData[] options = type.GetCustomAttributesData().Where(attribute =>
                attribute.AttributeType == typeof(Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute)).ToArray();
            Check(options.Length == 2 && (int)options[0].ConstructorArguments[0].Value == 1 &&
                (int)options[1].ConstructorArguments[0].Value == 2 && options.All(attribute => (bool)attribute.ConstructorArguments[1].Value == false),
                "original ordered IL2CPP null/bounds options");
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(methods.Length == 9 && type.GetConstructors().Length == 1 && type.GetProperties().Length == 0,
                "complete nine methods plus original constructor, no invented property");
            foreach (MethodInfo method in methods)
            {
                Check(method.Attributes == ((method.Name == "RemoveManagedGameObject" ? MethodAttributes.Private : MethodAttributes.Public) | MethodAttributes.HideBySig),
                    "original visibility and method flags: " + method.Name);
                if (!method.IsGenericMethodDefinition)
                {
                    Check(method.Name == "Shutdown" && method.GetParameters().Length == 0, "only Shutdown is nongeneric");
                    continue;
                }
                Type parameter = method.GetGenericArguments().Single();
                bool component = method.Name == "AddMonoBehaviour" || method.Name == "ReplacePrefab";
                bool construction = method.Name == "AddManagedSystem" && method.GetParameters().Length == 1;
                GenericParameterAttributes flags = component ? GenericParameterAttributes.None : GenericParameterAttributes.ReferenceTypeConstraint;
                if (construction) flags |= GenericParameterAttributes.DefaultConstructorConstraint;
                Check(parameter.Name == "T" && parameter.GenericParameterPosition == 0 && parameter.GenericParameterAttributes == flags,
                    "original generic flags: " + method.Name);
                Type[] constraints = parameter.GetGenericParameterConstraints();
                Check(constraints.SequenceEqual(component ? new[] { typeof(UnityEngine.Component), typeof(ISystem) } : new[] { typeof(ISystem) }),
                    "complete original generic constraints: " + method.Name);
                foreach (ParameterInfo argument in method.GetParameters().Where(argument => argument.Name == "canReplace"))
                    Check(argument.ParameterType == typeof(bool) && (int)argument.Attributes == 4112 && Equals(argument.DefaultValue, false),
                        "original optional false replacement flag");
            }
        }
    }
}
