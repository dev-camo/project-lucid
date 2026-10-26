using System;
using System.Collections.Specialized;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Verification;

namespace ProjectLucid.Tests
{
    public sealed class HLHapticsRegistryOwnershipTests
    {
        private static string Copy(string value) => new string(value.ToCharArray());

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ExactOwnedRowMayOccupyAnySlotWhileForeignOrderIsPreserved(int slot)
        {
            string first = Copy("first"), second = Copy("second");
            object a = new object(), b = new object(), owned = new object();
            var before = new OrderedDictionary { { first, a }, { second, b } };
            var after = new OrderedDictionary { { first, a }, { second, b } };
            after.Insert(slot, typeof(VibrationManager).ToString(), owned);
            HLHapticsVerification.CheckIndependentRegistryRows(before, after, owned);
        }

        [TestCase("key-identity")]
        [TestCase("value-identity")]
        [TestCase("order")]
        [TestCase("missing-foreign")]
        [TestCase("extra-foreign")]
        [TestCase("wrong-owned-key")]
        [TestCase("wrong-owned-value")]
        [TestCase("missing-owned")]
        public void ComparatorRejectsForeignMutationOrUnprovenOwnedRow(string mutation)
        {
            string first = Copy("first"), second = Copy("second"), target = typeof(VibrationManager).ToString();
            object a = new object(), b = new object(), owned = new object();
            var before = new OrderedDictionary { { first, a }, { second, b } };
            var after = new OrderedDictionary { { first, a }, { second, b }, { target, owned } };
            switch (mutation)
            {
                case "key-identity": after.Remove(first); after.Insert(0, Copy(first), a); break;
                case "value-identity": after[first] = new object(); break;
                case "order": after.Remove(first); after.Insert(1, first, a); break;
                case "missing-foreign": after.Remove(first); break;
                case "extra-foreign": after.Add("extra", new object()); break;
                case "wrong-owned-key": after.Remove(target); after.Add("wrong", owned); break;
                case "wrong-owned-value": after[target] = new object(); break;
                case "missing-owned": after.Remove(target); break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
            Assert.Throws<InvalidOperationException>(() => HLHapticsVerification.CheckIndependentRegistryRows(before, after, owned));
        }

        [Test]
        public void ExistingTargetRemainsForeignAndCannotBeReplaced()
        {
            string target = Copy(typeof(VibrationManager).ToString());
            object borrowed = new object();
            var before = new OrderedDictionary { { target, borrowed } };
            var after = new OrderedDictionary { { target, borrowed } };
            HLHapticsVerification.CheckIndependentRegistryRows(before, after, null);
            object replacement = new object();
            after[target] = replacement;
            Assert.Throws<InvalidOperationException>(() => HLHapticsVerification.CheckIndependentRegistryRows(before, after, replacement));
        }
    }
}
