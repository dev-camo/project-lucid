using System;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Original HLUnityCore.Runtime InspectableType 0x02000278, SerialisableEnum 0x02000222
    // and ImplementsTypeAttribute 0x02000277, checked against the 1.10.1 x86-64 bodies.
    // Every instance is owned by its test and none of the three types has static state.
    // The failure paths that call HLOutput.LogError are captured through an owned,
    // inactive HLUnityCore (OwnedHLOutputErrorCapture) and their exact text is asserted.
    public sealed class OriginalSerialisedTypeReferenceTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public enum Colour { Red, Green }

        [Flags]
        public enum Mask { None = 0, A = 1, B = 2 }

        public abstract class BaseRow { }

        public sealed class ConcreteRow : BaseRow { }

        public abstract class AbstractRow : BaseRow { }

        public sealed class UnrelatedRow { }

        public interface IRow { }

        public struct ValueRow : IRow { }

        public sealed class ImplementingRow : IRow { }

        private sealed class HiddenRow : BaseRow { }

        private static string SerializedType(InspectableType inspectable)
        {
            return (string)typeof(InspectableType).GetField("m_serializedType", Private).GetValue(inspectable);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, Private).SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, Private).GetValue(target);
        }

        // 0x1b1f310: FullName + ", " + Assembly.GetName().Name, so the simple assembly
        // name is used and version, culture and key token are absent.
        [Test]
        public void InspectableTypeStoresFullNameWithTheSimpleAssemblyName()
        {
            var inspectable = new InspectableType(typeof(KeyValueParser));

            Assert.That(inspectable.Type, Is.SameAs(typeof(KeyValueParser)));
            Assert.That(SerializedType(inspectable), Is.EqualTo("Hardlight.KeyValueParser, HLUnityCore.Runtime"));
            Assert.That(SerializedType(inspectable), Is.Not.EqualTo(typeof(KeyValueParser).AssemblyQualifiedName));
            Assert.That(inspectable.ToString(), Is.EqualTo("Hardlight.KeyValueParser"));
        }

        // 0x1b1f3ce returns "" (not null) for a null type; ToString 0x1b1f529 gives "(None)".
        [Test]
        public void InspectableTypeWithoutATypeSerialisesEmptyAndPrintsNone()
        {
            var inspectable = new InspectableType(null);

            Assert.That(InspectableType.GetAssemblyName(null), Is.EqualTo(string.Empty));
            Assert.That(SerializedType(inspectable), Is.EqualTo(string.Empty));
            Assert.That(inspectable.ToString(), Is.EqualTo("(None)"));
        }

        // 0x1b1f3e0: the stored name is resolved with Type.GetType after deserialisation;
        // an empty name clears the type and an unknown name resolves to null.
        [Test]
        public void InspectableTypeResolvesItsNameAfterDeserialisation()
        {
            var inspectable = new InspectableType(typeof(KeyValueParser));
            ISerializationCallbackReceiver receiver = inspectable;

            SetField(inspectable, "m_type", null);
            receiver.OnAfterDeserialize();
            Assert.That(inspectable.Type, Is.SameAs(typeof(KeyValueParser)));

            SetField(inspectable, "m_serializedType", "Hardlight.NoSuchType, HLUnityCore.Runtime");
            receiver.OnAfterDeserialize();
            Assert.That(inspectable.Type, Is.Null);

            SetField(inspectable, "m_type", typeof(KeyValueParser));
            SetField(inspectable, "m_serializedType", string.Empty);
            receiver.OnAfterDeserialize();
            Assert.That(inspectable.Type, Is.Null);
        }

        // 0x1b1f4a0 is a bare return: serialising never rewrites the stored name from m_type.
        [Test]
        public void InspectableTypeBeforeSerialiseLeavesTheStoredNameUnchanged()
        {
            var inspectable = new InspectableType(typeof(KeyValueParser));
            SetField(inspectable, "m_type", typeof(BuildMetaData));

            ((ISerializationCallbackReceiver)inspectable).OnBeforeSerialize();

            Assert.That(SerializedType(inspectable), Is.EqualTo("Hardlight.KeyValueParser, HLUnityCore.Runtime"));
        }

        // 0x1b13420 writes the assembly-qualified type name and Enum.ToString(); the
        // constructor (0x1b133d0) sets only m_value.
        [Test]
        public void SerialisableEnumWritesTheQualifiedTypeAndValueTextOnlyWhenSerialised()
        {
            var serialisable = new SerialisableEnum(Mask.A | Mask.B);

            Assert.That(GetField(serialisable, "m_enumType"), Is.Null);
            Assert.That(GetField(serialisable, "m_enumValue"), Is.Null);

            ((ISerializationCallbackReceiver)serialisable).OnBeforeSerialize();

            Assert.That(GetField(serialisable, "m_enumType"), Is.EqualTo(typeof(Mask).AssemblyQualifiedName));
            Assert.That(GetField(serialisable, "m_enumValue"), Is.EqualTo("A, B"));
        }

        // The null-conditional paths at 0x1b13451 and 0x1b1347e store null in both strings.
        [Test]
        public void SerialisableEnumWithoutAValueSerialisesNulls()
        {
            var serialisable = new SerialisableEnum(Colour.Green);
            ((ISerializationCallbackReceiver)serialisable).OnBeforeSerialize();
            serialisable.Assign(null);

            ((ISerializationCallbackReceiver)serialisable).OnBeforeSerialize();

            Assert.That(GetField(serialisable, "m_enumType"), Is.Null);
            Assert.That(GetField(serialisable, "m_enumValue"), Is.Null);
            Assert.That((Enum)serialisable, Is.Null);
        }

        // 0x1b134a0: Type.GetType then Enum.TryParse(Type, string, out object), so a
        // flags combination and a numeric string both parse back to the boxed enum.
        [Test]
        public void SerialisableEnumParsesTheStoredTextBackAfterDeserialisation()
        {
            var serialisable = new SerialisableEnum(Mask.A | Mask.B);
            ISerializationCallbackReceiver receiver = serialisable;
            receiver.OnBeforeSerialize();
            serialisable.Assign(null);

            receiver.OnAfterDeserialize();
            Assert.That((Enum)serialisable, Is.EqualTo(Mask.A | Mask.B));
            Assert.That(((Enum)serialisable).GetType(), Is.SameAs(typeof(Mask)));

            SetField(serialisable, "m_enumValue", "1");
            receiver.OnAfterDeserialize();
            Assert.That((Enum)serialisable, Is.EqualTo(Mask.A));
        }

        // An unparsable value (0x1b13688) logs the five-part Concat and clears the previous
        // value instead of keeping it.
        [Test]
        public void SerialisableEnumLogsAndClearsTheValueWhenTheTextDoesNotParse()
        {
            var serialisable = new SerialisableEnum(Colour.Green);
            ISerializationCallbackReceiver receiver = serialisable;
            receiver.OnBeforeSerialize();
            string enumType = typeof(Colour).AssemblyQualifiedName;

            using (var capture = new OwnedHLOutputErrorCapture())
            {
                SetField(serialisable, "m_enumValue", "Purple");
                receiver.OnAfterDeserialize();

                Assert.That((Enum)serialisable, Is.Null);
                Assert.That(capture.Errors, Is.EqualTo(new[] { "Unable to parse Enum type (" + enumType + ") for (Purple).|0" }));
            }
        }

        // An unknown type (0x1b1358f) is reported only when a value string is present
        // (IsNullOrEmpty at 0x1b13596); both cases clear the previous value.
        [Test]
        public void SerialisableEnumReportsAnUnknownTypeOnlyWhenAValueIsPresent()
        {
            const string unknownType = "Hardlight.NoSuchEnum, HLUnityCore.Runtime";
            var serialisable = new SerialisableEnum(Colour.Red);
            ISerializationCallbackReceiver receiver = serialisable;

            using (var capture = new OwnedHLOutputErrorCapture())
            {
                SetField(serialisable, "m_enumType", unknownType);
                SetField(serialisable, "m_enumValue", string.Empty);
                receiver.OnAfterDeserialize();
                Assert.That((Enum)serialisable, Is.Null);
                Assert.That(capture.Errors, Is.Empty);

                serialisable.Assign(Colour.Red);
                SetField(serialisable, "m_enumValue", "Red");
                receiver.OnAfterDeserialize();
                Assert.That((Enum)serialisable, Is.Null);
                Assert.That(capture.Errors, Is.EqualTo(new[] { "Could not find enum type: " + unknownType + "|0" }));
            }
        }

        // 0x1b1f210: null, non-visible, non-class and abstract candidates fail before the
        // IsAssignableFrom test; the base type itself is abstract and fails too.
        [Test]
        public void ImplementsTypeAcceptsOnlyVisibleConcreteAssignableClasses()
        {
            var attribute = new ImplementsTypeAttribute(typeof(BaseRow));

            Assert.That(attribute.IsConstraintSatisfied(typeof(ConcreteRow)), Is.True);
            Assert.That(attribute.IsConstraintSatisfied(null), Is.False);
            Assert.That(attribute.IsConstraintSatisfied(typeof(HiddenRow)), Is.False);
            Assert.That(attribute.IsConstraintSatisfied(typeof(AbstractRow)), Is.False);
            Assert.That(attribute.IsConstraintSatisfied(typeof(BaseRow)), Is.False);
            Assert.That(attribute.IsConstraintSatisfied(typeof(UnrelatedRow)), Is.False);
        }

        // An interface base admits implementing classes but never the value types or
        // interfaces that also satisfy IsAssignableFrom.
        [Test]
        public void ImplementsTypeWithAnInterfaceBaseRejectsStructsAndInterfaces()
        {
            var attribute = new ImplementsTypeAttribute(typeof(IRow));

            Assert.That(attribute.IsConstraintSatisfied(typeof(ImplementingRow)), Is.True);
            Assert.That(typeof(IRow).IsAssignableFrom(typeof(ValueRow)), Is.True);
            Assert.That(attribute.IsConstraintSatisfied(typeof(ValueRow)), Is.False);
            Assert.That(attribute.IsConstraintSatisfied(typeof(IRow)), Is.False);
        }
    }
}
