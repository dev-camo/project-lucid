using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.ParticleController, driven through a SplineComputer that holds the Linear polyline
    // (0,0,0) -> (10,0,0) -> (10,0,10): 20 units long, so percent p is (20p,0,0) up to 0.5 and (10,0,20p-10) after. The samples
    // look along +x on the first segment and +z on the second, with up (0,1,0), so the sample's right (Cross(up, forward)
    // normalised) is -z on the first segment and +x on the second, and Cross(forward, up) is +z on the first. The particles live
    // in a real ParticleSystem of an owned game object: a test hands it particles with SetParticles (and the custom data stream the
    // controller needs, a zero w marking a particle as not yet born), runs the controller's LateRun and reads the particles back.
    // Expected values are hand arithmetic from the native bodies cited per test. TearDown destroys everything the test made.
    public sealed class OriginalDreamteckParticleControllerTests
    {
        private const float Tolerance = 1e-3f;

        private const int MaxParticles = 10;

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

        private sealed class Rig
        {
            public GameObject Host;
            public ParticleSystem System;
            public ParticleController Controller;
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F5") + ")");
        }

        private static bool IsMissing(object value)
        {
            return value == null || (value is UnityEngine.Object unityObject && unityObject == null);
        }

        private GameObject NewObject(string name)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        private SplineComputer NewLine()
        {
            SplineComputer computer = NewObject("line").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f))
            });
            computer.RebuildImmediate();
            return computer;
        }

        // A stopped particle system (world simulation space, the custom data stream the controller reads and writes, constant
        // two-value curves so the constant min and max the controller reads are what the test says) and a controller on it.
        private Rig NewRig(SplineComputer computer)
        {
            Rig rig = new Rig();
            rig.Host = NewObject("particles");
            rig.System = rig.Host.AddComponent<ParticleSystem>();
            ParticleSystem.EmissionModule emission = rig.System.emission;
            emission.enabled = false;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(10f, 10f);
            ParticleSystem.MainModule main = rig.System.main;
            main.playOnAwake = false;
            main.maxParticles = MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 4f);
            ParticleSystem.CustomDataModule data = rig.System.customData;
            data.enabled = true;
            data.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            data.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
            rig.System.Simulate(0f, true, true);
            rig.Controller = rig.Host.AddComponent<ParticleController>();
            rig.Controller.spline = computer;
            rig.Controller.RebuildImmediate();
            rig.Controller.particleSystemComponent = rig.System;
            return rig;
        }

        private static ParticleSystem.Particle NewParticle(Vector3 position, float startLifetime, float remainingLifetime, Color32 color)
        {
            ParticleSystem.Particle particle = new ParticleSystem.Particle();
            particle.position = position;
            particle.startLifetime = startLifetime;
            particle.remainingLifetime = remainingLifetime;
            particle.startColor = color;
            return particle;
        }

        // Hands the system the particles with a zero custom data (not yet born), or with the given w.
        private static void Inject(ParticleSystem system, float w, params ParticleSystem.Particle[] particles)
        {
            system.SetParticles(particles, particles.Length);
            List<Vector4> data = new List<Vector4>();
            foreach (ParticleSystem.Particle particle in particles)
            {
                data.Add(new Vector4(0f, 0f, 0f, w));
            }

            system.SetCustomParticleData(data, ParticleSystemCustomData.Custom1);
        }

        private static ParticleSystem.Particle[] ReadParticles(ParticleSystem system)
        {
            ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[MaxParticles];
            int count = system.GetParticles(buffer);
            return buffer.Take(count).ToArray();
        }

        private static List<Vector4> ReadData(ParticleSystem system)
        {
            List<Vector4> data = new List<Vector4>();
            system.GetCustomParticleData(data, ParticleSystemCustomData.Custom1);
            return data;
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

        // The internal state of the controller kept for the particle at index.
        private static T ControllerField<T>(ParticleController controller, int index, string name)
        {
            Array controllers = Read<Array>(controller, "_controllers");
            object particle = controllers.GetValue(index);
            Assert.That(particle, Is.Not.Null, "controller " + index);
            return (T)typeof(ParticleController.Particle).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(particle);
        }

        private static double SplinePercent(ParticleController.Particle particle, ParticleController.Wrap wrap, ParticleSystem.Particle source, ParticleController.MotionType motion)
        {
            MethodInfo method = typeof(ParticleController.Particle).GetMethod("GetSplinePercent", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return (double)method.Invoke(particle, new object[] { wrap, source, motion });
        }

        private static ParticleController.Particle NewState(float cycleSpeed, double startPercent)
        {
            ParticleController.Particle state = new ParticleController.Particle();
            FieldInfo cycle = typeof(ParticleController.Particle).GetField("cycleSpeed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            cycle.SetValue(state, cycleSpeed);
            FieldInfo start = typeof(ParticleController.Particle).GetField("startPercent", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            start.SetValue(state, startPercent);
            return state;
        }

        // Constructors 0x4ac9d0 and 0x4aaf40: no offset, scale one, UseParticleSystem motion, one cycle, empty arrays; a new
        // particle state has zero offsets, no cycle and white.
        [Test]
        public void FreshControllerAndParticleStateKeepTheShippedDefaults()
        {
            ParticleController controller = NewObject("controller").AddComponent<ParticleController>();
            Assert.That(controller.offset, Is.EqualTo(Vector2.zero));
            Assert.That(controller.scale, Is.EqualTo(Vector2.one));
            Assert.That(controller.motionType, Is.EqualTo(ParticleController.MotionType.UseParticleSystem));
            Assert.That(controller.minCycles, Is.EqualTo(1f));
            Assert.That(controller.maxCycles, Is.EqualTo(1f));
            Assert.That(controller.emitPoint, Is.EqualTo(ParticleController.EmitPoint.Beginning));
            Assert.That(controller.wrapMode, Is.EqualTo(ParticleController.Wrap.Default));
            Assert.That(controller.pauseWhenNotVisible, Is.False);
            Assert.That(controller.volumetric, Is.False);
            Assert.That(controller.emitFromShell, Is.False);
            Assert.That(controller.apply3DRotation, Is.False);
            Assert.That(Read<ParticleSystem.Particle[]>(controller, "_particles").Length, Is.EqualTo(0));
            Assert.That(Read<ParticleController.Particle[]>(controller, "_controllers").Length, Is.EqualTo(0));
            Assert.That(Read<List<Vector4>>(controller, "_customParticleData"), Is.Not.Null);
            Assert.That(Read<int>(controller, "_particleCount"), Is.EqualTo(0));
            Assert.That(Read<int>(controller, "_birthIndex"), Is.EqualTo(0));

            ParticleController.Particle state = new ParticleController.Particle();
            Type stateType = typeof(ParticleController.Particle);
            BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            Assert.That(stateType.GetField("startOffset", any).GetValue(state), Is.EqualTo(Vector2.zero));
            Assert.That(stateType.GetField("endOffset", any).GetValue(state), Is.EqualTo(Vector2.zero));
            Assert.That(stateType.GetField("cycleSpeed", any).GetValue(state), Is.EqualTo(0f));
            Assert.That(stateType.GetField("startColor", any).GetValue(state), Is.EqualTo(Color.white));
            Assert.That(stateType.GetField("startPercent", any).GetValue(state), Is.EqualTo(0.0));
        }

        // Particle.GetSplinePercent 0x4ac4f0 as a table. With the life left L = remaining / start (1 - L when following backward):
        // Default is Clamp01((1 - L) * cycleSpeed + startPercent) with the product in single precision, Loop is
        // cycleSpeed * (1 - L) + startPercent in double precision with the whole part (taken from the float value) removed only
        // above 1, any other wrap is 0. Start lifetime 4 and remaining 1 give L = 0.25.
        [Test]
        public void SplinePercentFollowsTheNativeTable()
        {
            ParticleSystem.Particle source = NewParticle(Vector3.zero, 4f, 1f, new Color32(255, 255, 255, 255));
            Near(SplinePercent(NewState(1f, 0.1), ParticleController.Wrap.Default, source, ParticleController.MotionType.FollowForward), 0.85, "default forward");
            Near(SplinePercent(NewState(1f, 0.1), ParticleController.Wrap.Default, source, ParticleController.MotionType.FollowBackward), 0.35, "default backward: L is 0.75");
            Near(SplinePercent(NewState(1f, 0.1), ParticleController.Wrap.Default, source, ParticleController.MotionType.None), 0.85, "None does not reverse");
            Near(SplinePercent(NewState(2f, 0.1), ParticleController.Wrap.Default, source, ParticleController.MotionType.FollowForward), 1.0, "clamped above");
            Near(SplinePercent(NewState(-2f, 0.1), ParticleController.Wrap.Default, source, ParticleController.MotionType.FollowForward), 0.0, "clamped below");
            Near(SplinePercent(NewState(3f, 0.1), ParticleController.Wrap.Loop, source, ParticleController.MotionType.FollowForward), 0.35, "loop 2.35");
            Near(SplinePercent(NewState(1f, 0.1), ParticleController.Wrap.Loop, source, ParticleController.MotionType.FollowForward), 0.85, "loop below 1 unchanged");
            Near(SplinePercent(NewState(4f, 0.0), ParticleController.Wrap.Loop, source, ParticleController.MotionType.FollowForward), 0.0, "loop exactly 3 is above 1 and wraps to 0");
            Near(SplinePercent(NewState(-1f, 0.1), ParticleController.Wrap.Loop, source, ParticleController.MotionType.FollowForward), -0.65, "loop does not clamp below");
            Near(SplinePercent(NewState(1f, 0.25), ParticleController.Wrap.Loop, source, ParticleController.MotionType.FollowForward), 1.0, "loop exactly 1 stays 1");
            Near(SplinePercent(NewState(3f, 0.1), ParticleController.Wrap.Loop, source, ParticleController.MotionType.FollowBackward), 0.85, "loop backward: 3 * 0.25 + 0.1");
            Near(SplinePercent(NewState(3f, 0.1), (ParticleController.Wrap)2, source, ParticleController.MotionType.FollowForward), 0.0, "unknown wrap");
        }

        private static void Near(double actual, double expected, string message)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(1e-9), message);
        }

        // The Default product (1 - L) * cycleSpeed has no cast in the text. The evidence that the shipped build multiplies in float
        // before widening (0.75f * 0.1f = 0.07500000298..., not 0.75 * (double)0.1f = 0.07500000111...) is the disassembly (mulss,
        // cvtss2sd). This test cannot tell float from double arithmetic: the IL is sub, mul, conv.r8 with no conv.r4, and the
        // editor's Mono evaluates it at double precision. It pins the value the editor returns for Default and Loop (the double
        // product) and that the Default value narrows to the float product.
        [Test]
        public void SplinePercentEditorProductNarrowsToTheFloatProduct()
        {
            ParticleSystem.Particle source = NewParticle(Vector3.zero, 4f, 1f, new Color32(255, 255, 255, 255));
            double floatProduct = (double)(float)((1f - 0.25f) * 0.1f);
            double doubleProduct = (1.0 - 0.25) * (double)0.1f;
            Assert.That(floatProduct, Is.Not.EqualTo(doubleProduct), "the two orders differ in the last bits");
            double defaultPercent = SplinePercent(NewState(0.1f, 0.0), ParticleController.Wrap.Default, source, ParticleController.MotionType.FollowForward);
            Assert.That(defaultPercent, Is.EqualTo(doubleProduct), "the editor (Mono) value");
            Assert.That((float)defaultPercent, Is.EqualTo((float)floatProduct), "narrows to the float product");
            Assert.That(SplinePercent(NewState(0.1f, 0.0), ParticleController.Wrap.Loop, source, ParticleController.MotionType.FollowForward), Is.EqualTo(doubleProduct));
        }

        // set_particleSystemComponent 0x4aa880 stores the system and fetches its renderer (and faults on null); Reset 0x4ac440
        // calls the base, turns the update method to LateUpdate and fetches a missing particle system from the own object.
        [Test]
        public void SystemSetterFetchesTheRendererAndResetFindsTheSystem()
        {
            GameObject host = NewObject("host");
            ParticleController controller = host.AddComponent<ParticleController>();
            ParticleSystem own = host.AddComponent<ParticleSystem>();
            controller.particleSystemComponent = own;
            Assert.That(controller.particleSystemComponent, Is.SameAs(own));
            Assert.That(Read<ParticleSystemRenderer>(controller, "_renderer"), Is.SameAs(host.GetComponent<ParticleSystemRenderer>()));
            Assert.Throws<NullReferenceException>(() => controller.particleSystemComponent = null);
            Assert.That(controller.particleSystemComponent == null, Is.True, "the field was cleared before the fault");

            controller.updateMethod = SplineUser.UpdateMethod.Update;
            Invoke(controller, "Reset");
            Assert.That(controller.updateMethod, Is.EqualTo(SplineUser.UpdateMethod.LateUpdate));
            Assert.That(controller.particleSystemComponent, Is.SameAs(host.GetComponent<ParticleSystem>()), "Reset found the own system");

            ParticleSystem other = NewObject("other").AddComponent<ParticleSystem>();
            controller.particleSystemComponent = other;
            Invoke(controller, "Reset");
            Assert.That(controller.particleSystemComponent, Is.SameAs(other), "a set system is kept");
        }

        // LateRun 0x4aa900 does nothing without a particle system; the arrays follow the system's maximum particle count (kept
        // controllers carry over by index), and a pause for an invisible renderer fetches the renderer first.
        [Test]
        public void LateRunSizesItsArraysFromTheSystem()
        {
            ParticleController empty = NewObject("empty").AddComponent<ParticleController>();
            Assert.That(() => Invoke(empty, "LateRun"), Throws.Nothing);
            Assert.That(Read<ParticleSystem.Particle[]>(empty, "_particles").Length, Is.EqualTo(0));

            Rig rig = NewRig(NewLine());
            Inject(rig.System, 1f, NewParticle(new Vector3(1f, 2f, 3f), 2f, 1f, new Color32(255, 255, 255, 255)));
            Invoke(rig.Controller, "LateRun");
            Assert.That(Read<ParticleSystem.Particle[]>(rig.Controller, "_particles").Length, Is.EqualTo(MaxParticles));
            Assert.That(Read<ParticleController.Particle[]>(rig.Controller, "_controllers").Length, Is.EqualTo(MaxParticles));
            Assert.That(Read<int>(rig.Controller, "_particleCount"), Is.EqualTo(1));
            object kept = Read<ParticleController.Particle[]>(rig.Controller, "_controllers")[0];
            Assert.That(kept, Is.Not.Null, "a controller was created for the live particle");

            ParticleSystem.MainModule main = rig.System.main;
            main.maxParticles = 5;
            Invoke(rig.Controller, "LateRun");
            Assert.That(Read<ParticleSystem.Particle[]>(rig.Controller, "_particles").Length, Is.EqualTo(5));
            Assert.That(Read<ParticleController.Particle[]>(rig.Controller, "_controllers").Length, Is.EqualTo(5));
            Assert.That(Read<ParticleController.Particle[]>(rig.Controller, "_controllers")[0], Is.SameAs(kept), "kept by index");
            main.maxParticles = 20;
            Invoke(rig.Controller, "LateRun");
            Assert.That(Read<ParticleController.Particle[]>(rig.Controller, "_controllers").Length, Is.EqualTo(20));
            Assert.That(Read<ParticleController.Particle[]>(rig.Controller, "_controllers")[0], Is.SameAs(kept));

            // A controller given its system through the serialized field has no renderer yet; the pause fetches it, whether or not
            // the renderer is visible.
            ParticleController bare = rig.Host.AddComponent<ParticleController>();
            typeof(ParticleController).GetField("_particleSystem", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bare, rig.System);
            bare.pauseWhenNotVisible = true;
            Assert.That(IsMissing(Read<ParticleSystemRenderer>(bare, "_renderer")), Is.True);
            Invoke(bare, "LateRun");
            Assert.That(Read<ParticleSystemRenderer>(bare, "_renderer"), Is.SameAs(rig.Host.GetComponent<ParticleSystemRenderer>()));
        }

        // OnParticleBorn 0x4ab010 marks the particle (custom data w = 1, the other components kept) and a later LateRun leaves
        // an already born particle alone: the birth counter stays.
        [Test]
        public void BornParticlesAreMarkedAndNotBornAgain()
        {
            Rig rig = NewRig(NewLine());
            rig.Controller.motionType = ParticleController.MotionType.UseParticleSystem;
            Inject(rig.System, 0f, NewParticle(new Vector3(1f, 2f, 3f), 2f, 1f, new Color32(10, 20, 30, 255)));
            Invoke(rig.Controller, "LateRun");
            List<Vector4> data = ReadData(rig.System);
            Assert.That(data.Count, Is.EqualTo(1));
            Assert.That(data[0], Is.EqualTo(new Vector4(0f, 0f, 0f, 1f)));
            Assert.That(Read<int>(rig.Controller, "_birthIndex"), Is.EqualTo(1));
            Invoke(rig.Controller, "LateRun");
            Assert.That(Read<int>(rig.Controller, "_birthIndex"), Is.EqualTo(1), "not born again");
            Assert.That(ReadData(rig.System)[0].w, Is.EqualTo(1f));
        }

        // Born with the Beginning emit point and FollowForward (Default wrap, cycleSpeed 1 from the one-cycle range, startPercent 0,
        // life left 0.25): HandleParticle 0x4abe90 puts the particle at percent (1 - 0.25) * 1 = 0.75, (10,0,5), with the sample's
        // forward as velocity and the sample colour times the colour it was born with. The controller remembers its birth state.
        [Test]
        public void BornParticleFollowsTheSplineFromTheBeginning()
        {
            SplineComputer line = NewLine();
            Rig rig = NewRig(line);
            rig.Controller.motionType = ParticleController.MotionType.FollowForward;
            Inject(rig.System, 0f, NewParticle(new Vector3(50f, 50f, 50f), 2f, 0.5f, new Color32(255, 128, 0, 255)));
            Invoke(rig.Controller, "LateRun");
            ParticleSystem.Particle[] particles = ReadParticles(rig.System);
            Assert.That(particles.Length, Is.EqualTo(1));
            Near(particles[0].position, new Vector3(10f, 0f, 5f), "position");
            // The velocity is the evaluated sample's forward (0x4abe90), which on the Linear line is the interpolation towards
            // the corner's averaged direction, not the plain +z of the second segment.
            Near(particles[0].velocity, line.Evaluate(0.75).forward, "velocity is the sample forward");
            Color32 color = particles[0].startColor;
            Assert.That((int)color.r, Is.EqualTo(255), "colour r");
            Assert.That((int)color.g, Is.EqualTo(128), "colour g");
            Assert.That((int)color.b, Is.EqualTo(0), "colour b");
            Assert.That((int)color.a, Is.EqualTo(255), "colour a");
            Assert.That(ControllerField<double>(rig.Controller, 0, "startPercent"), Is.EqualTo(0.0));
            Assert.That(ControllerField<float>(rig.Controller, 0, "cycleSpeed"), Is.EqualTo(1f));
            Color remembered = ControllerField<Color>(rig.Controller, 0, "startColor");
            Assert.That(remembered.r, Is.EqualTo(1f));
            Assert.That(remembered.g, Is.EqualTo(128f / 255f).Within(1e-6f));
            Assert.That(remembered.b, Is.EqualTo(0f));
        }

        // The emit point (OnParticleBorn 0x4ab010) and wrap (GetSplinePercent) pick the percent: Ending starts at 1 and the Default
        // wrap clamps (1 - 0.25) * 1 + 1 to the end; Loop keeps the fraction 0.75; the cycle range gives cycleSpeed.
        [Test]
        public void EmitPointAndWrapPickThePercent()
        {
            Rig rig = NewRig(NewLine());
            rig.Controller.motionType = ParticleController.MotionType.FollowForward;
            rig.Controller.emitPoint = ParticleController.EmitPoint.Ending;
            rig.Controller.minCycles = 2f;
            rig.Controller.maxCycles = 2f;
            Inject(rig.System, 0f, NewParticle(Vector3.zero, 2f, 0.5f, new Color32(255, 255, 255, 255)));
            Invoke(rig.Controller, "LateRun");
            Assert.That(ControllerField<double>(rig.Controller, 0, "startPercent"), Is.EqualTo(1.0));
            Assert.That(ControllerField<float>(rig.Controller, 0, "cycleSpeed"), Is.EqualTo(2f));
            Near(ReadParticles(rig.System)[0].position, new Vector3(10f, 0f, 10f), "clamped at the end: 0.75 * 2 + 1");

            Rig looping = NewRig(NewLine());
            looping.Controller.motionType = ParticleController.MotionType.FollowForward;
            looping.Controller.emitPoint = ParticleController.EmitPoint.Ending;
            looping.Controller.wrapMode = ParticleController.Wrap.Loop;
            Inject(looping.System, 0f, NewParticle(Vector3.zero, 2f, 0.5f, new Color32(255, 255, 255, 255)));
            Invoke(looping.Controller, "LateRun");
            Near(ReadParticles(looping.System)[0].position, new Vector3(10f, 0f, 5f), "looped: 0.75 + 1 wraps to 0.75");

            Rig backward = NewRig(NewLine());
            backward.Controller.motionType = ParticleController.MotionType.FollowBackward;
            Inject(backward.System, 0f, NewParticle(Vector3.zero, 2f, 0.5f, new Color32(255, 255, 255, 255)));
            Invoke(backward.Controller, "LateRun");
            Near(ReadParticles(backward.System)[0].position, new Vector3(5f, 0f, 0f), "backward: life left 0.75 gives percent 0.25");

            Rig random = NewRig(NewLine());
            random.Controller.motionType = ParticleController.MotionType.FollowForward;
            random.Controller.emitPoint = ParticleController.EmitPoint.Random;
            Inject(random.System, 0f, NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255)));
            Invoke(random.Controller, "LateRun");
            double start = ControllerField<double>(random.Controller, 0, "startPercent");
            Assert.That(start, Is.InRange(0.0, 1.0));
            Vector3 onLine = ReadParticles(random.System)[0].position;
            Near(onLine, start <= 0.5 ? new Vector3((float)(20.0 * start), 0f, 0f) : new Vector3(10f, 0f, (float)(20.0 * start - 10.0)), "at the random percent");
        }

        // The Ordered emit point: the birth counter over the expected number of live particles (emission rate (10, 10) -> 10, start
        // lifetime 2 -> 20) gives 1/20, 2/20, 3/20 for three particles born together; life left 1 puts each at its start percent.
        // Passing the expected number restarts the counter, and a zero number gives percent 0.
        [Test]
        public void OrderedEmissionCountsTheBirths()
        {
            Rig rig = NewRig(NewLine());
            rig.Controller.motionType = ParticleController.MotionType.FollowForward;
            rig.Controller.emitPoint = ParticleController.EmitPoint.Ordered;
            ParticleSystem.Particle[] born =
            {
                NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255)),
                NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255)),
                NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255))
            };
            Inject(rig.System, 0f, born);
            Invoke(rig.Controller, "LateRun");
            ParticleSystem.Particle[] particles = ReadParticles(rig.System);
            Near(particles[0].position, new Vector3(1f, 0f, 0f), "1 / 20");
            Near(particles[1].position, new Vector3(2f, 0f, 0f), "2 / 20");
            Near(particles[2].position, new Vector3(3f, 0f, 0f), "3 / 20");
            Assert.That(Read<int>(rig.Controller, "_birthIndex"), Is.EqualTo(3));

            Rig restarting = NewRig(NewLine());
            restarting.Controller.motionType = ParticleController.MotionType.FollowForward;
            restarting.Controller.emitPoint = ParticleController.EmitPoint.Ordered;
            ParticleSystem.EmissionModule emission = restarting.System.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0.01f, 0.01f);
            Inject(restarting.System, 0f, born);
            Invoke(restarting.Controller, "LateRun");
            foreach (ParticleSystem.Particle particle in ReadParticles(restarting.System))
            {
                Near(particle.position, Vector3.zero, "restarted at the beginning");
            }

            Assert.That(Read<int>(restarting.Controller, "_birthIndex"), Is.EqualTo(0));
        }

        // HandleParticle 0x4abe90: the offset field moves the particle along the sample's right and up (scaled by the sample
        // size), and apply3DRotation turns it to the sample's rotation in degrees. At percent 0: right (0,0,-1), up (0,1,0),
        // rotation looking along +x with up +y = (0,90,0).
        [Test]
        public void OffsetAndRotationFollowTheSample()
        {
            Rig rig = NewRig(NewLine());
            rig.Controller.motionType = ParticleController.MotionType.FollowForward;
            rig.Controller.offset = new Vector2(2f, 3f);
            rig.Controller.apply3DRotation = true;
            Inject(rig.System, 0f, NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255)));
            Invoke(rig.Controller, "LateRun");
            ParticleSystem.Particle particle = ReadParticles(rig.System)[0];
            Near(particle.position, new Vector3(0f, 3f, -2f), "right * 2 + up * 3");
            Near(particle.rotation3D, new Vector3(0f, 90f, 0f), "rotation in degrees");
        }

        // The motion types UseParticleSystem, ByNormal and ByNormalRandomized leave a particle to HandleParticle untouched (they
        // return at once); FollowForward and None go through.
        [Test]
        public void HandleParticleSkipsTheMotionsThatMoveByThemselves()
        {
            foreach (ParticleController.MotionType motion in new[]
            {
                ParticleController.MotionType.UseParticleSystem, ParticleController.MotionType.ByNormal, ParticleController.MotionType.ByNormalRandomized
            })
            {
                Rig rig = NewRig(NewLine());
                rig.Controller.motionType = motion;
                Inject(rig.System, 1f, NewParticle(new Vector3(7f, 8f, 9f), 2f, 1f, new Color32(255, 255, 255, 255)));
                Invoke(rig.Controller, "LateRun");
                Near(ReadParticles(rig.System)[0].position, new Vector3(7f, 8f, 9f), motion + " is left alone");
            }

            foreach (ParticleController.MotionType motion in new[] { ParticleController.MotionType.FollowForward, ParticleController.MotionType.None })
            {
                Rig rig = NewRig(NewLine());
                rig.Controller.motionType = motion;
                Inject(rig.System, 1f, NewParticle(new Vector3(7f, 8f, 9f), 2f, 2f, new Color32(255, 255, 255, 255)));
                Invoke(rig.Controller, "LateRun");
                Near(ReadParticles(rig.System)[0].position, Vector3.zero, motion + " places the particle on the spline");
            }
        }

        // A volumetric emitter gives the particle a start offset (OnParticleBorn 0x4ab010: a random point in the unit circle, or on
        // its rim with emitFromShell, halved; none otherwise) and always a random end offset in the halved circle. HandleParticle then
        // adds the start offset (life left 1) to the offset field: scaled by `scale` for the moving motions (FollowForward); None
        // adds the start offset unscaled. At percent 0 right is (0,0,-1) and up (0,1,0).
        [Test]
        public void VolumetricEmissionOffsetsTheParticle()
        {
            Rig plain = NewRig(NewLine());
            plain.Controller.motionType = ParticleController.MotionType.UseParticleSystem;
            Inject(plain.System, 0f, NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255)));
            Invoke(plain.Controller, "LateRun");
            Assert.That(ControllerField<Vector2>(plain.Controller, 0, "startOffset"), Is.EqualTo(Vector2.zero), "not volumetric");
            Assert.That(ControllerField<Vector2>(plain.Controller, 0, "endOffset").magnitude, Is.LessThanOrEqualTo(0.5f + 1e-5f));

            Rig shell = NewRig(NewLine());
            shell.Controller.motionType = ParticleController.MotionType.UseParticleSystem;
            shell.Controller.volumetric = true;
            shell.Controller.emitFromShell = true;
            shell.Controller.scale = new Vector2(2f, 3f);
            Inject(shell.System, 0f, NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255)));
            Invoke(shell.Controller, "LateRun");
            Vector2 rim = ControllerField<Vector2>(shell.Controller, 0, "startOffset");
            Assert.That(rim.magnitude, Is.EqualTo(0.5f).Within(1e-4f), "on the halved rim");
            Assert.That(ControllerField<Vector2>(shell.Controller, 0, "endOffset").magnitude, Is.LessThanOrEqualTo(0.5f + 1e-5f));
            // Spawn position: the sample position (0,0,0) plus Cross(forward, up) = +z scaled by start.x * scale.x and up scaled by
            // start.y * scale.y. UseParticleSystem leaves it there.
            Near(ReadParticles(shell.System)[0].position, new Vector3(0f, rim.y * 3f, rim.x * 2f), "spawn position");

            Rig inside = NewRig(NewLine());
            inside.Controller.motionType = ParticleController.MotionType.UseParticleSystem;
            inside.Controller.volumetric = true;
            Inject(inside.System, 0f, NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255)));
            Invoke(inside.Controller, "LateRun");
            Assert.That(ControllerField<Vector2>(inside.Controller, 0, "startOffset").magnitude, Is.LessThanOrEqualTo(0.5f + 1e-5f), "inside the halved circle");

            Rig moving = NewRig(NewLine());
            moving.Controller.motionType = ParticleController.MotionType.FollowForward;
            moving.Controller.volumetric = true;
            moving.Controller.emitFromShell = true;
            moving.Controller.scale = new Vector2(2f, 3f);
            Inject(moving.System, 0f, NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255)));
            Invoke(moving.Controller, "LateRun");
            Vector2 movingStart = ControllerField<Vector2>(moving.Controller, 0, "startOffset");
            Near(ReadParticles(moving.System)[0].position, new Vector3(0f, movingStart.y * 3f, -movingStart.x * 2f), "right * (x * 2) + up * (y * 3)");

            Rig none = NewRig(NewLine());
            none.Controller.motionType = ParticleController.MotionType.None;
            none.Controller.volumetric = true;
            none.Controller.emitFromShell = true;
            none.Controller.scale = new Vector2(2f, 3f);
            Inject(none.System, 0f, NewParticle(Vector3.zero, 2f, 2f, new Color32(255, 255, 255, 255)));
            Invoke(none.Controller, "LateRun");
            Vector2 noneStart = ControllerField<Vector2>(none.Controller, 0, "startOffset");
            Near(ReadParticles(none.System)[0].position, new Vector3(0f, noneStart.y, -noneStart.x), "None ignores the scale");
        }

        // The ByNormal motion (OnParticleBorn 0x4ab010): with start speed 4, force over lifetime (2,0,0) and a particle that has
        // lived 0.5 s, the sample up (0,1,0) moves it by up * 4 * 0.5 = (0,2,0), the force by 0.5 * force * 0.5^2 = (0.25,0,0), and
        // the velocity is up * 4 + force * 0.5 = (1,4,0). The particle starts from the sample position (0,0,0).
        [Test]
        public void ByNormalMotionUsesTheStartSpeedAndTheForce()
        {
            Rig rig = NewRig(NewLine());
            rig.Controller.motionType = ParticleController.MotionType.ByNormal;
            ParticleSystem.ForceOverLifetimeModule force = rig.System.forceOverLifetime;
            force.x = new ParticleSystem.MinMaxCurve(2f, 2f);
            force.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            force.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Inject(rig.System, 0f, NewParticle(new Vector3(30f, 30f, 30f), 2f, 1.5f, new Color32(255, 255, 255, 255)));
            Invoke(rig.Controller, "LateRun");
            ParticleSystem.Particle particle = ReadParticles(rig.System)[0];
            Near(particle.position, new Vector3(0.25f, 2f, 0f), "position");
            Near(particle.velocity, new Vector3(1f, 4f, 0f), "velocity");
        }

        // ByNormalRandomized turns up by a random angle around the sample's forward: whatever the angle, the direction stays
        // perpendicular to forward (0 along x here) with the start speed as length, so velocity - force * lived has length 4 and no
        // x part; a randomized force is drawn from the min and max of each axis.
        [Test]
        public void ByNormalRandomizedTurnsUpAroundForward()
        {
            Rig rig = NewRig(NewLine());
            rig.Controller.motionType = ParticleController.MotionType.ByNormalRandomized;
            ParticleSystem.ForceOverLifetimeModule force = rig.System.forceOverLifetime;
            force.x = new ParticleSystem.MinMaxCurve(2f, 2f);
            force.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            force.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Inject(rig.System, 0f, NewParticle(new Vector3(30f, 30f, 30f), 2f, 1.5f, new Color32(255, 255, 255, 255)));
            Invoke(rig.Controller, "LateRun");
            ParticleSystem.Particle particle = ReadParticles(rig.System)[0];
            Vector3 direction = particle.velocity - new Vector3(1f, 0f, 0f);
            Assert.That(direction.magnitude, Is.EqualTo(4f).Within(1e-3f), "start speed along the turned up");
            Assert.That(direction.x, Is.EqualTo(0f).Within(1e-3f), "perpendicular to forward");
            Near(particle.position, new Vector3(0.25f, 0f, 0f) + direction * 0.5f, "moved along the direction and by the force");

            Rig randomized = NewRig(NewLine());
            randomized.Controller.motionType = ParticleController.MotionType.ByNormal;
            ParticleSystem.ForceOverLifetimeModule spread = randomized.System.forceOverLifetime;
            spread.randomized = true;
            spread.x = new ParticleSystem.MinMaxCurve(1f, 3f);
            spread.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            spread.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Inject(randomized.System, 0f, NewParticle(Vector3.zero, 2f, 1.5f, new Color32(255, 255, 255, 255)));
            Invoke(randomized.Controller, "LateRun");
            float forceX = ReadParticles(randomized.System)[0].velocity.x / 0.5f;
            Assert.That(forceX, Is.InRange(1f - 1e-3f, 3f + 1e-3f), "x force drawn between min and max");
        }

        // A Local simulation space is converted to world around the handling (TransformParticle 0x4aafb0, InverseTransformParticle
        // 0x4ac3e0): the controller places the particle at the world sample (0,0,0), which is (-100,0,0) in the local space of a
        // system at (100,0,0). A velocity is rotated, not translated.
        [Test]
        public void LocalSimulationSpaceRoundTripsThroughWorldSpace()
        {
            Rig rig = NewRig(NewLine());
            ParticleSystem.MainModule main = rig.System.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            rig.Host.transform.position = new Vector3(100f, 0f, 0f);
            rig.Controller.motionType = ParticleController.MotionType.FollowForward;
            Inject(rig.System, 0f, NewParticle(new Vector3(5f, 5f, 5f), 2f, 2f, new Color32(255, 255, 255, 255)));
            Invoke(rig.Controller, "LateRun");
            Near(ReadParticles(rig.System)[0].position, new Vector3(-100f, 0f, 0f), "local position of the world sample");

            Rig still = NewRig(NewLine());
            ParticleSystem.MainModule stillMain = still.System.main;
            stillMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            still.Host.transform.position = new Vector3(100f, 0f, 0f);
            still.Host.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            still.Controller.motionType = ParticleController.MotionType.UseParticleSystem;
            ParticleSystem.Particle moving = NewParticle(new Vector3(1f, 2f, 3f), 2f, 1f, new Color32(255, 255, 255, 255));
            moving.velocity = new Vector3(0f, 0f, 5f);
            Inject(still.System, 1f, moving);
            Invoke(still.Controller, "LateRun");
            ParticleSystem.Particle after = ReadParticles(still.System)[0];
            Near(after.position, new Vector3(1f, 2f, 3f), "left alone in the local space");
            Near(after.velocity, new Vector3(0f, 0f, 5f), "so is the velocity");
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(ParticleController).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "_particleSystem", "_renderer", "pauseWhenNotVisible", "offset", "volumetric", "emitFromShell", "apply3DRotation", "scale",
                "emitPoint", "motionType", "wrapMode", "minCycles", "maxCycles", "_particles", "_controllers", "_particleCount",
                "_birthIndex", "_customParticleData"
            }));
            Assert.That(typeof(ParticleController).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "EmitPoint", "MotionType", "Wrap", "Particle" }));
            Assert.That(Enum.GetNames(typeof(ParticleController.EmitPoint)), Is.EqualTo(new[] { "Beginning", "Ending", "Random", "Ordered" }));
            Assert.That(Enum.GetNames(typeof(ParticleController.MotionType)),
                Is.EqualTo(new[] { "None", "UseParticleSystem", "FollowForward", "FollowBackward", "ByNormal", "ByNormalRandomized" }));
            Assert.That(Enum.GetNames(typeof(ParticleController.Wrap)), Is.EqualTo(new[] { "Default", "Loop" }));
            Assert.That(typeof(ParticleController.Particle).GetFields(flags).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "startOffset", "endOffset", "cycleSpeed", "startColor", "startPercent" }));
            Assert.That(typeof(ParticleController.Particle).GetField("startOffset", flags).IsAssembly, Is.True);
            Assert.That(typeof(ParticleController.Particle).GetMethod("GetSplinePercent", flags).IsAssembly, Is.True);
            Assert.That(typeof(ParticleController.Particle).GetCustomAttributes(typeof(SerializableAttribute), false).Length, Is.EqualTo(0));
            AddComponentMenu menu = (AddComponentMenu)typeof(ParticleController).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Particle Controller"));
            Assert.That(typeof(ParticleController).GetCustomAttributes(typeof(ExecuteInEditMode), false).Length, Is.EqualTo(1));
            FieldInfo system = typeof(ParticleController).GetField("_particleSystem", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(system.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1));
            Assert.That(system.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            foreach (string name in new[]
            {
                "pauseWhenNotVisible", "offset", "volumetric", "emitFromShell", "apply3DRotation", "scale", "emitPoint", "motionType",
                "wrapMode", "minCycles", "maxCycles"
            })
            {
                Assert.That(typeof(ParticleController).GetField(name).GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            Assert.That(typeof(ParticleController).GetField("_renderer", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(0));
        }
    }
}
