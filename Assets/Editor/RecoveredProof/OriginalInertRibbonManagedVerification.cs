using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // These cases use genuine managed owners and value types. They create no Unity objects.
    // Parent geometry, authored-knot copying and Unity serialization require separate Engine cases.
    public static class OriginalInertRibbonManagedVerification
    {
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, Fields);
            if (field == null) throw new InvalidOperationException(type.FullName + ": missing " + name);
            return field;
        }
        private static void Set(object owner, string name, object value) => Field(owner.GetType(), name).SetValue(owner, value);
        private static T Get<T>(object owner, string name) => (T)Field(owner.GetType(), name).GetValue(owner);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static void Check(ref int count, bool value, string label)
        {
            ++count;
            if (!value) throw new InvalidOperationException(label);
        }
        private static void SameFloat(ref int count, float actual, float expected, string label) => Check(ref count, Bits(actual) == Bits(expected), label);
        private static void SameVector(ref int count, Vector3 actual, Vector3 expected, string label)
        {
            SameFloat(ref count, actual.x, expected.x, label + ".x");
            SameFloat(ref count, actual.y, expected.y, label + ".y");
            SameFloat(ref count, actual.z, expected.z, label + ".z");
        }
        private static void NullFault(ref int count, Action callback, string label)
        {
            bool fault = false;
            try { callback(); } catch (NullReferenceException) { fault = true; }
            Check(ref count, fault, label);
        }
        private static InertRibbonSubKnot Knot(RibbonAlignment alignment, float length)
        {
            var knot = new InertRibbonSubKnot();
            Set(knot, "m_alignment", alignment); Set(knot, "m_length", length);
            return knot;
        }

        public static int VerifyLutAndInertLength()
        {
            int count = 0;
            var knot = new InertRibbonSubKnot();
            var runtime = (ISplineKnotRuntimeHandle)knot;
            var handle = (ISplineKnotHandle)knot;
            Check(ref count, knot.TLUT == null && knot.Metadata == null, "original null defaults");
            SameFloat(ref count, knot.Length, 0f, "length default");
            SameFloat(ref count, knot.LengthInverse, 0f, "stored reciprocal default");
            foreach (int index in new[] { int.MinValue, -1, 0, 3, int.MaxValue })
            {
                SameFloat(ref count, knot.GetLUTValue(index), 0f, "null LUT");
                SameFloat(ref count, handle.GetLUTValue(index), 0f, "explicit null LUT");
            }
            float[] lut = { -0f, 0.125f, 0.625f, 0.875f };
            Set(knot, "m_tLUT", lut);
            int[] indices = { int.MinValue, -1, 0, 1, 2, 3, 4, int.MaxValue };
            float[] expected = { 0f, 0f, -0f, 0.125f, 0.625f, 0.875f, 1f, 1f };
            for (int i = 0; i < indices.Length; ++i)
            {
                SameFloat(ref count, knot.GetLUTValue(indices[i]), expected[i], "fixed public LUT");
                SameFloat(ref count, handle.GetLUTValue(indices[i]), expected[i], "fixed explicit LUT");
            }
            Set(knot, "m_tLUT", new float[0]);
            SameFloat(ref count, knot.GetLUTValue(-1), 0f, "empty negative");
            SameFloat(ref count, knot.GetLUTValue(0), 1f, "empty upper");
            Set(knot, "m_tLUT", lut); Set(knot, "m_length", -7f); Set(knot, "m_lengthInverse", 19f);
            Check(ref count, !runtime.IsLengthDirty(), "always clean");
            runtime.SetLengthDirty(); runtime.RecalculateLength();
            Check(ref count, !runtime.IsLengthDirty(), "empty dirty/recalculate remain clean");
            Check(ref count, ReferenceEquals(knot.TLUT, lut), "no-op LUT identity");
            SameFloat(ref count, knot.Length, -7f, "no-op length");
            SameFloat(ref count, knot.LengthInverse, 19f, "no-op reciprocal");
            return count;
        }

        public static int VerifyAlignmentCacheAndCenterForwarding()
        {
            int count = 0;
            var knot = new InertRibbonKnot();
            var cache = Get<Dictionary<RibbonAlignment, InertRibbonSubKnot>>(knot, "m_cachedSubKnots");
            Check(ref count, cache.Count == 0, "constructor empty cache");
            Check(ref count, ReferenceEquals(cache.Comparer, HLSplinesEnumComparers.RibbonAlignmentComparer), "genuine comparer identity");
            Check(ref count, knot.GetSubKnot(RibbonAlignment.Center) == null, "null array miss");
            Check(ref count, cache.Count == 0, "miss not cached");
            NullFault(ref count, () => { float value = knot.Length; }, "center missing faults");
            var first = Knot(RibbonAlignment.Center, 13f); var duplicate = Knot(RibbonAlignment.Center, 29f);
            var left = Knot(RibbonAlignment.Left, 31f);
            Set(knot, "m_subKnots", new[] { left, first, duplicate });
            Check(ref count, ReferenceEquals(knot.GetSubKnot(RibbonAlignment.Center), first), "first duplicate wins");
            Check(ref count, ReferenceEquals(knot[RibbonAlignment.Center], first), "indexer cached identity");
            Check(ref count, cache.Count == 1, "single cache entry");
            Check(ref count, knot.GetSubKnot(RibbonAlignment.Right) == null, "missing alignment");
            Check(ref count, cache.Count == 1, "missing alignment remains uncached");
            var right = Knot(RibbonAlignment.Right, 41f);
            Set(knot, "m_subKnots", new[] { right, duplicate });
            Check(ref count, ReferenceEquals(knot.GetSubKnot(RibbonAlignment.Center), first), "cache survives raw array change");
            Check(ref count, ReferenceEquals(knot.GetSubKnot(RibbonAlignment.Right), right), "later miss sees later array");
            Check(ref count, cache.Count == 2, "only matching entries cached");
            Set(knot, "m_subKnots", null);
            Check(ref count, ReferenceEquals(knot.GetSubKnot(RibbonAlignment.Center), first), "cache checked before missing array");
            var metadata = new MetadataGroups(); var lut = new[] { 0f, 0.4f, 1f };
            Set(first, "m_metadata", metadata); Set(first, "m_tLUT", lut); Set(first, "m_lengthInverse", -3f);
            first.SetTransform(new Vector3(2f, -4f, 8f), new Quaternion(0f, 0f, 0f, 1f));
            Set(first, "m_localOffset", new Vector3(3f, 5f, 7f));
            string[] fields = { "m_localControlPointNext", "m_worldControlPointNext", "m_localControlPointPrev", "m_worldControlPointPrev", "m_tangentNext", "m_tangentPrev" };
            Vector3[] vectors = { new Vector3(1,2,3), new Vector3(4,5,6), new Vector3(7,8,9), new Vector3(10,11,12), new Vector3(13,14,15), new Vector3(16,17,18) };
            for (int i=0;i<fields.Length;++i) Set(first, fields[i], vectors[i]);
            Check(ref count, ReferenceEquals(knot.Metadata, metadata), "center metadata identity");
            Check(ref count, ReferenceEquals(knot.TLUT, lut), "center LUT identity");
            SameVector(ref count, knot.Transform.Location, new Vector3(2,-4,8), "center position");
            SameVector(ref count, knot.UpVector, new Vector3(0,1,0), "center up");
            SameVector(ref count, knot.LocalControlPointNext, vectors[0], "center local next");
            SameVector(ref count, knot.WorldControlPointNext, vectors[1], "center world next");
            SameVector(ref count, knot.LocalControlPointPrev, vectors[2], "center local prev");
            SameVector(ref count, knot.WorldControlPointPrev, vectors[3], "center world prev");
            SameVector(ref count, knot.TangentNext, vectors[4], "center tangent next");
            SameVector(ref count, knot.TangentPrev, vectors[5], "center tangent prev");
            SameFloat(ref count, knot.Length, 13f, "center length"); SameFloat(ref count, knot.LengthInverse, -3f, "center reciprocal");
            SameFloat(ref count, knot.GetLUTValue(1), 0.4f, "center LUT forwarding");
            var faulty = new InertRibbonKnot();
            Set(faulty, "m_subKnots", new InertRibbonSubKnot[] { null, left });
            NullFault(ref count, () => faulty.GetSubKnot(RibbonAlignment.Left), "null earlier entry faults");
            Check(ref count, Get<Dictionary<RibbonAlignment, InertRibbonSubKnot>>(faulty, "m_cachedSubKnots").Count == 0, "fault stores no entry");
            Set(faulty, "m_subKnots", new[] { left });
            Check(ref count, ReferenceEquals(faulty.GetSubKnot(RibbonAlignment.Left), left), "retry after fault reads genuine entry");
            return count;
        }

        public static int VerifyTransformOnlyMutation()
        {
            int count=0; var knot=Knot(RibbonAlignment.Right,-11f); var lut=new[]{0f,1f};var metadata=new MetadataGroups();
            Set(knot,"m_tLUT",lut);Set(knot,"m_metadata",metadata);Set(knot,"m_lengthInverse",23f);
            string[] fields={"m_localOffset","m_localControlPointNext","m_worldControlPointNext","m_localControlPointPrev","m_worldControlPointPrev","m_tangentNext","m_tangentPrev"};
            var controls=new Vector3(17f,19f,29f); foreach(string field in fields) Set(knot,field,controls);
            Vector3 location=new Vector3(-0f,37f,-41f);Quaternion orientation=new Quaternion(2f,-3f,5f,-7f);
            ((IInertSplineKnotRuntimeHandle)knot).SetTransform(location,orientation);
            SameVector(ref count,knot.Transform.Location,location,"raw position");
            SameFloat(ref count,knot.Transform.Orientation.x,2f,"raw rotation x");SameFloat(ref count,knot.Transform.Orientation.y,-3f,"raw rotation y");
            SameFloat(ref count,knot.Transform.Orientation.z,5f,"raw rotation z");SameFloat(ref count,knot.Transform.Orientation.w,-7f,"raw rotation w");
            foreach(string field in fields) SameVector(ref count,Get<Vector3>(knot,field),controls,"retained "+field);
            Check(ref count,ReferenceEquals(knot.TLUT,lut),"retained LUT");Check(ref count,ReferenceEquals(knot.Metadata,metadata),"retained metadata");
            Check(ref count,knot.Alignment==RibbonAlignment.Right,"retained alignment");SameFloat(ref count,knot.Length,-11f,"retained length");SameFloat(ref count,knot.LengthInverse,23f,"retained reciprocal");
            return count;
        }

        public static int VerifySplineCopyCacheAndOriginalThrow()
        {
            int count=0;var spline=new InertRibbonSubSpline();
            SameFloat(ref count,spline.Length,0f,"default spline length");SameFloat(ref count,spline.LengthInverse,0f,"default stored reciprocal");Check(ref count,spline.LUTCount==0,"default LUT count");
            var bounds=new List<ISplineRuntimeHandle.KnotBounds>();Set(spline,"m_knotBoundsList",bounds);
            float[] lengths={8f,-2f,0f,-0f,float.PositiveInfinity,float.NaN};
            int[] expectedReciprocals={Bits(0.125f),Bits(-0.5f),0x7f800000,unchecked((int)0xff800000),Bits(0f),0};
            for(int i=0;i<lengths.Length;++i)
            {
                var original=new RibbonSubSpline(RibbonAlignment.Left);Set(original,"m_length",lengths[i]);Set(original,"<LUTCount>k__BackingField",17+i);
                spline.Initialise(null,original);
                Check(ref count,spline.Alignment==RibbonAlignment.Left,"copied alignment");SameFloat(ref count,spline.Length,lengths[i],"copied length bits");
                Check(ref count,i==5 ? float.IsNaN(spline.LengthInverse) : Bits(spline.LengthInverse)==expectedReciprocals[i],"unrepaired reciprocal");
                Check(ref count,spline.LUTCount==17+i,"copied LUT count");Check(ref count,ReferenceEquals(Get<object>(spline,"m_knotBoundsList"),bounds),"initialise retains bounds identity");
            }
            NullFault(ref count,()=>spline.Initialise(null,null),"null source faults");
            Check(ref count,spline.LUTCount==22,"fault retains prior LUT count");Check(ref count,ReferenceEquals(Get<object>(spline,"m_knotBoundsList"),bounds),"fault retains bounds");
            foreach(bool force in new[]{false,true})
            {
                bool threw=false;try{((ISplineRuntimeHandle)spline).RecalculateLength(force);}catch(NotImplementedException){threw=true;}
                Check(ref count,threw,"original explicit throw");Check(ref count,ReferenceEquals(Get<object>(spline,"m_knotBoundsList"),bounds),"throw does not clear bounds");
            }
            NullFault(ref count,()=>{ int value=spline.KnotCount; },"null parent count faults");
            NullFault(ref count,()=>spline.GetKnot(0),"null parent knot faults");
            NullFault(ref count,()=>spline.KnotsUpdated(),"null parent update faults");
            return count;
        }
    }
}
