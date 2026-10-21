using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.LengthCalculator and BlankUser, driven through a SplineComputer that holds the Linear polyline
    // (0,0,0) -> (10,0,0) -> (10,0,end): 10 + end units long. Expected values are hand arithmetic from the native bodies cited
    // per test. The users are plain components on owned game objects and TearDown destroys everything the test made.
    public sealed class OriginalDreamteckLengthCalculatorTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private GameObject NewObject(string name)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        private static void SetLine(SplineComputer computer, float end)
        {
            computer.SetPoints(new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, end))
            });
            computer.RebuildImmediate();
        }

        private SplineComputer NewLine(float end)
        {
            SplineComputer computer = NewObject("line").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            SetLine(computer, end);
            return computer;
        }

        private LengthCalculator NewCalculator(SplineComputer computer = null)
        {
            LengthCalculator calculator = NewObject("calculator").AddComponent<LengthCalculator>();
            if (computer != null)
            {
                calculator.spline = computer;
                calculator.RebuildImmediate();
            }

            return calculator;
        }

        private static T Read<T>(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return (T)field.GetValue(target);
                }
            }

            Assert.Fail("no field " + name);
            return default(T);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == arguments.Length);
                if (method != null)
                {
                    return method.Invoke(target, arguments);
                }
            }

            Assert.Fail("no method " + name);
            return null;
        }

        private static LengthCalculator.LengthEvent NewEvent(LengthCalculator.LengthEvent.Type type, float target, int[] counter, int slot, bool enabled = true)
        {
            LengthCalculator.LengthEvent lengthEvent = new LengthCalculator.LengthEvent(type);
            lengthEvent.targetLength = target;
            lengthEvent.enabled = enabled;
            lengthEvent.onChange.AddListener(delegate { counter[slot]++; });
            return lengthEvent;
        }

        // The constructors 0x49cb90 (empty event array, ideal length 1), 0x49cc00 and 0x49cc70 (enabled, its own UnityEvent, type
        // Both unless one is given).
        [Test]
        public void FreshCalculatorAndEventKeepTheShippedDefaults()
        {
            LengthCalculator calculator = NewCalculator();
            Assert.That(calculator.lengthEvents, Is.Not.Null);
            Assert.That(calculator.lengthEvents.Length, Is.EqualTo(0));
            Assert.That(calculator.idealLength, Is.EqualTo(1f));
            Assert.That(calculator.length, Is.EqualTo(0f), "nothing measured without samples");
            Assert.That(calculator is SplineUser, Is.True);

            LengthCalculator.LengthEvent plain = new LengthCalculator.LengthEvent();
            Assert.That(plain.enabled, Is.True);
            Assert.That(plain.targetLength, Is.EqualTo(0f));
            Assert.That(plain.onChange, Is.Not.Null);
            Assert.That(plain.type, Is.EqualTo(LengthCalculator.LengthEvent.Type.Both));
            LengthCalculator.LengthEvent growing = new LengthCalculator.LengthEvent(LengthCalculator.LengthEvent.Type.Growing);
            Assert.That(growing.enabled, Is.True);
            Assert.That(growing.onChange, Is.Not.Null);
            Assert.That(growing.onChange, Is.Not.SameAs(plain.onChange));
            Assert.That(growing.type, Is.EqualTo(LengthCalculator.LengthEvent.Type.Growing));
        }

        // LengthEvent.Check 0x49ca30 as a truth table of (type, from, to) for the target 5: Growing needs to >= target and target >
        // from, Shrinking needs to <= target and target < from, Both is either; a disabled event and NaN never fire. The strict
        // side separates the shipped comparison from a >= / <= variant: an event whose target equals the previous length is silent.
        [Test]
        public void CheckFollowsTheNativeTruthTable()
        {
            // type, from, to, fires
            object[][] table =
            {
                new object[] { LengthCalculator.LengthEvent.Type.Growing, 0f, 5f, true },
                new object[] { LengthCalculator.LengthEvent.Type.Growing, 4f, 6f, true },
                new object[] { LengthCalculator.LengthEvent.Type.Growing, 0f, 4f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Growing, 5f, 6f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Growing, 6f, 4f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Growing, 10f, 5f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Shrinking, 10f, 5f, true },
                new object[] { LengthCalculator.LengthEvent.Type.Shrinking, 6f, 4f, true },
                new object[] { LengthCalculator.LengthEvent.Type.Shrinking, 10f, 6f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Shrinking, 5f, 4f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Shrinking, 4f, 6f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Shrinking, 0f, 5f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 0f, 5f, true },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 4f, 6f, true },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 10f, 5f, true },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 6f, 4f, true },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 5f, 5f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 6f, 6f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 0f, 4f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 10f, 6f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 5f, 6f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Both, float.NaN, 5f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Both, 0f, float.NaN, false },
                new object[] { LengthCalculator.LengthEvent.Type.Growing, float.NaN, 5f, false },
                new object[] { LengthCalculator.LengthEvent.Type.Shrinking, 10f, float.NaN, false }
            };

            foreach (object[] row in table)
            {
                int[] counter = new int[1];
                LengthCalculator.LengthEvent lengthEvent = NewEvent((LengthCalculator.LengthEvent.Type)row[0], 5f, counter, 0);
                lengthEvent.Check((float)row[1], (float)row[2]);
                Assert.That(counter[0], Is.EqualTo((bool)row[3] ? 1 : 0), row[0] + " from " + row[1] + " to " + row[2]);

                LengthCalculator.LengthEvent disabled = NewEvent((LengthCalculator.LengthEvent.Type)row[0], 5f, counter, 0, false);
                disabled.Check((float)row[1], (float)row[2]);
                Assert.That(counter[0], Is.EqualTo((bool)row[3] ? 1 : 0), "a disabled event never fires");
            }
        }

        // AddEvent 0x49caa0 appends to a copy of the array; the old array is left as it was.
        [Test]
        public void AddEventAppendsToACopyOfTheArray()
        {
            LengthCalculator calculator = NewCalculator();
            LengthCalculator.LengthEvent first = new LengthCalculator.LengthEvent();
            LengthCalculator.LengthEvent second = new LengthCalculator.LengthEvent(LengthCalculator.LengthEvent.Type.Shrinking);
            LengthCalculator.LengthEvent[] initial = calculator.lengthEvents;
            calculator.AddEvent(first);
            Assert.That(calculator.lengthEvents, Is.Not.SameAs(initial));
            Assert.That(initial.Length, Is.EqualTo(0));
            LengthCalculator.LengthEvent[] afterFirst = calculator.lengthEvents;
            Assert.That(afterFirst.Length, Is.EqualTo(1));
            Assert.That(afterFirst[0], Is.SameAs(first));
            calculator.AddEvent(second);
            Assert.That(calculator.lengthEvents, Is.Not.SameAs(afterFirst));
            Assert.That(afterFirst.Length, Is.EqualTo(1));
            Assert.That(calculator.lengthEvents.Length, Is.EqualTo(2));
            Assert.That(calculator.lengthEvents[0], Is.SameAs(first));
            Assert.That(calculator.lengthEvents[1], Is.SameAs(second));
        }

        // Awake 0x49c896 measures from 0.0 to 1.0 through UnclipPercent (CalculateLength 0x4d1eb0), so only the clip range counts:
        // the first half of the 20 unit line is 10 long.
        [Test]
        public void AwakeMeasuresOnlyTheClipRange()
        {
            SplineComputer computer = NewLine(10f);
            LengthCalculator calculator = NewCalculator();
            calculator.clipTo = 0.5;
            calculator.spline = computer;
            Invoke(calculator, "Awake");
            Assert.That(calculator.length, Is.EqualTo(10f).Within(1e-3f));
        }

        // Build 0x49c936 measures the clip range too: a change of the range alone changes the length and fires the events.
        [Test]
        public void BuildNoticesAChangeOfTheClipRangeAlone()
        {
            SplineComputer computer = NewLine(10f);
            LengthCalculator calculator = NewCalculator();
            calculator.clipTo = 0.5;
            calculator.spline = computer;
            calculator.RebuildImmediate();
            Assert.That(calculator.length, Is.EqualTo(10f).Within(1e-3f));
            int[] counter = new int[1];
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Growing, 15f, counter, 0));

            calculator.clipTo = 1.0;
            calculator.RebuildImmediate();
            Assert.That(calculator.length, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(counter[0], Is.EqualTo(1), "10 -> 20 crosses the growing event at 15");
        }

        // Build 0x49c910: measures again; a changed length is checked against every event (through Check) and remembered, an
        // unchanged one does nothing. Lengths 20 -> 30 -> 20: the growing and the both events with their target between fire on
        // the way up, the shrinking and both on the way back; an event whose target is a measured end follows the strict rule.
        [Test]
        public void BuildChecksTheEventsAgainstTheChangedLength()
        {
            SplineComputer computer = NewLine(10f);
            LengthCalculator calculator = NewCalculator(computer);
            float shorter = calculator.length;
            SetLine(computer, 20f);
            calculator.RebuildImmediate();
            float longer = calculator.length;
            Assert.That(longer, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(shorter, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(Read<float>(calculator, "lastLength"), Is.EqualTo(longer));
            SetLine(computer, 10f);
            calculator.RebuildImmediate();
            Assert.That(calculator.length, Is.EqualTo(shorter));

            float middle = (shorter + longer) * 0.5f;
            int[] counter = new int[9];
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Growing, middle, counter, 0));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Shrinking, middle, counter, 1));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Both, middle, counter, 2));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Both, middle, counter, 3, false));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Growing, longer, counter, 4));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Shrinking, longer, counter, 5));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Growing, shorter, counter, 6));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Shrinking, shorter, counter, 7));

            calculator.RebuildImmediate();
            Assert.That(counter, Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }), "an unchanged length checks nothing");

            SetLine(computer, 20f);
            calculator.RebuildImmediate();
            Assert.That(counter, Is.EqualTo(new[] { 1, 0, 1, 0, 1, 0, 0, 0, 0 }), "growing 20 -> 30");

            SetLine(computer, 10f);
            calculator.RebuildImmediate();
            Assert.That(counter, Is.EqualTo(new[] { 1, 1, 2, 0, 1, 0, 0, 1, 0 }), "shrinking 30 -> 20");
            Assert.That(Read<float>(calculator, "lastLength"), Is.EqualTo(shorter));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(LengthCalculator).GetFields(flags).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "lengthEvents", "idealLength", "_length", "lastLength" }));
            Assert.That(typeof(LengthCalculator).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "LengthEvent" }));
            Assert.That(typeof(LengthCalculator.LengthEvent).GetFields(flags).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "enabled", "targetLength", "onChange", "type" }));
            Assert.That(typeof(LengthCalculator.LengthEvent).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "Type" }));
            Assert.That(Enum.GetNames(typeof(LengthCalculator.LengthEvent.Type)), Is.EqualTo(new[] { "Growing", "Shrinking", "Both" }));
            Assert.That(typeof(LengthCalculator.LengthEvent).GetCustomAttributes(typeof(SerializableAttribute), false).Length, Is.EqualTo(1));
            Assert.That(typeof(LengthCalculator.LengthEvent).GetField("onChange").FieldType, Is.EqualTo(typeof(UnityEvent)));
            AddComponentMenu menu = (AddComponentMenu)typeof(LengthCalculator).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Length Calculator"));
            Assert.That(typeof(LengthCalculator).GetField("lengthEvents").GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            Assert.That(typeof(LengthCalculator).GetField("idealLength").GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            Assert.That(typeof(LengthCalculator).GetProperty("length").CanWrite, Is.False);
        }

        // BlankUser 0x49c200..0x49c250: Start (0x49c210) is a bare ret; Awake, LateRun, Build and PostBuild tail-jump to the
        // SplineUser bodies (0x4cd790, 0x4cbea0, 0x4cf320, 0x4ca190). So Awake fetches the samples like any user that calls the
        // base. Neither component has ExecuteInEditMode, so the test calls Awake itself.
        [Test]
        public void BlankUserOnlyForwardsToTheBase()
        {
            SplineComputer computer = NewLine(10f);
            BlankUser blank = NewObject("blank").AddComponent<BlankUser>();
            blank.spline = computer;
            Invoke(blank, "Awake");
            Assert.That(blank.sampleCount, Is.GreaterThan(0), "the base Awake fetches the samples");

            LengthCalculator reference = NewCalculator();
            reference.spline = computer;
            Invoke(reference, "Awake");
            Assert.That(blank.sampleCount, Is.EqualTo(reference.sampleCount));

            string[] declared = typeof(BlankUser)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.That(declared, Is.EqualTo(new[] { "Awake", "Build", "LateRun", "PostBuild", "Start" }));
            Assert.That(typeof(BlankUser).GetFields(AllDeclared).Length, Is.EqualTo(0));
            Assert.That(typeof(BlankUser).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).IsPrivate, Is.True);
            Assert.That(Invoke(blank, "Start"), Is.Null);
            Assert.That(() => Invoke(blank, "LateRun"), Throws.Nothing);
        }

        private const BindingFlags AllDeclared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    }
}
