using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000016. Every method body follows the complete ARM64 and x86-64 originals.
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

        // Original 0x0600003f
        public IApplePluginControllerInputKeySource KeySource
        {
            get { return m_sourceApplePluginController; }
        }

        // Original 0x06000040
        public IApplePluginControllerInputAxisSource AxisSource
        {
            get { return m_sourceApplePluginController; }
        }

        // Original 0x06000041
        public IApplePluginControllerGlyphSource GlyphSource
        {
            get { return m_sourceApplePluginController; }
        }

        // Original 0x06000042
        public override void Initialise(IBaseControllerProvider baseControllerProvider)
        {
            m_sourceApplePluginController =
                new SourceApplePluginController(baseControllerProvider as IControllerProvider<GCController>);
        }

        // Original 0x06000043
        public override void Shutdown()
        {
            m_sourceApplePluginController = null;
        }

        // Original 0x06000044
        public override void Update()
        {
            m_sourceApplePluginController?.Update();
        }

        // Original 0x06000045
        public SourceApplePluginControllerProvider()
        {
        }
    }
}
