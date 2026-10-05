using System;
using System.Reflection;

namespace Hardlight
{
    public class JSONFactoryClass
    {
        public readonly Type ClassType;
        public readonly MethodInfo ConstructInstanceMethod;
        public readonly Type JSONCtorArgsType;

        // HLUnityCore.Runtime.dll:Hardlight.JSONFactoryClass:0x0600093b;
        // arm64 0x1af5ebc stores the three supplied references unchanged.
        public JSONFactoryClass(Type classType, MethodInfo constructInstanceMethod, Type jsonCtorArgsType)
        {
            ClassType = classType;
            ConstructInstanceMethod = constructInstanceMethod;
            JSONCtorArgsType = jsonCtorArgsType;
        }
    }
}
