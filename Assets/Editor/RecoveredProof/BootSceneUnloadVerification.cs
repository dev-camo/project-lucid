using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    // Source/factory metadata proof only. Actual original unload path requires the separate live test.
    public static class BootSceneUnloadVerification
    {
        private static int checks;
        private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        private static string Name() => "ProjectLucid-Unload-state-proof-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        public static void Run() { Execute(); Debug.Log("Original boot unload state factory verified checks=" + checks); }
        public static int RunManaged() { Execute(); return checks; }
        private static void Execute()
        {
            checks = 0;
            Type type = typeof(ApplicationStateUnloadBoot);
            Check(type.BaseType == typeof(FSMState) && type.IsPublic && !type.IsSealed, "original state base/visibility");
            Check(type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic).Length == 0, "original state has no synthetic fields");
            var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>().ToArray();
            Check(options.Length == 2 && options[0].Option == Option.NullChecks && options[1].Option == Option.ArrayBoundsChecks, "original ordered compiler options");
            Check(options.All(option => Equals(option.Value, false)), "original compiler option false values");
            Check(type.GetCustomAttribute<GraphNodeMenuFormatAttribute>().Format == "Application/{0}", "original authoring format");
            var machine = Machine(); FSMIdentifier id = Name();
            var first = ApplicationStateUnloadBoot.ConstructInstance(machine, id, "malformed unused JSON");
            var second = ApplicationStateUnloadBoot.ConstructInstance(machine, id, null);
            Check(first is ApplicationStateUnloadBoot && second is ApplicationStateUnloadBoot && !ReferenceEquals(first, second), "factory constructs original state independently and ignores JSON");
            Check(first.FSMId == machine.FSMId && first.StateId == id.Id && second.StateId == id.Id, "original ID/name constructor roundtrip");
            Check(first.GetStateTransitions().Count == 0, "base constructor owns an empty transition list");
            Check(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single().IsPrivate, "original constructor stays private");
            // No SceneManager operation is triggered in this Editor/CLI metadata helper.
        }
    }
}
