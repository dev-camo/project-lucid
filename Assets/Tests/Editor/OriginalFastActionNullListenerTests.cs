using System;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OriginalFastActionNullListenerTests
    {
        // HLUnityCore.Runtime 0x06000de6/0x06000de7, x64 0x134fd30/0x134fe80:
        // null listeners use IsInst before the checked return conversion.
        [Test]
        public void NullListenerReturnsTheSameInstanceForTheSelfType()
        {
            Good action = new Good();

            Assert.AreSame(action, action + (Action)null);
            Assert.That(action.GetInvocationListCount(), Is.Zero);
            Assert.AreSame(action, FastActionBase<Good, Action>.AddUnique(action, null));
            Assert.That(action.GetInvocationListCount(), Is.Zero);
        }

        [Test]
        public void NullListenerOnASiblingSubclassReturnsNullWithoutThrowing()
        {
            FastActionBase<Good, Action> action = new Sibling();

            Assert.DoesNotThrow(() => Assert.IsNull(action + (Action)null));
            Assert.That(action.GetInvocationListCount(), Is.Zero);
            Assert.DoesNotThrow(() => Assert.IsNull(FastActionBase<Good, Action>.AddUnique(action, null)));
            Assert.That(action.GetInvocationListCount(), Is.Zero);
        }

        [Test]
        public void NullListenerOnNullReturnsNull()
        {
            FastActionBase<Good, Action> action = null;

            Assert.IsNull(action + (Action)null);
            Assert.IsNull(FastActionBase<Good, Action>.AddUnique(action, null));
        }

        [Test]
        public void NonNullListenerOnASiblingStillThrowsAfterAdding()
        {
            Action listener = () => { };
            FastActionBase<Good, Action> addition = new Sibling();
            FastActionBase<Good, Action> unique = new Sibling();

            Assert.Throws<InvalidCastException>(() => { _ = addition + listener; });
            Assert.That(addition.GetInvocationListCount(), Is.EqualTo(1));
            Assert.AreSame(listener, addition.GetInvocationList()[0]);

            Assert.Throws<InvalidCastException>(() => FastActionBase<Good, Action>.AddUnique(unique, listener));
            Assert.That(unique.GetInvocationListCount(), Is.EqualTo(1));
            Assert.AreSame(listener, unique.GetInvocationList()[0]);
        }

        // 0x06000de8, x64 0x1350080: null listener returns null; otherwise
        // removal precedes IsInst/RaiseInvalidCastException at 0x1350187/0x13501cc.
        [Test]
        public void SubtractionKeepsItsOwnResults()
        {
            Action listener = () => { };
            FastActionBase<Good, Action> action = new Sibling();
            action.GetInvocationList().Add(listener);

            Assert.IsNull(action - (Action)null);
            Assert.That(action.GetInvocationListCount(), Is.EqualTo(1));
            Assert.AreSame(listener, action.GetInvocationList()[0]);
            Assert.Throws<InvalidCastException>(() => { _ = action - listener; });
            Assert.That(action.GetInvocationListCount(), Is.Zero);
        }

        public sealed class Good : FastActionBase<Good, Action> { }
        public sealed class Sibling : FastActionBase<Good, Action> { }
    }
}
