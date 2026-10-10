using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006ca, complete eight-method declaration.
    // This source requires the real LevelManager/LevelData graph; it is not
    // independently accepted or backed by a replacement manager.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Renderer))]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class RegisterLevelRenderer : MonoBehaviour
    {
        [SerializeField] private LevelRendererIdentifier m_identifier;
        [SerializeField] private Renderer m_renderer;
        private SystemRef<LevelManager> m_levelManagerRef;

        // Original 0x060024d5/0x060024d6; ARM 0x57332c/0x573334.
        public LevelRendererIdentifier Identifier => m_identifier;
        public Renderer Renderer => m_renderer;

        // Original 0x060024d7; ARM 0x57333c. Acquire and assign the system
        // reference on every enable, before registering the valid callback.
        private void OnEnable()
        {
            m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
            m_levelManagerRef.InvokeOnValid(OnLevelManagerValid);
        }

        // Original 0x060024d8; ARM 0x573440. IsNull is a method on the stored
        // reference, not a managed null guard. Remove the pending callback first.
        private void OnDisable()
        {
            if (m_levelManagerRef.IsNull()) return;
            LevelManager levelManager = m_levelManagerRef.Get();
            levelManager.RemoveLevelLoadedAction(RegisterWithLevelData);
            if (levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                level.Data.UnregisterRenderer(this);
        }

        // Original 0x060024d9; ARM 0x57358c. The valid current level takes
        // the direct path; otherwise the loaded callback is retained by manager.
        private void OnLevelManagerValid(LevelManager levelManager)
        {
            if (levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                RegisterWithLevelData(level);
            else levelManager.InvokeOnLevelLoaded(RegisterWithLevelData);
        }

        // Original 0x060024da; ARM 0x573668. Remove the callback before
        // dereferencing supplied level data, preserving fault and reentrancy order.
        private void RegisterWithLevelData(LevelManagerLevel level)
        {
            if (m_levelManagerRef.IsNull()) return;
            m_levelManagerRef.Get().RemoveLevelLoadedAction(RegisterWithLevelData);
            level.Data.RegisterRenderer(this);
        }

        // Original 0x060024db; ARM 0x573764. Unity equality also treats a
        // destroyed Renderer as absent; GetComponent is only called in that case.
        private void OnValidate()
        {
            if (m_renderer == null) m_renderer = GetComponent<Renderer>();
        }

        // Original 0x060024dc; ARM 0x573840: the real MonoBehaviour base only.
        public RegisterLevelRenderer() { }
    }
}
