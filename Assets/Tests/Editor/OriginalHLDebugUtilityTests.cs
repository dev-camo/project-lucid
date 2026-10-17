using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Original HLUnityCore.Runtime HLDebug 0x0200016c, HLAssert 0x0200016b, DebugArrow
    // 0x02000267, HLHealthMonitor 0x02000143 and HLProfile 0x02000179, checked against the
    // 1.10.1 x86-64 bodies. [Conditional("BUILD_DEVELOPMENT")] members are invoked through
    // reflection, since direct calls from this assembly would be compiled out. The fixture
    // owns the only shared state it touches: the current culture and HLProfile's static
    // stack, which is swapped for a fresh one and put back after each test.
    public sealed class OriginalHLDebugUtilityTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        private CultureInfo m_culture;
        private FieldInfo m_profileStackField;
        private object m_profileStack;

        [SetUp]
        public void OwnSharedState()
        {
            m_culture = Thread.CurrentThread.CurrentCulture;
            m_profileStackField = typeof(HLProfile).GetField("m_profileTimeStack", PrivateStatic);
            m_profileStack = m_profileStackField.GetValue(null);
            m_profileStackField.SetValue(null, new Stack<float>());
        }

        [TearDown]
        public void RestoreSharedState()
        {
            Thread.CurrentThread.CurrentCulture = m_culture;
            m_profileStackField.SetValue(null, m_profileStack);
        }

        private static void InvokeStatic(Type type, string name, Type[] parameters, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, parameters, null);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(null, arguments);
        }

        private static Stack<float> ProfileStack()
        {
            return (Stack<float>)typeof(HLProfile).GetField("m_profileTimeStack", PrivateStatic).GetValue(null);
        }

        // 0x1af87d3..0x1af87e5 compare with jge against 0x40000001, 0x100001 and 0x401,
        // so each unit starts strictly above the previous one.
        [Test]
        public void FormatMemorySizeSwitchesUnitStrictlyAboveEachBoundary()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Assert.That(HLDebug.FormatMemorySize(0), Is.EqualTo("0B"));
            Assert.That(HLDebug.FormatMemorySize(1024), Is.EqualTo("1024B"));
            Assert.That(HLDebug.FormatMemorySize(1536), Is.EqualTo("1.50KB"));
            Assert.That(HLDebug.FormatMemorySize(1048576), Is.EqualTo("1024.00KB"));
            Assert.That(HLDebug.FormatMemorySize(3 * 1048576), Is.EqualTo("3.00MB"));
            Assert.That(HLDebug.FormatMemorySize(1073741824), Is.EqualTo("1024.00MB"));
            Assert.That(HLDebug.FormatMemorySize(5L * 1073741824), Is.EqualTo("5.00GB"));
            Assert.That(HLDebug.FormatMemorySize(-2048), Is.EqualTo("-2048B"));
        }

        // Single.ToString(string) 0x0600097c is called without a provider, so the current
        // culture's decimal separator is used.
        [Test]
        public void FormatMemorySizeUsesTheCurrentCulture()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            Assert.That(HLDebug.FormatMemorySize(1536), Is.EqualTo("1,50KB"));
        }

        // 0x1af88d0 returns the constant false (no BUILD_DEVELOPMENT). IsEditor
        // (0x1af88c0) is left out: the player shows only its retail value, and any
        // UNITY_EDITOR branch of the source cannot be seen, so no Editor-time oracle exists.
        [Test]
        public void ConditionalNamesAndRetailDevelopmentCheck()
        {
            Assert.That(HLDebug.IsDevelopmentBuild(), Is.False);
            Assert.That(HLDebug.DebugConditional, Is.EqualTo("BUILD_DEVELOPMENT"));
            Assert.That(HLDebug.EditorConditional, Is.EqualTo("UNITY_EDITOR"));
        }

        // Every HLAssert member carries [Conditional("BUILD_DEVELOPMENT")] in the metadata.
        [Test]
        public void EveryAssertIsConditionalOnDevelopmentBuilds()
        {
            MethodInfo[] methods = typeof(HLAssert).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.That(methods.Length, Is.EqualTo(14));
            foreach (MethodInfo method in methods)
            {
                ConditionalAttribute conditional = method.GetCustomAttributes<ConditionalAttribute>().SingleOrDefault();
                Assert.That(conditional, Is.Not.Null, method.ToString());
                Assert.That(conditional.ConditionString, Is.EqualTo("BUILD_DEVELOPMENT"), method.ToString());
            }
        }

        // 0x1af86d0..0x1af8700 are bare returns: the predicate overloads never invoke it.
        [Test]
        public void PredicateAssertsNeverInvokeTheirPredicate()
        {
            int calls = 0;
            Func<bool> predicate = () => { calls++; return false; };
            Type[] parameters = { typeof(Func<bool>), typeof(object), typeof(UnityEngine.Object) };

            using (var capture = new OwnedHLOutputErrorCapture())
            {
                foreach (string name in new[] { "Warn", "Error", "False", "True" })
                {
                    InvokeStatic(typeof(HLAssert), name, parameters, predicate, "message", null);
                }
                Assert.That(capture.Errors, Is.Empty);
            }
            Assert.That(calls, Is.EqualTo(0));
        }

        // 0x1af8410: only a failed test reaches HLOutput.LogError; Warn and False (bare
        // returns at 0x1af8400 and 0x1af8530) report nothing either way.
        [Test]
        public void BoolErrorReportsOnlyAFailedTestAndWarnAndFalseStaySilent()
        {
            Type[] parameters = { typeof(bool), typeof(object), typeof(UnityEngine.Object) };

            using (var capture = new OwnedHLOutputErrorCapture())
            {
                InvokeStatic(typeof(HLAssert), "Error", parameters, true, "passed", null);
                InvokeStatic(typeof(HLAssert), "Warn", parameters, false, "warned", null);
                InvokeStatic(typeof(HLAssert), "False", parameters, true, "inverted", null);
                InvokeStatic(typeof(HLAssert), "Error", parameters, false, "failed", null);

                Assert.That(capture.Errors, Is.EqualTo(new[] { "failed|0" }));
            }
        }

        // 0x1af8540: a failed True appends " - context: <name>" when a context is given.
        // Application.isPlaying is false in EditMode, so Debug.Break is not reached.
        [Test]
        public void BoolTrueReportsAFailureWithTheContextName()
        {
            Type[] parameters = { typeof(bool), typeof(object), typeof(UnityEngine.Object) };
            var context = new GameObject("Owned assert context");

            try
            {
                using (var capture = new OwnedHLOutputErrorCapture())
                {
                    InvokeStatic(typeof(HLAssert), "True", parameters, true, "passed", context);
                    InvokeStatic(typeof(HLAssert), "True", parameters, false, "plain", null);
                    InvokeStatic(typeof(HLAssert), "True", parameters, false, "named", context);

                    Assert.That(capture.Errors, Is.EqualTo(new[] { "plain|0", "named - context: Owned assert context|0" }));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(context);
            }
        }

        // 0x1b1c950: forward turned by 180 + angle degrees of yaw in the direction's look
        // frame, scaled by the length. The left stroke is checked against the 180 - angle
        // yaw that DrawGizmo and DrawDebugRay compute inline (0x1b1c7a4, 0x1b1cca0), since
        // the reconstruction reaches it through the helper with -angle. Expected values
        // come from the yaws themselves.
        [Test]
        public void ArrowHeadDirectionTurnsBackByTheHeadAngle()
        {
            MethodInfo head = typeof(DebugArrow).GetMethod("GetArrowHeadDirection", PrivateStatic);
            float yaw = (180f + 35f) * Mathf.Deg2Rad;
            float leftYaw = (180f - 35f) * Mathf.Deg2Rad;

            var right = (Vector3)head.Invoke(null, new object[] { Vector3.forward, 35f, 0.25f });
            var left = (Vector3)head.Invoke(null, new object[] { Vector3.forward, -35f, 0.25f });
            var turned = (Vector3)head.Invoke(null, new object[] { Vector3.right, 35f, 2f });

            Assert.That(Vector3.Distance(right, new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 0.25f), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(left, new Vector3(Mathf.Sin(leftYaw), 0f, Mathf.Cos(leftYaw)) * 0.25f), Is.LessThan(1e-5f));
            Assert.That(turned.magnitude, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(Vector3.Angle(turned, Vector3.right), Is.EqualTo(180f - 35f).Within(1e-3f));
        }

        // 0x1aeead0 over the constant "0,0,0" from 0x1aeec00: three zero fields, no log.
        [Test]
        public void HealthDataUsesTheRetailFallbackOfThreeZeros()
        {
            Assert.That(HLHealthMonitor.GetDeviceHealthData(), Is.EqualTo("0,0,0"));

            using (var capture = new OwnedHLOutputErrorCapture())
            {
                HLHealthMonitor.Start();
                HLHealthMonitor.HealthData data = HLHealthMonitor.GetHealthData();
                HLHealthMonitor.Stop();

                Assert.That(data.BatteryLevel, Is.EqualTo(0));
                Assert.That(data.NetworkSent, Is.EqualTo(0));
                Assert.That(data.NetworkReceived, Is.EqualTo(0));
                Assert.That(capture.Errors, Is.Empty);
            }
        }

        // 0x1b03b30 pushes realtimeSinceStartup per Begin; 0x1b03bb0 pops one per End and
        // ignores an End with nothing to pop.
        [Test]
        public void ProfileBeginPushesAndEndPopsOnlyWhileEntriesRemain()
        {
            Type[] parameters = { typeof(string) };

            InvokeStatic(typeof(HLProfile), "Begin", parameters, "outer");
            InvokeStatic(typeof(HLProfile), "Begin", parameters, new object[] { null });
            Assert.That(ProfileStack().Count, Is.EqualTo(2));
            Assert.That(ProfileStack().Peek(), Is.LessThanOrEqualTo(Time.realtimeSinceStartup));

            InvokeStatic(typeof(HLProfile), "End", parameters, "inner");
            Assert.That(ProfileStack().Count, Is.EqualTo(1));
            InvokeStatic(typeof(HLProfile), "End", parameters, "outer");
            InvokeStatic(typeof(HLProfile), "End", parameters, "unmatched");
            Assert.That(ProfileStack().Count, Is.EqualTo(0));
        }
    }
}
