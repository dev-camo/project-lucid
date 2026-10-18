using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime FX helpers: PrefabSpawner, PlayParticleSystemOnEnable,
    // ShaderParameterBinderTarget, SceneMaterialManipulator, PrefabPoolActivation,
    // RateOfChangeBindable and the projectile spawners. ShaderProperties was restored with the
    // earlier FX helpers; only its published name is checked here.
    // Each expectation comes from the shipped x86-64 body named in its comment.
    // TimeScaledComponent descendants (RateOfChangeBindable, StraightLineProjectile)
    // are never instantiated here: their type initializer appends the permanent
    // 'Hardlight.TimeManager' row, so only their static and compiled forms are read.
    // Tests named "...Emits..." are emission-only checks of the restored IL, not
    // behaviour oracles; their comments mark which callees are inlined natively.
    // Open, without a behaviour test (both need a resolved TimeManager or
    // SystemConfiguration fixture): the DoTriggerStay fire predicate (a NaN
    // interval fires, equal does not, Fire then zeroes the timer; ucomiss/jae
    // 0x5810f6) and the RateOfChangeBindable.OnValidate bound correction (NaN
    // bounds stay, ucomiss/jb 0x6b35b4, constant -1.0f at 0x29a6df0); also
    // RateOfChangeBindable Awake/InternalUpdate/SetOutput/SetRateOfChange values,
    // StraightLineProjectileSpawner.Awake and the AnimateMaterial loop past time 0.
    public sealed class OriginalFxSpawnersAndShadersTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string HostName = "Lucid FX fixture";

        private static FieldInfo Field(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (Type current = target.GetType(); current != null; current = current.BaseType)
            {
                MethodInfo method = current.GetMethod(name, Instance | BindingFlags.DeclaredOnly);
                if (method == null) continue;
                try
                {
                    return method.Invoke(target, arguments);
                }
                catch (TargetInvocationException failure)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                }
            }
            throw new MissingMethodException(target.GetType().FullName, name);
        }

        private static string[] CallNames(Type type, string method) =>
            OriginalILCallSequence.Names(OriginalILCallSequence.Calls(type.GetMethod(method, Instance | BindingFlags.DeclaredOnly)));

        // The opcode pushed directly before the only call to callee in the method.
        private static OpCode ArgumentBefore(Type type, string method, string callee)
        {
            MethodInfo body = type.GetMethod(method, Instance | BindingFlags.DeclaredOnly);
            int callOffset = OriginalILCallSequence.Calls(body).Single(call => call.Target.Name == callee).Offset;
            List<(int Offset, OpCode Code)> instructions = OriginalILCallSequence.Instructions(body);
            return instructions[instructions.FindIndex(instruction => instruction.Offset == callOffset) - 1].Code;
        }

        // ShaderProperties..cctor 0x060024e9 (x86-64 0x59bab0) stores the
        // literal "_Dream_Noise_ST" (0x59badc) into the static readonly field;
        // metadata 0x04001882 is Public|Static|InitOnly, not a Literal constant.
        [Test]
        public void ShaderPropertiesPublishesTheDreamNoiseScaleOffsetName()
        {
            FieldInfo field = typeof(ShaderProperties).GetField(nameof(ShaderProperties.DreamNoiseST), Static);
            Assert.That(field.IsStatic && field.IsInitOnly && !field.IsLiteral, Is.True);
            Assert.That(ShaderProperties.DreamNoiseST, Is.EqualTo("_Dream_Noise_ST"));
        }

        // RateOfChangeBindable.LoopOutputWithinBounds 0x060039ef (x86-64
        // 0x6b3630): range = upper - lower; ucomiss lower, output / jbe takes
        // the low branch only for ordered output < lower and adds
        // ceil((lower - output) / range) * range (roundss 0xa); otherwise an
        // ordered output > upper subtracts ceil((output - upper) / range) * range.
        // A wrap by floor, Mathf.Repeat, or clamping each fails a row below: a
        // value on either bound stays, an exact multiple lands on the upper
        // bound, and a zero range turns an outside value into 0 * inf = NaN.
        // Both compares are unordered-skipping (jbe 0x6b363e, 0x6b365b), so a
        // NaN bound leaves an inside value alone; the negated forms
        // !(output >= lower) and !(output <= upper) would wrap it into NaN.
        [Test]
        public void LoopOutputWrapsOutsideValuesByCeilingMultiplesOfTheRange()
        {
            MethodInfo loop = typeof(RateOfChangeBindable).GetMethod("LoopOutputWithinBounds", Static | BindingFlags.DeclaredOnly);
            float Loop(float output, float lower, float upper) => (float)loop.Invoke(null, new object[] { output, lower, upper });

            Assert.That(Loop(3.25f, 2f, 5f), Is.EqualTo(3.25f), "inside");
            Assert.That(Loop(2f, 2f, 5f), Is.EqualTo(2f), "on the lower bound");
            Assert.That(Loop(5f, 2f, 5f), Is.EqualTo(5f), "on the upper bound");
            Assert.That(Loop(6.5f, 2f, 5f), Is.EqualTo(3.5f), "one partial range above");
            Assert.That(Loop(8f, 2f, 5f), Is.EqualTo(5f), "an exact range above lands on the upper bound");
            Assert.That(Loop(11f, 2f, 5f), Is.EqualTo(5f), "two ranges above");
            Assert.That(Loop(0.5f, 2f, 5f), Is.EqualTo(3.5f), "one partial range below");
            Assert.That(Loop(-4f, 2f, 5f), Is.EqualTo(2f), "an exact multiple below lands on the lower bound");
            Assert.That(float.IsNaN(Loop(float.NaN, 2f, 5f)), Is.True, "NaN passes through both unordered compares");
            Assert.That(float.IsNaN(Loop(2f, 1f, 1f)), Is.True, "a zero range divides by zero and multiplies infinity by zero");
            Assert.That(Loop(1f, 1f, 1f), Is.EqualTo(1f), "a zero range keeps its own bound");
            Assert.That(Loop(3f, float.NaN, 5f), Is.EqualTo(3f), "a NaN lower bound is unordered and skips the low wrap (jbe 0x6b363e)");
            Assert.That(Loop(3f, 2f, float.NaN), Is.EqualTo(3f), "a NaN upper bound is unordered and skips the high wrap (jbe 0x6b365b)");
        }

        // PrefabSpawner.Awake 0x060039e6 (x86-64 0x6b32c0) replaces the spawn
        // point only when UnityEngine.Object.op_Equality (0x6b3309) reports it
        // null, so a destroyed point is replaced too; an assigned one stays.
        [Test]
        public void PrefabSpawnerAwakeFallsBackToItsOwnTransformForAMissingPoint()
        {
            var host = new GameObject(HostName);
            var other = new GameObject(HostName + " point");
            var destroyed = new GameObject(HostName + " destroyed point");
            try
            {
                var unassigned = host.AddComponent<PrefabSpawner>();
                Invoke(unassigned, "Awake");
                Assert.That(Get(unassigned, "m_spawnPoint"), Is.SameAs(host.transform));

                var assigned = host.AddComponent<PrefabSpawner>();
                Set(assigned, "m_spawnPoint", other.transform);
                Invoke(assigned, "Awake");
                Assert.That(Get(assigned, "m_spawnPoint"), Is.SameAs(other.transform));

                var stale = host.AddComponent<PrefabSpawner>();
                Set(stale, "m_spawnPoint", destroyed.transform);
                Object.DestroyImmediate(destroyed);
                Invoke(stale, "Awake");
                Assert.That(Get(stale, "m_spawnPoint"), Is.SameAs(host.transform), "Unity null, not reference null");
            }
            finally
            {
                if (destroyed != null) Object.DestroyImmediate(destroyed);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(host);
            }
        }

        // PrefabSpawner.Spawn 0x060039e7 (x86-64 0x6b3340): for each prefab in
        // order, parented spawning calls Instantiate<GameObject>(original,
        // parent) (0x06000fa8, 0x6b33b0), which keeps the original's local
        // transform under the point; otherwise Instantiate(original, position,
        // rotation) (0x06000fa6, 0x6b3452) with the point's world pose and no parent.
        [Test]
        public void PrefabSpawnerParentsOrCopiesTheSpawnPointPoseForEveryPrefab()
        {
            var host = new GameObject(HostName);
            var point = new GameObject(HostName + " point");
            var first = new GameObject("LucidFxPrefabFirst");
            var second = new GameObject("LucidFxPrefabSecond");
            var clones = new List<GameObject>();
            try
            {
                first.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 0f, 30f));
                second.transform.position = new Vector3(-4f, 0f, 1f);
                point.transform.SetPositionAndRotation(new Vector3(10f, 0f, -2f), Quaternion.Euler(0f, 90f, 0f));
                var spawner = host.AddComponent<PrefabSpawner>();
                Set(spawner, "m_prefabsToSpawn", new[] { first, second });
                Set(spawner, "m_spawnPoint", point.transform);

                spawner.Spawn();
                GameObject[] Clones(string prefab) => Object.FindObjectsOfType<Transform>(true)
                    .Where(transform => transform.name == prefab + "(Clone)").Select(transform => transform.gameObject).ToArray();
                GameObject[] firstClones = Clones(first.name), secondClones = Clones(second.name);
                clones.AddRange(firstClones);
                clones.AddRange(secondClones);
                Assert.That(firstClones.Length, Is.EqualTo(1));
                Assert.That(secondClones.Length, Is.EqualTo(1));
                foreach (GameObject clone in clones)
                {
                    Assert.That(clone.transform.parent, Is.Null);
                    Assert.That(Vector3.Distance(clone.transform.position, point.transform.position), Is.LessThan(1e-5f));
                    Assert.That(Quaternion.Angle(clone.transform.rotation, point.transform.rotation), Is.LessThan(0.01f));
                }

                Set(spawner, "m_parentToSpawnPoint", true);
                spawner.Spawn();
                Assert.That(point.transform.childCount, Is.EqualTo(2));
                Transform parentedFirst = point.transform.GetChild(0), parentedSecond = point.transform.GetChild(1);
                clones.Add(parentedFirst.gameObject);
                clones.Add(parentedSecond.gameObject);
                Assert.That(parentedFirst.name, Is.EqualTo(first.name + "(Clone)"), "prefabs spawn in array order");
                Assert.That(Vector3.Distance(parentedFirst.localPosition, new Vector3(1f, 2f, 3f)), Is.LessThan(1e-5f));
                Assert.That(Quaternion.Angle(parentedFirst.localRotation, Quaternion.Euler(0f, 0f, 30f)), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(parentedSecond.localPosition, new Vector3(-4f, 0f, 1f)), Is.LessThan(1e-5f));
            }
            finally
            {
                foreach (GameObject clone in clones)
                    if (clone != null) Object.DestroyImmediate(clone);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(host);
            }
        }

        // PlayParticleSystemOnEnable.Reset 0x060039e4 (x86-64 0x6b3260) stores
        // GetComponent<ParticleSystem>(); OnEnable 0x060039e3 (0x6b3250) tail
        // calls ParticleSystem.Play() (0x06000051) on the field with no guard.
        [Test]
        public void ParticlePlayerFindsItsSystemOnResetAndPlaysItOnEnable()
        {
            var host = new GameObject(HostName);
            try
            {
                var player = host.AddComponent<PlayParticleSystemOnEnable>();
                Set(player, "m_particleSystem", null);
                Assert.Throws<NullReferenceException>(() => Invoke(player, "OnEnable"), "the field is used unguarded");

                var system = host.AddComponent<ParticleSystem>();
                Invoke(player, "Reset");
                Assert.That(Get(player, "m_particleSystem"), Is.SameAs(system));

                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                Assert.That(system.isPlaying, Is.False);
                Invoke(player, "OnEnable");
                Assert.That(system.isPlaying, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static Material NewUIMaterial()
        {
            Shader shader = Shader.Find("UI/Default");
            Assert.That(shader, Is.Not.Null, "A genuine available Unity UI shader is required.");
            return new Material(shader);
        }

        // ShaderParameterBinderTarget.Awake 0x06003a0b (x86-64 0x6b3f60): an
        // empty name returns first (IsNullOrEmpty 0x6b3f8f); a renderer source
        // (op_Inequality 0x6b3fbf) replaces the material with sharedMaterial
        // when m_instanceMaterial is false (0x6b3fdd); the id is resolved by
        // Shader.PropertyToID (0x6b401d) only when the material is not null.
        [Test]
        public void BinderAwakeTakesTheSourceMaterialAndResolvesOnlyANamedProperty()
        {
            var host = new GameObject(HostName);
            var rendered = new GameObject(HostName + " renderer");
            Material serialized = NewUIMaterial(), shared = NewUIMaterial();
            try
            {
                var renderer = rendered.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = shared;

                var unnamed = host.AddComponent<ShaderParameterBinderTarget>();
                Set(unnamed, "m_rendererAsMaterialSource", renderer);
                Set(unnamed, "m_rendererMaterial", serialized);
                Set(unnamed, "m_propertyName", "");
                Invoke(unnamed, "Awake");
                Assert.That(Get(unnamed, "m_rendererMaterial"), Is.SameAs(serialized), "an empty name returns before the source is read");
                Assert.That(Get(unnamed, "m_propertyId"), Is.EqualTo(0));

                var sourced = host.AddComponent<ShaderParameterBinderTarget>();
                Set(sourced, "m_rendererAsMaterialSource", renderer);
                Set(sourced, "m_instanceMaterial", false);
                Set(sourced, "m_rendererMaterial", serialized);
                Set(sourced, "m_propertyName", "_Stencil");
                Invoke(sourced, "Awake");
                Assert.That(Get(sourced, "m_rendererMaterial"), Is.SameAs(shared));
                Assert.That(Get(sourced, "m_propertyId"), Is.EqualTo(Shader.PropertyToID("_Stencil")));

                var direct = host.AddComponent<ShaderParameterBinderTarget>();
                Set(direct, "m_rendererMaterial", serialized);
                Set(direct, "m_propertyName", "_Color");
                Invoke(direct, "Awake");
                Assert.That(Get(direct, "m_rendererMaterial"), Is.SameAs(serialized));
                Assert.That(Get(direct, "m_propertyId"), Is.EqualTo(Shader.PropertyToID("_Color")));

                renderer.sharedMaterial = null;
                var missing = host.AddComponent<ShaderParameterBinderTarget>();
                Set(missing, "m_rendererAsMaterialSource", renderer);
                Set(missing, "m_rendererMaterial", serialized);
                Set(missing, "m_propertyName", "_Color");
                Invoke(missing, "Awake");
                Assert.That(Get(missing, "m_rendererMaterial"), Is.Null, "the null source material still replaces the serialized one");
                Assert.That(Get(missing, "m_propertyId"), Is.EqualTo(0), "no id without a material");
            }
            finally
            {
                Object.DestroyImmediate(rendered);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(shared);
                Object.DestroyImmediate(serialized);
            }
        }

        // ShaderParameterBinderTarget setters 0x06003a0c..0x06003a1b (x86-64
        // 0x6b4030..0x6b4224) each tail call the Material overload taking the
        // cached int id (+0x40), never the name (+0x38). The name here names a
        // different property, so a name-based setter fails.
        [Test]
        public void BinderSettersWriteThroughTheCachedPropertyId()
        {
            var host = new GameObject(HostName);
            Material material = NewUIMaterial();
            var texture = new Texture2D(2, 2);
            try
            {
                var binder = host.AddComponent<ShaderParameterBinderTarget>();
                Set(binder, "m_rendererMaterial", material);
                Set(binder, "m_propertyName", "_MainTex");

                Set(binder, "m_propertyId", Shader.PropertyToID("_Stencil"));
                binder.SetFloat(3.5f);
                Assert.That(material.GetFloat("_Stencil"), Is.EqualTo(3.5f));
                binder.SetInt(7);
                Assert.That(material.GetInt("_Stencil"), Is.EqualTo(7));

                Set(binder, "m_propertyId", Shader.PropertyToID("_Color"));
                var colour = new Color(0.25f, 0.5f, 0.75f, 1f);
                binder.SetColor(colour);
                Assert.That(material.GetColor("_Color"), Is.EqualTo(colour));
                binder.SetVector(new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
                Assert.That(material.GetVector("_Color"), Is.EqualTo(new Vector4(0.1f, 0.2f, 0.3f, 0.4f)));

                Set(binder, "m_propertyId", Shader.PropertyToID("_MainTex"));
                Assert.That(material.GetTexture("_MainTex"), Is.Not.SameAs(texture));
                binder.SetTexture(texture);
                Assert.That(material.GetTexture("_MainTex"), Is.SameAs(texture));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
            }
        }

        // ShaderParameterBinderTarget.Reset 0x06003a0a (x86-64 0x6b3e90)
        // returns when the material is already set (op_Inequality 0x6b3ee8),
        // and stores renderer.material only when GetComponent<Renderer>
        // finds one (0x6b3f1e). Neither path here reaches the Editor-leaking
        // renderer.material getter.
        [Test]
        public void BinderResetKeepsAnAssignedMaterialAndNeedsARendererOtherwise()
        {
            var host = new GameObject(HostName);
            var bare = new GameObject(HostName + " bare");
            Material material = NewUIMaterial();
            try
            {
                // The Editor sends Reset on AddComponent; the renderer is added
                // afterwards so that call cannot reach renderer.material.
                var assigned = host.AddComponent<ShaderParameterBinderTarget>();
                host.AddComponent<MeshRenderer>();
                Set(assigned, "m_rendererMaterial", material);
                Invoke(assigned, "Reset");
                Assert.That(Get(assigned, "m_rendererMaterial"), Is.SameAs(material));

                var unrendered = bare.AddComponent<ShaderParameterBinderTarget>();
                Set(unrendered, "m_rendererMaterial", null);
                Invoke(unrendered, "Reset");
                Assert.That(Get(unrendered, "m_rendererMaterial"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(bare);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // <AnimateMaterial>d__5.MoveNext 0x060024e5 (x86-64 0x59b8d0) starts
        // the timer at 0 and loops only while the last key's time is greater
        // (ucomiss/jbe 0x59b96d), so a curve ending at 0 sets no colour and
        // finishes at once; a '<=' loop would write Evaluate(0) first.
        [Test]
        public void MaterialAnimationEndingAtTimeZeroSetsNoColour()
        {
            var host = new GameObject(HostName);
            Material material = NewUIMaterial();
            try
            {
                var manipulator = host.AddComponent<SceneMaterialManipulator>();
                var before = new Color(0.1f, 0.2f, 0.3f, 0.4f);
                material.SetColor("_Color", before);
                var config = new SceneMaterialManipulator.MaterialColourConfig
                {
                    Multiplier = new AnimationCurve(new Keyframe(0f, 2f)),
                    Material = material,
                    ColourToSet = Color.white,
                    ColourName = "_Color",
                };
                var routine = (IEnumerator)Invoke(manipulator, "AnimateMaterial", config);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(material.GetColor("_Color"), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
            }
        }

        // SceneMaterialManipulator.Trigger 0x060024de (x86-64 0x59a320) skips
        // every config whose Trigger differs (cmp/jne 0x59a3cc) before touching
        // its routine, curve or material.
        [Test]
        public void MaterialTriggerTouchesOnlyMatchingConfigs()
        {
            var host = new GameObject(HostName);
            try
            {
                var manipulator = host.AddComponent<SceneMaterialManipulator>();
                MaterialManipulationTriggerType trigger = Enum.GetValues(typeof(MaterialManipulationTriggerType)).Cast<MaterialManipulationTriggerType>().First();
                var other = (MaterialManipulationTriggerType)unchecked((int)trigger + 1);
                var config = new SceneMaterialManipulator.MaterialColourConfig { Trigger = trigger };
                Set(manipulator, "m_materialColourConfigs", new[] { config });
                manipulator.Trigger(other);
                Assert.That(config.ManipulatingRoutine, Is.Null, "a non-matching config with no curve or material is never started");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Emission check. The matching path of Trigger 0x060024de stops the old
        // routine (SafeStopCoroutine 0x59a3e0), then jumps back to build the
        // iterator and start it (StartCoroutine 0x59a39c). AnimateMaterial is
        // inferred: natively it is inlined as the iterator allocation (0x59a35a).
        [Test]
        public void MaterialTriggerEmitsTheInferredCallOrder()
        {
            Assert.That(CallNames(typeof(SceneMaterialManipulator), "Trigger"), Is.EqualTo(new[]
            {
                "MonoBehaviourExtensions.SafeStopCoroutine",
                "SceneMaterialManipulator.AnimateMaterial",
                "MonoBehaviour.StartCoroutine",
            }));
        }

        private sealed class RecordingEntityPool : IEntityPrefabPool
        {
            public readonly List<(Transform transform, float scale, UpdateSpawnedObjectCallback callback)> Spawns =
                new List<(Transform, float, UpdateSpawnedObjectCallback)>();
            public PrefabPoolType ObjectPoolType { get; set; }
            public string name => "Lucid FX entity pool";
            public bool HasOverflowed => false;
            public int UsedObjects => 0;
            public int MaxObjects => 0;
            public int MaxUsedObjects => 0;
            public void SpawnInstance(Transform transform, float scaleMod = 1f, UpdateSpawnedObjectCallback callback = null) =>
                Spawns.Add((transform, scaleMod, callback));
            public PooledPrefab SpawnInstance(Transform parent) => throw new NotSupportedException("Only the entity overload is expected.");
            public void DespawnInstance(in PooledPrefab prefabInstance) => throw new NotSupportedException();
        }

        private sealed class PlainPool : IPrefabPool
        {
            public PrefabPoolType ObjectPoolType { get; set; }
            public string name => "Lucid FX plain pool";
            public bool HasOverflowed => false;
            public int UsedObjects => 0;
            public int MaxObjects => 0;
            public int MaxUsedObjects => 0;
            public PooledPrefab SpawnInstance(Transform parent) => throw new NotSupportedException();
            public void DespawnInstance(in PooledPrefab prefabInstance) => throw new NotSupportedException();
        }

        // PrefabPoolActivation.Activate 0x06003d09 (x86-64 0x6d21c0) skips each
        // transform that op_Equality (0x6d226e) reports null, destroyed ones
        // included, and calls interface slot 0 SpawnInstance with scale 1.0
        // (constant 0x29a6be0) and a null callback (edx 0, 0x6d223c).
        [Test]
        public void PoolActivationSpawnsAtEveryLiveTransformWithTheDefaults()
        {
            using var levelRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<LevelManager>());
            using var poolManagerRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<PrefabPoolManager>());
            var host = new GameObject(HostName);
            var first = new GameObject(HostName + " first");
            var second = new GameObject(HostName + " second");
            var destroyed = new GameObject(HostName + " destroyed");
            try
            {
                var activation = host.AddComponent<PrefabPoolActivation>();
                var pool = new RecordingEntityPool();
                Set(activation, "m_prefabPool", pool);
                Transform destroyedTransform = destroyed.transform;
                Object.DestroyImmediate(destroyed);
                Set(activation, "m_spawnTransforms", new[] { first.transform, null, destroyedTransform, second.transform });
                activation.Activate();
                Assert.That(pool.Spawns.Select(spawn => spawn.transform), Is.EqualTo(new[] { first.transform, second.transform }));
                Assert.That(pool.Spawns.Select(spawn => spawn.scale), Is.EqualTo(new[] { 1f, 1f }));
                Assert.That(pool.Spawns.All(spawn => spawn.callback == null), Is.True);
            }
            finally
            {
                if (destroyed != null) Object.DestroyImmediate(destroyed);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(host);
            }
        }

        // PrefabPoolActivation..ctor 0x06003d0a (x86-64 0x6d22f0) initializes
        // both readonly references with GetSystemRef<T>(null, true)
        // (0x6d2356, 0x6d237c); OnLevelActivated 0x06003d08 (0x6d2120) stores
        // GetPoolByType(...) as IEntityPrefabPool (IsInst 0x6d2186), so a plain
        // pool becomes null. OnDestroy 0x06003d07 (0x6d2070) uses TryGet
        // (0x6d20cb) and is silent without a level manager.
        [Test]
        public void PoolActivationResolvesOnlyAnEntityPoolAndToleratesNoLevelManager()
        {
            using var levelRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<LevelManager>());
            var poolManagerRow = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<PrefabPoolManager>());
            var host = new GameObject(HostName);
            PrefabPoolType entityKey = null, plainKey = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                var manager = host.AddComponent<PrefabPoolManager>();
                poolManagerRow.Register(manager);
                entityKey = ScriptableObject.CreateInstance<PrefabPoolType>();
                plainKey = ScriptableObject.CreateInstance<PrefabPoolType>();
                // The manager's dictionary keys hash and compare m_guid (HLUnityCore 0x06000e5f/e60); bare
                // CreateInstance keys share the empty guid, so the plain pool would replace the entity pool.
                Set(entityKey, "m_guid", "lucid-pool-activation-entity");
                Set(plainKey, "m_guid", "lucid-pool-activation-plain");
                var entityPool = new RecordingEntityPool { ObjectPoolType = entityKey };
                manager.RegisterPool(entityPool);
                manager.RegisterPool(new PlainPool { ObjectPoolType = plainKey });

                var activation = host.AddComponent<PrefabPoolActivation>();
                var levelRef = (SystemRef<LevelManager>)Get(activation, "m_levelManagerRef");
                var poolRef = (SystemRef<PrefabPoolManager>)Get(activation, "m_prefabPoolManagerRef");
                Assert.That(levelRef.SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<LevelManager>()));
                Assert.That(poolRef.Get(), Is.SameAs(manager));

                Set(activation, "m_prefabPoolType", entityKey);
                Invoke(activation, "OnLevelActivated", new object[] { null });
                Assert.That(Get(activation, "m_prefabPool"), Is.SameAs(entityPool));

                Set(activation, "m_prefabPoolType", plainKey);
                Invoke(activation, "OnLevelActivated", new object[] { null });
                Assert.That(Get(activation, "m_prefabPool"), Is.Null, "a non-entity pool is cast to null");

                Assert.That(levelRef.IsNull(), Is.True);
                Assert.DoesNotThrow(() => Invoke(activation, "OnDestroy"));
            },
            () => Object.DestroyImmediate(host),
            () => { if (entityKey != null) Object.DestroyImmediate(entityKey); },
            () => { if (plainKey != null) Object.DestroyImmediate(plainKey); },
            poolManagerRow.Dispose);
        }

        // PrefabPoolActivation.Awake 0x06003d06 (x86-64 0x6d1fd0) calls
        // SystemRef<LevelManager>.Get (0x6d201f) and only then builds the
        // OnLevelActivated delegate (0x6d204b), so with no level manager the Get
        // fault comes first and nothing is queued on the reference, as
        // InvokeOnValid would have done.
        [Test]
        public void PoolActivationAwakeFailsWithoutALevelManagerAndQueuesNothing()
        {
            using var levelRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<LevelManager>());
            using var poolManagerRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<PrefabPoolManager>());
            var host = new GameObject(HostName);
            try
            {
                var activation = host.AddComponent<PrefabPoolActivation>();
                var levelRef = (SystemRef<LevelManager>)Get(activation, "m_levelManagerRef");
                Assert.That(levelRef.IsNull(), Is.True);
                var missing = Assert.Throws<NullReferenceException>(() => Invoke(activation, "Awake"));
                Assert.That(missing.Message, Is.EqualTo("System '" + ProcessManager.GetDefaultName<LevelManager>() + "' is null"));
                Assert.That(Get(levelRef, "m_actionOnSystemValid"), Is.Null, "no deferred level callback");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static StraightLineProjectileSpawner NewSpawner(GameObject host)
        {
            host.AddComponent<BoxCollider>();
            return host.AddComponent<StraightLineProjectileSpawner>();
        }

        private static Array Configs(params (int count, ProjectileType type)[] rows)
        {
            Type configType = typeof(ProjectileSpawnerBase).GetNestedType("ProjectilesAndCount", BindingFlags.NonPublic);
            Array configs = Array.CreateInstance(configType, rows.Length);
            for (int i = 0; i < rows.Length; ++i)
            {
                object config = Activator.CreateInstance(configType);
                configType.GetField("ProjectileCount").SetValue(config, rows[i].count);
                configType.GetField("ProjectileType").SetValue(config, rows[i].type);
                configs.SetValue(config, i);
            }
            return configs;
        }

        // ProjectileSpawnerBase..ctor 0x060022f6 (x86-64 0x581290) stores
        // m_updateTimeCategory 0xcc1de769 (TimeCategory.EnemyMovement,
        // 0x5812e6), a new List<Projectile>, then GetSystemRef<TimeManager>
        // and GetSystemRef<GameplayIslandManager> (null, true) before the
        // CharacterTrigger constructor.
        [Test]
        public void SpawnerConstructorDefaultsToEnemyMovementTimeAndBothReferences()
        {
            using var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>());
            using var islandRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<GameplayIslandManager>());
            var host = new GameObject(HostName);
            try
            {
                StraightLineProjectileSpawner spawner = NewSpawner(host);
                Assert.That(Get(spawner, "m_updateTimeCategory"), Is.EqualTo(TimeCategory.EnemyMovement));
                Assert.That((List<Projectile>)Get(spawner, "m_firedProjectiles"), Is.Empty);
                Assert.That(((SystemRef<TimeManager>)Get(spawner, "m_timeManagerRef")).SystemName(),
                    Is.EqualTo(ProcessManager.GetDefaultName<TimeManager>()));
                Assert.That(((SystemRef<GameplayIslandManager>)Get(spawner, "m_gameplayIslandManagerRef")).SystemName(),
                    Is.EqualTo(ProcessManager.GetDefaultName<GameplayIslandManager>()));
                Assert.That(Get(spawner, "m_fireOnStayTimer"), Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ProjectileSpawnerBase.DoTriggerEnter 0x060022f1 (x86-64 0x581060)
        // zeroes m_stayTimer (0x581070) before the virtual Fire (slot 12,
        // +0x1f8), which runs only with m_fireImmediatelyOnEntry. The spawner's
        // Fire reaches ProjectileManager.AcquireProjectile for the configured
        // type, whose pool lookup fails on a manager with no pools.
        [Test]
        public void TriggerEntryResetsTheStayTimerThenFiresOnlyWhenConfigured()
        {
            using var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>());
            using var islandRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<GameplayIslandManager>());
            var host = new GameObject(HostName);
            try
            {
                StraightLineProjectileSpawner spawner = NewSpawner(host);
                var manager = host.AddComponent<ProjectileManager>();
                Set(spawner, "m_projectileManager", manager);
                Set(spawner, "m_projectileConfigs", Configs((1, ProjectileType.BuzzBomberProjectile)));

                Set(spawner, "m_stayTimer", 7f);
                Set(spawner, "m_fireImmediatelyOnEntry", false);
                Assert.DoesNotThrow(() => Invoke(spawner, "DoTriggerEnter", new object[] { null }));
                Assert.That(Get(spawner, "m_stayTimer"), Is.EqualTo(0f));

                Set(spawner, "m_stayTimer", 7f);
                Set(spawner, "m_fireImmediatelyOnEntry", true);
                Assert.Throws<KeyNotFoundException>(() => Invoke(spawner, "DoTriggerEnter", new object[] { null }),
                    "Fire acquires the configured projectile type");
                Assert.That(Get(spawner, "m_stayTimer"), Is.EqualTo(0f), "the timer is zeroed before Fire");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ProjectileSpawnerBase.DoTriggerStay 0x060022f2 (x86-64 0x5810b0)
        // returns after the base call when m_fireOnStay is false (0x5810c4),
        // before reading the time manager or the timer.
        [Test]
        public void TriggerStayWithoutFireOnStayLeavesTheTimer()
        {
            using var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>());
            using var islandRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<GameplayIslandManager>());
            var host = new GameObject(HostName);
            try
            {
                StraightLineProjectileSpawner spawner = NewSpawner(host);
                Set(spawner, "m_fireOnStay", false);
                Set(spawner, "m_stayTimer", 5f);
                Assert.DoesNotThrow(() => Invoke(spawner, "DoTriggerStay", new object[] { null }), "no time manager is read");
                Assert.That(Get(spawner, "m_stayTimer"), Is.EqualTo(5f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // GetPosition 0x060022f4 (x86-64 0x581220) returns transform.position;
        // IsDestroyed 0x060022f5 (0x581240) is op_Equality(this, null), so it
        // turns true once the component is destroyed and the reference is kept.
        [Test]
        public void ResetElementReportsItsPositionAndUnityDestruction()
        {
            using var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>());
            using var islandRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<GameplayIslandManager>());
            var host = new GameObject(HostName);
            try
            {
                StraightLineProjectileSpawner spawner = NewSpawner(host);
                IResetCapableGameplayElement element = spawner;
                host.transform.position = new Vector3(4f, -5f, 6f);
                Assert.That(element.GetPosition(), Is.EqualTo(new Vector3(4f, -5f, 6f)));
                Assert.That(element.IsDestroyed(), Is.False);
                Object.DestroyImmediate(spawner);
                Assert.That(element.IsDestroyed(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // Emission check. ResetGameplayElement 0x060022f3 (x86-64 0x581120)
        // inlines ProjectileManager.ForceCoolDownOfType, so the callee is
        // inferred: m_activeProjectiles (+0x38) in reverse, ProjectileType
        // compare, virtual ForceEnterCoolDown (slot 39) with esi 0 (0x5811fc).
        // Kill is non-virtual, so KillAllOfType would differ. The argument
        // pushed before the call must be false (ldc.i4.0), not true.
        [Test]
        public void ResetElementEmitsACoolDownWithoutExceptions()
        {
            Assert.That(CallNames(typeof(ProjectileSpawnerBase), "ResetGameplayElement"),
                Is.EqualTo(new[] { "ProjectileManager.ForceCoolDownOfType" }));
            Assert.That(ArgumentBefore(typeof(ProjectileSpawnerBase), "ResetGameplayElement", "ForceCoolDownOfType"),
                Is.EqualTo(OpCodes.Ldc_I4_0));
        }

        // Emission check. ProjectileSpawnerBase.Awake 0x060022ef (x86-64
        // 0x580f00) calls CharacterTrigger.Awake (0x580f82), builds the time
        // manager delegate and calls InvokeOnValid (0x580fc4), then
        // GetSystem<ProjectileManager> (0x580ff2), then the island delegate and
        // the tail InvokeOnValid (0x581051). Every entry is a native call.
        [Test]
        public void SpawnerAwakeEmitsTheTimeRequestBeforeTheManagerFetch()
        {
            Assert.That(CallNames(typeof(ProjectileSpawnerBase), "Awake"), Is.EqualTo(new[]
            {
                "CharacterTrigger.Awake",
                "Action`1..ctor",
                "SystemRef`1.InvokeOnValid",
                "ProcessManager.GetSystem",
                "Action`1..ctor",
                "SystemRef`1.InvokeOnValid",
            }));
        }

        // Emission check. StraightLineProjectileSpawner.Fire 0x0600230f (x86-64
        // 0x5830d0): the projectile's transform (0x583282) takes
        // m_transform.position (0x583293, set 0x58329d); the focus is a fresh
        // position (0x5832ab) plus forward (0x5832c2) times 100; the virtual
        // Fire follows (slot 33, 0x5833f3). The other branch formats the
        // projectile and GetString of its type (0x58323d, 0x583254) for
        // HLOutput.LogError with a null context (0x583187). Inferred, because
        // natively inlined: AcquireProjectile (dictionary get_Item 0x5831ce and
        // ObjectPool.Get 0x5831e0), the Vector3 operators (scalar SSE), Setup
        // (0x58336b..0x5833e0) and the ProjectileType getter (a field read).
        [Test]
        public void StraightLineFireEmitsTheInferredCallOrder()
        {
            Assert.That(CallNames(typeof(StraightLineProjectileSpawner), "Fire"), Is.EqualTo(new[]
            {
                "ProjectileManager.AcquireProjectile",
                "Component.get_transform",
                "Transform.get_position",
                "Transform.set_position",
                "Transform.get_position",
                "Transform.get_forward",
                "Vector3.op_Multiply",
                "Vector3.op_Addition",
                "Projectile.Setup",
                "Projectile.Fire",
                "Projectile.get_ProjectileType",
                "HardlightEnumExtensions.GetString",
                "String.Format",
                "HLOutput.LogError",
            }));
        }

        // Emission and metadata check. StraightLineProjectileSpawner.Fire scales
        // forward by 100 (mulss 100.0f at 0x29a70cc, mulps (100,100,0,0) at
        // 0x29a79a0; 0x5832c7, 0x5832cf). Metadata 0x04001753 Target_Distance is
        // Private|Static|Literal|HasDefault = 100.0f, so C# folds it into an
        // ldc.r4 pushed directly before the one Vector3 multiply.
        [Test]
        public void StraightLineFireEmitsTheHundredUnitAimDistance()
        {
            FieldInfo distance = typeof(StraightLineProjectileSpawner).GetField("Target_Distance", Static | BindingFlags.DeclaredOnly);
            Assert.That(distance.IsLiteral && distance.IsPrivate, Is.True);
            Assert.That(distance.GetRawConstantValue(), Is.EqualTo(100f));

            MethodInfo fire = typeof(StraightLineProjectileSpawner).GetMethod("Fire", Instance | BindingFlags.DeclaredOnly);
            int multiply = OriginalILCallSequence.Calls(fire).Single(call => call.Target.Name == "op_Multiply").Offset;
            List<(int Offset, OpCode Code)> instructions = OriginalILCallSequence.Instructions(fire);
            (int Offset, OpCode Code) scale = instructions[instructions.FindIndex(instruction => instruction.Offset == multiply) - 1];
            Assert.That(scale.Code, Is.EqualTo(OpCodes.Ldc_R4));
            Assert.That(OriginalILCallSequence.SingleOperand(fire, scale.Offset), Is.EqualTo(100f));
        }

        // Emission check. StraightLineProjectile.DoMovement 0x0600230a (x86-64
        // 0x582fb0) calls Transform.Translate(Vector3, Space) with esi 0
        // (Space.World, 0x582fe9); UpdateFocus 0x06002309 (0x582e70) reads
        // transform once (0x582e89) for get_position (0x582e96) and the tail
        // set_rotation, and passes the direction to the one-argument
        // LookRotation (0x582f99). The vector arithmetic, the normalization
        // (sqrt, 1e-5 compare and Vector3.zero fallback, 0x582f09..0x582f6d) and
        // the definition's Velocity are inlined, so their source form is
        // unknown and they are left out of the compared lists.
        [Test]
        public void StraightLineProjectileEmitsWorldMovementAndASingleTransformFocus()
        {
            string[] NativeCalls(string method) => CallNames(typeof(StraightLineProjectile), method)
                .Where(name => !name.StartsWith("Vector3.", StringComparison.Ordinal)
                    && !name.StartsWith("Mathf.", StringComparison.Ordinal)
                    && !name.StartsWith("ProjectileDefinition.", StringComparison.Ordinal))
                .ToArray();

            Assert.That(NativeCalls("DoMovement"), Is.EqualTo(new[] { "Component.get_transform", "Transform.Translate" }));
            MethodBase translate = OriginalILCallSequence.Calls(typeof(StraightLineProjectile).GetMethod("DoMovement", Instance | BindingFlags.DeclaredOnly))
                .Single(call => call.Target.Name == "Translate").Target;
            Assert.That(translate.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(Vector3), typeof(Space) }));
            Assert.That(ArgumentBefore(typeof(StraightLineProjectile), "DoMovement", "Translate"), Is.EqualTo(OpCodes.Ldc_I4_0), "Space.World");
            Assert.That(NativeCalls("UpdateFocus"), Is.EqualTo(new[]
            {
                "Component.get_transform",
                "Transform.get_position",
                "Quaternion.LookRotation",
                "Transform.set_rotation",
            }));
            MethodBase lookRotation = OriginalILCallSequence.Calls(typeof(StraightLineProjectile).GetMethod("UpdateFocus", Instance | BindingFlags.DeclaredOnly))
                .Single(call => call.Target.Name == "LookRotation").Target;
            Assert.That(lookRotation.GetParameters().Length, Is.EqualTo(1));
        }

        // Emission check. RateOfChangeBindable.Awake 0x060039e9 (x86-64
        // 0x6b3490) calls the TimeScaledComponent_SDT Awake (0x6b349b) and then
        // writes the wrapped start value through the virtual Value setter (tail
        // jump 0x6b3516); InternalUpdate 0x060039eb (0x6b3680) reads the virtual
        // Value getter (slot 8, 0x6b369c) before the same setter (0x6b371a).
        // SetOutputWithConstraint and LoopOutputWithinBounds are inlined in
        // both, so those callees are inferred.
        [Test]
        public void RateOfChangeBindableEmitsTheInferredOutputCalls()
        {
            Assert.That(CallNames(typeof(RateOfChangeBindable), "Awake"),
                Is.EqualTo(new[] { "TimeScaledComponent_SDT.Awake", "RateOfChangeBindable.SetOutputWithConstraint" }));
            Assert.That(CallNames(typeof(RateOfChangeBindable), "InternalUpdate"),
                Is.EqualTo(new[] { "Bindable`1.get_Value", "RateOfChangeBindable.SetOutputWithConstraint" }));
            Assert.That(CallNames(typeof(RateOfChangeBindable), "SetOutputWithConstraint"),
                Is.EqualTo(new[] { "RateOfChangeBindable.LoopOutputWithinBounds", "Bindable`1.set_Value" }));
        }

        // Emission check against the original metadata names: ProjectileSpawnerBase
        // 060022f7/060022f8 are <Awake>b__12_0(TimeManager) and
        // <Awake>b__12_1(GameplayIslandManager), and SceneMaterialManipulator's
        // iterator is 020006cd <AnimateMaterial>d__5. Both ordinals depend on
        // the nested type being declared before the fields.
        [Test]
        public void CompilerGeneratedNamesMatchTheOriginalMemberOrdinals()
        {
            MethodInfo timeLambda = typeof(ProjectileSpawnerBase).GetMethod("<Awake>b__12_0", Instance | BindingFlags.DeclaredOnly);
            MethodInfo islandLambda = typeof(ProjectileSpawnerBase).GetMethod("<Awake>b__12_1", Instance | BindingFlags.DeclaredOnly);
            Assert.That(timeLambda?.GetParameters().Single().ParameterType, Is.EqualTo(typeof(TimeManager)));
            Assert.That(islandLambda?.GetParameters().Single().ParameterType, Is.EqualTo(typeof(GameplayIslandManager)));
            Assert.That(typeof(SceneMaterialManipulator).GetNestedType("<AnimateMaterial>d__5", BindingFlags.NonPublic), Is.Not.Null);
        }
    }
}
