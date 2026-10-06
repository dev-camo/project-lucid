using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace ProjectLucid.CodeGen
{
    // Preserve the authored six auto-accessor marker orders after Unity's compiler.
    // The patcher validates the complete three-type declarations before any writes.
    public sealed class RibbonAccessorAttributeOrderPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => new RibbonAccessorAttributeOrderPostProcessor();
        public override bool WillProcess(ICompiledAssembly assembly) => assembly.Name == "HLSplines.Runtime";
        public override ILPostProcessResult Process(ICompiledAssembly assembly)
        {
            if (!WillProcess(assembly)) return null;
            try
            {
                byte[] output = RibbonAccessorAttributes.Restore(assembly.InMemoryAssembly.PeData);
                return new ILPostProcessResult(new InMemoryAssembly(output, assembly.InMemoryAssembly.PdbData));
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is InvalidOperationException || error is AssemblyResolutionException || error is BadImageFormatException || error is OverflowException || error is ArgumentException || error is IndexOutOfRangeException)
            {
                // Compilation must stop on an unexpected graph; retain both input
                // buffers and do not publish partially reordered metadata.
                return new ILPostProcessResult(assembly.InMemoryAssembly, new List<DiagnosticMessage> {
                    new DiagnosticMessage { DiagnosticType = DiagnosticType.Error, MessageData = "Ribbon accessor metadata restoration refused: " + error.Message }
                });
            }
        }
    }
}
