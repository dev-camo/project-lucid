using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Hardlight.UI.Binding;
using Hardlight.UI.Binding.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Format and conversion binder nodes on an owned inactive GameObject, bound to an
    // owned carrier; Refresh is called directly, so no Awake, listening or lifecycle
    // runs. Expected values follow the x86-64 Refresh bodies cited per case (HLBinders
    // 1.10.1). Text cases run under the invariant culture, restored afterwards.
    // The genuine member lookup cache may retain the carrier type's metadata.
    public sealed class OriginalBinderFormatNodeTests
    {
        private static void Run<TNode>(Action<TNode, OriginalBinderFormatSourceCarrier> body) where TNode : BinderNode
        {
            Assert.IsFalse(Application.isPlaying, "Owned case requires the Editor (uncached) binding path");
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            GameObject owner = null;
            OriginalBinderFormatSourceCarrier carrier = null;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                owner = new GameObject("Owned inactive " + typeof(TNode).Name + " case");
                owner.SetActive(false);
                TNode node = owner.AddComponent<TNode>();
                carrier = ScriptableObject.CreateInstance<OriginalBinderFormatSourceCarrier>();
                body(node, carrier);
                Assert.IsFalse(owner.activeInHierarchy);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
                try
                {
                    if (owner != null) Object.DestroyImmediate(owner);
                }
                finally
                {
                    if (carrier != null) Object.DestroyImmediate(carrier);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Writes a private [SerializeField] through the genuine serialization path.
        private static void SetSerialized(Object node, string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(node);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(Object node, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(node);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // BindReference equality compares targets, so unset references are matched by identity.
        private static void AssertSameReferences(List<BindReference> actual, BindReference[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(actual[i], Is.SameAs(expected[i]), "bind reference " + i);
        }

        private static List<BindReference> SetUpBindReferences(BinderNode node)
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BinderNode).GetMethod("SetUpBindReferenceFields", instance).Invoke(node, null);
            return (List<BindReference>)typeof(BinderNode).GetField("bindReferences", instance).GetValue(node);
        }

        // DateOffset.Refresh 0x19fb4b0: Input (+0x58) is read before Offset (+0x60) and the
        // two are combined with DateTime.op_Addition (0x19fb54b), keeping the input's Kind.
        [Test]
        public void DateOffsetAddsTheOffsetToTheInputAfterReadingTheInputFirst()
        {
            Run<DateOffset>((node, carrier) =>
            {
                carrier.Date = carrier.Recording("Input", new DateTime(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc));
                carrier.Span = carrier.Recording("Offset", new TimeSpan(-1, 2, 0, 0));
                node.Input.SetTarget(carrier, "Date");
                node.Offset.SetTarget(carrier, "Span");

                carrier.Reads.Clear();
                node.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(new[] { "Input", "Offset" }));
                Assert.That(node.Output.Value, Is.EqualTo(new DateTime(2021, 3, 3, 7, 6, 7, DateTimeKind.Utc)));
                Assert.That(node.Output.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            });
        }

        // DateStringFormatter.Refresh 0x19fb8a0: dateTime1..3 (+0x60..+0x70) are each read and
        // passed through DateTime.ToLocalTime (0x19fb987, 0x19fba22, 0x19fbaad) before String
        // (+0x58) is read at 0x19fbb2b; String.IsNullOrEmpty (0x19fbb38) selects "" over Format.
        [Test]
        public void DateStringFormatterConvertsEachDateToLocalTimeBeforeReadingTheFormat()
        {
            Run<DateStringFormatter>((node, carrier) =>
            {
                DateTime utc = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
                carrier.Date = carrier.Recording("dateTime1", utc);
                carrier.SecondDate = carrier.Recording("dateTime2", new DateTime(2002, 5, 6, 0, 0, 0, DateTimeKind.Local));
                carrier.ThirdDate = carrier.Recording("dateTime3", new DateTime(2003, 8, 9, 0, 0, 0, DateTimeKind.Local));
                carrier.Format = carrier.Recording("String", "{0:O}|{1:yyyy-MM-dd}|{2:yyyy-MM-dd}");
                node.String.SetTarget(carrier, "Format");
                node.dateTime1.SetTarget(carrier, "Date");
                node.dateTime2.SetTarget(carrier, "SecondDate");
                node.dateTime3.SetTarget(carrier, "ThirdDate");
                string[] order = { "dateTime1", "dateTime2", "dateTime3", "String" };

                carrier.Reads.Clear();
                node.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(order));
                // A Utc date round-trips with a "Z" suffix unless it was converted first.
                Assert.That(node.Output.Value, Is.EqualTo(utc.ToLocalTime().ToString("O") + "|2002-05-06|2003-08-09"));
                StringAssert.DoesNotEndWith("Z", node.Output.Value.Split('|')[0]);

                carrier.Format = carrier.Recording("String", string.Empty);
                carrier.Reads.Clear();
                node.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(order), "an empty format is still read after every date");
                Assert.That(node.Output.Value, Is.EqualTo(string.Empty));

                carrier.Format = carrier.Recording("String", "{0:O}");
                node.Refresh();
                carrier.Format = carrier.Recording<string>("String", null);
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo(string.Empty), "a null format publishes empty, not a format fault");
            });
        }

        // EnumToInt.Refresh 0x19fc610: a null Input skips GetValue<int> and publishes 0
        // (0x19fc65a xor esi, esi); otherwise the boxed enum unboxes to its value.
        [Test]
        public void EnumToIntPublishesTheEnumValueAndZeroForANullReference()
        {
            Run<EnumToInt>((node, carrier) =>
            {
                carrier.Kind = new Bindable<DateTimeKind>(DateTimeKind.Local);
                node.Input.SetTarget(carrier, "Kind");
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo(2));

                carrier.Kind = new Bindable<DateTimeKind>(DateTimeKind.Utc);
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo(1));

                node.Input = null;
                Assert.DoesNotThrow(() => node.Refresh());
                Assert.That(node.Output.Value, Is.EqualTo(0));
            });
        }

        // EnumToString.Refresh 0x19fc8e0: a null First publishes String.Empty (0x19fc919 ->
        // 0x19fc938); otherwise GetValue<string> runs once for IsNullOrEmpty (0x19fc925) and
        // again for the output (0x19fc97c).
        [Test]
        public void EnumToStringReadsTheNameTwiceAndPublishesEmptyForANullReference()
        {
            Run<EnumToString>((node, carrier) =>
            {
                carrier.Kind = carrier.Recording("First", DateTimeKind.Utc);
                node.First.SetTarget(carrier, "Kind");

                carrier.Reads.Clear();
                node.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(new[] { "First", "First" }));
                Assert.That(node.Output.Value, Is.EqualTo("Utc"));

                node.First = new BindReference();
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo(string.Empty), "an unset reference reads as empty");

                node.Output.Value = "stale";
                node.First = null;
                Assert.DoesNotThrow(() => node.Refresh());
                Assert.That(node.Output.Value, Is.EqualTo(string.Empty));
            });
        }

        // IntToSingle 0x19fd016 cvtsi2ss; IntToString 0x19fd2da Int32.ToString();
        // NumberOrdinalFormatter 0x1a06dd6 Int32.ToString() with no ordinal suffix.
        [Test]
        public void IntConversionsUseTheNativeIntToSingleAndPlainToString()
        {
            Run<IntToSingle>((node, carrier) =>
            {
                carrier.Int = new Bindable<int>(16777217);
                node.Input.SetTarget(carrier, "Int");
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo(16777216f), "single precision rounds 2^24 + 1 to even");
            });
            Run<IntToString>((node, carrier) =>
            {
                carrier.Int = new Bindable<int>(-42);
                node.Input.SetTarget(carrier, "Int");
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo("-42"));
            });
            Run<NumberOrdinalFormatter>((node, carrier) =>
            {
                node.Number.SetTarget(carrier, "Int");
                int[] numbers = { 1, 2, 3, 11, 22 };
                foreach (int number in numbers)
                {
                    carrier.Int = new Bindable<int>(number);
                    node.Refresh();
                    Assert.That(node.Output.Value, Is.EqualTo(number.ToString(CultureInfo.InvariantCulture)), "no ordinal suffix for " + number);
                }
            });
        }

        // IntToTimeSpan.Refresh 0x19fd560 calls TimeSpan(int hours, int minutes, int seconds)
        // (0x19fd5b6) with hours = minutes = 0, so the input counts seconds.
        [Test]
        public void IntToTimeSpanTreatsTheInputAsSeconds()
        {
            Run<IntToTimeSpan>((node, carrier) =>
            {
                node.Input.SetTarget(carrier, "Int");
                carrier.Int = new Bindable<int>(3725);
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo(new TimeSpan(1, 2, 5)));

                carrier.Int = new Bindable<int>(-5);
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo(TimeSpan.FromSeconds(-5)));
            });
        }

        // MemoryFormatter.Refresh 0x1a06320 passes the int to FormatMemorySize(long) with
        // mov edi, eax (0x1a06361): zero extension, so -1 is 4294967295 bytes ("4.00GB")
        // rather than "-1B".
        [Test]
        public void MemoryFormatterZeroExtendsTheIntSize()
        {
            Run<MemoryFormatter>((node, carrier) =>
            {
                node.Input.SetTarget(carrier, "Int");
                carrier.Int = new Bindable<int>(1536);
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo("1.50KB"));

                carrier.Int = new Bindable<int>(-1);
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo("4.00GB"));
            });
        }

        // NumberCutOffFormatter.Refresh 0x1a06630: "{0}+" with the cut-off only when
        // cutOff > 0 (0x1a06697 jle) and value > cutOff (0x1a0669b jle); else Int32.ToString.
        [Test]
        public void NumberCutOffFormatterCapsOnlyAboveAPositiveCutOff()
        {
            Run<NumberCutOffFormatter>((node, carrier) =>
            {
                node.Input.SetTarget(carrier, "Int");
                var cases = new[]
                {
                    (cutOff: 99, value: 150, expected: "99+"),
                    (cutOff: 99, value: 100, expected: "99+"),
                    (cutOff: 99, value: 99, expected: "99"),
                    (cutOff: 99, value: -150, expected: "-150"),
                    (cutOff: 0, value: 150, expected: "150"),
                    (cutOff: -5, value: 3, expected: "3"),
                };
                foreach (var c in cases)
                {
                    SetSerialized(node, "cutOff", c.cutOff);
                    carrier.Int = new Bindable<int>(c.value);
                    node.Refresh();
                    Assert.That(node.Output.Value, Is.EqualTo(c.expected), "cutOff " + c.cutOff + ", value " + c.value);
                }
            });
        }

        // NumberDeltaFormatter: the constructor sets withSpace and prefixOnZero (0x1a06cdb,
        // 0x1a06cdf). Refresh 0x1a06950 truncates when integer (0x1a069f6), hides an exact zero
        // when hideIfZero (0x1a069fd), picks "{0}{1}{2:0}" or "{0}{1}{2:1}" (0x1a06a4d), signs a
        // nonzero value, or zero with prefixOnZero, "+" unless below zero or unordered (jb at
        // 0x1a06a96), adds " " only with a sign, and formats |value| (andps 0x1a06ad6).
        [Test]
        public void NumberDeltaFormatterFollowsTheNativeSignSpaceAndFormatTable()
        {
            Run<NumberDeltaFormatter>((node, carrier) =>
            {
                node.Number.SetTarget(carrier, "Float");
                void Expect(float value, string expected, string label)
                {
                    carrier.Float = new Bindable<float>(value);
                    node.Refresh();
                    Assert.That(node.Output.Value, Is.EqualTo(expected), label + " " + value.ToString("R", CultureInfo.InvariantCulture));
                }

                // Defaults. "{2:1}" is a custom format with only the literal '1'.
                Expect(2.5f, "+ 1", "default");
                Expect(-2.5f, "- 1", "default");
                Expect(0f, "+ 1", "default zero keeps the prefix");

                SetSerialized(node, "integer", true);
                Expect(2.7f, "+ 2", "integer truncates");
                Expect(-2.7f, "- 2", "integer truncates toward zero");
                Expect(0.4f, "+ 0", "integer zero with prefixOnZero");

                SetSerialized(node, "prefixOnZero", false);
                Expect(0.4f, "0", "zero without prefixOnZero has no sign or space");
                Expect(-3f, "- 3", "nonzero still signed");

                SetSerialized(node, "withSpace", false);
                Expect(5f, "+5", "no space");
                Expect(-5f, "-5", "no space");

                SetSerialized(node, "hideIfZero", true);
                Expect(0.4f, string.Empty, "hidden after truncation");
                Expect(-0.9f, string.Empty, "hidden after truncation");
                Expect(1f, "+1", "nonzero not hidden");

                SetSerialized(node, "integer", false);
                Expect(0.4f, "+1", "non-integer 0.4 is not zero");
                carrier.Float = new Bindable<float>(float.NaN);
                node.Refresh();
                StringAssert.StartsWith("-", node.Output.Value, "NaN is unordered, so it takes the '-' branch");
            });
        }

        // NumberPercentFormatter: the constructor sets integer and decimalPlaces = 1 (0x1a0755b,
        // 0x1a0755f). Refresh 0x1a07060 multiplies by 100f (0x1a0714d), floors (roundss 0x1a0715c),
        // uses "{0}{1}{2:0}{3}" or "{0}{1}{2:F" + decimalPlaces + "}{3}" (0x1a07191), signs only
        // with prefix ("+" when >= 0, 0x1a071c5), spaces only with withSpace and prefix
        // (0x1a07242..0x1a0724e), formats |value| (0x1a072b8) and appends '%' (0x1a07316).
        [Test]
        public void NumberPercentFormatterFollowsTheNativeFlagTable()
        {
            Run<NumberPercentFormatter>((node, carrier) =>
            {
                node.Number.SetTarget(carrier, "Float");
                void Expect(float value, string expected, string label)
                {
                    carrier.Float = new Bindable<float>(value);
                    node.Refresh();
                    Assert.That(node.Output.Value, Is.EqualTo(expected), label + " " + value.ToString("R", CultureInfo.InvariantCulture));
                }

                Expect(42.6f, "43%", "default integer rounds");
                Expect(-3.2f, "3%", "no prefix drops the sign");

                SetSerialized(node, "withSpace", true);
                Expect(3.2f, "3%", "space needs the prefix");

                SetSerialized(node, "prefix", true);
                Expect(3.2f, "+ 3%", "prefix and space");
                Expect(0f, "+ 0%", "zero is non-negative");
                Expect(-3.2f, "- 3%", "negative");

                SetSerialized(node, "withSpace", false);
                SetSerialized(node, "multiplyBy100", true);
                Expect(0.256f, "+26%", "multiplied");

                SetSerialized(node, "floor", true);
                Expect(0.256f, "+25%", "multiplied then floored");
                Expect(-0.256f, "-26%", "floor goes down before the sign is taken");

                SetSerialized(node, "multiplyBy100", false);
                SetSerialized(node, "integer", false);
                Expect(12.9f, "+12.0%", "floored, F1 from the default decimalPlaces");

                SetSerialized(node, "floor", false);
                SetSerialized(node, "decimalPlaces", 3);
                Expect(3.14159f, "+3.142%", "F3");
            });
        }

        // SingleToInt.Refresh 0x1a07d20 truncates with cvttss2si (0x1a07d66).
        [Test]
        public void SingleToIntTruncatesTowardZero()
        {
            Run<SingleToInt>((node, carrier) =>
            {
                node.Input.SetTarget(carrier, "Float");
                float[] values = { 2.9f, -2.9f, 0.5f, -0.5f };
                int[] expected = { 2, -2, 0, 0 };
                for (int i = 0; i < values.Length; i++)
                {
                    carrier.Float = new Bindable<float>(values[i]);
                    node.Refresh();
                    Assert.That(node.Output.Value, Is.EqualTo(expected[i]), values[i].ToString("R", CultureInfo.InvariantCulture));
                }
            });
        }

        // SingleToString.Refresh 0x1a07fe0: floor (roundss 0x9 at 0x1a08068) then ceil (roundss
        // 0xa at 0x1a08079); a jump table on m_numberOfDecimalPlaces - 1 (0x1a08087..0x1a0809c,
        // table 0x1a08104) selects "F1".."F4" for 1..4, else Single.ToString() (0x1a080ad).
        [Test]
        public void SingleToStringAppliesFloorThenCeilAndOnlyOneToFourDecimalPlaces()
        {
            Run<SingleToString>((node, carrier) =>
            {
                node.Input.SetTarget(carrier, "Float");
                void Expect(float value, string expected, string label)
                {
                    carrier.Float = new Bindable<float>(value);
                    node.Refresh();
                    Assert.That(node.Output.Value, Is.EqualTo(expected), label);
                }

                Expect(2.5f, "2.5", "default 0 places uses ToString()");
                SetSerialized(node, "m_numberOfDecimalPlaces", 1);
                Expect(1.26f, "1.3", "F1");
                SetSerialized(node, "m_numberOfDecimalPlaces", 2);
                Expect(2.5f, "2.50", "F2");
                SetSerialized(node, "m_numberOfDecimalPlaces", 3);
                Expect(2.5f, "2.500", "F3");
                SetSerialized(node, "m_numberOfDecimalPlaces", 4);
                Expect(1f, "1.0000", "F4");
                SetSerialized(node, "m_numberOfDecimalPlaces", 5);
                Expect(2.5f, "2.5", "5 places falls back to ToString()");
                SetSerialized(node, "m_numberOfDecimalPlaces", -1);
                Expect(2.5f, "2.5", "negative places falls back to ToString()");

                SetSerialized(node, "m_numberOfDecimalPlaces", 0);
                SetSerialized(node, "m_floor", true);
                Expect(2.7f, "2", "floor");
                Expect(-0.5f, "-1", "floor");
                SetSerialized(node, "m_floor", false);
                SetSerialized(node, "m_ceil", true);
                Expect(2.2f, "3", "ceil");
                SetSerialized(node, "m_floor", true);
                Expect(2.5f, "2", "floor runs first, so ceil sees an integer");
            });
        }

        // StringFormatter_6Args.Refresh 0x1a08ba0: BindReference.GetValue on Arg1..Arg6
        // (+0x60..+0x88) precedes GetValue<string> on String (+0x58) at 0x1a08ec3; only a null
        // result becomes String.Empty (0x1a08ecb).
        [Test]
        public void StringFormatterSixArgsReadsEveryArgumentBeforeTheFormat()
        {
            Run<StringFormatter_6Args>((node, carrier) =>
            {
                carrier.Text = carrier.Recording("Arg1", "a");
                carrier.SecondText = carrier.Recording("Arg2", "b");
                carrier.ThirdText = carrier.Recording("Arg3", "c");
                carrier.FourthText = carrier.Recording("Arg4", "d");
                carrier.FifthText = carrier.Recording("Arg5", "e");
                carrier.SixthText = carrier.Recording("Arg6", "f");
                carrier.Format = carrier.Recording("String", "{5}{4}{3}{2}{1}{0}");
                node.String.SetTarget(carrier, "Format");
                node.Arg1.SetTarget(carrier, "Text");
                node.Arg2.SetTarget(carrier, "SecondText");
                node.Arg3.SetTarget(carrier, "ThirdText");
                node.Arg4.SetTarget(carrier, "FourthText");
                node.Arg5.SetTarget(carrier, "FifthText");
                node.Arg6.SetTarget(carrier, "SixthText");
                string[] order = { "Arg1", "Arg2", "Arg3", "Arg4", "Arg5", "Arg6", "String" };

                carrier.Reads.Clear();
                node.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(order));
                Assert.That(node.Output.Value, Is.EqualTo("fedcba"));

                carrier.Format = carrier.Recording<string>("String", null);
                carrier.Reads.Clear();
                node.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(order));
                Assert.That(node.Output.Value, Is.EqualTo(string.Empty));
            });
        }

        // TimeSpanToDays 0x1a09640 / TimeSpanToSeconds 0x1a09940: TotalDays / TotalSeconds plus
        // 0.5 (addsd, constant 0x29a6e58 = 0.5) truncated by cvttsd2si, so midpoints round up
        // and negative values round toward zero.
        [Test]
        public void TimeSpanToDaysAndSecondsAddAHalfAndTruncate()
        {
            double[] units = { 1.5, 2.5, 0.49, -1, -2 };
            int[] expected = { 2, 3, 0, 0, -1 };
            Run<TimeSpanToDays>((node, carrier) =>
            {
                node.Input.SetTarget(carrier, "Span");
                for (int i = 0; i < units.Length; i++)
                {
                    carrier.Span = new Bindable<TimeSpan>(TimeSpan.FromHours(units[i] * 24));
                    node.Refresh();
                    Assert.That(node.Output.Value, Is.EqualTo(expected[i]), units[i] + " days");
                }
            });
            Run<TimeSpanToSeconds>((node, carrier) =>
            {
                node.Input.SetTarget(carrier, "Span");
                for (int i = 0; i < units.Length; i++)
                {
                    carrier.Span = new Bindable<TimeSpan>(TimeSpan.FromMilliseconds(units[i] * 1000));
                    node.Refresh();
                    Assert.That(node.Output.Value, Is.EqualTo(expected[i]), units[i] + " seconds");
                }
            });
        }

        // TimeSpanToString.Refresh 0x1a09c40: Days, Hours, Minutes and Seconds are read first;
        // only the branch for the first positive component among days (0x1a09d27), hours
        // (0x1a09d86) and minutes (0x1a09e32), else the less-than-minute one, reads its format.
        // Days and hours use Format(string, object, object) (0x1a09df4), minutes and seconds
        // Format(string, object) (0x1a09eb8); an empty format keeps String.Empty.
        [Test]
        public void TimeSpanToStringReadsOnlyTheFormatForTheLargestPositiveComponent()
        {
            Run<TimeSpanToString>((node, carrier) =>
            {
                carrier.Text = carrier.Recording("Day", "{0}d {1}h");
                carrier.SecondText = carrier.Recording("Hour", "{0}h {1}m");
                carrier.ThirdText = carrier.Recording("Min", "{0}m {1}");
                carrier.FourthText = carrier.Recording("LessMin", "{0}s");
                node.Input.SetTarget(carrier, "Span");
                node.GreaterThanDayFormatString.SetTarget(carrier, "Text");
                node.GreaterThanHourFormatString.SetTarget(carrier, "SecondText");
                node.GreaterThanMinFormatString.SetTarget(carrier, "ThirdText");
                node.LessThanMinFormatString.SetTarget(carrier, "FourthText");
                void Expect(TimeSpan span, string expected, string read)
                {
                    carrier.Span = new Bindable<TimeSpan>(span);
                    carrier.Reads.Clear();
                    node.Refresh();
                    Assert.That(carrier.Reads, Is.EqualTo(new[] { read }), span.ToString());
                    Assert.That(node.Output.Value, Is.EqualTo(expected), span.ToString());
                }

                Expect(new TimeSpan(1, 2, 3, 4), "1d 2h", "Day");
                Expect(new TimeSpan(0, 2, 3, 4), "2h 3m", "Hour");
                Expect(new TimeSpan(0, 0, 0, 4), "4s", "LessMin");
                Expect(TimeSpan.FromHours(-1), "0s", "LessMin");
                // The minutes branch passes one argument, so a second placeholder faults.
                carrier.Span = new Bindable<TimeSpan>(new TimeSpan(0, 0, 3, 4));
                Assert.Throws<FormatException>(() => node.Refresh());
                carrier.ThirdText = carrier.Recording("Min", "{0}m");
                Expect(new TimeSpan(0, 0, 3, 4), "3m", "Min");

                carrier.Text = carrier.Recording("Day", string.Empty);
                Expect(new TimeSpan(1, 2, 3, 4), string.Empty, "Day");
            });
        }

        // TimeStampToDateTime.Refresh 0x1a0a440 passes GetValue<ulong> to
        // TimeUtils.FromUnixTime(ulong) (0x1a0a4aa), a millisecond offset from the Utc epoch.
        [Test]
        public void TimeStampToDateTimeReadsUnixMilliseconds()
        {
            Run<TimeStampToDateTime>((node, carrier) =>
            {
                carrier.Ulong = new Bindable<ulong>(1000UL);
                node.Ulong.SetTarget(carrier, "Ulong");
                node.Refresh();
                Assert.That(node.Output.Value, Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc)));
                Assert.That(node.Output.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            });
        }

        // TraversalTime.Refresh 0x1a0a730: Distance (+0x58) then UnitsPerSecond (+0x60), divided
        // in single precision (divss 0x1a0a79d) and widened for TimeSpan.FromSeconds (0x1a0a7d0),
        // which faults for an infinite quotient.
        [Test]
        public void TraversalTimeDividesDistanceBySpeedIntoSeconds()
        {
            Run<TraversalTime>((node, carrier) =>
            {
                carrier.Float = carrier.Recording("Distance", 10f);
                carrier.SecondFloat = carrier.Recording("UnitsPerSecond", 4f);
                node.Distance.SetTarget(carrier, "Float");
                node.UnitsPerSecond.SetTarget(carrier, "SecondFloat");

                carrier.Reads.Clear();
                node.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(new[] { "Distance", "UnitsPerSecond" }));
                Assert.That(node.Time.Value, Is.EqualTo(TimeSpan.FromMilliseconds(2500)));

                carrier.SecondFloat = new Bindable<float>(0f);
                Assert.Throws<OverflowException>(() => node.Refresh());
            });
        }

        // Vector2Splitter.Refresh 0x1a0ab30 publishes x to X (+0x60) then y to Y (+0x68).
        // VectorDistance.Refresh 0x1a0b470 publishes the inlined Vector2.Distance (sqrtss 0x1a0b508).
        [Test]
        public void Vector2SplitterAndVectorDistanceUseTheVectorComponents()
        {
            Run<Vector2Splitter>((node, carrier) =>
            {
                carrier.Vector = new Bindable<Vector2>(new Vector2(3f, -4f));
                node.First.SetTarget(carrier, "Vector");
                node.Refresh();
                Assert.That(node.X.Value, Is.EqualTo(3f));
                Assert.That(node.Y.Value, Is.EqualTo(-4f));
            });
            Run<VectorDistance>((node, carrier) =>
            {
                carrier.Vector = new Bindable<Vector2>(new Vector2(1f, 1f));
                carrier.SecondVector = new Bindable<Vector2>(new Vector2(4f, -3f));
                node.First.SetTarget(carrier, "Vector");
                node.Second.SetTarget(carrier, "SecondVector");
                node.Refresh();
                Assert.That(node.Distance.Value, Is.EqualTo(5f));
            });
        }

        // VectorBuilder.Refresh 0x1a0ae40 reads X, Y, Z, W (0x1a0ae78..0x1a0aea8) before
        // publishing Vector2Output, Vector3Output and Vector4Output from them.
        [Test]
        public void VectorBuilderReadsEveryComponentBeforePublishing()
        {
            Run<VectorBuilder>((node, carrier) =>
            {
                carrier.Float = carrier.Recording("X", 1f);
                carrier.SecondFloat = carrier.Recording("Y", 2f);
                carrier.ThirdFloat = carrier.Recording("Z", 3f);
                carrier.FourthFloat = carrier.Recording("W", 4f);
                node.X.SetTarget(carrier, "Float");
                node.Y.SetTarget(carrier, "SecondFloat");
                node.Z.SetTarget(carrier, "ThirdFloat");
                node.W.SetTarget(carrier, "FourthFloat");
                string[] readsAtFirstPublish = null;
                Action listener = () => readsAtFirstPublish = carrier.Reads.ToArray();
                node.Vector2Output.AddListener(listener);
                try
                {
                    carrier.Reads.Clear();
                    node.Refresh();
                }
                finally
                {
                    node.Vector2Output.RemoveListener(listener);
                }
                Assert.That(readsAtFirstPublish, Is.EqualTo(new[] { "X", "Y", "Z", "W" }));
                Assert.That(node.Vector2Output.Value, Is.EqualTo(new Vector2(1f, 2f)));
                Assert.That(node.Vector3Output.Value, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(node.Vector4Output.Value, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
            });
        }

        // Each SetUpBindReferenceFields override builds its list in the order the native
        // body adds the fields (for example DateStringFormatter 0x19fbbb0: String, then
        // dateTime1..3, unlike the Refresh read order).
        [Test]
        public void SetUpBindReferenceFieldsListsTheNativeReferenceOrder()
        {
            Run<DateStringFormatter>((node, carrier) =>
                AssertSameReferences(SetUpBindReferences(node), new[] { node.String, node.dateTime1, node.dateTime2, node.dateTime3 }));
            Run<StringFormatter_6Args>((node, carrier) =>
                AssertSameReferences(SetUpBindReferences(node), new[] { node.String, node.Arg1, node.Arg2, node.Arg3, node.Arg4, node.Arg5, node.Arg6 }));
            Run<TimeSpanToString>((node, carrier) =>
                AssertSameReferences(SetUpBindReferences(node), new[] { node.Input, node.GreaterThanDayFormatString, node.GreaterThanHourFormatString, node.GreaterThanMinFormatString, node.LessThanMinFormatString }));
            Run<VectorBuilder>((node, carrier) =>
                AssertSameReferences(SetUpBindReferences(node), new[] { node.X, node.Y, node.Z, node.W }));
            Run<DateOffset>((node, carrier) =>
                AssertSameReferences(SetUpBindReferences(node), new[] { node.Input, node.Offset }));
            Run<TraversalTime>((node, carrier) =>
                AssertSameReferences(SetUpBindReferences(node), new[] { node.Distance, node.UnitsPerSecond }));
            Run<VectorDistance>((node, carrier) =>
                AssertSameReferences(SetUpBindReferences(node), new[] { node.First, node.Second }));
            Run<NumberPercentFormatter>((node, carrier) =>
                AssertSameReferences(SetUpBindReferences(node), new[] { node.Number }));
        }
    }
}
