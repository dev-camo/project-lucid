using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Bounded scalar/native-branch checks; real nonnull ISpline owner execution remains pending.
    public static class SplineSharedRuntimeVerification
    {
        private static int checks;
        private static Type component;
        private static void Check(bool ok,string message) { if(!ok)throw new InvalidOperationException("Spline shared runtime proof: "+message); ++checks; }
        private static float F(uint bits) { return BitConverter.ToSingle(BitConverter.GetBytes(bits),0); }
        private static uint Bits(float value) { return BitConverter.ToUInt32(BitConverter.GetBytes(value),0); }
        private static void Scalar(float actual,float expected,string message) { Check(float.IsNaN(expected)?float.IsNaN(actual):Bits(actual)==Bits(expected),message); }
        private static void Vector(Vector3 actual,Vector3 expected,string message) { Scalar(actual.x,expected.x,message+" x");Scalar(actual.y,expected.y,message+" y");Scalar(actual.z,expected.z,message+" z"); }
        private static MethodInfo Method(string name,int arity) { foreach(var m in component.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static))if(m.Name==name&&m.GetParameters().Length==arity)return m;throw new MissingMethodException(name); }
        private static void Adjacent(string name,Vector3 position,Vector3 forward,Vector3 start,Vector3 end,Vector3 expected,float expectedT,bool expectedHit)
        {
            object[] args={position,forward,start,end,null,null}; bool hit=(bool)Method("FindAdjacentLinePosition",6).Invoke(null,args);
            Vector((Vector3)args[4],expected,"native scalar adjacency "+name);Scalar((float)args[5],expectedT,"adjusted parameter "+name);Check(hit==expectedHit,"native projection hit "+name);
        }
        private static void Nearest(string name,Vector3 position,Vector3 start,Vector3 end,Vector3 expected)
        {
            object[] args={position,start,end,null};Method("FindNearestLinePosition",4).Invoke(null,args);Vector((Vector3)args[3],expected,"native line projection "+name);
        }
        private static void NullFault(string name,object[] args)
        {
            try { Method(name,args.Length).Invoke(null,args); }
            catch(TargetInvocationException ex)when(ex.InnerException is NullReferenceException){Check(true,"original null receiver "+name+"/"+args.Length);return;}
            throw new InvalidOperationException("Original null receiver did not fault: "+name);
        }
        public static int RunManaged()
        {
            checks=0;component=typeof(SplineLocation).Assembly.GetType("Hardlight.SplineRuntimeComponent",true);
            Adjacent("interior",new Vector3(F(0x40800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x40800200u),F(0x00000000u),F(0x00000000u)),F(0x3eccc800u),true);
            Adjacent("opposite-forward",new Vector3(F(0x40800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0xbf800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x40800200u),F(0x00000000u),F(0x00000000u)),F(0x3eccc800u),true);
            Adjacent("start",new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3aa00000u),F(0x00000000u),F(0x00000000u)),F(0x38800000u),true);
            Adjacent("end",new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x411ffb00u),F(0x00000000u),F(0x00000000u)),F(0x3f7ffc00u),true);
            Adjacent("before",new Vector3(F(0xc0000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3aa00000u),F(0x00000000u),F(0x00000000u)),F(0x38800000u),false);
            Adjacent("after",new Vector3(F(0x41400000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x411ffb00u),F(0x00000000u),F(0x00000000u)),F(0x3f7ffc00u),false);
            Adjacent("off-axis",new Vector3(F(0x40800000u),F(0x447a0000u),F(0xc1880000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x40800200u),F(0x00000000u),F(0x00000000u)),F(0x3eccc800u),true);
            Adjacent("zero-forward",new Vector3(F(0x40800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3aa00000u),F(0x00000000u),F(0x00000000u)),F(0x38800000u),true);
            Adjacent("perpendicular-forward",new Vector3(F(0x40800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x3f800000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3aa00000u),F(0x00000000u),F(0x00000000u)),F(0x38800000u),true);
            Adjacent("NaN-forward",new Vector3(F(0x40800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x7fc00000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x411ffb00u),F(0x00000000u),F(0x00000000u)),F(0x3f7ffc00u),false);
            Adjacent("NaN-position",new Vector3(F(0x7fc00000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x411ffb00u),F(0x00000000u),F(0x00000000u)),F(0x3f7ffc00u),false);
            Adjacent("descending-segment",new Vector3(F(0x40800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41200000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x40800200u),F(0x00000000u),F(0x00000000u)),F(0x3f199c00u),true);
            Adjacent("degenerate-segment",new Vector3(F(0x40800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x40400000u),F(0x40000000u),F(0x3f800000u)),new Vector3(F(0x40400000u),F(0x40000000u),F(0x3f800000u)),new Vector3(F(0x40400000u),F(0x40000000u),F(0x3f800000u)),F(0x38800000u),false);
            Adjacent("diagonal",new Vector3(F(0x40000000u),F(0x40800000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x3f800000u),F(0x00000000u)),new Vector3(F(0x00000000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0x41000000u),F(0x41000000u),F(0x00000000u)),new Vector3(F(0x403ff000u),F(0x403ff000u),F(0x00000000u)),F(0x3ebff800u),true);
            Adjacent("negative-range",new Vector3(F(0xc0800000u),F(0x40000000u),F(0x00000000u)),new Vector3(F(0x3f800000u),F(0x00000000u),F(0x00000000u)),new Vector3(F(0xc1200000u),F(0x40000000u),F(0x00000000u)),new Vector3(F(0x40000000u),F(0x40000000u),F(0x00000000u)),new Vector3(F(0xc0800c00u),F(0x40000000u),F(0x00000000u)),F(0x3efff800u),true);
            Nearest("interior",new Vector3(4,8,-2),Vector3.zero,new Vector3(10,0,0),new Vector3(4,0,0));
            Nearest("before",new Vector3(-2,8,-2),Vector3.zero,new Vector3(10,0,0),Vector3.zero);
            Nearest("after",new Vector3(12,8,-2),Vector3.zero,new Vector3(10,0,0),new Vector3(10,0,0));
            Nearest("descending",new Vector3(4,8,-2),new Vector3(10,0,0),Vector3.zero,new Vector3(4,0,0));
            Nearest("degenerate NaN position",new Vector3(float.NaN,0,0),new Vector3(3,2,1),new Vector3(3,2,1),new Vector3(3,2,1));
            Nearest("approximate equal endpoints",new Vector3(1,0,0),Vector3.zero,new Vector3(.000001f,0,0),Vector3.zero);
            Nearest("outside equal threshold",new Vector3(1,0,0),Vector3.zero,new Vector3(.00002f,0,0),new Vector3(.00002f,0,0));
            Nearest("NaN position propagates",new Vector3(float.NaN,0,0),Vector3.zero,new Vector3(10,0,0),new Vector3(float.NaN,float.NaN,float.NaN));
            Nearest("NaN endpoint propagates",new Vector3(1,0,0),Vector3.zero,new Vector3(float.NaN,0,0),new Vector3(float.NaN,float.NaN,float.NaN));
            var cacheField=component.GetField("s_lutCache",BindingFlags.Static|BindingFlags.NonPublic);
            Check(cacheField!=null&&cacheField.IsInitOnly,"original readonly cache");
            float[] cache=(float[])cacheField.GetValue(null);Check(cache.Length==2048,"real initializer cache size");
            Check(Array.TrueForAll(cache,value=>value==0f),"cache initializer zero state, no provider run");
            Check((component.Attributes&TypeAttributes.BeforeFieldInit)!=0,"genuine field-initializer scheduling flag");
            Check((int)component.GetField("SampleLUTSize",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue()==2048,"original sample constant");
            Scalar((float)component.GetField("EndEdgeDistanceTolerance",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue(),.1f,"original edge distance constant");
            var recalc=Method("RecalculateKnotsLength",2).GetParameters()[1];Check(recalc.IsOptional&&(bool)recalc.DefaultValue==false,"original force default false");
            var lutCount=Method("RecalculateKnotLength",4).GetParameters()[3];Check(lutCount.IsOptional&&(int)lutCount.DefaultValue==-1,"original LUT default sentinel");
            Type closure=component.GetNestedType("<>c",BindingFlags.NonPublic);
            Check(closure!=null,"original natural cache type");
            object closureInstance=closure.GetField("<>9",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).GetValue(null);
            MethodInfo compare=closure.GetMethod("<FindNearestSplinePosition>b__26_0",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            Check(compare!=null,"original natural method ordinal26");
            var comparison=(Comparison<ISplineRuntimeHandle.KnotBounds>)Delegate.CreateDelegate(typeof(Comparison<ISplineRuntimeHandle.KnotBounds>),closureInstance,compare);
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=3})==4,"native tolerance/index comparator case0");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=3})==4,"native tolerance/index comparator case1");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=3})==4,"native tolerance/index comparator case2");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=3})==4,"native tolerance/index comparator case3");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==4,"native tolerance/index comparator case4");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=3})==4,"native tolerance/index comparator case5");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=3})==4,"native tolerance/index comparator case6");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=3})==4,"native tolerance/index comparator case7");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=3})==4,"native tolerance/index comparator case8");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=3})==-1,"native tolerance/index comparator case9");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=3})==-1,"native tolerance/index comparator case10");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==-1,"native tolerance/index comparator case11");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=3})==-1,"native tolerance/index comparator case12");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=3})==-1,"native tolerance/index comparator case13");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=3})==4,"native tolerance/index comparator case14");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=3})==1,"native tolerance/index comparator case15");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=3})==4,"native tolerance/index comparator case16");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=3})==-1,"native tolerance/index comparator case17");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==-1,"native tolerance/index comparator case18");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=3})==-1,"native tolerance/index comparator case19");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=3})==-1,"native tolerance/index comparator case20");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=3})==4,"native tolerance/index comparator case21");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=3})==1,"native tolerance/index comparator case22");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=3})==1,"native tolerance/index comparator case23");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=3})==4,"native tolerance/index comparator case24");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==4,"native tolerance/index comparator case25");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=3})==-1,"native tolerance/index comparator case26");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=3})==-1,"native tolerance/index comparator case27");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=3})==4,"native tolerance/index comparator case28");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=3})==1,"native tolerance/index comparator case29");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=3})==1,"native tolerance/index comparator case30");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=3})==4,"native tolerance/index comparator case31");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==4,"native tolerance/index comparator case32");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=3})==-1,"native tolerance/index comparator case33");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=3})==-1,"native tolerance/index comparator case34");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=3})==4,"native tolerance/index comparator case35");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=3})==1,"native tolerance/index comparator case36");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=3})==1,"native tolerance/index comparator case37");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=3})==1,"native tolerance/index comparator case38");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==1,"native tolerance/index comparator case39");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=3})==4,"native tolerance/index comparator case40");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=3})==-1,"native tolerance/index comparator case41");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=3})==4,"native tolerance/index comparator case42");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=3})==1,"native tolerance/index comparator case43");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xc0000000u),KnotIndex=3})==1,"native tolerance/index comparator case44");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x80000000u),KnotIndex=3})==1,"native tolerance/index comparator case45");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==1,"native tolerance/index comparator case46");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x40000000u),KnotIndex=3})==1,"native tolerance/index comparator case47");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=3})==4,"native tolerance/index comparator case48");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x38d1b717u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==4,"native tolerance/index comparator case49");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x38d1b718u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==1,"native tolerance/index comparator case50");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x38d1b716u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==4,"native tolerance/index comparator case51");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xb8d1b717u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==4,"native tolerance/index comparator case52");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xb8d1b718u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==-1,"native tolerance/index comparator case53");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xb8d1b716u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==4,"native tolerance/index comparator case54");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x38d1b717u),KnotIndex=3})==4,"native tolerance/index comparator case55");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xb8d1b717u),KnotIndex=3})==4,"native tolerance/index comparator case56");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=-8},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=2})==-10,"native tolerance/index comparator case57");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=-2147483648},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=2147483647})==1,"native tolerance/index comparator case58");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=2147483647},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=-2147483648})==-1,"native tolerance/index comparator case59");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=2},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=2})==0,"native tolerance/index comparator case60");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7fc00000u),KnotIndex=-2147483648},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=2147483647})==1,"native tolerance/index comparator case61");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=-8},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x7f800000u),KnotIndex=2})==-10,"native tolerance/index comparator case62");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=2147483647},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0xff800000u),KnotIndex=-2147483648})==-1,"native tolerance/index comparator case63");
            Check(comparison(new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x37d1b717u),KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=F(0x0u),KnotIndex=3})==4,"native tolerance/index comparator case64");
            var sorted=new List<ISplineRuntimeHandle.KnotBounds>{new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=.000025f,KnotIndex=7},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=float.NaN,KnotIndex=4},new ISplineRuntimeHandle.KnotBounds{SqrDistanceToBounds=0f,KnotIndex=2}};
            sorted.Sort(comparison);Check(sorted[0].KnotIndex==2&&sorted[1].KnotIndex==4&&sorted[2].KnotIndex==7,"genuine near-equal/unordered distance records sorted by original knot index");
            NullFault("GetKnotIndex",new object[]{null,null});
            NullFault("GetKnot",new object[]{null,-1});
            NullFault("GetKnotMetadataFromDistance",new object[]{null,-1f});
            NullFault("GetKnotMetadataFromKnotT",new object[]{null,new KnotT(-1,.5f)});
            NullFault("GetKnotMetadataFromLinearRatio",new object[]{null,new LinearRatio(-1,.5f)});
            NullFault("GetLinearRatioFromLerpT",new object[]{null,new KnotT(-1,.5f),-1});
            NullFault("GetLinearRatioFromKnotT",new object[]{null,new KnotT(-1,.5f)});
            NullFault("GetKnotTFromLinearRatio",new object[]{null,new LinearRatio(-1,.5f)});
            NullFault("GetApproximateMidPoint",new object[]{null});
            NullFault("RecalculateKnotsLength",new object[]{null,false});
            NullFault("RecalculateKnotLength",new object[]{null,-1,new float[]{17},-1});
            NullFault("GetControlsForKnots",new object[]{null,-1,0});
            NullFault("PointAlongSpline",new object[]{null,.5f,default(SplineControlPoints)});
            NullFault("PointAndTangentAlongSpline",new object[]{null,new KnotT(-1,.5f),false});
            NullFault("PointAndTangentAlongSpline",new object[]{null,17f,.5f,default(SplineControlPoints)});
            NullFault("FindNearestPointOnSpline",new object[]{null,Vector3.zero,default(SplineControlPoints)});
            NullFault("FindNearestKnotPositionOnSpline",new object[]{null,Vector3.zero,-1,null,null});
            NullFault("FindAdjacentSplinePosition",new object[]{null,Vector3.zero,Vector3.zero,null,null,null});
            NullFault("FindAdjacentKnotPosition",new object[]{null,Vector3.zero,Vector3.zero,-1});
            NullFault("FindNearestSplinePosition",new object[]{null,Vector3.zero,null,null});
            NullFault("FindNearestKnotPosition",new object[]{null,Vector3.zero,-1,null,null});
            NullFault("GetSplineLocationFromDistance",new object[]{null,-1f,null});
            NullFault("CalculatePositionBoundsInfo",new object[]{Vector3.zero,default(SplineLocation)});
            return checks;
        }
    }
}
