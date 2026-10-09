using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ActiveBindingsInputMonitor<TActiveInputBindings, TInputBindingProvider, TBindingData, TInputButtonKey, TInputAxisKey> : BaseInputMonitor
        where TActiveInputBindings : BaseActiveBindings<TInputBindingProvider, TBindingData>
        where TInputBindingProvider : BaseInputBinding<TBindingData>
        where TBindingData : BaseBindingData
    {
        [SerializeField] private TActiveInputBindings m_inputBinding;
        [SerializeField] protected RuntimePlatform[] m_activePlatforms;
        public override ICurrentBaseInputBindingsProvider CurrentBaseInputBindingsProvider => m_inputBinding; // Original06000173.
        protected abstract BaseSourceProvider BaseSourceProvider { get; } // Original06000174.
        protected IGlyphLookupSystemSetter GlyphLookupSystemSetter { get; private set; } // Original06000175/176.

        // Original06000177: the real base's platform guard does not guard this
        // remaining sequence. Subscribe only after initial processor creation.
        public sealed override void Initialise(IBaseControllerProvider baseControllerProvider, ControlMap controlMap,
            IGlyphLookupSystemSetter glyphLookupSystemSetter)
        {
            base.Initialise(baseControllerProvider, controlMap, glyphLookupSystemSetter);
            BaseSourceProvider.Initialise(baseControllerProvider);
            InitialiseGlyphMapSuppliers();
            GlyphLookupSystemSetter = glyphLookupSystemSetter;
            m_inputBinding.Initialise(baseControllerProvider);
            InitialiseInputProcessors(GlyphLookupSystemSetter, m_inputBinding.CurrentInputBindings);
            m_inputBinding.OnCurrentInputBindingsUpdate += OnCurrentInputBindingsUpdate;
        }

        protected abstract void InitialiseGlyphMapSuppliers(); // Original06000178.
        protected abstract void ShutdownGlyphMapSuppliers(); // Original06000179.
        protected abstract void InitialiseInputProcessors(IGlyphLookupSystemSetter glyphLookupSystemSetter,
            IReadOnlyList<TInputBindingProvider> currentInputBindings); // Original0600017a.
        protected abstract void ShutdownInputProcessors(); // Original0600017b.

        private void OnCurrentInputBindingsUpdate(IReadOnlyList<TInputBindingProvider> currentInputBindings) // Original0600017c.
        {
            RefreshInputProcessors(GlyphLookupSystemSetter, currentInputBindings);
        }

        private void RefreshInputProcessors(IGlyphLookupSystemSetter glyphLookupSystemSetter,
            IReadOnlyList<TInputBindingProvider> currentInputBindings) // Original0600017d.
        {
            ShutdownInputProcessors();
            InitialiseInputProcessors(glyphLookupSystemSetter, currentInputBindings);
        }

        private void OnDestroy() { Shutdown(); } // Original0600017e, genuine virtual dispatch.

        // Original0600017f retains base, unsubscribe, processors, bindings,
        // setter clear, maps, source order. No re-entry or null guard is added.
        public sealed override void Shutdown()
        {
            base.Shutdown();
            m_inputBinding.OnCurrentInputBindingsUpdate -= OnCurrentInputBindingsUpdate;
            ShutdownInputProcessors();
            m_inputBinding.Shutdown();
            GlyphLookupSystemSetter = null;
            ShutdownGlyphMapSuppliers();
            BaseSourceProvider.Shutdown();
        }

        protected override bool IsActiveOnPlatform() => ArrayExtensions.Contains(m_activePlatforms, Application.platform); // Original06000180.
        public override void Update() { base.Update(); BaseSourceProvider.Update(); } // Original06000181.

        // Original06000182 captures both arguments before processor shutdown.
        // Losing focus and a CLR-null setter do nothing; bindings remain unguarded.
        public sealed override void ApplicationFocus(bool focused)
        {
            if (focused && GlyphLookupSystemSetter != null)
                RefreshInputProcessors(GlyphLookupSystemSetter, m_inputBinding.CurrentInputBindings);
        }

        protected ActiveBindingsInputMonitor() : base() { } // Original06000183.
    }
}
