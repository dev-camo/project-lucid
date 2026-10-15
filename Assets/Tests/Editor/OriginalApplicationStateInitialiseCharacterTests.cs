using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // InitialiseMissions iterator MoveNext 06000682 (1.10.1 RVA 0x6474F0). Its compiler
    // fault handler (0x64819D-0x6481D1) runs the 1/-3 cleanup (Dispose 06000681, 0x647430
    // -> <>m__Finally1 06000683, 0x648200: state -1, dispose the mission-state enumerator)
    // before rethrowing, so a resume that faults inside the loop ends the iterator.
    // Only the iterator's real constructor (06000680, 0x6470E0) is used, as for the
    // generated closure in CameraInputDisablerVerification. No field is written, and
    // ProcessManager, MissionManager, App, WaitUntil and coroutine hosts stay untouched.
    public sealed class OriginalApplicationStateInitialiseCharacterTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        [Test]
        public void FaultInTheResumedMissionLoopTerminatesTheIterator() => AssertFaultTerminates(-3);

        [Test]
        public void FaultAfterTheMissionWaitTerminatesTheIterator() => AssertFaultTerminates(1);

        private static void AssertFaultTerminates(int resumeState)
        {
            Type type = IteratorType();
            ConstructorInfo constructor = type.GetConstructor(Instance, null, new[] { typeof(int) }, null);
            Assert.That(constructor, Is.Not.Null);
            var iterator = (IEnumerator)constructor.Invoke(new object[] { resumeState });
            FieldInfo state = ReadOnlyField(type, "_003C_003E1__state", typeof(int));
            Assert.That(state.GetValue(iterator), Is.EqualTo(resumeState));
            Assert.That(ReadOnlyField(type, "_003C_003E7__wrap3", typeof(System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, MissionState>>)).GetValue(iterator), Is.Null);

            // Both resume states enter the protected loop, whose first enumerator call
            // faults on the never-acquired enumerator; the fault itself is rethrown.
            Assert.Throws<NullReferenceException>(() => iterator.MoveNext());
            Assert.That(state.GetValue(iterator), Is.EqualTo(-1), "the fault handler ran the 1/-3 cleanup");
            Assert.That(iterator.Current, Is.Null);

            // A later resume finds the finished state rather than re-entering the failed loop.
            Assert.That(iterator.MoveNext(), Is.False);
            ((IDisposable)iterator).Dispose();
            Assert.That(iterator.MoveNext(), Is.False);
            Assert.That(state.GetValue(iterator), Is.EqualTo(-1));
        }

        private static Type IteratorType()
        {
            MethodInfo factory = typeof(ApplicationStateInitialiseCharacter).GetMethod("InitialiseMissions", Instance);
            Assert.That(factory, Is.Not.Null);
            Type type = factory.GetCustomAttribute<IteratorStateMachineAttribute>(false)?.StateMachineType;
            Assert.That(type, Is.Not.Null);
            Assert.That(type.DeclaringType, Is.EqualTo(typeof(ApplicationStateInitialiseCharacter)));
            Assert.That(typeof(IDisposable).IsAssignableFrom(type), Is.True);
            return type;
        }

        private static FieldInfo ReadOnlyField(Type type, string name, Type fieldType)
        {
            FieldInfo field = type.GetField(name, Instance);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(fieldType));
            return field; // Read only; never assign a private field.
        }
    }
}
