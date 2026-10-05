using System;
using System.Reflection;
using Hardlight;
namespace ProjectLucid
{
    // Private diagnostic classes exercise the genuine original interface. They
    // are test fixtures, never runtime Actor or scheduler substitutes.
    public static class DefaultInterfaceCompatibility
    {
        private sealed class DefaultCallbacks : ITimeScaled
        {
            public bool IsPaused { get; set; }
        }
        private class ExplicitCallbacks : ITimeScaled
        {
            public bool IsPaused { get; set; }
            internal string Calls = "";
            public void OnUpdate(float deltaTime) { Calls += "U" + deltaTime.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            public void OnPause() { Calls += "P"; }
            public void OnResume() { Calls += "R"; }
        }
        private sealed class InheritedCallbacks : ExplicitCallbacks { }
        private static int checks;
        private static void Require(bool condition, string name)
        { if(!condition) throw new InvalidOperationException(name); checks++; }
        public static int Run()
        {
            checks = 0;
            Type t=typeof(ITimeScaled);
            Require(t.IsInterface && t.IsAbstract && t.IsPublic,"original interface kind");
            MethodInfo[] all=t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
            Require(all.Length==7,"complete original own declarations");
            string[] names={"OnUpdate","OnFixedUpdate","OnLateUpdate","OnPause","OnResume","get_IsPaused","set_IsPaused"};
            for(int i=0;i<names.Length;i++)
            {
                MethodInfo m=all[i];
                Require(m.Name==names[i],"original ordered member");
                Require(m.IsVirtual && (m.Attributes & MethodAttributes.NewSlot)!=0,"original virtual new slot");
                Require(m.IsAbstract==(i>=5),"five bodies versus two contracts");
                if(i<5)
                {
                    Require(!m.IsFinal && m.GetMethodBody()!=null,"genuine default method body");
                    byte[] il=m.GetMethodBody().GetILAsByteArray();
                    Require(il.Length==1 && il[0]==0x2a,"original empty callback compiled RET");
                    ParameterInfo[] pars=m.GetParameters();
                    Require(pars.Length==(i<3?1:0),"callback parameters");
                    if(i<3)Require(pars[0].Name=="deltaTime" && pars[0].ParameterType==typeof(float),"original delta parameter");
                }
                else Require(m.GetMethodBody()==null && m.IsSpecialName,"real pause accessor contract");
            }
            var raw=new DefaultCallbacks(); ITimeScaled defaults=raw;
            defaults.OnUpdate(float.NaN); defaults.OnFixedUpdate(float.NegativeInfinity); defaults.OnLateUpdate(-12.5f);
            defaults.OnPause(); defaults.OnResume();
            Require(!defaults.IsPaused,"empty callback defaults do not implement pause state");
            defaults.IsPaused=true; defaults.OnPause(); defaults.OnResume();
            Require(raw.IsPaused,"empty default callbacks retain concrete accessor state");
            defaults.IsPaused=false;
            Require(!raw.IsPaused,"real accessor dispatch");
            var explicitObject=new ExplicitCallbacks(); ITimeScaled explicitInterface=explicitObject;
            explicitInterface.OnUpdate(2.5f); explicitInterface.OnFixedUpdate(5); explicitInterface.OnLateUpdate(6);
            explicitInterface.OnPause(); explicitInterface.OnResume();
            Require(explicitObject.Calls=="U2.5PR","explicit overrides plus inherited defaults dispatch");
            var derived=new InheritedCallbacks(); ITimeScaled inherited=derived;
            inherited.OnUpdate(-3); inherited.OnPause(); inherited.OnResume(); inherited.OnFixedUpdate(0);
            Require(derived.Calls=="U-3PR","inherited concrete implementations beat interface defaults");
            InterfaceMapping map=typeof(DefaultCallbacks).GetInterfaceMap(t);
            Require(map.InterfaceMethods.Length==7 && map.TargetMethods.Length==7,"complete actual interface map");
            for(int i=0;i<5;i++)Require(map.TargetMethods[i].DeclaringType==t,"default map targets genuine interface method");
            Require(map.TargetMethods[5].DeclaringType==typeof(DefaultCallbacks) && map.TargetMethods[6].DeclaringType==typeof(DefaultCallbacks),"pause accessors target concrete fixture");
            return checks;
        }
    }
}
