using System;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class KnotRuntimePrerequisiteVerification
    {
        public static int RunManaged()
        {
            int checks = 0;
            Type type = typeof(ISplineKnotHandle).Assembly.GetType("Hardlight.SplineKnotRuntimeComponent", true);
            foreach (string name in new[] { "GetLUTValue", "TransformPositionLocalToWorld", "TransformPositionWorldToLocal" })
            {
                MethodInfo method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                object[] args = name == "GetLUTValue" ? new object[] { null, 0 } : new object[] { null, default(Vector3) };
                try { method.Invoke(null, args); }
                catch (TargetInvocationException error)
                {
                    if (error.InnerException?.GetType() != typeof(NullReferenceException)) throw;
                    checks++;
                    if (!error.InnerException.StackTrace.Contains("SplineKnotRuntimeComponent." + name))
                        throw new InvalidOperationException("Original knot runtime: null receiver must fail in original method " + name);
                    checks++;
                    continue;
                }
                throw new InvalidOperationException("Original knot runtime: null receiver was repaired in " + name);
            }
            // No invented handle is used. TLUT/non-null matrix behavior must be
            // exercised through a complete genuine owner after its graph closes.
            return checks;
        }
    }
}
