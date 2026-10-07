using System;
using System.Reflection;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalAddressableManagerBehaviorTests
    {
        [Test]
        public void OriginalAddressableManagerRetainsManagedCacheAndIteratorContracts()
        {
            Assert.That(ProjectLucid.Editor.AddressableManagerVerification.RunManaged(), Is.EqualTo(52));
        }

        [Test]
        public void OriginalAddressableManagerRetainsRealPackageHandleAndCallbackContracts()
        {
            // The genuine failed package operation invokes the process-wide handler.
            // Use the real types without adding package dependencies to this test assembly.
            var resourceManager = Assembly.Load("Unity.ResourceManager").GetType("UnityEngine.ResourceManagement.ResourceManager", true);
            var exceptionHandler = resourceManager.GetProperty("ExceptionHandler", BindingFlags.Public | BindingFlags.Static);
            var processManager = Assembly.Load("HLUnityCore.Runtime").GetType("Hardlight.ProcessManager", true);
            var registry = processManager.GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static);
            var originalHandler = exceptionHandler.GetValue(null);
            var originalRegistry = registry.GetValue(null);
            var fixtureRegistry = Activator.CreateInstance(registry.FieldType);
            try
            {
                registry.SetValue(null, fixtureRegistry);
                exceptionHandler.SetValue(null, null);
                Assert.That(ProjectLucid.Editor.AddressableManagerVerification.RunResourceManagerManaged(), Is.EqualTo(24));
            }
            finally
            {
                try { exceptionHandler.SetValue(null, originalHandler); }
                finally { registry.SetValue(null, originalRegistry); }
            }
        }

        [Test]
        public void OriginalAddressableManagerRetainsRealComponentCloneAndFailureContracts()
        {
            Assert.That(ProjectLucid.Editor.AddressableManagerVerification.RunEngine(), Is.EqualTo(29));
        }
    }
}
