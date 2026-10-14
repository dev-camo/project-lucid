using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace ProjectLucid.Tests.Editor
{
    public sealed class VerificationAssemblyReferencesTests
    {
        [TestCase("ProjectLucid.Verification")]
        [TestCase("ProjectLucid.PlayMode")]
        [TestCase("ProjectLucid.CoroutineScroll.EngineVerification")]
        public void VerificationAssemblyResolvesPinnedUI(string assemblyName)
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            var owners = assemblies.Where(assembly => assembly.name == assemblyName).ToArray();
            Assert.That(owners, Has.Length.EqualTo(1), assemblyName);

            // Check Unity's resolved dependency graph after a real Editor import.
            var uiReferences = owners[0].assemblyReferences
                .Where(assembly => assembly.name == "UnityEngine.UI").ToArray();
            Assert.That(uiReferences, Has.Length.EqualTo(1), assemblyName);
        }
    }
}
