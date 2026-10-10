using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000016. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public sealed class SourceApplePluginControllerProvider : Hardlight.BaseSourceProvider, Hardlight.ISourceApplePluginControllerProvider, Hardlight.IBaseSourceProvider<Hardlight.IApplePluginControllerInputKeySource, Apple.GameController.Controller.GCControllerInputName, Hardlight.IApplePluginControllerInputAxisSource, Apple.GameController.Controller.GCControllerInputName>
    {
        private Hardlight.SourceApplePluginController m_sourceApplePluginController;

        // Original 0x0600003f; complete ARM64 and x86-64 bodies retained.
        public Hardlight.IApplePluginControllerInputKeySource KeySource
        {
            get { return m_sourceApplePluginController; }
        }

        // Original 0x06000040; complete ARM64 and x86-64 bodies retained.
        public Hardlight.IApplePluginControllerInputAxisSource AxisSource
        {
            get { return m_sourceApplePluginController; }
        }

        // Original 0x06000041; complete ARM64 and x86-64 bodies retained.
        public Hardlight.IApplePluginControllerGlyphSource GlyphSource
        {
            get { return m_sourceApplePluginController; }
        }

        // Original 0x06000042; complete ARM64 and x86-64 bodies retained.
        public override void Initialise(Hardlight.IBaseControllerProvider baseControllerProvider)
        {
            m_sourceApplePluginController = new SourceApplePluginController(baseControllerProvider as IControllerProvider<GCController>);
        }

        // Original 0x06000043; complete ARM64 and x86-64 bodies retained.
        public override void Shutdown()
        {
            m_sourceApplePluginController = null;
        }

        // Original 0x06000044; complete ARM64 and x86-64 bodies retained.
        public override void Update()
        {
            m_sourceApplePluginController?.Update();
        }

        // Original 0x06000045; complete ARM64 and x86-64 bodies retained.
        public SourceApplePluginControllerProvider()
        {
        }

    }
}
