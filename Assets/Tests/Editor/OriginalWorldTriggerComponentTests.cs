using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime EventOnVisibility, HashedColliderGroup, HideOnAwake,
    // LevelDataGameObjectActivation, MaterialManipulationTrigger, MergeColliders,
    // SequentialActivator, SiblingOrderEvent, StickyControlsTrigger and the three enum metadata
    // values against the 1.10.1 x86-64 bodies. Edit Mode sends no Unity messages, so each case
    // calls the hooks itself; refused coroutines and edit-mode Destroy calls are counted
    // through their log errors.
    public sealed class OriginalWorldTriggerComponentTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");

        private static UnityEvent Logged(List<string> log, Func<string> entry)
        {
            var unityEvent = new UnityEvent();
            unityEvent.AddListener(() => log.Add(entry()));
            return unityEvent;
        }

        // TypeDef 0x02000a04: NullChecks, RequireComponent(Renderer), ArrayBoundsChecks. The
        // getters read +0x20/+0x28; OnBecameVisible 0x6B09D0 and OnBecameInvisible 0x6B09F0
        // store the flag (+0x30) before invoking their event, with no null test.
        [Test]
        public void VisibilityPublishesTheFlagBeforeItsEvent()
        {
            AssertMembers(typeof(EventOnVisibility), 0x00100001,
                new[] { ("m_onVisibleEvent", 0x0001), ("m_onInvisibleEvent", 0x0001), ("<IsVisible>k__BackingField", 0x0001) },
                new[] { ("get_OnVisibleEvent", 0x0886), ("get_OnInvisibleEvent", 0x0886), ("get_IsVisible", 0x0886), ("set_IsVisible", 0x0881),
                        ("OnBecameVisible", 0x0081), ("OnBecameInvisible", 0x0081), (".ctor", 0x1886) });
            Assert.That(AttributeTypes(typeof(EventOnVisibility)).Select(type => type.Name),
                Is.EqualTo(new[] { "Il2CppSetOptionAttribute", "RequireComponent", "Il2CppSetOptionAttribute" }));
            AssertIl2CppOptions(typeof(EventOnVisibility), 1, 2);

            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid visibility", false);
                host.AddComponent<MeshRenderer>();
                var visibility = host.AddComponent<EventOnVisibility>();
                var log = new List<string>();
                UnityEvent onVisible = Logged(log, () => "visible " + visibility.IsVisible);
                UnityEvent onInvisible = Logged(log, () => "invisible " + visibility.IsVisible);
                Set(visibility, "m_onVisibleEvent", onVisible);
                Set(visibility, "m_onInvisibleEvent", onInvisible);
                Assert.That(visibility.OnVisibleEvent, Is.SameAs(onVisible));
                Assert.That(visibility.OnInvisibleEvent, Is.SameAs(onInvisible));
                Assert.That(visibility.IsVisible, Is.False);

                Call(visibility, "OnBecameVisible");
                Call(visibility, "OnBecameInvisible");
                Assert.That(log, Is.EqualTo(new[] { "visible True", "invisible False" }));

                Set(visibility, "m_onVisibleEvent", null);
                Assert.Throws<NullReferenceException>(() => Call(visibility, "OnBecameVisible"));
                Assert.That(visibility.IsVisible, Is.True, "the flag is stored before the missing event faults");
            });
        }

        // TypeDef 0x02000a7e derives HashedGroup and requires a BoxCollider. OnTrigger 0x6C9A70
        // enumerates m_colliderList and calls set_enabled (0x6c9b27) on each entry with no null
        // test, so a null entry stops the loop.
        [Test]
        public void ColliderGroupSetsEveryListedColliderAndStopsAtANullEntry()
        {
            AssertMembers(typeof(HashedColliderGroup), 0x00100001,
                new[] { ("m_colliderList", 0x0001), ("m_collidersHashCode", 0x0001) },
                new[] { ("OnTrigger", 0x0086), (".ctor", 0x1886) });
            Assert.That(typeof(HashedColliderGroup).BaseType, Is.EqualTo(typeof(HashedGroup)));
            Assert.That(AttributeTypes(Field(typeof(HashedColliderGroup), "m_colliderList")),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HashedColliderGroup), "m_collidersHashCode")),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(Field(typeof(HashedColliderGroup), "m_colliderList")), Is.EqualTo("All colliders that are located inside the box collider."));

            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid collider group", false);
                var group = host.AddComponent<HashedColliderGroup>();
                BoxCollider own = host.GetComponent<BoxCollider>();
                Assert.That(own, Is.Not.Null, "RequireComponent adds the box collider");
                var first = owned.Host("Lucid grouped first", false).AddComponent<SphereCollider>();
                var second = owned.Host("Lucid grouped second", false).AddComponent<CapsuleCollider>();
                Set(group, "m_colliderList", new List<Collider> { first, second });

                group.OnTrigger(false);
                Assert.That((first.enabled, second.enabled, own.enabled), Is.EqualTo((false, false, true)));
                group.OnTrigger(true);
                Assert.That((first.enabled, second.enabled), Is.EqualTo((true, true)));

                Set(group, "m_colliderList", new List<Collider> { first, null, second });
                Assert.Throws<NullReferenceException>(() => group.OnTrigger(false));
                Assert.That((first.enabled, second.enabled), Is.EqualTo((false, true)));
            });
        }

        // Awake 0x6CB620 and HideObjects 0x6CB670 deactivate each renderer's GameObject; the
        // renderer itself stays enabled. Reset 0x6CB6C0 stores GetComponentsInChildren<MeshRenderer>()
        // with its default, so inactive children are left out.
        [Test]
        public void HideOnAwakeDeactivatesTheRenderersObjects()
        {
            AssertMembers(typeof(HideOnAwake), 0x00100001,
                new[] { ("m_meshRenderers", 0x0001) },
                new[] { ("Awake", 0x0081), ("HideObjects", 0x0086), ("Reset", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(HideOnAwake), 1, 2);

            Owned.Run(owned =>
            {
                GameObject root = owned.Host("Lucid hide root");
                MeshRenderer rootRenderer = root.AddComponent<MeshRenderer>();
                MeshRenderer activeChild = owned.Host("Lucid hide active child", true, root.transform).AddComponent<MeshRenderer>();
                owned.Host("Lucid hide inactive child", false, root.transform).AddComponent<MeshRenderer>();
                owned.Host("Lucid hide plain child", true, root.transform);
                var hide = root.AddComponent<HideOnAwake>();
                Call(hide, "Reset");
                Assert.That(Get<MeshRenderer[]>(hide, "m_meshRenderers"), Is.EqualTo(new[] { rootRenderer, activeChild }));

                foreach (string hook in new[] { "Awake", "HideObjects" })
                {
                    MeshRenderer first = owned.Host("Lucid hidden first " + hook).AddComponent<MeshRenderer>();
                    MeshRenderer second = owned.Host("Lucid hidden second " + hook).AddComponent<MeshRenderer>();
                    Set(hide, "m_meshRenderers", new[] { first, second });
                    Call(hide, hook);
                    Assert.That((first.gameObject.activeSelf, second.gameObject.activeSelf), Is.EqualTo((false, false)), hook);
                    Assert.That((first.enabled, second.enabled), Is.EqualTo((true, true)), hook + " leaves the renderers enabled");
                }
                Assert.That(root.activeSelf, Is.True);
            });
        }

        // TypeDef 0x02000a8a is a plain class; the constructor 0x6CE270 creates both lists.
        // Apply 0x6CE000 sets the enable list to the flag and the disable list to its inverse
        // (xor 1 at 0x6ce137), skipping entries op_Equality reports null.
        [Test]
        public void LevelDataActivationAppliesTheFlagAndItsInverse()
        {
            AssertMembers(typeof(LevelDataGameObjectActivation), 0x00100001,
                new[] { ("<IsExpanded>k__BackingField", 0x0001), ("<IsExpandedEnable>k__BackingField", 0x0001), ("<IsExpandedDisable>k__BackingField", 0x0001),
                        ("<EnableGameObjects>k__BackingField", 0x0021), ("<DisableGameObjects>k__BackingField", 0x0021) },
                new[] { ("get_IsExpanded", 0x0886), ("set_IsExpanded", 0x0886), ("get_IsExpandedEnable", 0x0886), ("set_IsExpandedEnable", 0x0886),
                        ("get_IsExpandedDisable", 0x0886), ("set_IsExpandedDisable", 0x0886), ("get_EnableGameObjects", 0x0886),
                        ("get_DisableGameObjects", 0x0886), ("Apply", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(LevelDataGameObjectActivation), 2, 1);

            var activation = new LevelDataGameObjectActivation();
            Assert.That(activation.EnableGameObjects, Is.Not.Null.And.Empty);
            Assert.That(activation.DisableGameObjects, Is.Not.Null.And.Empty.And.Not.SameAs(activation.EnableGameObjects));
            Assert.That((activation.IsExpanded, activation.IsExpandedEnable, activation.IsExpandedDisable), Is.EqualTo((false, false, false)));
            activation.IsExpandedEnable = true;
            Assert.That((activation.IsExpanded, activation.IsExpandedEnable, activation.IsExpandedDisable), Is.EqualTo((false, true, false)));

            Owned.Run(owned =>
            {
                GameObject enabled = owned.Host("Lucid level enable", false);
                GameObject disabled = owned.Host("Lucid level disable", true);
                GameObject destroyed = owned.Host("Lucid level destroyed");
                UnityEngine.Object.DestroyImmediate(destroyed);
                activation.EnableGameObjects.AddRange(new[] { null, destroyed, enabled });
                activation.DisableGameObjects.AddRange(new[] { destroyed, disabled, null });

                activation.Apply(true);
                Assert.That((enabled.activeSelf, disabled.activeSelf), Is.EqualTo((true, false)));
                activation.Apply(false);
                Assert.That((enabled.activeSelf, disabled.activeSelf), Is.EqualTo((false, true)));
            });
        }

        // Action_Trigger 0x59A2D0 sends every array entry, in order, to
        // SceneMaterialManipulator.Trigger (0x59a2f9), which starts a routine for each matching
        // config. On an inactive manipulator each start is refused with one error, so the
        // count separates every trigger from distinct or first-only ones.
        [Test]
        public void MaterialTriggerSendsEveryTriggerType()
        {
            AssertMembers(typeof(MaterialManipulationTrigger), 0x00100001,
                new[] { ("m_manipulator", 0x0001), ("m_manipulationTriggers", 0x0001) },
                new[] { ("Action_Trigger", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(MaterialManipulationTrigger), 1, 2);

            Owned.Run(owned =>
            {
                // Synthetic distinct keys: the generated enum has only Boss4PunchFloor.
                var a = (MaterialManipulationTriggerType)1;
                var b = (MaterialManipulationTriggerType)2;
                var manipulator = owned.Host("Lucid material manipulator", false).AddComponent<SceneMaterialManipulator>();
                Set(manipulator, "m_materialColourConfigs", new[]
                {
                    new SceneMaterialManipulator.MaterialColourConfig { Trigger = a },
                    new SceneMaterialManipulator.MaterialColourConfig { Trigger = b },
                    new SceneMaterialManipulator.MaterialColourConfig { Trigger = a },
                });
                var trigger = owned.Host("Lucid material trigger", false).AddComponent<MaterialManipulationTrigger>();
                Set(trigger, "m_manipulator", manipulator);
                Set(trigger, "m_manipulationTriggers", new[] { a, b, b, (MaterialManipulationTriggerType)3 });
                for (int i = 0; i < 4; i++) LogAssert.Expect(LogType.Error, RefusedCoroutine);
                trigger.Action_Trigger();
                LogAssert.NoUnexpectedReceived();

                Set(trigger, "m_manipulationTriggers", new MaterialManipulationTriggerType[0]);
                trigger.Action_Trigger();
                LogAssert.NoUnexpectedReceived();
            });
        }

        // DoMergeColliders 0x6B2570 returns on an empty GetComponentsInChildren<MeshFilter>().
        // Otherwise each filter's shared mesh and localToWorldMatrix fill a CombineInstance and
        // its own Collider is destroyed; a new MeshFilter on this object takes a new mesh, the
        // combined result, then a MeshCollider is added and the filter destroyed. Edit Mode
        // refuses each Destroy with one error and keeps the objects. The child's
        // localToWorldMatrix goes straight to set_transform (0x6b2689..0x6b26b6), so the root
        // sits away from the origin: the vertices are in world space, not relative to the root.
        [Test]
        public void MergeCollidersCombinesChildMeshesInWorldSpace()
        {
            AssertMembers(typeof(MergeColliders), 0x00100001, new (string, int)[0],
                new[] { ("Awake", 0x0084), ("DoMergeColliders", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(MergeColliders), 1, 2);

            Owned.Run(owned =>
            {
                var triangle = owned.Track(new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } });
                GameObject empty = owned.Host("Lucid merge empty");
                Call(empty.AddComponent<MergeColliders>(), "Awake");
                // Unity's overloaded == treats the editor's missing-component placeholder as null; the tuple's
                // default equality does not (it printed "(null, null)" against a real-null "(, )").
                Assert.That(empty.GetComponent<MeshFilter>() == null, Is.True, "no MeshFilter is added when there are no child filters");
                Assert.That(empty.GetComponent<MeshCollider>() == null, Is.True, "no MeshCollider is added when there are no child filters");

                // GetComponentsInChildren skips inactive objects, so this hierarchy is active.
                GameObject root = owned.Host("Lucid merge root");
                GameObject withCollider = owned.Host("Lucid merge collider child", true, root.transform);
                withCollider.transform.localPosition = new Vector3(1f, 0f, 0f);
                withCollider.AddComponent<MeshFilter>().sharedMesh = triangle;
                withCollider.AddComponent<BoxCollider>();
                GameObject plain = owned.Host("Lucid merge plain child", true, root.transform);
                plain.transform.localPosition = new Vector3(0f, 2f, 0f);
                plain.AddComponent<MeshFilter>().sharedMesh = triangle;
                root.transform.position = new Vector3(10f, 0f, 0f);
                var merge = root.AddComponent<MergeColliders>();

                // In order: the child collider's refused Destroy; then get_mesh (0x6b276e) after set_mesh, which
                // the engine answers in Edit Mode by instantiating the mesh with an error; then the filter's Destroy.
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, "Instantiating mesh due to calling MeshFilter.mesh during edit mode. This will leak meshes. Please use MeshFilter.sharedMesh instead.");
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                var priorMeshes = new HashSet<Mesh>(Resources.FindObjectsOfTypeAll<Mesh>());
                Call(merge, "Awake");
                LogAssert.NoUnexpectedReceived();
                // The new Mesh and its edit-mode instance are both created here; the case owns them.
                foreach (Mesh created in Resources.FindObjectsOfTypeAll<Mesh>())
                    if (!priorMeshes.Contains(created)) owned.Track(created);

                MeshFilter combined = root.GetComponent<MeshFilter>();
                Assert.That(combined, Is.Not.Null);
                Mesh mesh = combined.sharedMesh;
                Assert.That(mesh, Is.Not.SameAs(triangle));
                Assert.That(mesh.vertices, Is.EquivalentTo(new[]
                {
                    new Vector3(11f, 0f, 0f), new Vector3(12f, 0f, 0f), new Vector3(11f, 1f, 0f),
                    new Vector3(10f, 2f, 0f), new Vector3(11f, 2f, 0f), new Vector3(10f, 3f, 0f)
                }));
                Assert.That(root.GetComponent<MeshCollider>(), Is.Not.Null);
                Assert.That(withCollider.GetComponent<BoxCollider>(), Is.Not.Null, "the refused Destroy keeps the child collider");
            });
        }

        // Start 0x060039fe builds <Start>d__1; MoveNext 0x6B3D30 copies the array (<>7__wrap1),
        // yields null and only on the next resume activates <targetGameObject>5__4. The
        // constructor 0x6B3C50 stores Array.Empty<GameObject>().
        [Test]
        public void SequentialActivatorActivatesOneObjectPerResume()
        {
            AssertMembers(typeof(SequentialActivator), 0x00100001,
                new[] { ("m_objectsToActivate", 0x0001) },
                new[] { ("Start", 0x0081), (".ctor", 0x1886) });
            Type iterator = typeof(SequentialActivator).GetNestedType("<Start>d__1", BindingFlags.NonPublic);
            AssertMembers(iterator, 0x00100103,
                new[] { ("<>1__state", 0x0001), ("<>2__current", 0x0001), ("<>4__this", 0x0006), ("<>7__wrap1", 0x0001),
                        ("<>7__wrap2", 0x0001), ("<targetGameObject>5__4", 0x0001) },
                new[] { (".ctor", 0x1886), ("System.IDisposable.Dispose", 0x01e1), ("MoveNext", 0x01e1),
                        ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                        ("System.Collections.IEnumerator.Reset", 0x01e1), ("System.Collections.IEnumerator.get_Current", 0x09e1) });

            Owned.Run(owned =>
            {
                var activator = owned.Host("Lucid sequential activator", false).AddComponent<SequentialActivator>();
                Assert.That(Get<GameObject[]>(activator, "m_objectsToActivate"), Is.Not.Null.And.Empty);
                Assert.That(((IEnumerator)Call(activator, "Start")).MoveNext(), Is.False);

                GameObject[] targets = Enumerable.Range(0, 3).Select(i => owned.Host("Lucid sequential target " + i, false)).ToArray();
                Set(activator, "m_objectsToActivate", targets);
                var routine = (IEnumerator)Call(activator, "Start");
                Assert.That(routine.GetType(), Is.EqualTo(iterator));
                var active = new List<string>();
                while (routine.MoveNext())
                {
                    Assert.That(routine.Current, Is.Null);
                    active.Add(string.Concat(targets.Select(target => target.activeSelf ? "1" : "0")));
                }
                active.Add(string.Concat(targets.Select(target => target.activeSelf ? "1" : "0")));
                Assert.That(active, Is.EqualTo(new[] { "000", "100", "110", "111" }));
            });
        }

        // CheckConditions 0x6B4360 stores the sibling index, returns for a first child before
        // reading parent.childCount, then tests the last index (count - 1) and finally compares
        // m_triggerOnSpecificChild (default -1, ctor 0x6B43C0) with the index. OnEnable 0x6B42F0
        // invokes m_event on a match. Awake 0x6B4240 fills only a null target; Reset 0x6B42C0
        // always stores this transform.
        [Test]
        public void SiblingOrderEventMatchesFirstLastOrASpecificChild()
        {
            AssertMembers(typeof(SiblingOrderEvent), 0x00100001,
                new[] { ("m_targetTransform", 0x0001), ("m_triggerOnFirst", 0x0001), ("m_triggerOnLast", 0x0001), ("m_triggerOnSpecificChild", 0x0001),
                        ("m_event", 0x0001), ("m_siblingIndex", 0x0001), ("m_childCount", 0x0001) },
                new[] { ("Awake", 0x0081), ("Reset", 0x0081), ("OnEnable", 0x0081), ("CheckConditions", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(SiblingOrderEvent), 2, 1);

            Owned.Run(owned =>
            {
                GameObject parent = owned.Host("Lucid sibling parent", false);
                Transform[] children = Enumerable.Range(0, 3).Select(i => owned.Host("Lucid sibling " + i, false, parent.transform).transform).ToArray();
                GameObject host = owned.Host("Lucid sibling event", false);
                var siblingEvent = host.AddComponent<SiblingOrderEvent>();
                Assert.That(Get<int>(siblingEvent, "m_triggerOnSpecificChild"), Is.EqualTo(-1));
                var log = new List<string>();
                Set(siblingEvent, "m_event", Logged(log, () => "event"));

                bool Fires(int child, bool first, bool last, int specific)
                {
                    Set(siblingEvent, "m_targetTransform", children[child]);
                    Set(siblingEvent, "m_triggerOnFirst", first);
                    Set(siblingEvent, "m_triggerOnLast", last);
                    Set(siblingEvent, "m_triggerOnSpecificChild", specific);
                    log.Clear();
                    Call(siblingEvent, "OnEnable");
                    Assert.That((bool)Call(siblingEvent, "CheckConditions"), Is.EqualTo(log.Count == 1));
                    return log.Count == 1;
                }

                Assert.That(Enumerable.Range(0, 3).Select(i => Fires(i, true, false, -1)), Is.EqualTo(new[] { true, false, false }));
                Assert.That(Enumerable.Range(0, 3).Select(i => Fires(i, false, true, -1)), Is.EqualTo(new[] { false, false, true }));
                Assert.That(Enumerable.Range(0, 3).Select(i => Fires(i, false, false, 1)), Is.EqualTo(new[] { false, true, false }));
                Assert.That(Enumerable.Range(0, 3).Select(i => Fires(i, false, false, -1)), Is.EqualTo(new[] { false, false, false }));
                Assert.That(Get<int>(siblingEvent, "m_siblingIndex"), Is.EqualTo(2));
                Assert.That(Get<int>(siblingEvent, "m_childCount"), Is.EqualTo(3));

                Set(siblingEvent, "m_childCount", -7);
                Assert.That(Fires(0, true, true, 0), Is.True);
                Assert.That(Get<int>(siblingEvent, "m_childCount"), Is.EqualTo(-7), "a first child returns before the parent is read");

                Set(siblingEvent, "m_targetTransform", parent.transform);
                Set(siblingEvent, "m_triggerOnFirst", false);
                Assert.Throws<NullReferenceException>(() => Call(siblingEvent, "CheckConditions"), "a root target has no parent");

                Set(siblingEvent, "m_targetTransform", children[1]);
                Call(siblingEvent, "Awake");
                Assert.That(Get<Transform>(siblingEvent, "m_targetTransform"), Is.SameAs(children[1]));
                Set(siblingEvent, "m_targetTransform", null);
                Call(siblingEvent, "Awake");
                Assert.That(Get<Transform>(siblingEvent, "m_targetTransform"), Is.SameAs(host.transform));
                Set(siblingEvent, "m_targetTransform", children[1]);
                Call(siblingEvent, "Reset");
                Assert.That(Get<Transform>(siblingEvent, "m_targetTransform"), Is.SameAs(host.transform));
            });
        }

        // TypeDef 0x0200073f: the constructor 0x5CB440 allocates CharacterStickyControls.
        // SetTransformOverride 0x5CB390 writes the controls' +0x18; DoTriggerEnter 0x5CB3B0
        // passes both events to DoTriggerActivate, DoTriggerStay 0x5CB3E0 and DoTriggerExit
        // 0x5CB410 forward to the controls' stay and deactivate methods.
        [Test]
        public void StickyControlsTriggerForwardsItsHooksAndEvents()
        {
            AssertMembers(typeof(StickyControlsTrigger), 0x00100001,
                new[] { ("m_stickyControls", 0x0001), ("m_onActivated", 0x0001), ("m_onDeactivated", 0x0001) },
                new[] { ("SetTransformOverride", 0x0086), ("DoTriggerEnter", 0x00c4), ("DoTriggerStay", 0x00c4), ("DoTriggerExit", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(StickyControlsTrigger), 2, 1);
            Assert.That(Tooltip(Field(typeof(StickyControlsTrigger), "m_onDeactivated")), Is.EqualTo("Events to fire when sticky controls are deactivated."));

            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                GameObject host = owned.Host("Lucid sticky trigger", false);
                host.AddComponent<BoxCollider>();
                var trigger = host.AddComponent<StickyControlsTrigger>();
                var other = owned.Host("Lucid sticky other", false).AddComponent<BoxCollider>().gameObject.AddComponent<StickyControlsTrigger>();
                var controls = Get<CharacterStickyControls>(trigger, "m_stickyControls");
                Assert.That(controls, Is.Not.Null.And.Not.SameAs(Get<CharacterStickyControls>(other, "m_stickyControls")));

                Transform overrideTransform = owned.Host("Lucid sticky override", false).transform;
                overrideTransform.rotation = Quaternion.Euler(0f, 90f, 0f);
                trigger.SetTransformOverride(overrideTransform);
                Assert.That(Get<Transform>(controls, "m_transformOverride"), Is.SameAs(overrideTransform));

                Character character = owned.Host("Lucid sticky character", false).AddComponent<Character>();
                typeof(Actor).GetProperty("Storage").SetValue(character, new FSMStorage(0));
                var log = new List<string>();
                UnityEvent onActivated = Logged(log, () => "activated");
                UnityEvent onDeactivated = Logged(log, () => "deactivated");
                Set(trigger, "m_onActivated", onActivated);
                Set(trigger, "m_onDeactivated", onDeactivated);

                Call(trigger, "DoTriggerEnter", character);
                Assert.That(Get<UnityEvent>(controls, "m_onActivated"), Is.SameAs(onActivated));
                Assert.That(Get<UnityEvent>(controls, "m_onDeactivated"), Is.SameAs(onDeactivated));
                Assert.That(Get<bool>(controls, "m_initialised"), Is.True);
                Assert.That(log, Is.Empty, "without the sticky-controls modifier nothing activates");

                IGraphStorage storage = character.Storage;
                Assert.That(storage.GetCollection().ContainsKey(ActorFSMKeys.TurnCameraActive), Is.False);
                Call(trigger, "DoTriggerStay", character);
                Assert.That(storage.GetCollection().ContainsKey(ActorFSMKeys.TurnCameraRotation), Is.False, "no update is required yet");
                Set(controls, "m_updateRequired", true);
                Call(trigger, "DoTriggerStay", character);
                Assert.That(storage.GetValue(ActorFSMKeys.TurnCameraRotation, Quaternion.identity, false), Is.EqualTo(overrideTransform.rotation));

                Call(trigger, "DoTriggerExit", character);
                Assert.That(log, Is.Empty, "exit cancels only with m_cancelOnExit");
                Set(controls, "m_cancelOnExit", true);
                Call(trigger, "DoTriggerExit", character);
                Assert.That(log, Is.EqualTo(new[] { "deactivated" }));
                Assert.That(storage.GetValue(ActorFSMKeys.TurnCameraActive, true, false), Is.False);
            });
        }

        // TypeDefs 0x020005de, 0x020005cc and 0x020005d2 each add only a constructor
        // (0x55B730, 0x55AD80, 0x55AF00) that tail-calls MetadataValueTypeTEnum`1..ctor.
        [Test]
        public void EnumMetadataValuesBindTheirEnumAndAssetMenu()
        {
            foreach ((Type type, Type enumType, int[] options) in new[]
            {
                (typeof(RailTypeMetadataValue), typeof(RailType), new[] { 2, 1 }),
                (typeof(TerrainEffectTypeMetadataValue), typeof(TerrainEffectType), new[] { 2, 1 }),
                (typeof(TerrainTrackerTypeMetadataValue), typeof(TerrainTrackerType), new[] { 1, 2 }),
            })
            {
                AssertMembers(type, 0x00100001, new (string, int)[0], new[] { (".ctor", 0x1886) });
                AssertIl2CppOptions(type, options);
                Assert.That(type.BaseType, Is.EqualTo(typeof(MetadataValueTypeTEnum<>).MakeGenericType(enumType)));
                CustomAttributeData menu = type.GetCustomAttributesData().Single(data => data.AttributeType == typeof(CreateAssetMenuAttribute));
                Assert.That(menu.NamedArguments.Select(argument => (argument.MemberName, argument.TypedValue.Value)), Is.EqualTo(new[]
                {
                    ("fileName", (object)type.Name), ("menuName", "Hardlight/Metadata/" + type.Name)
                }));
                Owned.Run(owned => Assert.That(owned.Track(ScriptableObject.CreateInstance(type)), Is.InstanceOf(type)));
            }
            Assert.That(AttributeTypes(typeof(RailTypeMetadataValue)).First(), Is.EqualTo(typeof(CreateAssetMenuAttribute)));
            Assert.That(AttributeTypes(typeof(TerrainEffectTypeMetadataValue)).Last(), Is.EqualTo(typeof(CreateAssetMenuAttribute)));
            Assert.That(AttributeTypes(typeof(TerrainTrackerTypeMetadataValue)).Last(), Is.EqualTo(typeof(CreateAssetMenuAttribute)));
        }
    }
}
