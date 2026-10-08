using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Verification
{
    // Verification owns every object it changes. These checks exercise preserved
    // original scene code, with no gameplay, platform service or input dependency.
    public static class CharacterRigPreservationVerification
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

        private sealed class OwnedObjects : IDisposable
        {
            private readonly List<Object> objects = new List<Object>();
            public GameObject New(string name, Transform parent = null)
            {
                var value = new GameObject(name);
                value.SetActive(false);
                objects.Add(value);
                if (parent != null) value.transform.SetParent(parent, false);
                return value;
            }
            public Material Material(string name)
            {
                Shader shader = Shader.Find("Hidden/InternalErrorShader");
                if (shader == null) throw new InvalidOperationException("Unity's built-in verification shader is unavailable.");
                var value = new Material(shader) { name = name };
                objects.Add(value);
                return value;
            }
            public void Dispose()
            {
                for (int i = objects.Count - 1; i >= 0; --i)
                    if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            }
        }

        private sealed class RigObjects
        {
            public CharacterRigLookup Rig;
            public GameObject Root;
            public Animator CharacterAnimator;
            public Animator MeshAnimator;
            public GameObject Additional;
            public RigObjects(OwnedObjects owned, string prefix)
            {
                GameObject container = owned.New(prefix);
                Rig = container.AddComponent<CharacterRigLookup>();
                Root = owned.New(prefix + " root", container.transform);
                CharacterAnimator = owned.New(prefix + " character", container.transform).AddComponent<Animator>();
                MeshAnimator = owned.New(prefix + " mesh", container.transform).AddComponent<Animator>();
                Additional = owned.New(prefix + " additional", container.transform);
                Set(Rig, "m_characterRoot", Root);
                Set(Rig, "m_characterAnimator", CharacterAnimator);
                Set(Rig, "m_meshAnimator", MeshAnimator);
                Set(Rig, "m_additionalObjects", new[] { Additional });
            }
        }

        public static int CacheCorrespondence()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                var original = new RigObjects(owned, "Original");
                var replacement = new RigObjects(owned, "Replacement");
                BoxCollider first = original.Root.AddComponent<BoxCollider>();
                BoxCollider last = original.Root.AddComponent<BoxCollider>();
                replacement.Root.AddComponent<BoxCollider>();
                BoxCollider replacementLast = replacement.Root.AddComponent<BoxCollider>();
                original.Rig.Initialize(); replacement.Rig.Initialize();
                Require(Cache(original.Rig).Count == 4 && Cache(replacement.Rig).Count == 4, "four ordered objects cached", ref checks);
                Require(ReferenceEquals(original.Rig.CharacterRoot, original.Root), "root getter retains reference", ref checks);
                Require(ReferenceEquals(original.Rig.CharacterAnimator, original.CharacterAnimator), "character animator getter", ref checks);
                Require(ReferenceEquals(original.Rig.MeshAnimator, original.MeshAnimator), "mesh animator getter", ref checks);
                Require(original.Rig.TryGetReplacement(original.Root, replacement.Rig, out Object value) && ReferenceEquals(value, replacement.Root), "game object index correspondence", ref checks);
                Require(original.Rig.TryGetReplacement(original.Root.transform, replacement.Rig, out value) && ReferenceEquals(value, replacement.Root.transform), "exact Transform correspondence", ref checks);
                Require(!original.Rig.TryGetReplacement(first, replacement.Rig, out value) && value == null, "earlier duplicate component is rejected", ref checks);
                Require(original.Rig.TryGetReplacement(last, replacement.Rig, out value) && ReferenceEquals(value, replacementLast), "last exact component type wins", ref checks);
                Require(original.Rig.TryGetReplacement(original.Additional, replacement.Rig, out value) && ReferenceEquals(value, replacement.Additional), "additional object index", ref checks);
                Require(!original.Rig.TryGetReplacement(owned.New("Uncached"), replacement.Rig, out value) && value == null, "uncached object clears output", ref checks);
                BoxCollider later = original.Root.AddComponent<BoxCollider>();
                Require(!original.Rig.TryGetReplacement(later, replacement.Rig, out value) && value == null, "cache is a snapshot", ref checks);
                Set(original.Rig, "m_additionalObjects", new[] { original.Root });
                original.Rig.Initialize(); replacement.Rig.Initialize();
                Require(Cache(original.Rig).Count == 8 && Cache(replacement.Rig).Count == 8, "initialization appends without clearing", ref checks);
                Require(!original.Rig.TryGetReplacement(later, replacement.Rig, out value) && value == null, "first matching object retains failure despite later matching cache", ref checks);
                Require(original.Rig.TryGetReplacement(last, replacement.Rig, out value) && ReferenceEquals(value, replacementLast), "first old cache retains previous component identity", ref checks);
                Throws<NullReferenceException>(() => original.Rig.TryGetReplacement(null, replacement.Rig, out _), "null input faults before traversal", ref checks);
                var uninitialized = new RigObjects(owned, "Uninitialized");
                Throws<ArgumentOutOfRangeException>(() => original.Rig.TryGetReplacement(original.Root, uninitialized.Rig, out _), "replacement cache is not padded", ref checks);
                Set(uninitialized.Rig, "m_meshAnimator", null);
                Throws<NullReferenceException>(() => uninitialized.Rig.Initialize(), "initialization retains missing mesh fault", ref checks);
                Require(Cache(uninitialized.Rig).Count == 2, "root and character caches survive later failure", ref checks);
            }
            return checks;
        }

        public static int AttachmentParenting()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                var original = new RigObjects(owned, "Original"); var replacement = new RigObjects(owned, "Replacement");
                Transform empty = owned.New("Empty attachment").transform;
                Transform source = owned.New("Source attachment").transform;
                Transform target = owned.New("Target attachment").transform;
                source.SetPositionAndRotation(new Vector3(4, 3, 2), Quaternion.Euler(10, 40, 5));
                target.SetPositionAndRotation(new Vector3(-9, 6, 1), Quaternion.Euler(30, -60, 20));
                Transform existing = owned.New("Existing", target).transform;
                var children = new Transform[3]; var positions = new Vector3[3]; var rotations = new Quaternion[3];
                for (int i = 0; i < children.Length; ++i)
                {
                    children[i] = owned.New("Child " + i, source).transform;
                    children[i].SetLocalPositionAndRotation(new Vector3(i + 1, 2 - i, i * 3), Quaternion.Euler(i * 15, i * 20, i * 25));
                    positions[i] = children[i].position; rotations[i] = children[i].rotation;
                }
                Transform[] points = { null, empty, source };
                Set(original.Rig, "m_attachPoints", points); Set(replacement.Rig, "m_attachPoints", new[] { null, null, target });
                Require(ReferenceEquals(original.Rig.AttachPoints, points), "attachment getter preserves array identity", ref checks);
                original.Rig.MoveAttachments(replacement.Rig);
                Require(source.childCount == 0 && empty.childCount == 0 && target.childCount == 4, "null and empty points skipped", ref checks);
                Require(ReferenceEquals(target.GetChild(0), existing), "existing target child stays first", ref checks);
                for (int i = 0; i < children.Length; ++i)
                {
                    Require(ReferenceEquals(children[i].parent, target), "child reparented", ref checks);
                    Vector(children[i].position, positions[i], "world position retained", ref checks);
                    Rotation(children[i].rotation, rotations[i], "world rotation retained", ref checks);
                    Require(ReferenceEquals(target.GetChild(3 - i), children[i]), "reverse traversal becomes reverse sibling order", ref checks);
                }
                Set(original.Rig, "m_attachPoints", new[] { empty }); Set(replacement.Rig, "m_attachPoints", Array.Empty<Transform>());
                original.Rig.MoveAttachments(replacement.Rig);
                Require(empty.childCount == 0, "empty source does not access missing replacement", ref checks);
                Transform stranded = owned.New("Stranded", empty).transform;
                Throws<IndexOutOfRangeException>(() => original.Rig.MoveAttachments(replacement.Rig), "populated source accesses authored replacement index", ref checks);
                Require(ReferenceEquals(stranded.parent, empty), "index fault precedes child movement", ref checks);
            }
            return checks;
        }

        public static int RigLocalPoses()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                var original = new RigObjects(owned, "Original"); var replacement = new RigObjects(owned, "Replacement");
                Transform[] from = { original.Root.transform, original.CharacterAnimator.transform, original.MeshAnimator.transform };
                Transform[] to = { replacement.Root.transform, replacement.CharacterAnimator.transform, replacement.MeshAnimator.transform };
                var scales = new Vector3[3];
                for (int i = 0; i < from.Length; ++i)
                {
                    from[i].SetLocalPositionAndRotation(new Vector3(i + 2, -i - 1, i * 4), Quaternion.Euler(i * 17, i * 31, i * 7));
                    to[i].localScale = scales[i] = new Vector3(i + 4, i + 5, i + 6);
                }
                replacement.CharacterAnimator.enabled = false; replacement.MeshAnimator.enabled = false;
                original.Rig.SetPositions(replacement.Rig);
                for (int i = 0; i < from.Length; ++i)
                {
                    Vector(to[i].localPosition, from[i].localPosition, "authored local position", ref checks);
                    Rotation(to[i].localRotation, from[i].localRotation, "authored local rotation", ref checks);
                    Vector(to[i].localScale, scales[i], "replacement scale retained", ref checks);
                }
                Require(replacement.CharacterAnimator.enabled && !replacement.MeshAnimator.enabled, "only character animator enabled", ref checks);
                original.Root.transform.localPosition = new Vector3(51, 52, 53);
                original.CharacterAnimator.transform.localPosition = new Vector3(61, 62, 63);
                replacement.CharacterAnimator.enabled = false; Set(replacement.Rig, "m_meshAnimator", null);
                Throws<NullReferenceException>(() => original.Rig.SetPositions(replacement.Rig), "missing mesh faults last", ref checks);
                Vector(replacement.Root.transform.localPosition, original.Root.transform.localPosition, "root change survives mesh fault", ref checks);
                Vector(replacement.CharacterAnimator.transform.localPosition, original.CharacterAnimator.transform.localPosition, "character change survives mesh fault", ref checks);
                Require(replacement.CharacterAnimator.enabled, "enable precedes mesh fault", ref checks);
            }
            return checks;
        }

        public static int GhostMaterialCorrespondence()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                var rig = new RigObjects(owned, "Materials");
                Renderer first = owned.New("First renderer").AddComponent<MeshRenderer>();
                Renderer second = owned.New("Second renderer").AddComponent<MeshRenderer>();
                Material upper = owned.Material("GHOSTED"), plain = owned.Material("Plain"), ghost = owned.Material("skin_ghosted_variant"), extra = owned.Material("Extra");
                Set(rig.Rig, "m_meshRenderers", new[] { first, second });
                first.sharedMaterials = new[] { upper, plain }; second.sharedMaterials = new[] { ghost };
                Require(!rig.Rig.IsGhosted(), "case-sensitive first renderer only", ref checks);
                first.sharedMaterials = new[] { plain, ghost };
                Require(rig.Rig.IsGhosted(), "substring on any first-renderer material", ref checks);
                first.sharedMaterials = Array.Empty<Material>();
                Require(!rig.Rig.IsGhosted(), "empty first renderer", ref checks);
                SetGhostLists(rig.Rig, new[] { new List<Material> { ghost, extra }, new List<Material> { plain } });
                rig.Rig.ApplyGhostedMaterials();
                Require(first.sharedMaterials.Length == 2 && ReferenceEquals(first.sharedMaterials[0], ghost) && ReferenceEquals(first.sharedMaterials[1], extra), "first exact material list", ref checks);
                Require(second.sharedMaterials.Length == 1 && ReferenceEquals(second.sharedMaterials[0], plain), "second exact material list", ref checks);
                SetGhostLists(rig.Rig, new[] { new List<Material> { upper } });
                Throws<ArgumentOutOfRangeException>(() => rig.Rig.ApplyGhostedMaterials(), "authored list length not padded", ref checks);
                Require(first.sharedMaterials.Length == 1 && ReferenceEquals(first.sharedMaterials[0], upper), "earlier renderer survives later list fault", ref checks);
                Require(second.sharedMaterials.Length == 1 && ReferenceEquals(second.sharedMaterials[0], plain), "later renderer unchanged after fault", ref checks);
                Set(rig.Rig, "m_meshRenderers", Array.Empty<Renderer>());
                Throws<IndexOutOfRangeException>(() => rig.Rig.IsGhosted(), "first renderer remains required", ref checks);
            }
            return checks;
        }

        public static int SubstituteWorldPose()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                var substitute = owned.New("Substitution owner").AddComponent<SubstituteObject>();
                Transform parent = owned.New("Real parent").transform;
                parent.SetPositionAndRotation(new Vector3(10, -3, 20), Quaternion.Euler(0, 70, 0));
                GameObject real = owned.New("Real object", parent);
                Transform animation = owned.New("Animation root", real.transform).transform;
                animation.SetLocalPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(5, 20, 10));
                Transform replacementParent = owned.New("Replacement parent").transform;
                Transform replacement = owned.New("Replacement", replacementParent).transform;
                real.transform.SetPositionAndRotation(new Vector3(3, 4, 5), Quaternion.Euler(30, 50, 70));
                Configure(substitute, real, animation, replacement);
                Set(substitute, "m_realObjectPosition", true); Set(substitute, "m_realObjectRotation", true);
                real.SetActive(true); animation.gameObject.SetActive(false);
                Invoke(substitute, "OnEnable");
                Require(!real.activeSelf && !animation.gameObject.activeSelf, "enable hides only real object", ref checks);
                Vector(replacement.position, real.transform.position, "selected world position copied", ref checks);
                Rotation(replacement.rotation, real.transform.rotation, "selected world rotation copied", ref checks);
                Require(ReferenceEquals(replacement.parent, replacementParent), "nonpersistent parent retained", ref checks);
                Set(substitute, "m_substitutePosition", true); Set(substitute, "m_substituteRotation", true);
                replacement.SetPositionAndRotation(new Vector3(40, 50, 60), Quaternion.Euler(12, 80, 35));
                Vector3 expectedPosition = replacement.position - animation.localPosition;
                Quaternion expectedRotation = replacement.rotation * Quaternion.Inverse(animation.localRotation);
                Invoke(substitute, "OnDisable");
                Require(real.activeSelf && animation.gameObject.activeSelf, "disable reactivates real and animation root", ref checks);
                Vector(real.transform.position, expectedPosition, "local animation offset subtracted directly from world position", ref checks);
                Rotation(real.transform.rotation, expectedRotation, "replacement times inverse local animation rotation", ref checks);
                Require(ReferenceEquals(replacement.parent, replacementParent), "disable leaves replacement parent", ref checks);
                Set(substitute, "m_realObjectPosition", false); Set(substitute, "m_realObjectRotation", false);
                replacement.SetPositionAndRotation(new Vector3(91, 92, 93), Quaternion.Euler(1, 2, 3));
                Vector3 priorPosition = replacement.position; Quaternion priorRotation = replacement.rotation;
                Invoke(substitute, "OnEnable");
                Vector(replacement.position, priorPosition, "false position flag retains substitute pose", ref checks);
                Rotation(replacement.rotation, priorRotation, "false rotation flag retains substitute pose", ref checks);
                Set(substitute, "m_substitutePosition", false); Set(substitute, "m_substituteRotation", false);
                priorPosition = real.transform.position; priorRotation = real.transform.rotation;
                Invoke(substitute, "OnDisable");
                Vector(real.transform.position, priorPosition, "false restore-position flag", ref checks);
                Rotation(real.transform.rotation, priorRotation, "false restore-rotation flag", ref checks);
                Set(substitute, "m_realObjectPosition", true); Set(substitute, "m_replaceWithTransform", null); real.SetActive(true);
                Throws<NullReferenceException>(() => Invoke(substitute, "OnEnable"), "missing target retains original fault", ref checks);
                Require(!real.activeSelf, "hide precedes missing-target fault", ref checks);
                Set(substitute, "m_realAnimationRoot", null);
                Throws<NullReferenceException>(() => Invoke(substitute, "OnDisable"), "missing animation root fault", ref checks);
                Require(real.activeSelf, "real activation precedes animation-root fault", ref checks);
            }
            return checks;
        }

        public static int SubstitutePersistence()
        {
            int checks = 0;
            using (var owned = new OwnedObjects())
            {
                var substitute = owned.New("Persistent owner").AddComponent<SubstituteObject>();
                string[] flags = { "m_realObjectPosition", "m_realObjectRotation", "m_substitutePosition", "m_substituteRotation", "m_reparentToPersistCutsceneEnd" };
                foreach (string name in flags) Require(!(bool)Get(substitute, name), "original false flag " + name, ref checks);
                Transform parent = owned.New("Real parent").transform;
                parent.SetPositionAndRotation(new Vector3(8, 7, 6), Quaternion.Euler(20, 30, 40));
                GameObject real = owned.New("Real", parent); Transform animation = owned.New("Animation", real.transform).transform;
                Transform replacement = owned.New("Persisted substitute").transform;
                replacement.SetPositionAndRotation(new Vector3(11, 12, 13), Quaternion.Euler(50, 60, 70));
                Vector3 position = replacement.position; Quaternion rotation = replacement.rotation;
                Configure(substitute, real, animation, replacement); Set(substitute, "m_reparentToPersistCutsceneEnd", true);
                real.SetActive(true); Invoke(substitute, "OnEnable");
                Require(!real.activeSelf, "persistent enable hides original", ref checks);
                Require(ReferenceEquals(replacement.parent, parent), "persist under real parent", ref checks);
                Vector(replacement.position, position, "persistent reparent retains world position", ref checks);
                Rotation(replacement.rotation, rotation, "persistent reparent retains world rotation", ref checks);
                Set(substitute, "m_replacedObject", null); Set(substitute, "m_realAnimationRoot", null); Set(substitute, "m_replaceWithTransform", null);
                Invoke(substitute, "OnDisable");
                Require(!real.activeSelf, "persistent disable bypasses all real-object accesses", ref checks);
                Require(ReferenceEquals(replacement.parent, parent), "persisted substitute remains parented", ref checks);
                Vector(replacement.position, position, "persistent disable retains position", ref checks);
                Rotation(replacement.rotation, rotation, "persistent disable retains rotation", ref checks);
            }
            return checks;
        }

        private static void Configure(SubstituteObject substitute, GameObject real, Transform animation, Transform replacement)
        { Set(substitute, "m_replacedObject", real); Set(substitute, "m_realAnimationRoot", animation); Set(substitute, "m_replaceWithTransform", replacement); }
        private static IList Cache(CharacterRigLookup rig) => (IList)Get(rig, "m_objectCache");
        private static object Get(object owner, string name) => owner.GetType().GetField(name, Own).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Own).SetValue(owner, value);
        private static void SetGhostLists(CharacterRigLookup rig, List<Material>[] materials)
        {
            Type item = typeof(CharacterRigLookup).GetNestedType("MaterialList", BindingFlags.NonPublic);
            IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item));
            foreach (List<Material> value in materials)
            {
                object row = Activator.CreateInstance(item);
                item.GetField("Materials", Own).SetValue(row, value); list.Add(row);
            }
            Set(rig, "m_ghostedMaterials", list);
        }
        private static void Invoke(object owner, string name)
        {
            try { owner.GetType().GetMethod(name, Own).Invoke(owner, null); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }
        private static void Vector(Vector3 actual, Vector3 expected, string label, ref int checks) =>
            Require((actual - expected).sqrMagnitude < 0.000001f, label, ref checks);
        private static void Rotation(Quaternion actual, Quaternion expected, string label, ref int checks) =>
            Require(Quaternion.Angle(actual, expected) < 0.05f, label, ref checks);
        private static void Require(bool condition, string label, ref int checks)
        { if (!condition) throw new InvalidOperationException("Original character rig preservation: " + label); ++checks; }
        private static void Throws<T>(Action action, string label, ref int checks) where T : Exception
        {
            try { action(); } catch (T) { ++checks; return; }
            throw new InvalidOperationException("Original character rig preservation expected " + typeof(T).Name + ": " + label);
        }
    }
}
