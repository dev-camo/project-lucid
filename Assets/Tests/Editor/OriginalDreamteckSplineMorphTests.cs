using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineMorph and its Channel (x86-64 0x4c7350..0x4c8f72). The morph works on a Linear SplineComputer whose three
    // points sit at (0,0,0), (1,0,0) and (2,0,0); the channels hold copies or hand-made snapshots of its points. Expected values are
    // hand arithmetic from the native bodies cited per test. Objects are owned by the test and destroyed in TearDown.
    public sealed class OriginalDreamteckSplineMorphTests
    {
        private const float Tolerance = 1e-4f;

        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ", expected " + expected.ToString("F6") + ")");
        }

        private static T Read<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, InstanceAny);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }

        private static void Invoke(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, InstanceAny, null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, null);
        }

        private static SplinePoint[] LinePoints(int count)
        {
            SplinePoint[] points = new SplinePoint[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = new SplinePoint(new Vector3(i, 0f, 0f));
            }

            return points;
        }

        private SplineComputer NewComputer(int count = 3)
        {
            SplineComputer computer = NewObject("computer").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(LinePoints(count));
            return computer;
        }

        private SplineMorph NewMorph(SplineComputer computer)
        {
            SplineMorph morph = NewObject("morph").AddComponent<SplineMorph>();
            morph.spline = computer;
            return morph;
        }

        private static SplinePoint[] LocalPoints(SplineComputer computer)
        {
            return computer.GetPoints(SplineComputer.Space.Local);
        }

        // A morph with the base channel "A" (the line) and as many further channels as names are given, each a copy of the line.
        private SplineMorph NewMorphWithChannels(out SplineComputer computer, params string[] names)
        {
            computer = NewComputer();
            SplineMorph morph = NewMorph(computer);
            foreach (string name in names)
            {
                morph.AddChannel(name);
            }

            return morph;
        }

        // The enums (dump.cs) and the Channel defaults of the constructor 0x4c8c00: empty points, percent 1, empty name, no curve,
        // linear interpolation.
        [Test]
        public void EnumsAndChannelKeepTheShippedDefaults()
        {
            Assert.That(new[] { (int)SplineMorph.CycleMode.Default, (int)SplineMorph.CycleMode.Loop, (int)SplineMorph.CycleMode.PingPong }, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(new[] { (int)SplineMorph.UpdateMode.Update, (int)SplineMorph.UpdateMode.FixedUpdate, (int)SplineMorph.UpdateMode.LateUpdate }, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(new[] { (int)SplineMorph.Channel.Interpolation.Linear, (int)SplineMorph.Channel.Interpolation.Spherical }, Is.EqualTo(new[] { 0, 1 }));

            SplineMorph.Channel channel = new SplineMorph.Channel();
            Assert.That(Read<SplinePoint[]>(channel, "points"), Is.Not.Null);
            Assert.That(Read<SplinePoint[]>(channel, "points").Length, Is.EqualTo(0));
            Assert.That(Read<float>(channel, "percent"), Is.EqualTo(1f));
            Assert.That(channel.name, Is.EqualTo(""));
            Assert.That(channel.curve, Is.Null);
            Assert.That(channel.interpolation, Is.EqualTo(SplineMorph.Channel.Interpolation.Linear));
        }

        // The constructor 0x4c8ee0: local space, one second cycle, no cycling, no channels; Reset 0x4c7450 takes the SplineComputer on
        // the same object.
        [Test]
        public void MorphKeepsItsDefaultsAndResetTakesTheComputerOnTheObject()
        {
            GameObject host = NewObject("host");
            SplineMorph morph = host.AddComponent<SplineMorph>();
            Assert.That(morph.space, Is.EqualTo(SplineComputer.Space.Local));
            Assert.That(morph.cycle, Is.False);
            Assert.That(morph.cycleMode, Is.EqualTo(SplineMorph.CycleMode.Default));
            Assert.That(morph.cycleUpdateMode, Is.EqualTo(SplineMorph.UpdateMode.Update));
            Assert.That(morph.cycleDuration, Is.EqualTo(1f));
            Assert.That(morph.spline == null, Is.True);
            Assert.That(morph.GetChannelCount(), Is.EqualTo(0));
            Assert.That(Read<short>(morph, "cycleDirection"), Is.EqualTo((short)1));

            SplineComputer computer = host.AddComponent<SplineComputer>();
            Invoke(morph, "Reset");
            Assert.That(morph.spline, Is.SameAs(computer));
        }

        // The serialized layout: the shipped member order, the menu entry, the former name of the channel array and the internal
        // serialized fields of Channel.
        [Test]
        public void SerializedLayoutKeepsTheShippedOrderAndAttributes()
        {
            string[] morphFields = typeof(SplineMorph).GetFields(InstanceAny | BindingFlags.DeclaredOnly).Select(f => f.Name).ToArray();
            Assert.That(morphFields, Is.EqualTo(new[] { "space", "cycle", "cycleMode", "cycleUpdateMode", "cycleDuration", "_spline", "points", "cycleValue", "cycleDirection", "channels" }));
            AddComponentMenu menu = (AddComponentMenu)typeof(SplineMorph).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Morph"));
            FieldInfo channels = typeof(SplineMorph).GetField("channels", InstanceAny);
            Assert.That(channels.IsPrivate, Is.True);
            Assert.That(channels.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1));
            Assert.That(channels.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            Assert.That(((FormerlySerializedAsAttribute)channels.GetCustomAttributes(typeof(FormerlySerializedAsAttribute), false).Single()).oldName, Is.EqualTo("morphStates"));

            string[] channelFields = typeof(SplineMorph.Channel).GetFields(InstanceAny | BindingFlags.DeclaredOnly).Select(f => f.Name).ToArray();
            Assert.That(channelFields, Is.EqualTo(new[] { "points", "percent", "name", "curve", "interpolation" }));
            Assert.That(typeof(SplineMorph.Channel).GetField("points", InstanceAny).IsAssembly, Is.True);
            Assert.That(typeof(SplineMorph.Channel).GetField("percent", InstanceAny).IsAssembly, Is.True);
            Assert.That(typeof(SplineMorph.Channel).IsSerializable, Is.True);
        }

        // AddChannel 0x4c88d0: nothing without a spline; a channel starts at weight 1 with the spline's points (in the morph's
        // space), the given name and the curve (0,0) tangent out 1 -> (1,1) tangent in 1, which is the identity.
        [Test]
        public void AddChannelCapturesTheSplineWithTheDefaultCurve()
        {
            SplineMorph alone = NewObject("alone").AddComponent<SplineMorph>();
            alone.AddChannel("ignored");
            Assert.That(alone.GetChannelCount(), Is.EqualTo(0));

            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A");
            Assert.That(morph.GetChannelCount(), Is.EqualTo(1));
            SplineMorph.Channel channel = morph.GetChannel(0);
            Assert.That(channel.name, Is.EqualTo("A"));
            Assert.That(morph.GetWeight(0), Is.EqualTo(1f));
            Assert.That(morph.GetSnapshot(0).Length, Is.EqualTo(3));
            Near(morph.GetSnapshot(0)[2].position, new Vector3(2f, 0f, 0f), "captured point");
            Assert.That(channel.curve.keys.Length, Is.EqualTo(2));
            Assert.That(channel.curve.keys[0].time, Is.EqualTo(0f));
            Assert.That(channel.curve.keys[0].value, Is.EqualTo(0f));
            Assert.That(channel.curve.keys[0].inTangent, Is.EqualTo(0f));
            Assert.That(channel.curve.keys[0].outTangent, Is.EqualTo(1f));
            Assert.That(channel.curve.keys[1].time, Is.EqualTo(1f));
            Assert.That(channel.curve.keys[1].value, Is.EqualTo(1f));
            Assert.That(channel.curve.keys[1].inTangent, Is.EqualTo(1f));
            Assert.That(channel.curve.keys[1].outTangent, Is.EqualTo(0f));
            Assert.That(channel.curve.Evaluate(0.3f), Is.EqualTo(0.3f).Within(1e-5f));
        }

        // AddChannel 0x4c88d0 / CaptureSnapshot 0x4c85c0: a further channel (or snapshot, except index 0) with another point count
        // is refused with an error, naming the first channel's count for AddChannel and the spline's for CaptureSnapshot.
        [Test]
        public void MismatchedPointCountsAreRefusedWithAnError()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A");
            computer.SetPoints(LinePoints(4));
            LogAssert.Expect(LogType.Error, "Point count must be the same as 3");
            morph.AddChannel("B");
            Assert.That(morph.GetChannelCount(), Is.EqualTo(1));

            morph.CaptureSnapshot(0);
            Assert.That(morph.GetSnapshot(0).Length, Is.EqualTo(4), "index 0 always captures");
            morph.AddChannel("B");
            Assert.That(morph.GetChannelCount(), Is.EqualTo(2));
            computer.SetPoints(LinePoints(3));
            LogAssert.Expect(LogType.Error, "Point count must be the same as 3");
            morph.CaptureSnapshot(1);
            Assert.That(morph.GetSnapshot(1).Length, Is.EqualTo(4), "refused");
            morph.CaptureSnapshot(0);
            Assert.That(morph.GetSnapshot(0).Length, Is.EqualTo(3));
        }

        // GetChannelIndex 0x4c84d0: an unknown name logs and answers channel 0 (so the by-name accessors act on the first channel).
        [Test]
        public void AnUnknownChannelNameLogsAndFallsBackToTheFirstChannel()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A", "B");
            morph.SetWeight(0, 0.25f);
            LogAssert.Expect(LogType.Log, "Channel not found nope");
            Assert.That(morph.GetWeight("nope"), Is.EqualTo(0.25f));
            LogAssert.Expect(LogType.Log, "Channel not found nope");
            Assert.That(morph.GetChannel("nope"), Is.SameAs(morph.GetChannel(0)));
            Assert.That(morph.GetChannel("B"), Is.SameAs(morph.GetChannel(1)));
            Assert.That(morph.GetSnapshot("B"), Is.SameAs(morph.GetSnapshot(1)));
        }

        // SetWeight 0x4c77d0 / 0x4c8460 clamp to 0..1; SetCycle 0x4c77a0 clamps too. SetSnapshot / GetSnapshot just exchange the array.
        [Test]
        public void IndexedAndNamedWeightsAreClampedAndSnapshotsAreExchangedByReference()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A", "B");
            morph.SetWeight(1, 2f);
            Assert.That(morph.GetWeight(1), Is.EqualTo(1f));
            morph.SetWeight(1, -3f);
            Assert.That(morph.GetWeight(1), Is.EqualTo(0f));
            morph.SetWeight("B", 0.4f);
            Assert.That(morph.GetWeight("B"), Is.EqualTo(0.4f));
            morph.SetWeight("B", 7f);
            Assert.That(morph.GetWeight(1), Is.EqualTo(1f));
            morph.SetCycle(5f);
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(1f));
            morph.SetCycle(-5f);
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(0f));
            morph.SetCycle(0.3f);
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(0.3f));

            SplinePoint[] snapshot = LinePoints(3);
            morph.SetSnapshot(1, snapshot);
            Assert.That(morph.GetSnapshot(1), Is.SameAs(snapshot));
        }

        // SetWeight(percent, loop) 0x4c7610: the percent is spread over the channels (count - 1 steps, or count steps when looping),
        // a channel's weight falling linearly from 1 to 0 over one step. Three channels: 0.5 -> (0,1,0), 0.25 -> (0.5,0.5,0);
        // looping 0.9 -> value 2.7: channel 2 is 0.3 and the wrap gives channel 0 the weight 0.7.
        [Test]
        public void PercentWeightsCrossFadeNeighbouringChannelsAndLoopWraps()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A", "B", "C");
            morph.SetWeight(0.5f);
            Assert.That(new[] { morph.GetWeight(0), morph.GetWeight(1), morph.GetWeight(2) }, Is.EqualTo(new[] { 0f, 1f, 0f }));
            morph.SetWeight(0.25f);
            Assert.That(morph.GetWeight(0), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(morph.GetWeight(1), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(morph.GetWeight(2), Is.EqualTo(0f));
            morph.SetWeight(1f);
            Assert.That(new[] { morph.GetWeight(0), morph.GetWeight(1), morph.GetWeight(2) }, Is.EqualTo(new[] { 0f, 0f, 1f }));

            morph.SetWeight(0.9f, true);
            Assert.That(morph.GetWeight(0), Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(morph.GetWeight(1), Is.EqualTo(0f));
            Assert.That(morph.GetWeight(2), Is.EqualTo(0.3f).Within(1e-5f));
        }

        // UpdateMorph 0x4c7820, linear: channel 0 (the line) is the base and channel 1 adds (channel1 - channel0) * curve(weight)
        // to position, both tangents, normal, colour and size at weight 0.5; the normals end up unit length. The channel 1
        // snapshot has y = 10, tangents (x,10,+-2), normal (1,0,0), size 3, black. Point types: a mirrored base point takes the
        // channel's type (Broken, SmoothFree, Mirrored); mirrored points re-mirror tangent2 from the blended tangent.
        [Test]
        public void LinearBlendAddsTheWeightedDifferenceToTheBaseChannel()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A", "B");
            SplinePoint[] target = new SplinePoint[3];
            SplinePoint.Type[] types = { SplinePoint.Type.Broken, SplinePoint.Type.SmoothFree, SplinePoint.Type.SmoothMirrored };
            for (int i = 0; i < 3; i++)
            {
                SplinePoint point = new SplinePoint(new Vector3(i, 10f, 0f));
                point.type = types[i];
                point.tangent = new Vector3(i, 10f, 2f);
                point.tangent2 = i == 2 ? new Vector3(i, 10f, -4f) : new Vector3(i, 10f, -2f);
                point.normal = new Vector3(1f, 0f, 0f);
                point.size = 3f;
                point.color = new Color(0f, 0f, 0f, 1f);
                target[i] = point;
            }

            morph.SetSnapshot(1, target);
            morph.SetWeight(1, 0.5f);
            SplinePoint[] blended = LocalPoints(computer);
            Assert.That(blended.Length, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Near(blended[i].position, new Vector3(i, 5f, 0f), "position " + i);
                Near(blended[i].tangent, new Vector3(i, 5f, 1f), "tangent " + i);
                Near(blended[i].normal, new Vector3(0.70710678f, 0.70710678f, 0f), "normal " + i);
                Assert.That(blended[i].size, Is.EqualTo(2f).Within(1e-5f), "size " + i);
                Assert.That(blended[i].color.r, Is.EqualTo(0.5f).Within(1e-5f), "red " + i);
                Assert.That(blended[i].color.a, Is.EqualTo(1f).Within(1e-5f), "alpha " + i);
            }

            Assert.That(blended[0].type, Is.EqualTo(SplinePoint.Type.Broken));
            Near(blended[0].tangent2, new Vector3(0f, 5f, -1f), "broken tangent2");
            Assert.That(blended[1].type, Is.EqualTo(SplinePoint.Type.SmoothFree));
            Near(blended[1].tangent2, new Vector3(1f, 5f, -1f), "free tangent2");
            Assert.That(blended[2].type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Near(blended[2].tangent2, new Vector3(2f, 5f, -1f), "mirrored tangent2 is re-derived from the tangent, not blended (-2)");
        }

        // A SmoothFree base point turns Broken when the channel's point is Broken and stays SmoothFree otherwise; a Broken base point
        // never changes type.
        [Test]
        public void TheTypeOfTheBasePointOnlyChangesAsTheMergeRulesSay()
        {
            SplineComputer computer = NewComputer();
            SplinePoint[] baseline = LinePoints(3);
            baseline[0].type = SplinePoint.Type.SmoothFree;
            baseline[1].type = SplinePoint.Type.SmoothFree;
            baseline[2].type = SplinePoint.Type.Broken;
            computer.SetPoints(baseline);
            SplineMorph morph = NewMorph(computer);
            morph.AddChannel("A");
            morph.AddChannel("B");
            SplinePoint[] target = LinePoints(3);
            target[0].type = SplinePoint.Type.Broken;
            target[1].type = SplinePoint.Type.SmoothMirrored;
            target[2].type = SplinePoint.Type.SmoothFree;
            morph.SetSnapshot(1, target);
            morph.SetWeight(1, 1f);
            SplinePoint[] merged = LocalPoints(computer);
            Assert.That(merged[0].type, Is.EqualTo(SplinePoint.Type.Broken), "free + broken");
            Assert.That(merged[1].type, Is.EqualTo(SplinePoint.Type.SmoothFree), "free + mirrored");
            Assert.That(merged[2].type, Is.EqualTo(SplinePoint.Type.Broken), "broken stays");
        }

        // UpdateMorph, Spherical: position and tangents (and normal) are slerped from the base point to base + (channel - base).
        // Base (1,0,0) with channel (0,1,0) at weight 0.5 is the unit diagonal; (2,0,0) -> (0,2,0) has length 2.
        [Test]
        public void SphericalChannelsSlerpPositionsInsteadOfLerping()
        {
            SplineComputer computer = NewComputer();
            computer.SetPoints(new[]
            {
                new SplinePoint(new Vector3(1f, 0f, 0f)),
                new SplinePoint(new Vector3(2f, 0f, 0f)),
                new SplinePoint(new Vector3(3f, 0f, 0f))
            });
            SplineMorph morph = NewMorph(computer);
            morph.AddChannel("A");
            morph.AddChannel("B");
            morph.SetSnapshot(1, new[]
            {
                new SplinePoint(new Vector3(0f, 1f, 0f)),
                new SplinePoint(new Vector3(0f, 2f, 0f)),
                new SplinePoint(new Vector3(0f, 3f, 0f))
            });
            morph.GetChannel(1).interpolation = SplineMorph.Channel.Interpolation.Spherical;
            morph.SetWeight(1, 0.5f);
            SplinePoint[] result = LocalPoints(computer);
            Near(result[0].position, new Vector3(0.70710678f, 0.70710678f, 0f), "slerp 1");
            Near(result[1].position, new Vector3(1.41421356f, 1.41421356f, 0f), "slerp 2");
            Near(result[2].position, new Vector3(2.12132034f, 2.12132034f, 0f), "slerp 3");

            morph.GetChannel(1).interpolation = SplineMorph.Channel.Interpolation.Linear;
            morph.UpdateMorph();
            Near(LocalPoints(computer)[0].position, new Vector3(0.5f, 0.5f, 0f), "lerp 1");
        }

        // RemoveChannel 0x4c8ca0 / 0x4c8c80: an index outside the array is ignored; removal keeps the order of the others. Clear
        // 0x4c8760 empties the array without touching the spline.
        [Test]
        public void RemoveChannelKeepsTheOrderAndIgnoresBadIndicesAndClearEmpties()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A", "B", "C");
            morph.RemoveChannel(3);
            morph.RemoveChannel(-1);
            Assert.That(morph.GetChannelCount(), Is.EqualTo(3));
            morph.RemoveChannel("B");
            Assert.That(morph.GetChannelCount(), Is.EqualTo(2));
            Assert.That(morph.GetChannel(0).name, Is.EqualTo("A"));
            Assert.That(morph.GetChannel(1).name, Is.EqualTo("C"));
            morph.RemoveChannel(0);
            Assert.That(morph.GetChannelCount(), Is.EqualTo(1));
            Assert.That(morph.GetChannel(0).name, Is.EqualTo("C"));
            Assert.That(LocalPoints(computer).Length, Is.EqualTo(3));
            morph.Clear();
            Assert.That(morph.GetChannelCount(), Is.EqualTo(0));
            Assert.That(LocalPoints(computer).Length, Is.EqualTo(3));
        }

        private static void Write(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, InstanceAny);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        // RunUpdate 0x4c74c0 keeps the cycle position inside 0..1 by mode (an infinite duration stops the position moving, so the
        // position is placed with the owned morph's private fields): Default clamps 1.5 to 1; Loop subtracts the whole part, 1.5 ->
        // 0.5; PingPong reflects 1.25 to 0.75 and turns backwards, and -0.25 comes back as 0.25 and turns forwards. Only
        // PingPong keeps its direction between updates.
        [Test]
        public void RunUpdateKeepsTheCycleInsideZeroToOneByMode()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A", "B");
            morph.cycle = true;
            morph.cycleDuration = float.PositiveInfinity;

            Write(morph, "cycleValue", 1.5f);
            morph.cycleMode = SplineMorph.CycleMode.Default;
            Invoke(morph, "Update");
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(1f));
            Assert.That(morph.GetWeight(1), Is.EqualTo(1f));

            Write(morph, "cycleValue", 1.5f);
            Write(morph, "cycleDirection", (short)-1);
            morph.cycleMode = SplineMorph.CycleMode.Loop;
            Invoke(morph, "Update");
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(Read<short>(morph, "cycleDirection"), Is.EqualTo((short)1), "only ping pong keeps its direction");

            Write(morph, "cycleValue", 1.25f);
            morph.cycleMode = SplineMorph.CycleMode.PingPong;
            Invoke(morph, "Update");
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(Read<short>(morph, "cycleDirection"), Is.EqualTo((short)-1));

            Write(morph, "cycleValue", -0.25f);
            Invoke(morph, "Update");
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(Read<short>(morph, "cycleDirection"), Is.EqualTo((short)1));
        }

        // RunUpdate 0x4c74c0 wraps with floor (roundss imm 9 at 0x4c7553, 0x4c7570, 0x4c75a4), which differs from subtracting 1 or
        // negating once the position is further out: Loop 2.5 -> 0.5; PingPong 2.25 -> 1 - (2.25 - 2) = 0.75 turning backwards;
        // PingPong -1.25 -> 1.25 - 1 = 0.25 turning forwards.
        [Test]
        public void RunUpdateWrapsWithFloorWhenTheCycleIsMoreThanOneTurnOut()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A", "B");
            morph.cycle = true;
            morph.cycleDuration = float.PositiveInfinity;

            Write(morph, "cycleValue", 2.5f);
            morph.cycleMode = SplineMorph.CycleMode.Loop;
            Invoke(morph, "Update");
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(0.5f).Within(1e-6f), "loop");

            Write(morph, "cycleValue", 2.25f);
            morph.cycleMode = SplineMorph.CycleMode.PingPong;
            Invoke(morph, "Update");
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(0.75f).Within(1e-6f), "ping pong above one");
            Assert.That(Read<short>(morph, "cycleDirection"), Is.EqualTo((short)-1));

            Write(morph, "cycleValue", -1.25f);
            Invoke(morph, "Update");
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(0.25f).Within(1e-6f), "ping pong below zero");
            Assert.That(Read<short>(morph, "cycleDirection"), Is.EqualTo((short)1));
        }

        // RunUpdate 0x4c74c0 passes (cycleMode == Loop) to SetWeight (0x4c75bb cmp eax,1; sete sil): three channels at cycle 0.9 give
        // SetWeight(0.9, true), value 2.7, weights (0.7, 0, 0.3); without the flag the value would be 1.8 and the weights (0, 0.2, 0.8).
        // The step is cycleDirection * (deltaTime / cycleDuration) (0x4c74ef..0x4c7503): ping pong at 0.5 going backwards over a one
        // second cycle ends at 0.5 - deltaTime.
        [Test]
        public void RunUpdatePassesTheLoopFlagToSetWeightAndMovesByTheDirectionedStep()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A", "B", "C");
            morph.cycle = true;
            morph.cycleDuration = float.PositiveInfinity;
            Write(morph, "cycleValue", 0.9f);
            morph.cycleMode = SplineMorph.CycleMode.Loop;
            Invoke(morph, "Update");
            Assert.That(morph.GetWeight(0), Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(morph.GetWeight(1), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(morph.GetWeight(2), Is.EqualTo(0.3f).Within(1e-5f));

            Assume.That(Time.deltaTime > 0f && Time.deltaTime < 0.5f, "needs a positive frame time");
            morph.cycleDuration = 1f;
            morph.cycleMode = SplineMorph.CycleMode.PingPong;
            Write(morph, "cycleValue", 0.5f);
            Write(morph, "cycleDirection", (short)-1);
            Invoke(morph, "Update");
            Assert.That(Read<float>(morph, "cycleValue"), Is.EqualTo(0.5f - Time.deltaTime).Within(1e-6f));
            Assert.That(Read<short>(morph, "cycleDirection"), Is.EqualTo((short)-1));
        }

        // Update / FixedUpdate / LateUpdate 0x4c74a0, 0x4c75d0, 0x4c75f0: each runs the cycle only for its own update mode and RunUpdate
        // 0x4c74c0 does nothing unless cycling. With an infinite duration the cycle position does not move (delta / infinity = 0),
        // so the weights are those of SetWeight(position): at 0.25 over three channels (0.5, 0.5, 0).
        [Test]
        public void EachUpdateCallbackRunsTheCycleOnlyForItsOwnMode()
        {
            SplineComputer computer;
            SplineMorph morph = NewMorphWithChannels(out computer, "A", "B", "C");
            morph.cycleDuration = float.PositiveInfinity;
            morph.SetCycle(0.25f);
            string[] callbacks = { "Update", "FixedUpdate", "LateUpdate" };
            SplineMorph.UpdateMode[] modes = { SplineMorph.UpdateMode.Update, SplineMorph.UpdateMode.FixedUpdate, SplineMorph.UpdateMode.LateUpdate };

            morph.cycle = false;
            morph.SetWeight(0, 1f);
            morph.SetWeight(1, 1f);
            morph.SetWeight(2, 1f);
            for (int m = 0; m < 3; m++)
            {
                morph.cycleUpdateMode = modes[m];
                Invoke(morph, callbacks[m]);
            }

            Assert.That(new[] { morph.GetWeight(0), morph.GetWeight(1), morph.GetWeight(2) }, Is.EqualTo(new[] { 1f, 1f, 1f }), "not cycling");

            morph.cycle = true;
            for (int m = 0; m < 3; m++)
            {
                morph.cycleUpdateMode = modes[m];
                for (int other = 0; other < 3; other++)
                {
                    morph.SetWeight(0, 1f);
                    morph.SetWeight(1, 1f);
                    morph.SetWeight(2, 1f);
                    Invoke(morph, callbacks[other]);
                    bool ran = other == m;
                    Assert.That(morph.GetWeight(0), Is.EqualTo(ran ? 0.5f : 1f).Within(1e-5f), modes[m] + " mode, " + callbacks[other] + " callback");
                    Assert.That(morph.GetWeight(2), Is.EqualTo(ran ? 0f : 1f).Within(1e-5f), modes[m] + " mode, " + callbacks[other] + " callback");
                }
            }
        }
    }
}
