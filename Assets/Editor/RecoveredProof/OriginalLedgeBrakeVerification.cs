using System;
using System.Reflection;
using HardlightProject;

namespace ProjectLucid.Editor
{
    // Fixed constructor defaults and independent 0/45/60/90/180/360 degree witnesses
    // follow the shipped native constants. No original gameplay or native libc parity
    // is inferred from this bounded verification. Each invocation owns fresh instances.
    public static class OriginalLedgeBrakeVerification
    {
        static int checks;
        static void Check(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException(reason);
            checks++;
        }
        static FieldInfo Field(string name)
        {
            return typeof(LedgeBrakeParameters).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }
        static float Read(LedgeBrakeParameters p, string n)
        {
            return (float)Field(n).GetValue(p);
        }
        static void Write(LedgeBrakeParameters p, string n, float v)
        {
            Field(n).SetValue(p, v);
        }
        static void Near(float a, float b, string n)
        {
            Check(Math.Abs(a-b) <= 0.000001f, n);
        }
        public static int RunOriginalBoundaries()
        {
            checks=0;
            LedgeBrakeParameters p = new LedgeBrakeParameters();
            string[] names =
            {
                "m_maximumAngleToPlane", "m_maximumVelocityThreshold", "m_intentAngleBelowVelocity", "m_intentAngleAboveVelocity", "m_ledgeMinAngleLimit", "m_raycastStopDistance", "m_raycastDetectDistance", "m_raycastDownDistance", "m_maximumCosAngleToPlane", "m_ledgeCosAngleLimit", "m_intentCosAngleBelowVelocity", "m_intentCosAngleAboveVelocity"
            };
            float[] defaults =
            {
                60f,10f,90f,45f,45f,1f,2f,2f,0f,0f,0f,0f
            };
            Check(typeof(LedgeBrakeParameters).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length==12,"complete twelve original own fields");
            for(int i=0;i<names.Length;i++) Check(Read(p,names[i])==defaults[i],"native constructor default "+names[i]);
            Check(p.MaximumVelocityThreshold==10f && p.RaycastStopDistance==1f && p.RaycastDetectDistance==2f && p.RaycastDownDistance==2f,"four direct nonangle getters");
            Check(p.MaximumCosAngleToPlane==0f && p.LedgeCosAngleLimit==0f && p.IntentCosAngleBelowVelocity==0f && p.IntentCosAngleAboveVelocity==0f,"cache getters remain zero before explicit caching");
            p.CalculateCachedValues();
            Near(p.MaximumCosAngleToPlane,.5f,"60 degree plane cache");
            Near(p.LedgeCosAngleLimit,.7071067812f,"45 degree ledge cache");
            Near(p.IntentCosAngleBelowVelocity,0f,"90 degree below-velocity cache");
            Near(p.IntentCosAngleAboveVelocity,.7071067812f,"45 degree above-velocity cache");
            Write(p,"m_maximumAngleToPlane",180f);
            Check(Math.Abs(p.MaximumCosAngleToPlane-.5f)<.000001f,"serialized mutation does not auto-invalidate old cache");
            Write(p,"m_ledgeMinAngleLimit",0f);
            Write(p,"m_intentAngleBelowVelocity",360f);
            Write(p,"m_intentAngleAboveVelocity",180f);
            p.CalculateCachedValues();
            Near(p.MaximumCosAngleToPlane,-1f,"180 degree plane recache");
            Near(p.LedgeCosAngleLimit,1f,"0 degree ledge recache");
            Near(p.IntentCosAngleBelowVelocity,1f,"360 degree below recache");
            Near(p.IntentCosAngleAboveVelocity,-1f,"180 degree above recache");
            Write(p,"m_maximumVelocityThreshold",float.NaN);
            Write(p,"m_raycastStopDistance",-7f);
            Write(p,"m_raycastDetectDistance",float.PositiveInfinity);
            Write(p,"m_raycastDownDistance",float.NegativeInfinity);
            p.CalculateCachedValues();
            Check(float.IsNaN(p.MaximumVelocityThreshold),"threshold NaN transferred by direct getter");
            Check(p.RaycastStopDistance==-7f,"negative stop distance retained");
            Check(float.IsPositiveInfinity(p.RaycastDetectDistance),"detect infinity retained");
            Check(float.IsNegativeInfinity(p.RaycastDownDistance),"down infinity retained");
            Write(p,"m_maximumAngleToPlane",float.NaN);
            Write(p,"m_ledgeMinAngleLimit",float.PositiveInfinity);
            Write(p,"m_intentAngleBelowVelocity",float.NegativeInfinity);
            Write(p,"m_intentAngleAboveVelocity",float.NaN);
            p.CalculateCachedValues();
            Check(float.IsNaN(p.MaximumCosAngleToPlane),"NaN plane propagates");
            Check(float.IsNaN(p.LedgeCosAngleLimit),"infinite ledge angle propagates NaN");
            Check(float.IsNaN(p.IntentCosAngleBelowVelocity),"infinite intent below propagates NaN");
            Check(float.IsNaN(p.IntentCosAngleAboveVelocity),"NaN intent above propagates");
            foreach(string n in new[]
            {
                "m_maximumAngleToPlane","m_ledgeMinAngleLimit","m_intentAngleBelowVelocity","m_intentAngleAboveVelocity"
            }
            ) Write(p,n,0f);
            p.CalculateCachedValues();
            Check(p.MaximumCosAngleToPlane==1f && p.LedgeCosAngleLimit==1f && p.IntentCosAngleBelowVelocity==1f && p.IntentCosAngleAboveVelocity==1f,"finite recache replaces all exceptional caches");
            LedgeBrakeParameters second = new LedgeBrakeParameters();
            Check(second.MaximumCosAngleToPlane==0f && second.MaximumVelocityThreshold==10f,"second instance has original independent initialization");
            return checks;
        }
    }
}
