using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Reflection and ownership helpers shared by the world-trigger fixtures. Rows and
    // action rows are admitted through VerificationFixtureOwnership and released in
    // reverse order after the owned objects are destroyed.
    internal static class WorldTriggerTestSupport
    {
        internal const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                               BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal static FieldInfo Field(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Declared);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        internal static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);

        internal static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

        // The most derived declaration with this many parameters; an original exception is
        // rethrown unwrapped so a case can assert on it.
        internal static object Call(object target, string name, params object[] arguments)
        {
            for (Type current = target.GetType(); current != null; current = current.BaseType)
            {
                MethodInfo method = current.GetMethods(Members)
                    .FirstOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
                if (method == null) continue;
                try
                {
                    return method.Invoke(target, arguments);
                }
                catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                    throw;
                }
            }
            throw new MissingMethodException(target.GetType().FullName, name);
        }

        // Metadata flags of the declared fields and methods, in declaration order.
        internal static void AssertMembers(Type type, int typeFlags, (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            Assert.That((int)type.Attributes, Is.EqualTo(typeFlags), type.FullName + " flags");
            FieldInfo[] declaredFields = type.GetFields(Declared).OrderBy(field => field.MetadataToken).ToArray();
            Assert.That(declaredFields.Select(field => (field.Name, (int)field.Attributes)).ToArray(), Is.EqualTo(fields), type.FullName + " fields");
            MethodBase[] declaredMethods = type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared))
                .OrderBy(method => method.MetadataToken).ToArray();
            Assert.That(declaredMethods.Select(method => (method.Name, (int)method.Attributes)).ToArray(), Is.EqualTo(methods), type.FullName + " methods");
        }

        internal static Type[] AttributeTypes(MemberInfo member) => member.GetCustomAttributesData().Select(data => data.AttributeType).ToArray();

        internal static void AssertIl2CppOptions(Type type, params int[] options)
        {
            CustomAttributeData[] attributes = type.GetCustomAttributesData()
                .Where(data => data.AttributeType.Name == "Il2CppSetOptionAttribute").ToArray();
            Assert.That(attributes.Select(data => Convert.ToInt32(data.ConstructorArguments[0].Value)).ToArray(), Is.EqualTo(options), type.FullName);
            Assert.That(attributes.All(data => Equals(data.ConstructorArguments[1].Value, false)), Is.True, type.FullName);
        }

        internal static string Tooltip(FieldInfo field) =>
            (string)field.GetCustomAttributesData().Single(data => data.AttributeType == typeof(TooltipAttribute)).ConstructorArguments[0].Value;

        // The real TimeScaledUtilities_SDT iterator a wait yielded, read without stepping it.
        internal static (float seconds, object category) WaitArguments(object current)
        {
            Assert.That(current, Is.InstanceOf<IEnumerator>());
            Assert.That(current.GetType().DeclaringType, Is.EqualTo(typeof(HardlightProject.TimeScaledUtilities_SDT)), "an unstarted fixed-seconds wait");
            return ((float)current.GetType().GetField("waitSeconds").GetValue(current), current.GetType().GetField("timeCategory").GetValue(current));
        }

        internal sealed class Owned
        {
            private readonly List<Object> m_objects = new List<Object>();
            private readonly List<IDisposable> m_leases = new List<IDisposable>();
            private readonly HashSet<string> m_leasedRows = new HashSet<string>(); // the rows this Owned took with LeaseRow

            private Owned() { }

            internal T Track<T>(T value) where T : Object
            {
                m_objects.Add(value);
                return value;
            }

            internal GameObject Host(string name, bool active = true, Transform parent = null)
            {
                var host = Track(new GameObject(name));
                if (parent != null) host.transform.SetParent(parent, false);
                host.SetActive(active);
                return host;
            }

            // A lease the case takes itself, released with the others, newest first.
            internal T Lease<T>(T lease) where T : IDisposable
            {
                m_leases.Add(lease);
                return lease;
            }

            // Taken before any constructor that subscribes to these actions.
            internal void LeaseActions(params SystemAction[] actions) => m_leases.Add(new VerificationFixtureOwnership.ActionRows(actions));

            // A row that original construction or lookup creates; it must be released idle.
            internal void LeaseRow<T>() where T : class, ISystem
            {
                string name = ProcessManager.GetDefaultName<T>();
                m_leases.Add(new VerificationFixtureOwnership.LifecycleRow(name));
                m_leasedRows.Add(name);
            }

            // The row the fixture registers its own system into, before any component's field initialiser caches a
            // typed reference from it. An absent row becomes the fixture's. A row that exists must be idle, because
            // an original type initialiser creates some permanently (TimeScaledUtilities' TimeManager row): it is
            // borrowed, the system is unregistered first and the row is handed back as admitted.
            internal T Register<T>(T system) where T : class, ISystem
            {
                string name = ProcessManager.GetDefaultName<T>();
                var borrowed = new VerificationFixtureOwnership.LifecycleRow(name);
                if (borrowed.Existed)
                {
                    m_leases.Add(borrowed);
                    SystemRef reference = ProcessManager.RegisterSystem(system, name);
                    // As RegistryRow.Dispose: release only a row that still holds this fixture's own system.
                    m_leases.Add(new CharacterTriggerTestSupport.ReleaseAction(() =>
                    {
                        if (!ReferenceEquals(reference.GetSafe(), system))
                            throw new InvalidOperationException("Fixture registry row has a foreign owner: " + name);
                        ProcessManager.UnregisterSystem(name);
                    }));
                    return system;
                }
                var row = new VerificationFixtureOwnership.RegistryRow(name);
                m_leases.Add(row);
                row.Register(system);
                return system;
            }

            // The system goes into a row the case already leased with LeaseRow. Register borrows only an idle row, but a component
            // that waited on the row (InvokeOnValid) leaves its deferred callback there until the system arrives. The system leaves
            // the row first at release, and the row lease then checks the row idle.
            internal T RegisterIntoLeasedRow<T>(T system) where T : class, ISystem
            {
                string name = ProcessManager.GetDefaultName<T>();
                if (!m_leasedRows.Contains(name))
                    throw new InvalidOperationException("Fixture registry row was not leased by this case with LeaseRow: " + name);
                SystemRef reference = ProcessManager.RegisterSystem(system, name);
                m_leases.Add(new CharacterTriggerTestSupport.ReleaseAction(() =>
                {
                    if (!ReferenceEquals(reference.GetSafe(), system))
                        throw new InvalidOperationException("Fixture registry row has a foreign owner: " + name);
                    ProcessManager.UnregisterSystem(name);
                }));
                return system;
            }

            internal static void Run(Action<Owned> body)
            {
                var owned = new Owned();
                VerificationFixtureOwnership.RunThenCleanUp(() => body(owned),
                    () => { foreach (Object value in owned.m_objects) if (value != null) Object.DestroyImmediate(value); },
                    owned.ReleaseLeases);
            }

            // Newest first, each release attempted even after an earlier one failed.
            private void ReleaseLeases()
            {
                Action[] releases = Enumerable.Range(0, m_leases.Count).Reverse()
                    .Select(index => (Action)m_leases[index].Dispose).ToArray();
                VerificationFixtureOwnership.RunThenCleanUp(() => { }, releases);
            }
        }
    }
}
