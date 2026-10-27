using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class UIManagerFamilyVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Genuine UI manager verification: " + label); checks++; }
        private static Type SingleType => typeof(UIContainerManager).Assembly.GetType("Hardlight.UISingular", true);
        private static UIContainerManager Single(Transform parent) => (UIContainerManager)Activator.CreateInstance(SingleType, new object[] { parent });
        private static FieldInfo Own(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static void Declarations()
        {
            Check((int)typeof(UIContainerManager).Attributes == 1048705 && typeof(UIContainerManager).BaseType == typeof(object), "original complete abstract fieldless manager base");
            Check((int)SingleType.Attributes == 1048832 && SingleType.BaseType == typeof(UIContainerManager), "original internal sealed singular visibility and genuine base");
            Check((int)typeof(UITransitionContainerManager).Attributes == 1048577 && typeof(UITransitionContainerManager).BaseType == typeof(UIContainerManager), "original public unsealed transition manager");
            foreach (var row in new[] { new { Type = typeof(UIContainerManager), Expected = new[] { Option.ArrayBoundsChecks, Option.NullChecks } }, new { Type = SingleType, Expected = new[] { Option.ArrayBoundsChecks, Option.NullChecks } }, new { Type = typeof(UITransitionContainerManager), Expected = new[] { Option.NullChecks, Option.ArrayBoundsChecks } } })
            {
                var attrs = row.Type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray(); Check(attrs.Select(a => a.Option).SequenceEqual(row.Expected) && attrs.All(a => Equals(a.Value, false)), row.Type.Name + " exact ordered original options");
                Check(row.Type.GetInterfaces().Length == 0, row.Type.Name + " original zero direct interfaces");
            }
            var baseMethods = typeof(UIContainerManager).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).OrderBy(m => m.MetadataToken).ToArray();
            Check(baseMethods.Select(m => m.Name).SequenceEqual(new[] { "OpenContainer", "CloseAll", "Close", "IsOpen", "IsAnyOpen", "AreExclusivelyOpen", "TryGet", "CloseAllExcept", "SetEnabled" }), "complete original base method order");
            Check(baseMethods.Take(8).All(m => m.IsAbstract && m.IsVirtual) && !baseMethods.Last().IsAbstract && !baseMethods.Last().IsVirtual, "exact eight real contracts and concrete nonvirtual SetEnabled");
            Check(typeof(UIContainerManager).GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0, "original base has no own fields");
            foreach (var row in new[] { new { Type = SingleType, Container = "m_singleContainer" }, new { Type = typeof(UITransitionContainerManager), Container = "m_transitionContainer" } })
            {
                var fields = row.Type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
                Check(fields.Select(f => f.Name).SequenceEqual(new[] { "m_parent", row.Container }) && fields[0].FieldType == typeof(Transform) && fields[1].FieldType == typeof(UIContainer), row.Type.Name + " full original two-field graph/order");
                Check(fields.All(f => f.IsPrivate && !f.IsDefined(typeof(SerializeField), false)) && fields[0].IsInitOnly && !fields[1].IsInitOnly, row.Type.Name + " original readonly parent and mutable nonserialized storage");
                var ctor = row.Type.GetConstructors().Single(); Check(ctor.GetParameters().Single().Name == "parent" && ctor.GetParameters().Single().ParameterType == typeof(Transform), row.Type.Name + " original constructor signature");
                var outArg = row.Type.GetMethod("TryGet").GetParameters()[1]; Check(outArg.Name == "container" && outArg.IsOut && outArg.ParameterType == typeof(UIContainer).MakeByRefType(), row.Type.Name + " complete original out contract");
            }
        }
        public static int RunManaged()
        {
            checks = 0; Declarations();
            foreach (UIContainerManager manager in new[] { Single(null), new UITransitionContainerManager(null) })
            {
                Check(Own(manager.GetType(), "m_parent").GetValue(manager) == null, manager.GetType().Name + " genuine constructor stores passed null parent after real base construction");
                Check(!manager.IsAnyOpen(), manager.GetType().Name + " empty original Unity predicate path");
                Check(!manager.IsOpen(null), manager.GetType().Name + " empty IsOpen avoids identifier comparison");
                Check(manager.AreExclusivelyOpen(null), manager.GetType().Name + " original empty manager accepts null requested list without reading it");
                Check(manager.AreExclusivelyOpen(new UIContainerIdentifier[] { null, null }), manager.GetType().Name + " original empty manager accepts nonempty requested list");
                UIContainer found; Check(!manager.TryGet(null, out found) && ReferenceEquals(found, null), manager.GetType().Name + " failed original TryGet writes null");
                manager.SetEnabled(null, false); manager.Close(null); manager.CloseAll(); manager.CloseAllExcept(null); Check(!manager.IsAnyOpen(), manager.GetType().Name + " original empty guard routes require no provider/container/canvas");
            }
            return checks;
        }
        public static void Run() { int total = RunManaged(); Debug.Log("PASS genuine UI manager declarations/empty predicates checks=" + total + "; owned frame proof lives in UIManagerFamilyLifecycleTests."); }
    }
}
