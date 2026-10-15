using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Low-priority HLSplines and HLPhysics gaps. HLPhysics.Runtime is not an
    // EditMode reference, so its genuine public octree API is reached by
    // reflection; nothing is constructed without its real constructor.
    public sealed class OriginalSplinePhysicsLowGapTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // InertRibbonKnot.Initialise 06000429 (x86-64 0x1a88830) stores the new
        // array into m_subKnots (+0x10, 0x1a888d6) and clears the alignment
        // cache (0x1a888f5) before the loop; each iteration stores a fresh
        // sub-knot (0x1a88920) before reading the source alignment
        // (0x1a88941). A null first source sub-knot faults on that read.
        // Building into a local array, or clearing the cache after the loop,
        // leaves the old array or the stale cached entry.
        [Test]
        public void InitialisePublishesTheArrayAndClearsTheCacheBeforeAnEntryFaults()
        {
            var ribbonOwner = new GameObject("Lucid inert knot ribbon");
            var knotOwner = new GameObject("Lucid inert knot source");
            ribbonOwner.SetActive(false);
            knotOwner.SetActive(false);
            try
            {
                EditableRibbon ribbon = ribbonOwner.AddComponent<EditableRibbon>();
                RibbonKnot source = knotOwner.AddComponent<RibbonKnot>();
                typeof(RibbonKnot).GetField("m_mainSubKnot", Instance).SetValue(source, null);
                ribbon.Knots.Clear();
                ribbon.Knots.Add(source);

                var knot = new InertRibbonKnot();
                var cached = new InertRibbonSubKnot();
                typeof(InertRibbonKnot).GetField("m_subKnots", Instance).SetValue(knot, new[] { cached });
                Assert.That(knot.GetSubKnot(RibbonAlignment.Center), Is.SameAs(cached));
                var cache = (Dictionary<RibbonAlignment, InertRibbonSubKnot>)typeof(InertRibbonKnot).GetField("m_cachedSubKnots", Instance).GetValue(knot);
                Assert.That(cache.Count, Is.EqualTo(1));

                Assert.Throws<NullReferenceException>(() => knot.Initialise(ribbon, 0));

                var published = (InertRibbonSubKnot[])typeof(InertRibbonKnot).GetField("m_subKnots", Instance).GetValue(knot);
                Assert.That(published.Length, Is.EqualTo(3), "the new array sized from the three source sub-knots is published");
                Assert.That(published[0], Is.Not.Null.And.Not.SameAs(cached), "the faulting entry was stored first");
                Assert.That(published[1], Is.Null);
                Assert.That(published[2], Is.Null);
                Assert.That(cache.Count, Is.Zero, "the cache was cleared before the loop");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(knotOwner);
                UnityEngine.Object.DestroyImmediate(ribbonOwner);
            }
        }

        // BoundsOctree<T>.Grow 0x06000008 (HLPhysics), shared x86-64 0xe43210:
        // for each non-root child, x is chosen by (i & 1) == 0 (sete 0xe43476), y by i >= 4
        // (setae 0xe4347f), and z by i < 2 or (i & ~1) == 4 (0xe43486..
        // 0xe434a7), each offset by half the old length from the new centre.
        // An occupied root of length 2 at the origin grows towards +1 to
        // centre (1,1,1); the old root keeps octant 4. A swapped sign fails.
        [Test]
        public void GrowthChildrenUseTheOriginalOctantSignPattern()
        {
            Type octreeType = Type.GetType("Hardlight.BoundsOctree`1, HLPhysics.Runtime", true).MakeGenericType(typeof(int));
            object tree = Activator.CreateInstance(octreeType, 2f, Vector3.zero, 1f, 1f);
            MethodInfo add = octreeType.GetMethod("Add", new[] { typeof(int), typeof(Bounds) });
            FieldInfo rootField = octreeType.GetField("m_rootNode", Instance);
            add.Invoke(tree, new object[] { 0, new Bounds(Vector3.zero, Vector3.one * 0.5f) });
            object oldRoot = rootField.GetValue(tree);
            add.Invoke(tree, new object[] { 1, new Bounds(new Vector3(1.5f, 1.5f, 1.5f), Vector3.one * 0.25f) });
            object root = rootField.GetValue(tree);
            Type nodeType = root.GetType();
            Assert.That(nodeType.GetProperty("Center").GetValue(root), Is.EqualTo(Vector3.one));
            Assert.That(nodeType.GetProperty("BaseLength").GetValue(root), Is.EqualTo(4f));
            var children = (Array)nodeType.GetField("m_children", Instance).GetValue(root);
            Assert.That(children.GetValue(4), Is.SameAs(oldRoot));
            Vector3[] expected =
            {
                new Vector3(0f, 2f, 0f), new Vector3(2f, 2f, 0f), new Vector3(0f, 2f, 2f), new Vector3(2f, 2f, 2f),
                Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 2f), new Vector3(2f, 0f, 2f),
            };
            for (int i = 0; i < 8; i++)
            {
                object child = children.GetValue(i);
                Assert.That(nodeType.GetProperty("Center").GetValue(child), Is.EqualTo(expected[i]), "child " + i);
                Assert.That(nodeType.GetProperty("BaseLength").GetValue(child), Is.EqualTo(2f), "child " + i);
            }
        }
    }
}
