using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Pooling;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // ObjectPool<FSMStateChangeAction> is one static pool shared with runtime
    // states. Each FSM verifier method that leases actions destroyed them only
    // after its assertions, so a failure left them leased for the rest of the
    // domain. A fault injected after the method's acquisitions must now leave
    // the pool as it was, keep an action borrowed before the run leased with
    // its parent graph, and propagate unchanged.
    public sealed class FSMActionPoolOwnershipTests
    {
        private static readonly KeyValuePair<string, Action>[] LeasedSites =
        {
            new KeyValuePair<string, Action>(FSMInterpreterVerification.ActionsLeasedSite, FSMInterpreterVerification.Run),
            new KeyValuePair<string, Action>(FSMInterpreterVerification.UpdateOrderingLeasedSite, FSMInterpreterVerification.Run),
            new KeyValuePair<string, Action>(FSMInterpreterVerification.HistoryLeasedSite, FSMInterpreterVerification.Run),
            new KeyValuePair<string, Action>(FSMStateVerification.TransitionsLeasedSite, FSMStateVerification.Run),
            new KeyValuePair<string, Action>(FSMStateVerification.FailureLeasedSite, FSMStateVerification.Run),
            new KeyValuePair<string, Action>(FSMCompositionVerification.GroupLeasedSite, FSMCompositionVerification.Run),
            new KeyValuePair<string, Action>(FSMCompositionVerification.SubMachineLeasedSite, FSMCompositionVerification.Run),
        };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void FailureAfterAcquisitionReturnsOnlyTheFixturesActions(int index)
        {
            string site = LeasedSites[index].Key;
            Action run = LeasedSites[index].Value;
            var borrowedParent = FSMStateChangeAction.Create(FSMActionReason.Forced);
            var borrowed = FSMStateChangeAction.Create(FSMActionReason.TransitionFired, parentAction: borrowedParent);
            try
            {
                int prior = ObjectPool<FSMStateChangeAction>.UsedObjectCount;
                var primary = new InvalidProgramException("injected at " + site);
                bool reached = false;
                VerificationFixtureOwnership.FaultInjection = current =>
                {
                    if (current != site) return;
                    reached = true;
                    throw primary;
                };
                try
                {
                    Assert.AreSame(primary, Assert.Throws<InvalidProgramException>(() => run()));
                }
                finally
                {
                    VerificationFixtureOwnership.FaultInjection = null;
                }
                Assert.IsTrue(reached);
                Assert.AreEqual(prior, ObjectPool<FSMStateChangeAction>.UsedObjectCount, "fixture actions returned");
                List<FSMStateChangeAction> used = VerificationFixtureOwnership.ActionPoolLease.UsedActions();
                Assert.IsTrue(used.Contains(borrowed) && used.Contains(borrowedParent), "borrowed actions stay leased");
                Assert.AreSame(borrowedParent, borrowed.ParentAction);
                Assert.AreEqual(FSMActionReason.TransitionFired, borrowed.Reason);
                Assert.AreEqual(FSMActionReason.Forced, borrowedParent.Reason);
            }
            finally
            {
                borrowed.Destroy(true);
            }
        }
    }
}
