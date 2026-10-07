using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class OriginalBoundsOctreeVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static void Check(bool ok, ref int checks, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            checks++;
        }
        private static object Field(object instance, string name) { return instance.GetType().GetField(name, Own).GetValue(instance); }
        private static BoundsOctreeNode<T> Root<T>(BoundsOctree<T> tree) { return (BoundsOctreeNode<T>)Field(tree, "m_rootNode"); }
        private static BoundsOctreeNode<T>[] Children<T>(BoundsOctreeNode<T> node) { return (BoundsOctreeNode<T>[])Field(node, "m_children"); }
        private static IList Objects<T>(BoundsOctreeNode<T> node) { return (IList)Field(node, "m_objects"); }
        private static T Stored<T>(object row) { return (T)row.GetType().GetField("Obj").GetValue(row); }
        private static bool Throws<TException>(Action action) where TException : Exception
        {
            try { action(); } catch (TException) { return true; }
            return false;
        }
        private static void Grow<T>(BoundsOctree<T> tree, Vector3 direction)
        {
            typeof(BoundsOctree<T>).GetMethod("Grow", Own).Invoke(tree, new object[] { direction });
        }
        private static Bounds Box(float x, float y, float z, float size) { return new Bounds(new Vector3(x, y, z), Vector3.one * size); }
        private static Plane[] Cube(float center, float half)
        {
            return new[] { new Plane(Vector3.right, half - center), new Plane(Vector3.left, half + center),
                new Plane(Vector3.up, half - center), new Plane(Vector3.down, half + center),
                new Plane(Vector3.forward, half - center), new Plane(Vector3.back, half + center) };
        }

        public static int VerifyOriginalDeclarations()
        {
            int c = 0; Type outer = typeof(BoundsOctree<>), node = typeof(BoundsOctreeNode<>);
            Type record = node.GetNestedType("OctreeObject", BindingFlags.NonPublic);
            Check(outer.Assembly.GetName().Name == "HLPhysics.Runtime", ref c, "Original assembly owner");
            foreach (Type t in new[] { outer, node })
            {
                Check(t.IsPublic && !t.IsAbstract && !t.IsSealed, ref c, "Original public class");
                Check(t.GetGenericArguments().Length == 1 && t.GetGenericArguments()[0].GenericParameterAttributes == GenericParameterAttributes.None && t.GetGenericArguments()[0].GetGenericParameterConstraints().Length == 0, ref c, "Original unconstrained T");
                Check(t.GetCustomAttributes(false).Length == 0, ref c, "No invented type attributes");
            }
            Check(record != null && record.IsNestedPrivate && record.IsValueType && record.IsLayoutSequential, ref c, "Private sequential original record");
            Check(record.GetMethods(Own).Length == 0 && record.GetConstructors(Own).Length == 0, ref c, "Zero original record methods");
            string[][] names = { new[] { "<Count>k__BackingField", "m_rootNode", "m_looseness", "m_initialSize", "m_minSize" },
                new[] { "<Center>k__BackingField", "<BaseLength>k__BackingField", "m_looseness", "m_minSize", "m_adjLength", "m_bounds", "m_objects", "m_children", "m_childBounds", "NUM_OBJECTS_ALLOWED" }, new[] { "Obj", "Bounds" } };
            Type[] owners = { outer, node, record };
            for (int i = 0; i < owners.Length; i++)
            {
                FieldInfo[] fields = owners[i].GetFields(Own).OrderBy(f => f.MetadataToken).ToArray();
                Check(fields.Length == names[i].Length, ref c, "Complete original fields");
                for (int j = 0; j < fields.Length; j++) Check(fields[j].Name == names[i][j], ref c, "Original field order/name");
            }
            Check(outer.GetMethods(Own).Length + outer.GetConstructors(Own).Length == 7, ref c, "Seven original outer methods");
            Check(node.GetMethods(Own).Length + node.GetConstructors(Own).Length == 16, ref c, "Sixteen original node methods");
            Check(outer.GetProperty("Count").GetSetMethod(true).IsPrivate, ref c, "Private Count setter");
            Check(node.GetProperty("Center").GetSetMethod(true).IsPrivate && node.GetProperty("BaseLength").GetSetMethod(true).IsPrivate, ref c, "Private node setters");
            Check(node.GetProperty("HasChildren", Own).GetGetMethod(true).IsPrivate, ref c, "Private HasChildren");
            Check(node.GetMethod("Encapsulates", Own).IsPrivate && node.GetMethod("Encapsulates", Own).IsStatic, ref c, "Private static containment");
            Check((int)node.GetField("NUM_OBJECTS_ALLOWED", Own).GetRawConstantValue() == 8, ref c, "Original capacity constant");
            foreach (string name in new[] { "m_looseness", "m_initialSize", "m_minSize" }) Check(outer.GetField(name, Own).IsInitOnly, ref c, "Original readonly outer field");
            Check(node.GetField("m_objects", Own).IsInitOnly, ref c, "Original readonly list");
            Check(node.GetMethod("GetColliding").GetParameters()[0].ParameterType == typeof(Bounds).MakeByRefType() && !node.GetMethod("GetColliding").GetParameters()[0].IsOut, ref c, "Original ref Bounds");
            Check(outer.MakeGenericType(typeof(int)) != null && node.MakeGenericType(typeof(int)) != null, ref c, "Value-type T remains legal");
            return c;
        }

        public static int VerifyOctantComparisons()
        {
            int c = 0; var node = (BoundsOctreeNode<int>)FormatterServices.GetUninitializedObject(typeof(BoundsOctreeNode<int>));
            MethodInfo setCenter = typeof(BoundsOctreeNode<int>).GetProperty("Center").GetSetMethod(true);
            float[] values = { float.NegativeInfinity, -1f, -0f, 0f, 1f, float.PositiveInfinity, float.NaN };
            for (int axis = 0; axis < 3; axis++)
            {
                for (int i = 0; i < values.Length; i++) for (int j = 0; j < values.Length; j++)
                {
                    Vector3 center = Vector3.zero, point = Vector3.zero; center[axis] = values[i]; point[axis] = values[j]; setCenter.Invoke(node, new object[] { center });
                    bool bit = axis == 1 ? !(values[j] >= values[i]) : !(values[j] <= values[i]);
                    Check(node.BestFitChild(point) == (bit ? new[] { 1, 4, 2 }[axis] : 0), ref c, "Native unordered/equality octant table");
                }
            }
            setCenter.Invoke(node, new object[] { Vector3.zero });
            for (int i = 0; i < 8; i++) Check(node.BestFitChild(new Vector3((i & 1) == 0 ? -1 : 1, (i & 4) == 0 ? 1 : -1, (i & 2) == 0 ? -1 : 1)) == i, ref c, "All original octants");
            return c;
        }

        public static int VerifyConstructionAndChildren()
        {
            int c = 0; var tree = new BoundsOctree<int>(10f, new Vector3(1, 2, 3), 20f, 0.5f);
            Check(tree.Count == 0, ref c, "Initial count");
            Check((float)Field(tree, "m_initialSize") == 10f && (float)Field(tree, "m_minSize") == 10f, ref c, "Warning clamps min to initial");
            Check((float)Field(tree, "m_looseness") == 1f, ref c, "Lower looseness clamp");
            Check(Root(tree).Center == new Vector3(1, 2, 3) && Root(tree).BaseLength == 10f, ref c, "Initial root");
            var upper = new BoundsOctree<int>(2, Vector3.zero, 1, 3); Check((float)Field(upper, "m_looseness") == 2f, ref c, "Upper looseness clamp");
            var nan = new BoundsOctree<int>(2, Vector3.zero, 1, float.NaN); Check(float.IsNaN((float)Field(nan, "m_looseness")), ref c, "Unordered looseness retained");
            var node = new BoundsOctreeNode<int>(8, 4, 1.5f, new Vector3(10, 20, 30));
            Bounds bounds = (Bounds)Field(node, "m_bounds"); Check(bounds.center == node.Center && bounds.size == new Vector3(12, 12, 12), ref c, "Loose bounds geometry");
            Bounds[] children = (Bounds[])Field(node, "m_childBounds");
            Vector3[] centers = { new Vector3(8,22,28), new Vector3(12,22,28), new Vector3(8,22,32), new Vector3(12,22,32), new Vector3(8,18,28), new Vector3(12,18,28), new Vector3(8,18,32), new Vector3(12,18,32) };
            for (int i = 0; i < 8; i++) { Check(children[i].center == centers[i], ref c, "Authored child center"); Check(children[i].size == new Vector3(6,6,6), ref c, "Authored loose child size"); }
            Check(!node.HasAnyObjects() && Children(node) == null && Objects(node).Count == 0, ref c, "Fresh node state");
            var negative = new BoundsOctreeNode<int>(-4, 9, 0.5f, Vector3.zero); Check(negative.BaseLength == -4 && ((Bounds)Field(negative, "m_bounds")).extents == new Vector3(-1,-1,-1), ref c, "Negative construction remains unvalidated");
            var alias = new BoundsOctreeNode<int>[8]; node.SetChildren(alias); Check(ReferenceEquals(Children(node), alias), ref c, "Original child-array alias");
            node.SetChildren(new BoundsOctreeNode<int>[7]); Check(ReferenceEquals(Children(node), alias), ref c, "Rejected length preserves prior children");
            Check(Throws<NullReferenceException>(() => node.SetChildren(null)), ref c, "Null length faults before mutation");
            Check(ReferenceEquals(Children(node), alias), ref c, "Null failure preserves children");
            Check(Throws<NullReferenceException>(() => node.HasAnyObjects()), ref c, "Null child accepted then faults on traversal");
            alias[0] = new BoundsOctreeNode<int>(4, 4, 1, new Vector3(8,22,28)); alias[0].Add(42, Box(8,22,28,1));
            Check(node.HasAnyObjects(), ref c, "Aliased first child and short-circuit traversal");
            return c;
        }

        public static int VerifyInsertionAndQueries()
        {
            int c = 0; var node = new BoundsOctreeNode<int>(8, 4, 1, Vector3.zero);
            Check(!node.Add(9, Box(10,0,0,1)) && !node.HasAnyObjects(), ref c, "Outside rejected without mutation");
            Check(node.Add(0, Box(0,0,0,0.5f)), ref c, "Root-spanning object");
            for (int i = 1; i < 8; i++) Check(node.Add(i, Box(-2,2,-2,0.25f)), ref c, "Eight objects remain local");
            Check(Children(node) == null && Objects(node).Count == 8, ref c, "Capacity boundary");
            Check(node.Add(8, Box(-2,2,-2,0.25f)), ref c, "Ninth insertion splits");
            Check(Children(node).Length == 8 && Objects(node).Count == 1, ref c, "Crossing object stays in parent");
            Check(Objects(Children(node)[0]).Count == 8 && Children(Children(node)[0]) == null, ref c, "Minimum size suppresses child split");
            var wide = Box(0,0,0,8); var result = new List<int> { 99 }; node.GetColliding(ref wide, result);
            Check(result.SequenceEqual(new[] {99,0,7,6,5,4,3,2,1,8}), ref c, "Append, parent-first, reverse migration then incoming order");
            Check(wide == Box(0,0,0,8), ref c, "Ref check bounds unchanged");
            Bounds narrow = Box(-2,2,-2,1); result.Clear(); node.GetColliding(ref narrow, result); Check(result.SequenceEqual(new[] {7,6,5,4,3,2,1,8}), ref c, "Object bounds filter");
            Bounds outside = Box(100,0,0,1); node.GetColliding(ref outside, result); Check(result.Count == 8, ref c, "Root rejection preserves existing results");
            Check(Throws<NullReferenceException>(() => node.GetColliding(ref wide, null)), ref c, "Matching object faults on null result");
            node.GetColliding(ref outside, null); Check(true, ref c, "Rejected root does not dereference result");
            var empty = new BoundsOctreeNode<int>(8, 4, 1, Vector3.zero); empty.GetColliding(ref wide, null); Check(true, ref c, "Empty node permits null result");
            var exact = new BoundsOctreeNode<int>(2, 1, 1, Vector3.zero); Check(exact.Add(1, Box(0,0,0,2)), ref c, "Boundary containment inclusive");
            Check(!exact.Add(2, Box(0,0,0,2.001f)), ref c, "Larger extent rejected");
            var shortNode = new BoundsOctreeNode<int>(8,4,1,Vector3.zero); var slot = new[] { new BoundsOctreeNode<int>(4,4,1,Vector3.zero) }; typeof(BoundsOctreeNode<int>).GetField("m_children", Own).SetValue(shortNode, slot);
            Check(Throws<IndexOutOfRangeException>(() => shortNode.GetColliding(ref wide, new List<int>())), ref c, "Query traverses fixed eight slots");
            Check(Throws<IndexOutOfRangeException>(() => shortNode.HasAnyObjects()), ref c, "Object traversal uses fixed eight slots");
            var tree = new BoundsOctree<string>(8,Vector3.zero,4,1); tree.Add(null, Box(0,0,0,1)); var refs = new List<string> { "sentinel" }; tree.GetColliding(refs, Box(0,0,0,2));
            Check(tree.Count == 1 && refs.Count == 2 && refs[0] == "sentinel" && refs[1] == null, ref c, "Null T stored and outer query forwards");
            return c;
        }

        private sealed class Record
        {
            public readonly int Id; public static readonly List<string> Comparisons = new List<string>(); public static bool Fail;
            public Record(int id) { Id = id; }
            public override bool Equals(object obj) { var other = obj as Record; Comparisons.Add(Id + ":" + (other == null ? "null" : other.Id.ToString())); if (Fail) throw new InvalidOperationException("original equality witness"); return other != null && Id == other.Id; }
            public override int GetHashCode() { return Id; }
        }
        public static int VerifyMigrationFailureOrdering()
        {
            int c = 0; Record.Comparisons.Clear(); Record.Fail = false;
            try
            {
                var tree = new BoundsOctree<Record>(8,Vector3.zero,4,1);
                for (int i = 0; i < 8; i++) tree.Add(new Record(i), Box(-2,2,-2,0.25f));
                tree.Add(new Record(8), Box(-2,2,-2,0.25f));
                Check(Record.Comparisons.Count == 36, ref c, "Remove(record) searches equality for each reverse-migrated entry");
                Check(Record.Comparisons[0] == "0:7" && Record.Comparisons[35] == "0:0", ref c, "Reverse migration equality order");
                Check(tree.Count == 9 && Objects(Root(tree)).Count == 0, ref c, "Successful count after complete insertion");
                Check(Enumerable.Range(0,9).Select(i => Stored<Record>(Objects(Children(Root(tree))[0])[i]).Id).SequenceEqual(new[] {7,6,5,4,3,2,1,0,8}), ref c, "Migrated child order");
                Record.Comparisons.Clear(); var failing = new BoundsOctree<Record>(8,Vector3.zero,4,1);
                for (int i = 0; i < 8; i++) failing.Add(new Record(i), Box(-2,2,-2,0.25f));
                Record.Fail = true; Check(Throws<InvalidOperationException>(() => failing.Add(new Record(8), Box(-2,2,-2,0.25f))), ref c, "User equality exception propagates");
                Record.Fail = false;
                Check(failing.Count == 8, ref c, "Outer count not incremented on failure");
                Check(Objects(Root(failing)).Count == 8 && Objects(Children(Root(failing))[0]).Count == 1, ref c, "Child insert precedes failed parent removal");
                Check(Stored<Record>(Objects(Children(Root(failing))[0])[0]).Id == 7 && Record.Comparisons.SequenceEqual(new[] {"0:7"}), ref c, "Exact first migration failure");
                var result = new List<Record>(); failing.GetColliding(result, Box(0,0,0,8));
                Check(result.Select(r => r.Id).SequenceEqual(new[] {0,1,2,3,4,5,6,7,7}), ref c, "Original partial state contains the published duplicate");
            }
            finally { Record.Fail = false; Record.Comparisons.Clear(); }
            return c;
        }

        public static int VerifyGrowthAndAbort()
        {
            int c = 0; var tree = new BoundsOctree<int>(2,Vector3.zero,1,1); var old = Root(tree); Grow(tree, new Vector3(float.NaN,float.NaN,float.NaN));
            Check(Root(tree).BaseLength == 4 && Root(tree).Center == new Vector3(-1,-1,-1), ref c, "Unordered direction takes original negative signs");
            Check(!ReferenceEquals(old, Root(tree)) && Children(Root(tree)) == null && tree.Count == 0, ref c, "Empty old root discarded");
            var occupied = new BoundsOctree<int>(2,Vector3.zero,1,1); occupied.Add(0, Box(0,0,0,0.25f)); old = Root(occupied); Grow(occupied, Vector3.one);
            Check(Root(occupied).BaseLength == 4 && Root(occupied).Center == Vector3.one, ref c, "Root doubles and shifts");
            Check(ReferenceEquals(Children(Root(occupied))[4], old), ref c, "Original old-root octant");
            Check(Children(Root(occupied)).Length == 8 && Children(Root(occupied)).All(n => n != null), ref c, "All growth children populated");
            Check(occupied.Count == 1 && Root(occupied).HasAnyObjects(), ref c, "Growth retains objects and count");
            occupied.Add(1, Box(16,0,0,0.25f)); var result = new List<int>(); occupied.GetColliding(result, Box(0,0,0,64));
            Check(occupied.Count == 2 && result.OrderBy(i => i).SequenceEqual(new[] {0,1}), ref c, "Repeated growth retains old and new objects");
            Check(Root(occupied).BaseLength == 32, ref c, "Finite add growth stops once contained");
            var failed = new BoundsOctree<int>(1,Vector3.zero,0,1); old = Root(failed); failed.Add(4, Box(float.MaxValue,0,0,1));
            Check(failed.Count == 0 && !Root(failed).HasAnyObjects(), ref c, "Failed add does not publish object/count");
            Check(Root(failed).BaseLength == 2097152f && Root(failed).Center.x == 1048575.5f, ref c, "Exactly twenty-one retained growth attempts");
            Check(!ReferenceEquals(old, Root(failed)) && Children(Root(failed)) == null, ref c, "Failed add retains replacement empty root");
            return c;
        }

        public static int VerifyFrustumQueries()
        {
            int c = 0; var tree = new BoundsOctree<int>(16,Vector3.zero,8,1); tree.Add(1,Box(0,0,0,1)); tree.Add(2,Box(6,0,0,1));
            var result = new List<int> {99}; tree.GetWithinFrustum(Cube(0,5), result); Check(result.SequenceEqual(new[] {99,1}), ref c, "Original frustum tests object bounds and appends");
            tree.GetWithinFrustum(Cube(100,1), result); Check(result.SequenceEqual(new[] {99,1}), ref c, "Rejected node leaves list untouched");
            tree.GetWithinFrustum(Cube(100,1), null); Check(true, ref c, "Rejected node permits null result");
            Check(Throws<NullReferenceException>(() => tree.GetWithinFrustum(Cube(0,5), null)), ref c, "Matching object faults on null result");
            var node = new BoundsOctreeNode<int>(16,8,1,Vector3.zero); node.GetWithinFrustum(Cube(0,5), null); Check(true, ref c, "Accepted empty node permits null result");
            var alias = new BoundsOctreeNode<int>[8]; for (int i = 0; i < 8; i++) alias[i] = new BoundsOctreeNode<int>(8,8,1,Vector3.zero);
            alias[0].Add(3,Box(0,0,0,1)); alias[7].Add(4,Box(0,0,0,1)); node.Add(2,Box(0,0,0,1)); node.SetChildren(alias); result.Clear(); node.GetWithinFrustum(Cube(0,5),result);
            Check(result.SequenceEqual(new[] {2,3,4}), ref c, "Parent-first then increasing child index");
            alias[1] = null; result.Clear(); Check(Throws<NullReferenceException>(() => node.GetWithinFrustum(Cube(0,5),result)), ref c, "Null child traversal fails");
            Check(result.SequenceEqual(new[] {2,3}), ref c, "Results before child failure retained");
            return c;
        }
    }
}
