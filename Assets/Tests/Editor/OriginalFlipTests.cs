using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;
using OriginalFlip = Hardlight.Flip;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Specification of Hardlight.Flip (HLUnityUI.Runtime 0x0200005f, methods
    // 0x060002c6..cd) from the shipped x86-64 bodies. Every case owns its
    // GameObjects and destroys them; Graphic.OnDisable removes the owned Image
    // from CanvasUpdateRegistry, and each dirty callback is unregistered.
    [TestFixture]
    public sealed class OriginalFlipTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // dump.cs TypeDefIndex 11919: attribute, field and method declaration order.
        [Test]
        public void DeclarationKeepsTheOriginalAttributesFieldsAndMethodOrder()
        {
            Type type = typeof(OriginalFlip);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("Hardlight"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(BaseMeshEffect)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);

            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Count, Is.EqualTo(3));
            // dump.cs lists ArrayBoundsChecks (2) before NullChecks (1) for Flip.
            for (int i = 0; i < 2; ++i)
            {
                Assert.That(attributes[i].AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
                Assert.That(Convert.ToInt32(attributes[i].ConstructorArguments[0].Value), Is.EqualTo(2 - i));
                Assert.That(attributes[i].ConstructorArguments[1].Value, Is.EqualTo(false));
                Assert.That(attributes[i].NamedArguments, Is.Empty);
            }
            Assert.That(attributes[2].AttributeType, Is.EqualTo(typeof(AddComponentMenu)));
            Assert.That(attributes[2].ConstructorArguments.Select(a => a.Value).ToArray(), Is.EqualTo(new object[] { "Hardlight UI/Effects/Flip Sprite", 1 }));
            Assert.That(attributes[2].NamedArguments, Is.Empty);

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "flipHorizontal", "flipVertical" }));
            foreach (FieldInfo field in fields)
            {
                Assert.That(field.FieldType, Is.EqualTo(typeof(bool)));
                Assert.That(field.Attributes, Is.EqualTo(FieldAttributes.Private));
                Assert.That(field.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }));
            }

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[]
            {
                "ModifyMesh", "ModifyVertices", "FlipHorizontal", "FlipVertical",
                "SetVerticalFlip", "SetHorizontalFlip", "MarkDirty",
            }));
            CheckMethod(methods[0], 198, new[] { typeof(VertexHelper) }, new[] { "vh" });
            CheckMethod(methods[1], 134, new[] { typeof(List<UIVertex>) }, new[] { "verts" });
            CheckMethod(methods[2], 134, Type.EmptyTypes, new string[0]);
            CheckMethod(methods[3], 134, Type.EmptyTypes, new string[0]);
            CheckMethod(methods[4], 134, new[] { typeof(bool) }, new[] { "on" });
            CheckMethod(methods[5], 134, new[] { typeof(bool) }, new[] { "on" });
            CheckMethod(methods[6], 129, Type.EmptyTypes, new string[0]);
            Assert.That(methods[0].GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(BaseMeshEffect)));

            ConstructorInfo constructor = type.GetConstructors(Declared).Single();
            Assert.That((int)constructor.Attributes, Is.EqualTo(6278));
            Assert.That(constructor.GetParameters(), Is.Empty);
        }

        // .ctor 0x1b4dfc0 only tail-calls BaseMeshEffect..ctor: no field initialisers.
        [Test]
        public void FreshComponentStartsWithBothFlipsOff()
        {
            using (var owned = new Owned(withGraphic: false))
            {
                Assert.That(Horizontal(owned.Flip), Is.False);
                Assert.That(Vertical(owned.Flip), Is.False);
                using (var serialized = new SerializedObject(owned.Flip))
                {
                    Assert.That(serialized.FindProperty("flipHorizontal").boolValue, Is.False);
                    Assert.That(serialized.FindProperty("flipVertical").boolValue, Is.False);
                }
            }
        }

        // ModifyVertices 0x1b4da30: an enumerator pass (GetEnumerator/MoveNext/Current,
        // no Dispose; 0x1b4db10..0x1b4db6c) takes min/max of position.xy only
        // (minps/maxps seeded with +/-float.MaxValue), sums them once (addps 0x1b4db6e),
        // then an index pass writes x' = (min.x + max.x) - x where flipHorizontal
        // (+0x28) and y' = (min.y + max.y) - y where flipVertical (+0x29): blendvps
        // keeps each unflipped lane. z and every other channel are copied back intact.
        // The pivot is the vertex bounds, not the rect or the origin; fails for a zero
        // seed, a rect pivot, negation, swapped flag offsets or a one-pass rewrite.
        // x in [10, 30] mirrors with sum 40; y in [-3, 5] mirrors with sum 2.
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ModifyVerticesMirrorsEachFlaggedAxisAcrossTheVertexBounds(bool horizontal, bool vertical)
        {
            float[] xs = horizontal ? new[] { 30f, 10f, 28f, 24f } : new[] { 10f, 30f, 12f, 16f };
            float[] ys = vertical ? new[] { 5f, -2f, -3f, 1f } : new[] { -3f, 4f, 5f, 1f };
            using (var owned = new Owned(withGraphic: false))
            {
                SetFlags(owned.Flip, horizontal, vertical);
                List<UIVertex> before = Vertices(new Vector3(10f, -3f, 5f), new Vector3(30f, 4f, 6f), new Vector3(12f, 5f, -7f), new Vector3(16f, 1f, 0.5f));
                List<UIVertex> verts = new List<UIVertex>(before);
                owned.Flip.ModifyVertices(verts);
                Assert.That(verts.Count, Is.EqualTo(4));
                for (int i = 0; i < verts.Count; ++i)
                {
                    Assert.That(verts[i].position, Is.EqualTo(new Vector3(xs[i], ys[i], before[i].position.z)), "vertex " + i);
                    CheckOtherChannels(before[i], verts[i]);
                }
                Assert.That(Horizontal(owned.Flip), Is.EqualTo(horizontal));
                Assert.That(Vertical(owned.Flip), Is.EqualTo(vertical));
            }
        }

        // 0x1b4dadd..0x1b4daf3: Count == 0 returns, then both flags clear returns,
        // so neither the bounds pass nor set_Item runs.
        [Test]
        public void ModifyVerticesLeavesEmptyListsAndUnflaggedVerticesUntouched()
        {
            using (var owned = new Owned(withGraphic: false))
            {
                SetFlags(owned.Flip, true, true);
                var empty = new List<UIVertex>();
                Assert.DoesNotThrow(() => owned.Flip.ModifyVertices(empty));
                Assert.That(empty, Is.Empty);

                SetFlags(owned.Flip, false, false);
                List<UIVertex> before = Vertices(new Vector3(float.NaN, 2f, 1f), new Vector3(8f, 6f, 2f), new Vector3(3f, -9f, 3f));
                List<UIVertex> verts = new List<UIVertex>(before);
                owned.Flip.ModifyVertices(verts);
                for (int i = 0; i < verts.Count; ++i)
                {
                    Assert.That(verts[i].position.y, Is.EqualTo(before[i].position.y));
                    CheckOtherChannels(before[i], verts[i]);
                }
                Assert.That(float.IsNaN(verts[0].position.x), Is.True);
                Assert.That(verts[1].position.x, Is.EqualTo(8f));
                Assert.That(verts[2].position.x, Is.EqualTo(3f));
            }
        }

        // 0x1b4dadd reads verts.Count (+0x18) before either flag, so a null list
        // faults even when nothing is flagged; a flags-first guard would return.
        [Test]
        public void ModifyVerticesReadsTheCountBeforeTheFlags()
        {
            using (var owned = new Owned(withGraphic: false))
            {
                SetFlags(owned.Flip, false, false);
                Assert.Throws<NullReferenceException>(() => owned.Flip.ModifyVertices(null));
            }
        }

        // A single vertex has min == max, so (p + p) - p maps it onto itself.
        [Test]
        public void ModifyVerticesKeepsASingleVertexInPlace()
        {
            using (var owned = new Owned(withGraphic: false))
            {
                SetFlags(owned.Flip, true, true);
                List<UIVertex> verts = Vertices(new Vector3(-6f, 12.5f, 3f));
                owned.Flip.ModifyVertices(verts);
                Assert.That(verts[0].position, Is.EqualTo(new Vector3(-6f, 12.5f, 3f)));
            }
        }

        // maxps xmm2(max), xmm0(p) at 0x1b4db48 yields max > p ? max : p and
        // minps xmm1(min), xmm0(p) yields min < p ? min : p (Vector2.Max/Min(bound, p)).
        // A NaN x therefore replaces both bounds, and the next vertex replaces the NaN:
        // x = [10, NaN, 2, 4] gives bounds [2, 4], sum 6. Swapped operands
        // (Max(p, bound)) skip the NaN and give [2, 10]. The NaN vertex stays NaN, and
        // with only flipVertical the NaN x lane is copied back unchanged.
        [Test]
        public void ModifyVerticesBoundsFollowTheMinMaxOperandOrderForNaN()
        {
            using (var owned = new Owned(withGraphic: false))
            {
                SetFlags(owned.Flip, true, false);
                List<UIVertex> verts = Vertices(new Vector3(10f, 0f, 0f), new Vector3(float.NaN, 1f, 0f), new Vector3(2f, 2f, 0f), new Vector3(4f, 3f, 0f));
                owned.Flip.ModifyVertices(verts);
                Assert.That(verts[0].position.x, Is.EqualTo(-4f));
                Assert.That(float.IsNaN(verts[1].position.x), Is.True);
                Assert.That(verts[2].position.x, Is.EqualTo(4f));
                Assert.That(verts[3].position.x, Is.EqualTo(2f));
                Assert.That(verts.Select(v => v.position.y).ToArray(), Is.EqualTo(new[] { 0f, 1f, 2f, 3f }));

                SetFlags(owned.Flip, false, true);
                verts = Vertices(new Vector3(float.NaN, 0f, 0f), new Vector3(5f, 8f, 0f));
                owned.Flip.ModifyVertices(verts);
                Assert.That(float.IsNaN(verts[0].position.x), Is.True);
                Assert.That(verts[0].position.y, Is.EqualTo(8f));
                Assert.That(verts[1].position, Is.EqualTo(new Vector3(5f, 0f, 0f)));
            }
        }

        // ModifyMesh 0x1b4d990: new List, vh.GetUIVertexStream, ModifyVertices,
        // vh.Clear, vh.AddUIVertexTriangleStream. A two-triangle quad therefore comes
        // back as six unindexed stream vertices in index order, mirrored.
        [Test]
        public void ModifyMeshRebuildsTheHelperFromTheMirroredTriangleStream()
        {
            // ModifyMesh never reads graphic, so the fixture needs no Image or activation.
            using (var owned = new Owned(withGraphic: false))
            using (VertexHelper vh = Quad())
            {
                SetFlags(owned.Flip, true, false);
                List<UIVertex> before = Stream(vh);
                owned.Flip.ModifyMesh(vh);
                Assert.That(vh.currentVertCount, Is.EqualTo(6));
                Assert.That(vh.currentIndexCount, Is.EqualTo(6));
                List<UIVertex> after = Stream(vh);
                float[] xs = { 20f, 20f, 0f, 0f, 0f, 20f };
                for (int i = 0; i < 6; ++i)
                {
                    Assert.That(after[i].position, Is.EqualTo(new Vector3(xs[i], before[i].position.y, before[i].position.z)), "stream vertex " + i);
                    CheckOtherChannels(before[i], after[i]);
                }
            }
        }

        // ModifyMesh has no IsActive guard and no flag check of its own: a disabled,
        // graphic-less Flip with both flips off still re-expands the indexed quad into
        // a six-vertex triangle stream, and with a flip set it still mirrors.
        [Test]
        public void ModifyMeshRunsWithoutAnActiveGuardOrFlagShortcut()
        {
            using (var owned = new Owned(withGraphic: false))
            {
                owned.Flip.enabled = false;
                Assert.That(owned.Flip.IsActive(), Is.False);
                using (VertexHelper vh = Quad())
                {
                    List<UIVertex> before = Stream(vh);
                    owned.Flip.ModifyMesh(vh);
                    Assert.That(vh.currentVertCount, Is.EqualTo(6));
                    Assert.That(vh.currentIndexCount, Is.EqualTo(6));
                    List<UIVertex> after = Stream(vh);
                    for (int i = 0; i < 6; ++i) Assert.That(after[i].position, Is.EqualTo(before[i].position));
                }
                SetFlags(owned.Flip, false, true);
                using (VertexHelper vh = Quad())
                {
                    owned.Flip.ModifyMesh(vh);
                    float[] ys = { 10f, 0f, 0f, 0f, 10f, 10f };
                    List<UIVertex> after = Stream(vh);
                    for (int i = 0; i < 6; ++i) Assert.That(after[i].position.y, Is.EqualTo(ys[i]), "stream vertex " + i);
                }
            }
        }

        // SetHorizontalFlip 0x1b4df30 / SetVerticalFlip 0x1b4dea0: equal value returns
        // at once; otherwise the field (+0x28 / +0x29) is written first and then,
        // if graphic != null, graphic.SetVerticesDirty (Graphic vtable slot 28,
        // [klass + 0x2f8]) is tail-called. Never SetAllDirty, layout or material.
        [TestCase(true)]
        [TestCase(false)]
        public void SettersWriteOnlyTheirOwnFieldAndDirtyVerticesOnlyOnChange(bool horizontal)
        {
            using (var owned = new Owned(withGraphic: true))
            {
                owned.Activate();
                using (var probe = new DirtyProbe(owned))
                {
                    Action<bool> set = on => { if (horizontal) owned.Flip.SetHorizontalFlip(on); else owned.Flip.SetVerticalFlip(on); };
                    Func<bool> own = () => horizontal ? Horizontal(owned.Flip) : Vertical(owned.Flip);
                    Func<bool> other = () => horizontal ? Vertical(owned.Flip) : Horizontal(owned.Flip);
                    Func<bool, bool, (bool, bool)> state = (mine, theirs) => horizontal ? (mine, theirs) : (theirs, mine);

                    set(false);
                    Assert.That(probe.Vertices, Is.Empty, "equal value returns before dirtying");

                    set(true);
                    Assert.That(own(), Is.True);
                    Assert.That(other(), Is.False);
                    Assert.That(probe.Vertices, Is.EqualTo(new[] { state(true, false) }), "dirtied once, after the field write");

                    set(true);
                    Assert.That(probe.Vertices.Count, Is.EqualTo(1), "equal value returns before dirtying");

                    set(false);
                    Assert.That(own(), Is.False);
                    Assert.That(probe.Vertices, Is.EqualTo(new[] { state(true, false), state(false, false) }));
                    Assert.That(probe.Layout, Is.Zero);
                    Assert.That(probe.Material, Is.Zero);
                }
            }
        }

        // FlipHorizontal 0x1b4dcf0 / FlipVertical 0x1b4de10: xor the field with 1
        // unconditionally, then dirty vertices through the same graphic null check.
        [TestCase(true)]
        [TestCase(false)]
        public void TogglesInvertOnlyTheirOwnFieldAndDirtyVerticesEveryCall(bool horizontal)
        {
            using (var owned = new Owned(withGraphic: true))
            {
                owned.Activate();
                using (var probe = new DirtyProbe(owned))
                {
                    Action toggle = () => { if (horizontal) owned.Flip.FlipHorizontal(); else owned.Flip.FlipVertical(); };
                    Func<bool> other = () => horizontal ? Vertical(owned.Flip) : Horizontal(owned.Flip);
                    (bool, bool) on = horizontal ? (true, false) : (false, true);

                    toggle();
                    toggle();
                    toggle();
                    Assert.That(probe.Vertices, Is.EqualTo(new[] { on, (false, false), on }));
                    Assert.That(other(), Is.False);
                    Assert.That(probe.Layout, Is.Zero);
                    Assert.That(probe.Material, Is.Zero);
                }
            }
        }

        // MarkDirty 0x1b4dd80 is the same graphic-guarded SetVerticesDirty, with no
        // field change. None of the dirtying bodies check the Flip's own enabled
        // state, so a disabled Flip still dirties its graphic.
        [Test]
        public void MarkDirtyAndSettersDirtyTheGraphicEvenWhenTheFlipIsDisabled()
        {
            using (var owned = new Owned(withGraphic: true))
            {
                owned.Activate();
                owned.Flip.enabled = false;
                using (var probe = new DirtyProbe(owned))
                {
                    MethodInfo markDirty = typeof(OriginalFlip).GetMethod("MarkDirty", Declared);
                    markDirty.Invoke(owned.Flip, null);
                    Assert.That(probe.Vertices, Is.EqualTo(new[] { (false, false) }));
                    owned.Flip.SetVerticalFlip(true);
                    owned.Flip.FlipHorizontal();
                    // The FlipHorizontal entry (true, true) shows that toggle also writes before dirtying.
                    Assert.That(probe.Vertices, Is.EqualTo(new[] { (false, false), (false, true), (true, true) }));
                    Assert.That(probe.Layout, Is.Zero);
                    Assert.That(probe.Material, Is.Zero);
                }
            }
        }

        // Without a Graphic the op_Equality(graphic, null) branch returns after the
        // field write: no exception, state still changes.
        [Test]
        public void MutatorsWithoutAGraphicStillWriteTheFields()
        {
            using (var owned = new Owned(withGraphic: false))
            {
                Assert.DoesNotThrow(() => owned.Flip.SetHorizontalFlip(true));
                Assert.That(Horizontal(owned.Flip), Is.True);
                Assert.DoesNotThrow(() => owned.Flip.FlipVertical());
                Assert.That(Vertical(owned.Flip), Is.True);
                Assert.DoesNotThrow(() => owned.Flip.SetVerticalFlip(false));
                Assert.That(Vertical(owned.Flip), Is.False);
                Assert.DoesNotThrow(() => owned.Flip.FlipHorizontal());
                Assert.That(Horizontal(owned.Flip), Is.False);
                Assert.DoesNotThrow(() => typeof(OriginalFlip).GetMethod("MarkDirty", Declared).Invoke(owned.Flip, null));
            }
        }

        // Owns a GameObject, created inactive (Activate() enables it), with an optional
        // Image and the Flip; disposing destroys it, which disables the Image and drops
        // it from CanvasUpdateRegistry.
        sealed class Owned : IDisposable
        {
            readonly GameObject owner;
            public readonly Image Image;
            public readonly OriginalFlip Flip;

            public Owned(bool withGraphic)
            {
                owner = new GameObject("OriginalFlip fixture", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    owner.SetActive(false);
                    if (withGraphic) Image = owner.AddComponent<Image>();
                    Flip = owner.AddComponent<OriginalFlip>();
                }
                catch { Object.DestroyImmediate(owner); throw; }
            }

            public void Activate()
            {
                owner.SetActive(true);
                Assert.That(Image.IsActive(), Is.True);
            }

            public void Dispose() { if (owner != null) Object.DestroyImmediate(owner); }
        }

        // Records each Graphic dirty callback; each vertices entry is the
        // (flipHorizontal, flipVertical) pair seen inside the callback, which proves
        // the field write happens before the dirty call.
        sealed class DirtyProbe : IDisposable
        {
            readonly Owned owned;
            public readonly List<(bool, bool)> Vertices = new List<(bool, bool)>();
            public int Layout;
            public int Material;

            public DirtyProbe(Owned owned)
            {
                this.owned = owned;
                owned.Image.RegisterDirtyVerticesCallback(OnVertices);
                owned.Image.RegisterDirtyLayoutCallback(OnLayout);
                owned.Image.RegisterDirtyMaterialCallback(OnMaterial);
            }

            void OnVertices() { Vertices.Add((Horizontal(owned.Flip), Vertical(owned.Flip))); }
            void OnLayout() { ++Layout; }
            void OnMaterial() { ++Material; }

            public void Dispose()
            {
                owned.Image.UnregisterDirtyVerticesCallback(OnVertices);
                owned.Image.UnregisterDirtyLayoutCallback(OnLayout);
                owned.Image.UnregisterDirtyMaterialCallback(OnMaterial);
            }
        }

        static bool Horizontal(OriginalFlip flip) { return (bool)Field("flipHorizontal").GetValue(flip); }
        static bool Vertical(OriginalFlip flip) { return (bool)Field("flipVertical").GetValue(flip); }

        static FieldInfo Field(string name)
        {
            FieldInfo field = typeof(OriginalFlip).GetField(name, Instance);
            Assert.That(field, Is.Not.Null, name);
            return field;
        }

        static void SetFlags(OriginalFlip flip, bool horizontal, bool vertical)
        {
            Field("flipHorizontal").SetValue(flip, horizontal);
            Field("flipVertical").SetValue(flip, vertical);
        }

        static List<UIVertex> Vertices(params Vector3[] positions)
        {
            var result = new List<UIVertex>();
            for (int i = 0; i < positions.Length; ++i)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = positions[i];
                vertex.color = new Color32((byte)(7 + i), 11, 13, 17);
                vertex.normal = new Vector3(0f, i, -1f);
                vertex.tangent = new Vector4(1f, 0f, i, -1f);
                vertex.uv0 = new Vector4(i, 2f, 3f, 4f);
                vertex.uv1 = new Vector4(5f, i, 7f, 8f);
                vertex.uv2 = new Vector4(9f, 10f, i, 12f);
                vertex.uv3 = new Vector4(13f, 14f, 15f, i);
                result.Add(vertex);
            }
            return result;
        }

        // Quad (0,0) (0,10) (20,10) (20,0) at z = 1..4, triangles 0-1-2 and 2-3-0:
        // the stream order is 0,1,2,2,3,0, bounds x [0, 20], y [0, 10].
        static VertexHelper Quad()
        {
            var vh = new VertexHelper();
            try
            {
                List<UIVertex> corners = Vertices(new Vector3(0f, 0f, 1f), new Vector3(0f, 10f, 2f), new Vector3(20f, 10f, 3f), new Vector3(20f, 0f, 4f));
                foreach (UIVertex corner in corners) vh.AddVert(corner);
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(2, 3, 0);
                return vh;
            }
            catch { vh.Dispose(); throw; }
        }

        static List<UIVertex> Stream(VertexHelper vh)
        {
            var result = new List<UIVertex>();
            vh.GetUIVertexStream(result);
            return result;
        }

        static void CheckOtherChannels(UIVertex before, UIVertex after)
        {
            Assert.That(after.color, Is.EqualTo(before.color));
            Assert.That(after.normal, Is.EqualTo(before.normal));
            Assert.That(after.tangent, Is.EqualTo(before.tangent));
            Assert.That(after.uv0, Is.EqualTo(before.uv0));
            Assert.That(after.uv1, Is.EqualTo(before.uv1));
            Assert.That(after.uv2, Is.EqualTo(before.uv2));
            Assert.That(after.uv3, Is.EqualTo(before.uv3));
        }

        static void CheckMethod(MethodInfo method, int flags, Type[] parameters, string[] names)
        {
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)), method.Name);
            Assert.That((int)method.Attributes, Is.EqualTo(flags), method.Name);
            Assert.That(method.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(parameters), method.Name);
            Assert.That(method.GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(names), method.Name);
            Assert.That(method.GetCustomAttributesData(), Is.Empty, method.Name);
        }
    }
}
