using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Editor
{
    public static class RibbonBoundsAlignmentVerification
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new Exception("Original ribbon bounds/alignment: " + message);
        }
        private static bool Near(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x-b.x) < 0.0001f && Mathf.Abs(a.y-b.y) < 0.0001f && Mathf.Abs(a.z-b.z) < 0.0001f;
        }
        private static Vector3[] Expected()
        {
            return new [] {
                new Vector3(2,4,6), new Vector3(2,4,0), new Vector3(2,0,6), new Vector3(2,0,0),
                new Vector3(0,4,6), new Vector3(0,4,0), new Vector3(0,0,6), Vector3.zero
            };
        }
        public static int RunManaged()
        {
            checks=0;
            var bounds = new Bounds(new Vector3(1,2,3), new Vector3(2,4,6));
            Vector3[] expected = Expected();
            var corners = new Vector3[8];
            bounds.UpdateBoundsCorners(corners);
            for (int i=0;i<8;i++) Check(corners[i] == expected[i], "fixed corner order " + i);

            var extended = new Vector3[10];
            extended[8] = new Vector3(99,-87,55); extended[9] = new Vector3(-12,34,-56);
            bounds.UpdateBoundsCorners(extended);
            for (int i=0;i<8;i++) Check(extended[i] == expected[i], "extended scratch writes " + i);
            Check(extended[8] == new Vector3(99,-87,55) && extended[9] == new Vector3(-12,34,-56), "extra scratch values retained");

            var negative = new Bounds(new Vector3(1,2,3), new Vector3(-2,-4,-6));
            negative.UpdateBoundsCorners(corners);
            for (int i=0;i<8;i++) Check(corners[i] == expected[7-i], "negative extents keep literal ordering " + i);

            var point = new Vector3(-3,8,-12);
            new Bounds(point, Vector3.zero).UpdateBoundsCorners(corners);
            for (int i=0;i<8;i++) Check(corners[i] == point, "zero size collapse " + i);

            new Bounds(new Vector3(float.NaN,2,3), new Vector3(2,4,6)).UpdateBoundsCorners(corners);
            for (int i=0;i<8;i++) Check(float.IsNaN(corners[i].x) && corners[i].y==expected[i].y && corners[i].z==expected[i].z, "NaN axis remains local " + i);

            new Bounds(new Vector3(float.PositiveInfinity,2,3), new Vector3(2,4,6)).UpdateBoundsCorners(corners);
            for (int i=0;i<8;i++) Check(float.IsPositiveInfinity(corners[i].x) && corners[i].y==expected[i].y && corners[i].z==expected[i].z, "infinite axis remains local " + i);

            // These check rebuilt managed array faults, not unchecked native invalid memory access.
            for (int n=0;n<8;n++)
            {
                var shortArray=new Vector3[n]; bool caught=false;
                try { bounds.UpdateBoundsCorners(shortArray); }
                catch (IndexOutOfRangeException) { caught=true; }
                Check(caught,"short array managed fault " + n);
                bool prefix=true; for(int i=0;i<n;i++) prefix &= shortArray[i]==expected[i];
                Check(prefix,"partial prefix before managed array fault " + n);
            }
            bool nullCaught=false;
            try { bounds.UpdateBoundsCorners(null); }
            catch (NullReferenceException) { nullCaught=true; }
            Check(nullCaught,"null scratch managed fault");

            int[] raw = { int.MinValue, -2, -1, 0, 1, 2, int.MaxValue };
            var comparer = new RibbonAlignmentEqualityComparer();
            IEqualityComparer<RibbonAlignment> contract = comparer;
            for(int i=0;i<raw.Length;i++)
            {
                RibbonAlignment a=(RibbonAlignment)raw[i];
                Check(contract.GetHashCode(a)==raw[i],"raw signed enum hash " + raw[i]);
                for(int j=0;j<raw.Length;j++) Check(contract.Equals(a,(RibbonAlignment)raw[j])==(raw[i]==raw[j]),"exact enum equality " + raw[i] + "/" + raw[j]);
            }
            Check(HLSplinesEnumComparers.RibbonAlignmentComparer != null,"genuine registry creates comparer");
            Check(ReferenceEquals(HLSplinesEnumComparers.RibbonAlignmentComparer,HLSplinesEnumComparers.RibbonAlignmentComparer),"registry retains comparer reference");
            Check(HLSplinesEnumComparers.RibbonAlignmentComparer.GetType()==typeof(RibbonAlignmentEqualityComparer),"exact comparer registry type");
            Check(new HLSplinesEnumComparers()!=null,"original public registry constructor");
            Check((typeof(HLSplinesEnumComparers).Attributes & TypeAttributes.BeforeFieldInit)!=0,"registry BeforeFieldInit retained");
            var field=typeof(HLSplinesEnumComparers).GetField("RibbonAlignmentComparer",BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
            Check(field!=null && field.IsInitOnly && field.FieldType==typeof(RibbonAlignmentEqualityComparer),"original readonly concrete comparer field");
            Check(typeof(BoundsUtilities).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly).Length==0,"bounds full fieldless type");
            var options=typeof(BoundsUtilities).GetCustomAttributes(typeof(Il2CppSetOptionAttribute),false);
            Check(options.Length==2,"bounds original two IL2CPP options");
            return checks;
        }
        public static int RunEngine()
        {
            checks=0;
            GameObject owner=null;
            try
            {
                owner=new GameObject("ProjectLucid Original Ribbon Bounds Verification");
                Transform transform=owner.transform;
                transform.position=new Vector3(10,-20,30);
                transform.rotation=Quaternion.Euler(0,0,90);
                transform.localScale=new Vector3(2,-3,4);
                var bounds=new Bounds(new Vector3(1,2,3),new Vector3(2,4,6));
                Bounds world=bounds.LocalToWorldAlloc(transform);
                Check(Near(world.center,new Vector3(16,-18,42)),"allocated world center with rotation and negative scale");
                Check(Near(world.size,new Vector3(12,4,24)),"allocated world size with rotation and negative scale");
                var scratch=new Vector3[8];
                Bounds reused=bounds.LocalToWorld(transform,scratch);
                Check(Near(reused.center,world.center),"reused world center");
                Check(Near(reused.size,world.size),"reused world size");
                Vector3[] expected=Expected();
                for(int i=0;i<8;i++) Check(scratch[i]==expected[i],"world transform leaves local scratch corner " + i);

                Bounds local=bounds.WorldToLocalAlloc(transform);
                Check(Near(local.center,new Vector3(11,-3,-6.75f)),"allocated inverse center");
                Check(Near(local.size,new Vector3(2,2f/3f,1.5f)),"allocated inverse size");
                Bounds localReused=bounds.WorldToLocal(transform,scratch);
                Check(Near(localReused.center,local.center),"reused inverse center");
                Check(Near(localReused.size,local.size),"reused inverse size");
                for(int i=0;i<8;i++) Check(scratch[i]==expected[i],"inverse transform leaves world scratch corner " + i);

                transform.position=Vector3.zero; transform.rotation=Quaternion.identity; transform.localScale=Vector3.one;
                var extended=new Vector3[9]; extended[8]=new Vector3(100,-200,300);
                Bounds expanded=bounds.LocalToWorld(transform,extended);
                Check(Near(expanded.center,new Vector3(50,-98,150)),"extra scratch point affects real GeometryUtility center");
                Check(Near(expanded.size,new Vector3(100,204,300)),"extra scratch point affects real GeometryUtility size");
                Check(extended[8]==new Vector3(100,-200,300),"extra scratch point retained through real engine call");
                Bounds inverseExpanded=bounds.WorldToLocal(transform,extended);
                Check(Near(inverseExpanded.center,expanded.center),"inverse overload uses entire scratch array");
                Check(Near(inverseExpanded.size,expanded.size),"inverse entire scratch size");
                return checks;
            }
            finally { if(owner!=null) UnityEngine.Object.DestroyImmediate(owner); }
        }
        public static void Run()
        {
            int managed=RunManaged(); int engine=RunEngine();
            Debug.Log("Original ribbon bounds/alignment verification passed: " + managed + " managed checks and " + engine + " engine checks.");
        }
    }
}
