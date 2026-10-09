using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class SourceProvider : BaseSourceProvider, ISourceProvider
    {
        private SourceController m_sourceController;
        private SourceMouse m_sourceMouse;
        private SourceTouchScreen m_sourceTouchScreen;
        // Original06000288/289/28a/28c final virtual newslot bits remain a
        // stripped-interface emitted-shape frontier, without invented interface APIs.
        public IInputKeySource KeySource => m_sourceController;
        public IInputAxisSource AxisSource => m_sourceController;
        public IInputPointerSource PointerSource => m_sourceMouse;
        public IInputTouchSource TouchSource => m_sourceMouse; // Original0600028b, same original mouse field.
        public IInputTouchSource GestureSource => m_sourceMouse;

        public override void Initialise(IBaseControllerProvider baseControllerProvider) // Original0600028d: argument unused.
        {
            CreateControllerSource();
            CreateMouseSource();
            CreateTouchSource();
        }
        public override void Shutdown() // Original0600028e: no provider Shutdown calls.
        {
            m_sourceController = null;
            m_sourceMouse = null;
            m_sourceTouchScreen = null;
        }
        public override void Update() // Original0600028f: real CLR-null guards and captured receivers.
        {
            m_sourceController?.Update();
            m_sourceMouse?.Update();
        }
        private void CreateControllerSource() { m_sourceController = new SourceController(); } // Original06000290.
        private void CreateMouseSource() { m_sourceMouse = new SourceMouse(); } // Original06000291.
        private void CreateTouchSource() { m_sourceTouchScreen = new SourceTouchScreen(); } // Original06000292.
        public SourceProvider() : base() { } // Original06000293, no field initialization.
    }
}
