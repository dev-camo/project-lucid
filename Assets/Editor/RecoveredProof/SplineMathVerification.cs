using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Editor
{
    public static class SplineMathVerification
    {
        private static int checks;
        private static void Check(bool value, string label)
        {
            ++checks;
            if (!value) throw new Exception("Original spline math: " + label);
        }
        private static bool Near(float a, float b, float tolerance = 0.0001f)
        {
            return Mathf.Abs(a - b) < tolerance;
        }
        private static bool Near(Vector3 a, Vector3 b, float tolerance = 0.0001f)
        {
            return Near(a.x,b.x,tolerance) && Near(a.y,b.y,tolerance) && Near(a.z,b.z,tolerance);
        }
        private static SplineControlPoints Points(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            return new SplineControlPoints { m_controlPt0=a, m_controlPt1=b, m_controlPt2=c, m_controlPt3=d };
        }
        private static SplineControlPoints Line()
        {
            return Points(Vector3.zero,new Vector3(1,2,3),new Vector3(2,4,6),new Vector3(3,6,9));
        }
        private static void ControlsEqual(SplineControlPoints a, SplineControlPoints b, string label)
        {
            Check(Near(a.m_controlPt0,b.m_controlPt0),label+" pt0");
            Check(Near(a.m_controlPt1,b.m_controlPt1),label+" pt1");
            Check(Near(a.m_controlPt2,b.m_controlPt2),label+" pt2");
            Check(Near(a.m_controlPt3,b.m_controlPt3),label+" pt3");
        }
        public static int RunManaged()
        {
            checks=0;
            var control=Line();
            foreach(float t in new[]{-1f,0f,0.25f,0.5f,0.75f,1f,2f})
            {
                Check(Near(Bezier.PointAlongSpline(t,ref control),new Vector3(3*t,6*t,9*t)),"Bezier line extrapolation "+t);
                Check(Near(Bezier.DerivativeAlongBezier(t,ref control),new Vector3(3,6,9)),"Bezier full derivative "+t);
                Check(Near(Bezier.SecondDerivativeAlongBezier(t,ref control),Vector3.zero),"Bezier zero second derivative "+t);
                Check(Near(Bezier.TangentAlongSpline(t,ref control),new Vector3(1,2,3).normalized),"Bezier normalized tangent "+t);
                float magnitude=-123;
                var pair=Bezier.PointAndTangentAlongSpline(ref magnitude,t,ref control);
                Check(Near(pair.Position,new Vector3(3*t,6*t,9*t)),"Bezier pair position "+t);
                Check(Near(pair.Tangent,new Vector3(1,2,3).normalized),"Bezier pair tangent "+t);
                Check(Near(magnitude,Mathf.Sqrt(14)),"Bezier raw tangent third of derivative "+t);
            }
            Check(!control.m_cacheSet,"Bezier leaves all Catmull cache fields untouched");
            var aliasBezier=Line();
            var aliasBezierPair=Bezier.PointAndTangentAlongSpline(ref aliasBezier.m_controlPt0.x,0.5f,ref aliasBezier);
            Check(Near(aliasBezier.m_controlPt0.x,Mathf.Sqrt(14)),"Bezier magnitude output aliases original control field");
            Check(Near(aliasBezierPair.Position,new Vector3(1.5f,3,4.5f)),"Bezier pair keeps point snapshots across aliased magnitude store");
            Check(Bezier.CalculateLengthBruteForce(control,-9)==0 && Bezier.CalculateLengthBruteForce(control,0)==0,"non-positive brute-force iteration counts return zero");
            foreach(int n in new[]{1,2,5,32}) Check(Near(Bezier.CalculateLengthBruteForce(control,n),Mathf.Sqrt(126)),"straight brute length "+n);
            Check(Near(Bezier.CalculateLength(control),Mathf.Sqrt(126)),"straight recursive length");
            Check(Near(Bezier.CalculateSegmentLengthRecursive(0.75f,0.25f,ref control),Mathf.Sqrt(126)*0.5f),"reverse range positive length");
            Check(Bezier.CalculateSegmentLengthRecursive(0.25f,0.25f,ref control)==0,"zero range length");
            // Binary-exact asymmetric controls cover each original polynomial coefficient.
            var asymmetric=Points(new Vector3(3,-2,5),new Vector3(4,-8,2),new Vector3(6,1,3),new Vector3(8,0,-2));
            Check(Near(Bezier.PointAlongSpline(0.5f,ref asymmetric),new Vector3(5.125f,-2.875f,2.25f)),"Bezier asymmetric native Bernstein coefficients");
            Check(Near(Bezier.DerivativeAlongBezier(0.5f,ref asymmetric),new Vector3(5.25f,8.25f,-4.5f)),"Bezier asymmetric derivative coefficients");
            Check(Near(Bezier.SecondDerivativeAlongBezier(0.5f,ref asymmetric),new Vector3(3,15,-6)),"Bezier asymmetric second derivative coefficients");
            float asymmetricMagnitude=0;var asymmetricPair=Bezier.PointAndTangentAlongSpline(ref asymmetricMagnitude,0.5f,ref asymmetric);
            Check(Near(asymmetricPair.Position,new Vector3(5.125f,-2.875f,2.25f)),"Bezier asymmetric pair position");
            Check(Near(asymmetricMagnitude,new Vector3(1.75f,2.75f,-1.5f).magnitude) && Near(asymmetricPair.Tangent,new Vector3(1.75f,2.75f,-1.5f).normalized),"Bezier asymmetric unscaled magnitude/normalization");
            var bent=Points(Vector3.zero,new Vector3(0,1,0),new Vector3(1,1,0),new Vector3(1,0,0));
            Check(Near(Bezier.PointAlongSpline(0.5f,ref bent),new Vector3(0.5f,0.75f,0)),"curved midpoint");
            Check(Near(Bezier.DerivativeAlongBezier(0.5f,ref bent),new Vector3(1.5f,0,0)),"curved midpoint derivative");
            Check(Near(Bezier.SecondDerivativeAlongBezier(0.5f,ref bent),new Vector3(0,-6,0)),"curved midpoint curvature");
            Check(Near(Bezier.CalculateLengthBruteForce(bent,1),1),"one segment is endpoint chord");
            Check(Near(Bezier.CalculateLengthBruteForce(bent,2),Mathf.Sqrt(3.25f)),"two chords sampled through midpoint");
            // Native Single arithmetic with the original span/error stopping predicates gives this chord approximation.
            Check(Near(Bezier.CalculateLength(bent),1.99962878f,0.0000005f),"arch recursive length keeps native0.1 span cutoff");
            Check(Bezier.CalculateLength(bent)<2 && 2-Bezier.CalculateLength(bent)>0.0003f,"recursive arch intentionally remains below analytic length");
            float degenerateMagnitude=123;
            var degenerate=default(SplineControlPoints);
            Check(Bezier.TangentAlongSpline(0.5f,ref degenerate)==Vector3.zero,"zero Bezier tangent");
            var emptyPair=Bezier.PointAndTangentAlongSpline(ref degenerateMagnitude,0.5f,ref degenerate);
            Check(emptyPair.Position==Vector3.zero && emptyPair.Tangent==Vector3.zero && degenerateMagnitude==0,"zero pair still writes magnitude");
            var nan=Line();nan.m_controlPt0.x=float.NaN;
            Check(float.IsNaN(Bezier.PointAlongSpline(0.5f,ref nan).x),"NaN point arithmetic retained");
            Check(Bezier.NewtonRaphsonIterationX(0.2f,control)==0 && Bezier.NewtonRaphsonIterationZ(0.2f,control)==0,"constant nonzero derivative exhausts Newton to zero");
            Check(Bezier.NewtonRaphsonIterationX(0.5f,degenerate)==0 && Bezier.NewtonRaphsonIterationZ(0.5f,degenerate)==0,"zero over zero Newton exhausts to zero");
            var extremum=Points(Vector3.zero,new Vector3(1,10,1),new Vector3(1,10,1),Vector3.zero);
            Check(Bezier.NewtonRaphsonIterationX(0.5f,extremum)==0.5f && Bezier.NewtonRaphsonIterationZ(0.5f,extremum)==0.5f,"stationary midpoint returns original estimate");
            Check(Bezier.NewtonRaphsonIterationX(0.5005f,extremum)==0.5005f && Bezier.NewtonRaphsonIterationZ(0.5005f,extremum)==0.5005f,"convergence returns previous estimate not next root");
            Check(Near(Bezier.NewtonRaphsonIterationX(0.1f,extremum),0.5f),"Newton reaches interior X extremum");
            Check(Near(Bezier.NewtonRaphsonIterationZ(0.9f,extremum),0.5f),"Newton reaches interior Z extremum");
            Vector3 min=new Vector3(99,99,99),max=new Vector3(-99,-99,-99);
            Bezier.CalculateBoundsAxisAligned(ref min,ref max,10,extremum);
            Check(min==Vector3.zero && Near(max,new Vector3(0.75f,7.5f,0.75f)),"X/Z extremum contributes entire vector");
            var yOnly=Points(Vector3.zero,new Vector3(1,4,1),new Vector3(2,4,2),new Vector3(3,0,3));
            Bezier.CalculateBoundsAxisAligned(ref min,ref max,10,yOnly);
            Check(min==Vector3.zero && max==new Vector3(3,0,3),"bounds intentionally ignore pure Y extremum");
            foreach(int steps in new[]{-1,0,int.MaxValue})
            {
                Bezier.CalculateBoundsAxisAligned(ref min,ref max,steps,extremum);
                Check(min==Vector3.zero && max==Vector3.zero,"endpoint bounds for zero/negative/overflow step count "+steps);
            }
            var first=Line();first.m_cacheSet=true;first.m_cacheVector1=new Vector3(91,92,93);
            var second=first;second.m_cacheVector1=new Vector3(-91,-92,-93);
            Bezier.SplitSpline(ref first,ref second,control,0.5f);
            ControlsEqual(first,Points(Vector3.zero,new Vector3(0.5f,1,1.5f),new Vector3(1,2,3),new Vector3(1.5f,3,4.5f)),"half split first");
            ControlsEqual(second,Points(new Vector3(1.5f,3,4.5f),new Vector3(2,4,6),new Vector3(2.5f,5,7.5f),new Vector3(3,6,9)),"half split second");
            Check(first.m_cacheSet && first.m_cacheVector1==new Vector3(91,92,93),"first split preserves cache");
            Check(second.m_cacheSet && second.m_cacheVector1==new Vector3(-91,-92,-93),"second split preserves cache");
            Bezier.SplitSpline(ref first,ref second,control,2f);
            ControlsEqual(first,Points(Vector3.zero,new Vector3(2,4,6),new Vector3(4,8,12),new Vector3(6,12,18)),"unclamped split first");
            ControlsEqual(second,Points(new Vector3(6,12,18),new Vector3(5,10,15),new Vector3(4,8,12),new Vector3(3,6,9)),"unclamped split second");
            var aliased=default(SplineControlPoints);
            Bezier.SplitSpline(ref aliased,ref aliased,control,0.5f);
            ControlsEqual(aliased,Points(new Vector3(1.5f,3,4.5f),new Vector3(2,4,6),new Vector3(2.5f,5,7.5f),new Vector3(3,6,9)),"second split wins on same ref");
            var combined=Bezier.CombineSplines(control,Points(new Vector3(3,6,9),new Vector3(4,8,12),new Vector3(5,10,15),new Vector3(6,12,18)));
            // Literal ARM64 straight-line register/store probe gives these unusual handles.
            ControlsEqual(combined,Points(Vector3.zero,new Vector3(5,10,15),new Vector3(1,2,3),new Vector3(6,12,18)),"native combine handle extrapolation");
            Check(!combined.m_cacheSet && combined.m_cacheVector1==Vector3.zero && combined.m_cacheVector4==Vector3.zero,"combine returns fresh zero caches");
            var left=Points(new Vector3(3,-2,5),new Vector3(4,-8,2),new Vector3(6,1,3),new Vector3(8,0,-2));
            var right=Points(new Vector3(8,0,-2),new Vector3(0,9,4),new Vector3(11,-4,6),new Vector3(12,5,9));
            combined=Bezier.CombineSplines(left,right);
            ControlsEqual(combined,Points(new Vector3(3,-2,5),new Vector3(11.666666f,9,0),new Vector3(4.666666f,-10,-10),new Vector3(12,5,9)),"native asymmetric combine literal probe");
            Check(Bezier.FindNearestPointOnSpline(new Vector3(0.75f,1.5f,2.25f),control)==0.25f,"Bezier nearest straight quarter");
            Check(Bezier.FindNearestPointOnSpline(new Vector3(-100,-200,-300),control)>0 && Bezier.FindNearestPointOnSpline(new Vector3(-100,-200,-300),control)<0.002,"Bezier endpoint approach stays interior");
            Check(Bezier.FindNearestPointOnSpline(Vector3.zero,control,0.5f)==0.5f,"Bezier large epsilon early midpoint");
            Check(Bezier.FindNearestPointOnSpline(Vector3.zero,degenerate,-1f)==0.5f,"Bezier negative epsilon bounded by1000");
            var asymmetricCat=asymmetric;
            Check(Near(CatmullRom.PointAlongSpline(0.5f,ref asymmetricCat),new Vector3(4.9375f,-3.8125f,2.625f)),"Catmull asymmetric native coefficient evaluation");
            Check(asymmetricCat.m_cacheVector1==new Vector3(8,-16,4) && asymmetricCat.m_cacheVector2==new Vector3(3,3,-2),"Catmull asymmetric first/second cache literals");
            Check(asymmetricCat.m_cacheVector3==new Vector3(2,40,14) && asymmetricCat.m_cacheVector4==new Vector3(-1,-25,-10),"Catmull asymmetric cubic cache literals");
            Check(Near(CatmullRom.TangentAlongSpline(0.5f,ref asymmetricCat),new Vector3(4.25f,24.25f,4.5f).normalized),"Catmull asymmetric normalized tangent");
            float asymmetricCatMagnitude=0;var asymmetricCatPair=CatmullRom.PointAndTangentAlongSpline(ref asymmetricCatMagnitude,0.5f,ref asymmetricCat);
            Check(Near(asymmetricCatPair.Position,new Vector3(4.9375f,-3.8125f,2.625f)) && Near(asymmetricCatMagnitude,new Vector3(4.25f,24.25f,4.5f).magnitude),"Catmull asymmetric paired position and raw magnitude");
            var cat=Line();
            foreach(float t in new[]{-1f,0f,0.25f,0.5f,1f,2f})
            {
                Check(Near(CatmullRom.PointAlongSpline(t,ref cat),new Vector3(1+t,2+2*t,3+3*t)),"Catmull line midpoint interval "+t);
                Check(Near(CatmullRom.TangentAlongSpline(t,ref cat),new Vector3(1,2,3).normalized),"Catmull tangent "+t);
                float mag=-123;var pair=CatmullRom.PointAndTangentAlongSpline(ref mag,t,ref cat);
                Check(Near(pair.Position,new Vector3(1+t,2+2*t,3+3*t)),"Catmull pair position "+t);
                Check(Near(pair.Tangent,new Vector3(1,2,3).normalized),"Catmull pair tangent "+t);
                Check(Near(mag,2*Mathf.Sqrt(14)),"Catmull magnitude keeps omitted half factor "+t);
            }
            Check(cat.m_cacheSet,"Catmull initializes cache");
            Check(cat.m_cacheVector1==new Vector3(2,4,6) && cat.m_cacheVector2==new Vector3(2,4,6),"Catmull first two coefficients");
            Check(cat.m_cacheVector3==Vector3.zero && cat.m_cacheVector4==Vector3.zero,"Catmull linear higher coefficients");
            cat.m_controlPt0=new Vector3(100,100,100);cat.m_controlPt1=new Vector3(100,100,100);
            Check(Near(CatmullRom.PointAlongSpline(0.5f,ref cat),new Vector3(1.5f,3,4.5f)),"cached coefficients remain stale after control mutation");
            var supplied=default(SplineControlPoints);supplied.m_cacheSet=true;supplied.m_cacheVector1=new Vector3(2,4,6);supplied.m_cacheVector2=new Vector3(4,-2,8);supplied.m_cacheVector3=new Vector3(8,4,-2);supplied.m_cacheVector4=new Vector3(16,-8,4);
            Check(Near(CatmullRom.PointAlongSpline(0.5f,ref supplied),new Vector3(4,1.5f,5)),"caller-supplied cache is authoritative");
            float suppliedMagnitude=0;var suppliedPair=CatmullRom.PointAndTangentAlongSpline(ref suppliedMagnitude,0.5f,ref supplied);
            Check(Near(suppliedMagnitude,new Vector3(24,-4,9).magnitude),"caller cache raw tangent magnitude");
            Check(Near(suppliedPair.Tangent,new Vector3(24,-4,9).normalized),"caller cache tangent normalizes");
            var aliasCat=supplied;
            var aliasCatPair=CatmullRom.PointAndTangentAlongSpline(ref aliasCat.m_cacheVector1.x,0.5f,ref aliasCat);
            Check(Near(aliasCat.m_cacheVector1.x,new Vector3(24,-4,9).magnitude),"Catmull magnitude output aliases cache component");
            Check(Near(aliasCatPair.Position,new Vector3(4,1.5f,5)),"Catmull pair keeps coefficient snapshots across aliased magnitude store");
            var catLength=Line();
            Check(Near(CatmullRom.CalculateLength(ref catLength),Mathf.Sqrt(14)),"Catmull length between middle controls");
            Check(catLength.m_cacheSet,"Catmull length mutates ref cache");
            Check(Near(CatmullRom.CalculateSegmentLengthRecursive(1,0,ref catLength),Mathf.Sqrt(14)),"Catmull reverse segment");
            Check(CatmullRom.FindNearestPointOnSpline(new Vector3(1.25f,2.5f,3.75f),ref catLength)==0.25f,"Catmull quarter nearest");
            var catNoLoop=Line();
            Check(CatmullRom.FindNearestPointOnSpline(Vector3.zero,ref catNoLoop,0.5f)==0.5f && catNoLoop.m_cacheSet,"Catmull large epsilon still evaluates/cache midpoint");
            catNoLoop=Line();
            Check(CatmullRom.FindNearestPointOnSpline(Vector3.zero,ref catNoLoop,float.NaN)==0.5f && catNoLoop.m_cacheSet,"Catmull NaN epsilon still initializes cache");
            var trace=new List<float>();var callbackControl=default(SplineControlPoints);
            SplineCommon.PointAlongSpline shortCurve=(float t,ref SplineControlPoints points)=>
            { trace.Add(t);points.m_cacheVector1.x+=1;return t==0?Vector3.zero:t==1?new Vector3(0.02f,0,0):new Vector3(0,0.03f,0); };
            float shortLength=SplineCommon.CalculateSegmentLengthRecursive(0,1,ref callbackControl,shortCurve);
            Check(trace.Count==3 && trace[0]==0 && trace[1]==1 && trace[2]==0.5f,"callback start/end/midpoint order");
            Check(callbackControl.m_cacheVector1.x==3,"same genuine ref controls passed to callback");
            Check(Near(shortLength,0.03f+Mathf.Sqrt(0.0013f)),"short span stops despite chord error");
            bool nullFault=false;try{SplineCommon.CalculateSegmentLengthRecursive(0,1,ref callbackControl,null);}catch(NullReferenceException){nullFault=true;}
            Check(nullFault,"null genuine delegate faults before samples");
            trace.Clear();
            SplineCommon.PointAlongSpline parabola=(float t,ref SplineControlPoints points)=>{trace.Add(t);return new Vector3(t,t*(1-t),0);};
            float parabolaLength=SplineCommon.CalculateSegmentLengthRecursive(0,1,ref callbackControl,parabola);
            Check(Near(parabolaLength,1.14767838f,0.0000005f) && trace.Count==33,"recursive callback native stopping approximation and sample count");
            Check(trace.Count>10 && trace[0]==0 && trace[1]==1 && trace[2]==0.5f && trace[3]==0.25f && trace[4]==0.125f,"left recursion precedes right and carries endpoints");
            foreach(var type in new[]{typeof(Bezier),typeof(CatmullRom)})
            {
                var options=(Il2CppSetOptionAttribute[])type.GetCustomAttributes(typeof(Il2CppSetOptionAttribute),false);
                Check(options.Length==2,"complete native IL2CPP options "+type.Name);
                Check(options[0].Option==Option.NullChecks && (bool)options[0].Value==false && options[1].Option==Option.ArrayBoundsChecks && (bool)options[1].Value==false,"ordered exact false options "+type.Name);
            }
            var fields=typeof(SplineControlPoints).GetFields(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
            var names=new[]{"m_controlPt0","m_controlPt1","m_controlPt2","m_controlPt3","m_cacheSet","m_cacheVector1","m_cacheVector2","m_cacheVector3","m_cacheVector4"};
            Check(fields.Length==9,"complete genuine controls field layout");
            for(int i=0;i<fields.Length;i++) Check(fields[i].Name==names[i] && fields[i].FieldType==(i==4?typeof(bool):typeof(Vector3)),"ordered original controls field "+i);
            foreach(var row in new[]{new[]{typeof(Bezier).FullName,"6"},new[]{typeof(CatmullRom).FullName,"3"}})
            {
                var nested=typeof(Bezier).Assembly.GetType(row[0]+"+<>c");
                Check(nested!=null,"natural cached lambda type "+row[0]);
                Check(nested.GetField("<>9__"+row[1]+"_0",BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly)!=null,"original cached lambda ordinal "+row[0]);
                Check(nested.GetMethod("<CalculateSegmentLengthRecursive>b__"+row[1]+"_0",BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly)!=null,"original natural callback identity "+row[0]);
            }
            return checks;
        }
        public static int RunEngine()
        {
            checks=0;
            var controls=Points(new Vector3(10,-20,30),new Vector3(11,-20,31),new Vector3(12,-20,32),new Vector3(13,-20,33));
            var box=new Vector3[10];box[8]=new Vector3(91,92,93);box[9]=new Vector3(-91,-92,-93);
            var original=box;
            Bezier.CalculateBounds(ref box,8,controls);
            Check(ReferenceEquals(box,original),"oriented bounds retains caller array");
            for(int i=0;i<8;i++) Check(Near(box[i],(i==0||i==3||i==4||i==7)?controls.m_controlPt0:controls.m_controlPt3),"oriented diagonal line corner order "+i);
            Check(box[8]==new Vector3(91,92,93) && box[9]==new Vector3(-91,-92,-93),"oriented bounds preserves extra scratch entries");
            controls=Points(new Vector3(3,4,5),new Vector3(3,4,6),new Vector3(3,4,7),new Vector3(3,4,8));
            Bezier.CalculateBounds(ref box,8,controls);
            for(int i=0;i<8;i++) Check(Near(box[i],(i==0||i==3||i==4||i==7)?controls.m_controlPt0:controls.m_controlPt3),"forward-aligned line literal corner order "+i);
            controls=Points(Vector3.zero,new Vector3(1,10,1),new Vector3(1,10,1),Vector3.zero);
            Bezier.CalculateBounds(ref box,8,controls);
            var expected=new[]{Vector3.zero,new Vector3(0,0,0.75f),new Vector3(0.75f,0,0.75f),new Vector3(0.75f,0,0),new Vector3(0,7.5f,0),new Vector3(0,7.5f,0.75f),new Vector3(0.75f,7.5f,0.75f),new Vector3(0.75f,7.5f,0)};
            for(int i=0;i<8;i++) Check(Near(box[i],expected[i]),"zero endpoint rotation real engine box "+i);
            return checks;
        }
        public static void Run()
        {
            int managed=RunManaged();int engine=RunEngine();
            Debug.Log("Original spline math verification: managed="+managed+", engine="+engine+", total="+(managed+engine));
        }
    }
}
