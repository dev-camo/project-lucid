using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000016. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class SourceApplePluginControllerProvider
        : BaseSourceProvider,
          ISourceApplePluginControllerProvider,
          IBaseSourceProvider<
              IApplePluginControllerInputKeySource,
              GCControllerInputName,
              IApplePluginControllerInputAxisSource,
              GCControllerInputName>
    {
        private SourceApplePluginController m_sourceApplePluginController;

        public IApplePluginControllerInputKeySource KeySource
        {
            get { return m_sourceApplePluginController; }
        }

        public IApplePluginControllerInputAxisSource AxisSource
        {
            get { return m_sourceApplePluginController; }
        }

        public IApplePluginControllerGlyphSource GlyphSource
        {
            get { return m_sourceApplePluginController; }
        }

        public override void Initialise(IBaseControllerProvider baseControllerProvider)
        {
            m_sourceApplePluginController =
                new SourceApplePluginController(baseControllerProvider as IControllerProvider<GCController>);
        }

        public override void Shutdown()
        {
            m_sourceApplePluginController = null;
        }

        public override void Update()
        {
            m_sourceApplePluginController?.Update();
        }

        public SourceApplePluginControllerProvider()
        {
        }
    }
}
