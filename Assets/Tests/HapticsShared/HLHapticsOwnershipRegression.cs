using System;
using System.Collections;
using System.Collections.Generic;

namespace ProjectLucid.Verification
{
    public static partial class HLHapticsVerification
    {
        public static string OwnedManagerRegistryName => typeof(Hardlight.VibrationManager).ToString();

        // Expose the actual fixture comparator without borrowing the live registry for mutation controls.
        public static void CheckIndependentRegistryRows(IDictionary before, IDictionary after, object newlyOwnedInfo)
        {
            CheckRegistryRows(SharedSnapshot.Entries(before), SharedSnapshot.Entries(after), newlyOwnedInfo);
        }

        public static Action CaptureSharedOwnershipBoundary()
        {
            var snapshot = new SharedSnapshot(null);
            return () => snapshot.Check(null);
        }

        public static void ExerciseManagerAtOwnershipBoundary(Action observe)
        {
            WithManager(lease =>
            {
                var failures = new List<Exception>();
                try { lease.Activate(true); }
                catch (Exception failure) { failures.Add(failure); }
                // Observe genuine Awake registration even when activation fails, so every failure is reported together.
                try { observe(); }
                catch (Exception failure) { failures.Add(failure); }
                if (failures.Count != 0) throw new AggregateException(failures);
                lease.CheckShared();
            });
        }
    }
}
