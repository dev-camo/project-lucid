using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActiveGameInputBindings : BaseActiveBindings<GameInputBinding, BindingData>
    {
        [FormerlySerializedAs("m_editorBindings"), UsedImplicitly, SerializeField] private List<GameInputBinding> m_editorMacBindings;
        [UsedImplicitly, SerializeField] private List<GameInputBinding> m_editorWindowsBindings;
        [UsedImplicitly, SerializeField] private List<GameInputBinding> m_androidBindings;
        [UsedImplicitly, SerializeField] private List<GameInputBinding> m_iOSBindings;
        [UsedImplicitly, SerializeField] private List<GameInputBinding> m_tvOSBindings;
        [SerializeField, UsedImplicitly] private List<GameInputBinding> m_macOSBindings;
        [UsedImplicitly, SerializeField] private List<GameInputBinding> m_windowsBindings;
        [SerializeField, UsedImplicitly] private List<GameInputBinding> m_switchBindings;
        public const string DefaultFileName = "ActiveGameInputBindings";

        // HLInput.Runtime 0600003d: both shipped Mac architectures select the authored macOS list.
        // Other platform/editor preprocessor bodies are absent from this binary and have not been invented.
        protected override void SetupBindings() { SetActiveBindings(m_macOSBindings); }
        public ActiveGameInputBindings() { } // 0600003e: real base constructor only; every authored list stays null.
    }
}
