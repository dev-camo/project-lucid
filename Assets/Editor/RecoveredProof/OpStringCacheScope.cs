using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ProjectLucid
{
    // OpString.i (0x0600000d) hands its one cached builder to the first thread
    // that reads it and clears it on every owner read. A fixture that exercises
    // that route takes this scope: the type initializer runs first, a fresh
    // 1024-capacity cache with no owner thread replaces the shared one, and
    // Dispose puts back the exact prior instance and owner thread. The borrowed
    // builder is never read through OpString.i, so its content is not cleared.
    public sealed class OpStringCacheScope : IDisposable
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly FieldInfo InstanceField = typeof(OpString).GetField("instance", PrivateStatic);
        private static readonly FieldInfo ThreadField = typeof(OpString).GetField("singletonThread", PrivateStatic);

        private readonly object priorInstance;
        private readonly object priorThread;
        private bool released;

        public OpString Owned { get; }

        public OpStringCacheScope()
        {
            RuntimeHelpers.RunClassConstructor(typeof(OpString).TypeHandle);
            priorInstance = InstanceField.GetValue(null);
            priorThread = ThreadField.GetValue(null);
            Owned = OpString.Create(1024);
            InstanceField.SetValue(null, Owned);
            ThreadField.SetValue(null, null);
        }

        public void Dispose()
        {
            if (released) return;
            released = true;
            InstanceField.SetValue(null, priorInstance);
            ThreadField.SetValue(null, priorThread);
        }
    }
}
