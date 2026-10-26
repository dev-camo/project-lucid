using System;
using System.Collections.Generic;
using System.Reflection;
using Dreamteck;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // CalculateTangents (0x06000029 @ 0x4FA4E0) accumulates when lengths match.
    // Fixture lifecycle must use detached arrays and return the borrowed state exactly.
    public sealed class TangentScratchLease : IDisposable
    {
        private static readonly FieldInfo Tan1 = typeof(MeshUtility).GetField("tan1", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo Tan2 = typeof(MeshUtility).GetField("tan2", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly Vector3[] first;
        private readonly Vector3[] second;
        private readonly Vector3[] firstValues;
        private readonly Vector3[] secondValues;
        private bool disposed;

        public TangentScratchLease()
        {
            first = (Vector3[])Tan1.GetValue(null);
            second = (Vector3[])Tan2.GetValue(null);
            firstValues = first == null ? null : (Vector3[])first.Clone();
            secondValues = second == null ? null : (Vector3[])second.Clone();
            try
            {
                Tan1.SetValue(null, new Vector3[0]);
                Tan2.SetValue(null, new Vector3[0]);
            }
            catch (Exception primary)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var failures = new List<Exception>();
            Restore(Tan1, first, firstValues, failures);
            Restore(Tan2, second, secondValues, failures);
            if (failures.Count != 0) throw new AggregateException("Tangent scratch restoration failed", failures);
        }

        private static void Restore(FieldInfo field, Vector3[] borrowed, Vector3[] values, List<Exception> failures)
        {
            try { if (borrowed != null) Array.Copy(values, borrowed, values.Length); }
            catch (Exception failure) { failures.Add(failure); }
            try { field.SetValue(null, borrowed); }
            catch (Exception failure) { failures.Add(failure); }
        }
    }
}
