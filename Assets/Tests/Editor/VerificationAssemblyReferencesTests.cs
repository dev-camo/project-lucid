using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;

namespace ProjectLucid.Tests.Editor
{
    public sealed class VerificationAssemblyReferencesTests
    {
        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string name = string.Empty;
            public string[] references = Array.Empty<string>();
        }

        [TestCase("ProjectLucid.Verification", TestName = "VerificationAssemblyDeclaresAndResolvesPinnedUI")]
        [TestCase("ProjectLucid.PlayMode", TestName = "PlayModeAssemblyDeclaresAndResolvesPinnedUI")]
        [TestCase("ProjectLucid.CoroutineScroll.EngineVerification", TestName = "CoroutineScrollAssemblyDeclaresAndResolvesPinnedUI")]
        [TestCase("HLUnityUI.Runtime", TestName = "UnityUIRuntimeAssemblyDeclaresAndResolvesPinnedUI")]
        [TestCase("HLEventSystems.Runtime", TestName = "EventSystemsRuntimeAssemblyDeclaresAndResolvesPinnedUI")]
        [TestCase("HLCrossPromo.Runtime", TestName = "CrossPromoRuntimeAssemblyDeclaresAndResolvesPinnedUI")]
        [TestCase("ProjectLucid.CrossPromoUI.EngineVerification", TestName = "CrossPromoUIAssemblyDeclaresAndResolvesPinnedUI")]
        public void VerificationAssemblyResolvesPinnedUI(string assemblyName)
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            var owners = assemblies.Where(assembly => assembly.name == assemblyName).ToArray();
            Assert.That(owners, Has.Length.EqualTo(1), assemblyName);

            var uiReferences = owners[0].assemblyReferences
                .Where(assembly => assembly.name == "UnityEngine.UI").ToArray();
            Assert.That(uiReferences, Has.Length.EqualTo(1), assemblyName);

            // Unity can inject UI automatically; also require the explicit declaration.
            var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
            Assert.That(asmdefPath, Is.Not.Null.And.Not.Empty, assemblyName);
            var definition = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(asmdefPath));
            Assert.That(definition, Is.Not.Null, asmdefPath);
            Assert.That(definition.name, Is.EqualTo(assemblyName), asmdefPath);
            Assert.That(definition.references, Is.Not.Null, asmdefPath);
            Assert.That(definition.references.Count(reference => reference == "UnityEngine.UI"), Is.EqualTo(1),
                asmdefPath + " must explicitly declare UnityEngine.UI");
            Assert.That(definition.references, Does.Not.Contain("Unity.ugui"), asmdefPath);
        }
    }
}
