using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Octree facts read from the shipped HLPhysics.Runtime metadata and x86-64 bodies,
    // kept apart from OriginalBoundsOctreeVerification so each case names its source.
    public static class OriginalBoundsOctreeNativeGeometryVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static void Check(bool ok, ref int checks, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            checks++;
        }
        private static object Field(object instance, string name) { return instance.GetType().GetField(name, Own).GetValue(instance); }
        private static Bounds Box(float x, float y, float z, float size) { return new Bounds(new Vector3(x, y, z), Vector3.one * size); }

        // MethodDef rows 0x06000002..08 (BoundsOctree`1 0x02000003) and 0x06000009..18
        // (BoundsOctreeNode`1 0x02000004) in token order: name, raw MethodAttributes and
        // parameter names. Types are listed only where the metadata row names a type
        // definition; generic, array and by-ref parameter rows are left to the
        // declaration test because their Il2CppType rows are not decoded here.
        private static readonly string[] OuterRows =
        {
            "get_Count|886|System.Int32|",
            "set_Count|881|System.Void|System.Int32 value",
            ".ctor|1886|System.Void|System.Single initialWorldSize,UnityEngine.Vector3 initialWorldPos,System.Single minNodeSize,System.Single loosenessVal",
            "Add|86|System.Void|? obj,UnityEngine.Bounds objBounds",
            "GetColliding|86|System.Void|? collidingWith,UnityEngine.Bounds checkBounds",
            "GetWithinFrustum|86|System.Void|? planes,? result",
            "Grow|81|System.Void|UnityEngine.Vector3 direction",
        };
        private static readonly string[] NodeRows =
        {
            "get_Center|886|UnityEngine.Vector3|",
            "set_Center|881|System.Void|UnityEngine.Vector3 value",
            "get_BaseLength|886|System.Single|",
            "set_BaseLength|881|System.Void|System.Single value",
            "get_HasChildren|881|System.Boolean|",
            ".ctor|1886|System.Void|System.Single baseLengthVal,System.Single minSizeVal,System.Single loosenessVal,UnityEngine.Vector3 centerVal",
            "Add|86|System.Boolean|? obj,UnityEngine.Bounds objBounds",
            "GetColliding|86|System.Void|? checkBounds,? result",
            "GetWithinFrustum|86|System.Void|? planes,? result",
            "SetChildren|86|System.Void|? childOctrees",
            "BestFitChild|86|System.Int32|UnityEngine.Vector3 objBoundsCenter",
            "HasAnyObjects|86|System.Boolean|",
            "SetValues|81|System.Void|System.Single baseLengthVal,System.Single minSizeVal,System.Single loosenessVal,UnityEngine.Vector3 centerVal",
            "SubAdd|81|System.Void|? obj,UnityEngine.Bounds objBounds",
            "Split|81|System.Void|",
            "Encapsulates|91|System.Boolean|UnityEngine.Bounds outerBounds,UnityEngine.Bounds innerBounds",
        };

        public static int VerifyMethodRows()
        {
            int c = 0;
            c += CheckRows(typeof(BoundsOctree<>), OuterRows);
            c += CheckRows(typeof(BoundsOctreeNode<>), NodeRows);
            return c;
        }

        private static int CheckRows(Type owner, string[] rows)
        {
            int c = 0;
            MethodBase[] methods = owner.GetMethods(Own).Cast<MethodBase>().Concat(owner.GetConstructors(Own))
                .OrderBy(m => m.MetadataToken).ToArray();
            Check(methods.Length == rows.Length, ref c, owner.Name + " original method count");
            for (int i = 0; i < rows.Length; i++)
            {
                string[] expected = rows[i].Split('|');
                MethodBase method = methods[i];
                Check(method.Name == expected[0], ref c, owner.Name + " original method order " + expected[0]);
                Check(((int)method.Attributes).ToString("x") == expected[1], ref c, "Original attributes of " + expected[0]);
                Type returns = method is MethodInfo info ? info.ReturnType : typeof(void);
                Check(returns.FullName == expected[2], ref c, "Original return type of " + expected[0]);
                ParameterInfo[] parameters = method.GetParameters();
                string[] wanted = expected[3].Length == 0 ? new string[0] : expected[3].Split(',');
                Check(parameters.Length == wanted.Length, ref c, "Original parameter count of " + expected[0]);
                for (int p = 0; p < wanted.Length; p++)
                {
                    string[] pair = wanted[p].Split(' ');
                    Check(parameters[p].Name == pair[1], ref c, "Original parameter name of " + expected[0]);
                    if (pair[0] != "?") Check(parameters[p].ParameterType.FullName == pair[0], ref c, "Original parameter type of " + expected[0]);
                }
            }
            return c;
        }

        // BoundsOctree`1 .ctor 0x06000004, x86-64 0xe42dd0: ucomiss minNodeSize, initialWorldSize
        // then jbe skips the warning, so an unordered (NaN) minimum is kept without a log.
        public static int VerifyUnorderedMinimumSkipsWarning()
        {
            int c = 0; var logs = new List<string>();
            Application.LogCallback hook = (message, stack, type) => logs.Add(type + ": " + message);
            Application.logMessageReceived += hook;
            try
            {
                var tree = new BoundsOctree<int>(10f, Vector3.zero, float.NaN, 1f);
                Check(logs.Count == 0, ref c, "Unordered minimum is not reported");
                Check(float.IsNaN((float)Field(tree, "m_minSize")) && (float)Field(tree, "m_initialSize") == 10f, ref c, "Unordered minimum stored unadjusted");
                var root = (BoundsOctreeNode<int>)Field(tree, "m_rootNode");
                Check(root.BaseLength == 10f && float.IsNaN((float)Field(root, "m_minSize")), ref c, "Root receives the unadjusted minimum");
            }
            finally { Application.logMessageReceived -= hook; }
            return c;
        }

        // BoundsOctreeNode`1.GetColliding 0x06000010, x86-64 0xe3ea30: the result list is
        // first dereferenced at 0xe3ec3e, after the per-object Intersects test passes.
        public static int VerifyObjectFilterPrecedesResultAccess()
        {
            int c = 0; var node = new BoundsOctreeNode<int>(8, 4, 1, Vector3.zero);
            Check(node.Add(1, Box(3, 3, 3, 0.5f)), ref c, "Owned object stored in the node");
            Bounds query = Box(-3, -3, -3, 1);
            Check(((Bounds)Field(node, "m_bounds")).Intersects(query), ref c, "Query reaches the node's objects");
            node.GetColliding(ref query, null);
            Check(true, ref c, "Non-intersecting object never touches a null result");
            var result = new List<int> { 99 }; node.GetColliding(ref query, result);
            Check(result.SequenceEqual(new[] { 99 }), ref c, "Non-intersecting object is not appended");
            return c;
        }

        // BoundsOctree`1.Grow 0x06000008, x86-64 0xe43210: each new sibling i is centred at
        // newCenter + half * (i even ? -1 : 1, i >= 4 ? -1 : 1, i < 2 || i is 4 or 5 ? -1 : 1)
        // (0xe43470..0xe434fd), built with the old BaseLength, m_minSize [this+0x28] and
        // m_looseness [this+0x20]; the old root keeps its BestFitChild slot.
        public static int VerifyGrowthSiblingGeometry()
        {
            int c = 0; var tree = new BoundsOctree<int>(2f, Vector3.zero, 0.5f, 1.25f);
            tree.Add(0, Box(0, 0, 0, 0.25f));
            var old = (BoundsOctreeNode<int>)Field(tree, "m_rootNode");
            typeof(BoundsOctree<int>).GetMethod("Grow", Own).Invoke(tree, new object[] { Vector3.one });
            var root = (BoundsOctreeNode<int>)Field(tree, "m_rootNode");
            Check(root.BaseLength == 4f && root.Center == Vector3.one, ref c, "Root doubles toward the direction");
            Check((float)Field(root, "m_minSize") == 0.5f && (float)Field(root, "m_looseness") == 1.25f, ref c, "Root receives the tree minimum and looseness");
            var children = (BoundsOctreeNode<int>[])Field(root, "m_children");
            Vector3[] centers = { new Vector3(0, 2, 0), new Vector3(2, 2, 0), new Vector3(0, 2, 2), new Vector3(2, 2, 2),
                Vector3.zero, new Vector3(2, 0, 0), new Vector3(0, 0, 2), new Vector3(2, 0, 2) };
            for (int i = 0; i < 8; i++)
            {
                if (i == 4) { Check(ReferenceEquals(children[i], old), ref c, "Old root keeps its octant"); continue; }
                Check(children[i].Center == centers[i], ref c, "Native sibling centre " + i);
                Check(children[i].BaseLength == 2f, ref c, "Sibling uses the old base length " + i);
                Check((float)Field(children[i], "m_minSize") == 0.5f && (float)Field(children[i], "m_looseness") == 1.25f, ref c, "Sibling minimum and looseness " + i);
                Check(!children[i].HasAnyObjects(), ref c, "New sibling starts empty " + i);
            }
            return c;
        }
    }
}
