using Hardlight;
using Hardlight.UI.Binding;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000955, all four methods and two fields. Shows how far the
    // data manager has got through loading the definition data.
    public class DefinitionDataProgressView : ViewBase
    {
        public readonly Bindable<float> DataLoadProgress = new Bindable<float>();
        private SystemRef<DataManager> m_dataManager;

        // 0x060035a2: the reference is taken at Awake, not at construction.
        private void Awake()
        {
            m_dataManager = ProcessManager.GetSystemRef<DataManager>(null, true);
        }

        // 0x060035a3: authentic empty body.
        protected override void OnSetup(ViewParameters viewParams) { }

        // 0x060035a4: the manager's progress clamped to 0..1; nothing is published until it exists.
        private void Update()
        {
            if (m_dataManager.IsNull())
                return;
            DataLoadProgress.Value = Mathf.Clamp01(m_dataManager.Get().Progress);
        }

        // 0x060035a5: field initialiser, then the ViewBase constructor.
        public DefinitionDataProgressView() { }
    }
}
