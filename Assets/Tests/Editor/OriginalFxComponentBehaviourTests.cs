using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of the smaller restored fx types against their shipped x86-64
    // bodies: FlashRenderers, MaterialPropertyBlockSetter, LerpColour,
    // LineRendererContainer, CanvasShaderModifier, ShaderProperties,
    // PFXQualitySwitcher, BlobShadowModifier, AssignAddressableTexture and
    // InstancedObjectSpawner. Every case owns and destroys its objects; the
    // FlashRenderers instance is raw because a real TimeScaledComponent's
    // editor Reset/OnValidate reaches SystemConfiguration, and only its own
    // fields are read here. Paths that need the time manager, Addressables or
    // a running coroutine (FlashRenderers.OnEnable, AssignAddressableTexture
    // .OnEnable, LerpColour's restart branch) are not exercised.
    [TestFixture]
    public sealed class OriginalFxComponentBehaviourTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        sealed class Owned : IDisposable
        {
            readonly List<Object> objects = new List<Object>();

            public GameObject Inactive(string name)
            {
                var owner = new GameObject(name);
                owner.SetActive(false);
                objects.Add(owner);
                return owner;
            }

            public T Keep<T>(T value) where T : Object
            {
                objects.Add(value);
                return value;
            }

            public void Dispose()
            {
                for (int i = objects.Count - 1; i >= 0; i--)
                    if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            }
        }

        static FieldInfo FindField(Type type, string field)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo info = t.GetField(field, Instance | BindingFlags.DeclaredOnly);
                if (info != null) return info;
            }
            throw new MissingFieldException(type.Name, field);
        }
        static object Get(object target, string field) => FindField(target.GetType(), field).GetValue(target);
        static void Set(object target, string field, object value) => FindField(target.GetType(), field).SetValue(target, value);
        static object Call(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Instance).Invoke(target, arguments);

        // ---- FlashRenderers ----

        static FlashRenderers RawFlash(Renderer[] renderers, float initialDelay, float interval, float next)
        {
            var flash = (FlashRenderers)FormatterServices.GetUninitializedObject(typeof(FlashRenderers));
            Set(flash, "m_renderers", renderers);
            Set(flash, "m_initialDelay", initialDelay);
            Set(flash, "m_flashInterval", interval);
            Set(flash, "m_nextFlashIntervalSeconds", next);
            return flash;
        }

        static void Update(FlashRenderers flash, float deltaTime) =>
            typeof(FlashRenderers).GetMethod("InternalUpdate", Instance).Invoke(flash, new object[] { deltaTime });

        // InternalUpdate 0x6b0a50: timer += delta; return while
        // initialDelay > timer or next > timer (ucomiss/ja); otherwise invert
        // every renderer's enabled and add the interval to next. The
        // OnEnable 0x6b0a20 schedule (next = delay + interval) is set directly.
        [Test]
        public void FlashRenderersInvertEveryRendererOncePerInterval()
        {
            using (var owned = new Owned())
            {
                Renderer on = owned.Inactive("Lucid owned flash a").AddComponent<MeshRenderer>();
                Renderer off = owned.Inactive("Lucid owned flash b").AddComponent<MeshRenderer>();
                off.enabled = false;
                FlashRenderers flash = RawFlash(new[] { on, off }, 1f, 0.5f, 1.5f);

                Update(flash, 1f);
                Assert.That((on.enabled, off.enabled), Is.EqualTo((true, false)), "1.0 < next 1.5");
                Update(flash, 0.5f);
                Assert.That((on.enabled, off.enabled), Is.EqualTo((false, true)), "1.5 reaches next");
                Assert.That(Get(flash, "m_nextFlashIntervalSeconds"), Is.EqualTo(2f));
                Update(flash, 0.25f);
                Assert.That((on.enabled, off.enabled), Is.EqualTo((false, true)));
                Update(flash, 0.25f);
                Assert.That((on.enabled, off.enabled), Is.EqualTo((true, false)));
                Assert.That(Get(flash, "m_timerSeconds"), Is.EqualTo(2f));
                Assert.That(Get(flash, "m_nextFlashIntervalSeconds"), Is.EqualTo(2.5f));

                // The initial delay is its own gate: next = 0 does not flash before it.
                FlashRenderers delayed = RawFlash(new[] { on }, 5f, 1f, 0f);
                Update(delayed, 4f);
                Assert.That(on.enabled, Is.True);
                Assert.That(Get(delayed, "m_nextFlashIntervalSeconds"), Is.EqualTo(0f));
                Update(delayed, 1f);
                Assert.That(on.enabled, Is.False);
                Assert.That(Get(delayed, "m_nextFlashIntervalSeconds"), Is.EqualTo(1f));
            }
        }

        // Both early returns are ordered greater-than tests, so a NaN timer
        // passes them and flashes; a ">= then flash" reading would not.
        [Test]
        public void FlashRenderersFlashOnANaNTimer()
        {
            using (var owned = new Owned())
            {
                Renderer renderer = owned.Inactive("Lucid owned flash nan").AddComponent<MeshRenderer>();
                FlashRenderers flash = RawFlash(new[] { renderer }, 1f, 0.5f, 1.5f);
                Update(flash, float.NaN);
                Assert.That(renderer.enabled, Is.False);
                Assert.That(Get(flash, "m_nextFlashIntervalSeconds"), Is.EqualTo(2f));
            }
        }

        // Action_EnableAllRenderers 0x6b0ae0 stores true to every renderer.
        [Test]
        public void FlashRenderersEnableAllRenderersEnablesEach()
        {
            using (var owned = new Owned())
            {
                Renderer a = owned.Inactive("Lucid owned enable a").AddComponent<MeshRenderer>();
                Renderer b = owned.Inactive("Lucid owned enable b").AddComponent<MeshRenderer>();
                b.enabled = false;
                RawFlash(new[] { a, b }, 0f, 0f, 0f).Action_EnableAllRenderers();
                Assert.That((a.enabled, b.enabled), Is.EqualTo((true, true)));
            }
        }

        // ---- MaterialPropertyBlockSetter ----

        // .ctor 0x6b2500: "_Color" and the 16-byte constant (1,1,1,1) at
        // 0x29a70b0. Awake 0x6b2490: a new block, SetColor(name, colour),
        // Renderer.SetPropertyBlock, so an existing block is replaced.
        [Test]
        public void MaterialPropertyBlockSetterReplacesTheRendererBlock()
        {
            using (var owned = new Owned())
            {
                GameObject owner = owned.Inactive("Lucid owned property block");
                var renderer = owner.AddComponent<MeshRenderer>();
                var setter = owner.AddComponent<MaterialPropertyBlockSetter>();
                Assert.That(Get(setter, "m_colourPropertyName"), Is.EqualTo("_Color"));
                Assert.That(Get(setter, "m_colorToSet"), Is.EqualTo(new Color(1f, 1f, 1f, 1f)));

                var existing = new MaterialPropertyBlock();
                existing.SetFloat("_Other", 3f);
                renderer.SetPropertyBlock(existing);
                Set(setter, "m_renderer", renderer);
                Set(setter, "m_colourPropertyName", "_Tint");
                Set(setter, "m_colorToSet", new Color(0.25f, 0.5f, 0.75f, 1f));
                Call(setter, "Awake");

                var applied = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(applied);
                Assert.That(applied.GetColor("_Tint"), Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)));
                Assert.That(applied.HasFloat("_Other"), Is.False, "the earlier block is not merged");
            }
        }

        // ---- LerpColour ----

        // .ctor 0x6b2180: m_lerpSpeedMultiplier = 1f, then a new Bindable<Color>.
        // SetTargetColour 0x6b1f60 first returns when the squared distance to
        // Colour.Value is below 0x29a6e8c (9.99999944e-11, Color ==), then the
        // first-value branch stores the target directly.
        [Test]
        public void LerpColourIgnoresNearTargetsAndSetsTheFirstValueDirectly()
        {
            using (var owned = new Owned())
            {
                var lerp = owned.Inactive("Lucid owned lerp colour").AddComponent<LerpColour>();
                Assert.That(Get(lerp, "m_lerpSpeedMultiplier"), Is.EqualTo(1f));
                Assert.That(lerp.Colour, Is.Not.Null);
                Assert.That(lerp.Colour.Value, Is.EqualTo(new Color(0f, 0f, 0f, 0f)));
                Set(lerp, "m_setFirstValue", true);

                lerp.SetTargetColour(new Color(0f, 0f, 0f, 9e-6f));
                Assert.That(Get(lerp, "m_valueAssigned"), Is.False, "8.1e-11 is within the epsilon");
                Assert.That(lerp.Colour.Value, Is.EqualTo(new Color(0f, 0f, 0f, 0f)));

                var target = new Color(0f, 0f, 0f, 1.1e-5f);
                lerp.SetTargetColour(target);
                Assert.That(Get(lerp, "m_valueAssigned"), Is.True, "1.21e-10 is outside it");
                Assert.That(lerp.Colour.Value, Is.EqualTo(target));
                Assert.That(Get(lerp, "m_lerpRoutine"), Is.Null, "no coroutine for the first value");
            }
        }

        // <LerpStartColour>d__6.MoveNext 0x6b2210: lerp = 0 then the body;
        // later states continue only while 1f > lerp (jbe exits, NaN too).
        // The body adds deltaTime * multiplier, writes Color.Lerp with the
        // clamped t (minss 1 / t < 0 mask) and yields null; the exit writes
        // the target. A zero multiplier keeps deltaTime out of the values.
        [Test]
        public void LerpColourIteratorClampsAndEndsOnTheTarget()
        {
            using (var owned = new Owned())
            {
                var lerp = owned.Inactive("Lucid owned lerp routine").AddComponent<LerpColour>();
                Set(lerp, "m_lerpSpeedMultiplier", 0f);
                var start = new Color(0f, 0f, 0f, 0f);
                var target = new Color(1f, 0.5f, 0.25f, 1f);
                var routine = (IEnumerator)Call(lerp, "LerpStartColour", start, target);
                FieldInfo lerpValue = routine.GetType().GetField("<lerpValue>5__2", Instance);

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.Null);
                Assert.That(lerp.Colour.Value, Is.EqualTo(start));

                lerpValue.SetValue(routine, 0.5f);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(lerp.Colour.Value, Is.EqualTo(new Color(0.5f, 0.25f, 0.125f, 0.5f)));

                lerpValue.SetValue(routine, -2f);
                Assert.That(routine.MoveNext(), Is.True, "-2 < 1 keeps looping");
                Assert.That(lerp.Colour.Value, Is.EqualTo(start), "t clamps to 0");

                lerpValue.SetValue(routine, 1f);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(lerp.Colour.Value, Is.EqualTo(target));
                Assert.That(routine.MoveNext(), Is.False);

                var nan = (IEnumerator)Call(lerp, "LerpStartColour", target, start);
                Assert.That(nan.MoveNext(), Is.True);
                lerpValue.SetValue(nan, float.NaN);
                Assert.That(nan.MoveNext(), Is.False, "a NaN lerp value ends the loop");
                Assert.That(lerp.Colour.Value, Is.EqualTo(start));
                Assert.Throws<NotSupportedException>(nan.Reset);
            }
        }

        // ---- LineRendererContainer ----

        // .ctor 0x6b23a0: m_positionsCountMax = 32 before the base call, then
        // new Vector3[m_positionsCountMax]. AddPosition 0x6b2420 appends unless
        // count == max (je), so a count past the max still appends.
        // Finalise 0x6b2450: positionCount, SetPositions(whole buffer), count = 0.
        [Test]
        public void LineRendererContainerAppendsUntilExactlyFullAndFlushes()
        {
            using (var owned = new Owned())
            {
                var line = owned.Inactive("Lucid owned line").AddComponent<LineRenderer>();
                var container = new LineRendererContainer();
                Assert.That(Get(container, "m_positionsCountMax"), Is.EqualTo(32));
                Assert.That(((Vector3[])Get(container, "m_positions")).Length, Is.EqualTo(32));
                Set(container, "m_lineRenderer", line);

                Set(container, "m_positionsCountMax", 2);
                container.AddPosition(new Vector3(1f, 0f, 0f));
                container.AddPosition(new Vector3(2f, 0f, 0f));
                container.AddPosition(new Vector3(3f, 0f, 0f));
                Assert.That(Get(container, "m_positionsCount"), Is.EqualTo(2));
                Assert.That(((Vector3[])Get(container, "m_positions"))[2], Is.EqualTo(Vector3.zero));

                Set(container, "m_positionsCount", 3);
                container.AddPosition(new Vector3(4f, 0f, 0f));
                Assert.That(Get(container, "m_positionsCount"), Is.EqualTo(4), "only an exact match stops the append");
                Assert.That(((Vector3[])Get(container, "m_positions"))[3], Is.EqualTo(new Vector3(4f, 0f, 0f)));

                Set(container, "m_positionsCount", 2);
                container.Finalise();
                Assert.That(line.positionCount, Is.EqualTo(2));
                Assert.That(line.GetPosition(0), Is.EqualTo(new Vector3(1f, 0f, 0f)));
                Assert.That(line.GetPosition(1), Is.EqualTo(new Vector3(2f, 0f, 0f)));
                Assert.That(Get(container, "m_positionsCount"), Is.EqualTo(0));

                // SetActive 0x6b2410 is Renderer.enabled.
                container.SetActive(false);
                Assert.That(line.enabled, Is.False);
                container.SetActive(true);
                Assert.That(line.enabled, Is.True);
            }
        }

        // ---- CanvasShaderModifier and ShaderProperties ----

        // ShaderProperties .cctor 0x59bab0 stores "_Dream_Noise_ST".
        // SetDreamNoiseStandardOffset 0x599fb0: for each renderer, jle on
        // materialCount leaves the whole loop; otherwise GetMaterial(0),
        // GetVector, replace lane 0 of xmm1 (z) and SetVector. The enumerable
        // is kept by reference (.ctor 0x599f80), so later additions count.
        [Test]
        public void CanvasShaderModifierSetsTheOffsetUntilARendererHasNoMaterial()
        {
            using (var owned = new Owned())
            {
                Assert.That(ShaderProperties.DreamNoiseST, Is.EqualTo("_Dream_Noise_ST"));
                Shader shader = Shader.Find("UI/Default");
                Material first = owned.Keep(new Material(shader));
                Material last = owned.Keep(new Material(shader));
                var original = new Vector4(2f, 3f, 4f, 5f);
                first.SetVector(ShaderProperties.DreamNoiseST, original);
                last.SetVector(ShaderProperties.DreamNoiseST, original);
                Assert.That(first.GetVector(ShaderProperties.DreamNoiseST), Is.EqualTo(original), "precondition: the material keeps the vector");

                CanvasRenderer withFirst = owned.Inactive("Lucid owned canvas a").AddComponent<CanvasRenderer>();
                withFirst.materialCount = 1;
                withFirst.SetMaterial(first, 0);
                CanvasRenderer empty = owned.Inactive("Lucid owned canvas b").AddComponent<CanvasRenderer>();
                CanvasRenderer withLast = owned.Inactive("Lucid owned canvas c").AddComponent<CanvasRenderer>();
                withLast.materialCount = 1;
                withLast.SetMaterial(last, 0);

                var renderers = new List<CanvasRenderer> { withFirst };
                var modifier = new CanvasShaderModifier(renderers);
                Assert.That(Get(modifier, "m_renderers"), Is.SameAs(renderers));
                renderers.Add(empty);
                renderers.Add(withLast);

                modifier.SetDreamNoiseStandardOffset(9f);
                Assert.That(first.GetVector(ShaderProperties.DreamNoiseST), Is.EqualTo(new Vector4(2f, 3f, 9f, 5f)));
                Assert.That(last.GetVector(ShaderProperties.DreamNoiseST), Is.EqualTo(original), "the empty renderer ends the pass");

                renderers.Remove(empty);
                modifier.SetDreamNoiseStandardOffset(-1f);
                Assert.That(first.GetVector(ShaderProperties.DreamNoiseST), Is.EqualTo(new Vector4(2f, 3f, -1f, 5f)));
                Assert.That(last.GetVector(ShaderProperties.DreamNoiseST), Is.EqualTo(new Vector4(2f, 3f, -1f, 5f)));
            }
        }

        // ---- PFXQualitySwitcher ----

        // Awake 0x6b31b0 and OnDisable 0x6b3210 deactivate m_default then
        // m_veryLow; OnEnable 0x6b31e0 activates the field at +0x20 + 8 *
        // (GetQualityLevel() == 0), m_veryLow only at level 0.
        [Test]
        public void PFXQualitySwitcherShowsTheVariantForTheCurrentLevel()
        {
            using (var owned = new Owned())
            {
                var switcher = owned.Inactive("Lucid owned pfx switcher").AddComponent<PFXQualitySwitcher>();
                GameObject standard = owned.Inactive("Lucid owned pfx default");
                GameObject veryLow = owned.Inactive("Lucid owned pfx very low");
                standard.SetActive(true);
                veryLow.SetActive(true);
                Set(switcher, "m_default", standard);
                Set(switcher, "m_veryLow", veryLow);

                Call(switcher, "Awake");
                Assert.That((standard.activeSelf, veryLow.activeSelf), Is.EqualTo((false, false)));

                Call(switcher, "OnEnable");
                bool lowest = QualitySettings.GetQualityLevel() == 0;
                Assert.That((standard.activeSelf, veryLow.activeSelf), Is.EqualTo((!lowest, lowest)));

                Call(switcher, "OnDisable");
                Assert.That((standard.activeSelf, veryLow.activeSelf), Is.EqualTo((false, false)));
            }
        }

        // ---- BlobShadowModifier ----

        // .ctor 0x6a6ca0 caches GetSystemRef<CharacterManager>(null, true).
        // Action_Show/HideBlobShadow (0x6a6c00/0x6a6c50) read the manager's
        // m_currentCharacter (+0x38, GetCurrentCharacterUnsafe) with no
        // validity check and call ToggleBlobShadow(true/false). The manager
        // and character are raw: only these two fields are read.
        [Test]
        public void BlobShadowModifierTogglesTheCurrentCharactersShadow()
        {
            using (var owned = new Owned())
            {
                var manager = (CharacterManager)FormatterServices.GetUninitializedObject(typeof(CharacterManager));
                var character = (Character)FormatterServices.GetUninitializedObject(typeof(Character));
                GameObject shadow = owned.Inactive("Lucid owned blob shadow");
                Set(character, "m_blobShadowObject", shadow);
                VerificationFixtureOwnership.RegistryRow row = null;
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CharacterManager>());
                    row.Register(manager);
                    var modifier = owned.Inactive("Lucid owned blob modifier").AddComponent<BlobShadowModifier>();
                    Assert.That(Get(modifier, "m_characterManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<CharacterManager>()));

                    Set(manager, "m_currentCharacter", character);
                    modifier.Action_ShowBlobShadow();
                    Assert.That(shadow.activeSelf, Is.True);
                    modifier.Action_HideBlobShadow();
                    Assert.That(shadow.activeSelf, Is.False);

                    Set(manager, "m_currentCharacter", null);
                    Assert.Throws<NullReferenceException>(modifier.Action_ShowBlobShadow, "no current-character guard");
                }, owned.Dispose, () => row?.Dispose());
            }
        }

        // ---- AssignAddressableTexture ----

        // <OnEnable>b__2_0 0x6a6bf0, the load callback, is
        // m_camera.targetTexture = prefab.
        [Test]
        public void AssignAddressableTextureCallbackTargetsTheCamera()
        {
            using (var owned = new Owned())
            {
                GameObject owner = owned.Inactive("Lucid owned texture camera");
                var camera = owner.AddComponent<Camera>();
                var assign = owner.AddComponent<AssignAddressableTexture>();
                Set(assign, "m_camera", camera);
                RenderTexture texture = owned.Keep(new RenderTexture(8, 8, 0));
                try
                {
                    Call(assign, "<OnEnable>b__2_0", texture);
                    Assert.That(camera.targetTexture, Is.SameAs(texture));
                    Call(assign, "<OnEnable>b__2_0", new object[] { null });
                    Assert.That(camera.targetTexture, Is.Null);
                }
                finally { camera.targetTexture = null; }
            }
        }

        // ---- InstancedObjectSpawner ----

        // .ctor 0x6b1da0: m_maxRow 16 and m_maxColumn 9 (the qword 0x900000010)
        // and Array.Empty<T>() for both arrays. HasComponentsToToggle 0x6b1bc0
        // and HasFeaturesToToggle 0x6b1cd0 are index < Length - 1, so the last
        // entry is never toggled; Reset* (0x6b1b30, 0x6b1be0) disable all and
        // zero the index.
        [Test]
        public void InstancedObjectSpawnerTogglesAllButTheLastEntry()
        {
            using (var owned = new Owned())
            {
                var spawner = owned.Inactive("Lucid owned spawner").AddComponent<InstancedObjectSpawner>();
                Assert.That(Get(spawner, "m_maxRow"), Is.EqualTo(16));
                Assert.That(Get(spawner, "m_maxColumn"), Is.EqualTo(9));
                Assert.That(Get(spawner, "m_toggleableComponents"), Is.SameAs(Array.Empty<Behaviour>()));
                Assert.That(Get(spawner, "m_rendererFeatures"), Is.SameAs(Array.Empty<ScriptableRendererFeature>()));
                Assert.That(spawner.HasComponentsToToggle(), Is.False);
                Assert.DoesNotThrow(spawner.ToggleNextComponent);

                var lights = new Behaviour[3];
                for (int i = 0; i < lights.Length; i++)
                    lights[i] = owned.Inactive("Lucid owned spawner light " + i).AddComponent<Light>();
                Set(spawner, "m_toggleableComponents", lights);
                spawner.ResetComponents();
                Assert.That(Array.ConvertAll(lights, l => l.enabled), Is.EqualTo(new[] { false, false, false }));
                spawner.ToggleNextComponent();
                spawner.ToggleNextComponent();
                Assert.That(spawner.HasComponentsToToggle(), Is.False);
                spawner.ToggleNextComponent();
                Assert.That(Array.ConvertAll(lights, l => l.enabled), Is.EqualTo(new[] { true, true, false }));
                Assert.That(Get(spawner, "m_currentComponentIndex"), Is.EqualTo(2));
                spawner.ResetComponents();
                Assert.That(Array.ConvertAll(lights, l => l.enabled), Is.EqualTo(new[] { false, false, false }));
                Assert.That(Get(spawner, "m_currentComponentIndex"), Is.EqualTo(0));

                var features = new ScriptableRendererFeature[2];
                for (int i = 0; i < features.Length; i++)
                    features[i] = owned.Keep(ScriptableObject.CreateInstance<FullScreenPassRendererFeature>());
                Set(spawner, "m_rendererFeatures", features);
                spawner.ResetRenderingFeatures();
                Assert.That(Array.ConvertAll(features, f => f.isActive), Is.EqualTo(new[] { false, false }));
                Assert.That(spawner.HasFeaturesToToggle(), Is.True);
                spawner.ToggleNextRenderFeature();
                spawner.ToggleNextRenderFeature();
                Assert.That(Array.ConvertAll(features, f => f.isActive), Is.EqualTo(new[] { true, false }));
                Assert.That(Get(spawner, "m_currentFeatureIndex"), Is.EqualTo(1));
                Assert.That(spawner.HasFeaturesToToggle(), Is.False);
            }
        }

        // CreateInstances 0x6b1920 builds an empty List<Material> and reads
        // [0] before Instantiate, so with positive bounds it throws and
        // creates nothing. CleanUp 0x6b1cf0 DestroyImmediates every child,
        // last index first.
        [Test]
        public void InstancedObjectSpawnerThrowsOnTheEmptyMaterialListAndCleansUpChildren()
        {
            using (var owned = new Owned())
            {
                var spawner = owned.Inactive("Lucid owned spawner grid").AddComponent<InstancedObjectSpawner>();
                Set(spawner, "m_prefab", owned.Inactive("Lucid owned spawner prefab"));
                Assert.Throws<ArgumentOutOfRangeException>(spawner.CreateInstances);
                Assert.That(spawner.transform.childCount, Is.Zero);

                for (int i = 0; i < 3; i++)
                    new GameObject("Lucid owned spawner child " + i).transform.SetParent(spawner.transform, false);
                Assert.That(spawner.transform.childCount, Is.EqualTo(3));
                spawner.CleanUp();
                Assert.That(spawner.transform.childCount, Is.Zero);
            }
        }
    }
}
