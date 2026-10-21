using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 02000996; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UserEditableUIFlipper : UserEditableUIBase
    {
        [SerializeField] private HorizontalLayoutGroup m_flipLayoutGroup;
        [SerializeField] private UserEditableUIComponentController[] m_editableComponents;

        // 06003779 (1.10.1 RVA 0x698980)
        protected override void OnValidate()
        {
            base.OnValidate();
            gameObject.CanValidate();
        }

        // 0600377a (RVA 0x6989B0)
        protected override void DoInitialise()
        {
            base.DoInitialise();
            UpdateFlip();
        }

        // 0600377b (RVA 0x698AA0)
        protected override void OnEditFlip(in UIModernEvent uiModernEvent)
        {
            base.OnEditFlip(in uiModernEvent);
            UpdateFlip();
        }

        // 0600377c (RVA 0x698B20): the flip is written to the save settings before the base class publishes them.
        protected override void OnEditConfirm(in UIModernEvent uiModernEvent)
        {
            m_saveDataSettings.OnScreenControlsFlipped = m_isFlipped;
            base.OnEditConfirm(in uiModernEvent);
        }

        // 0600377d (RVA 0x698B50)
        protected override void OnEditReset(in UIModernEvent uiModernEvent)
        {
            base.OnEditReset(in uiModernEvent);
            UpdateFlip();
        }

        // 0600377e (RVA 0x698A30)
        private void UpdateFlip()
        {
            StartCoroutine(UpdateFlipDelayed());
        }

        // 0600377f (RVA 0x698BD0); MoveNext 06003783 (0x698C80). The layout is flipped at the end of the frame.
        private IEnumerator UpdateFlipDelayed()
        {
            yield return new WaitForEndOfFrame();
            m_flipLayoutGroup.reverseArrangement = m_isFlipped;
            foreach (UserEditableUIComponentController component in m_editableComponents)
            {
                if (component.gameObject.activeInHierarchy)
                    component.UpdateAfterParentFlipped();
            }
        }

        // 06003780 (RVA 0x698C60)
        public UserEditableUIFlipper()
        {
        }
    }
}
