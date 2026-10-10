using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HardlightProject
{
    // Complete original Game.Runtime 0200042a, own methods 06001888..06001893.
    // Inferred source preserves native callback/fault order; runtime acceptance is separate.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ComicPanel : MonoBehaviour
    {
        [SerializeField] private RawImage m_renderTextureOutput; // 04000e07
        [SerializeField] private Animation m_animatingRoot; // 04000e08
        [Space(5), Header("Player Choice Elements"), SerializeField]
        private PlayerChoicePosition m_playerChoicePosition; // 04000e09
        [SerializeField] private Button m_button; // 04000e0a
        [ShowIf("m_button", null), SerializeField]
        private GameInputGlyphView m_glyphView; // 04000e0b
        [ShowIf("m_button", null), SerializeField]
        private GameInput m_submitInput = GameInput.UISubmit; // 04000e0c
        [SerializeField] private UnityEvent m_OnSelected; // 04000e0d
        [SerializeField] private UnityEvent m_OnDeselected; // 04000e0e
        [SerializeField] private UnityEvent m_OnSubmitted; // 04000e0f
        [SerializeField] private UnityEvent m_onReset; // 04000e10
        private AnimationClip m_enterAnim; // 04000e11
        private AnimationClip m_loopAnim; // 04000e12
        private AnimationClip m_exitAnim; // 04000e13
        private ComicPanelState m_state; // 04000e14
        private ComicCutscene m_comicCutscene; // 04000e15
        private GameInput m_selectInput; // 04000e16
        private GameInput m_deselectInput; // 04000e17
        private InputType m_lastInputType; // 04000e18

        // Original nested-private Int32 enum 0200042b, zero method bodies.
        private enum PlayerChoicePosition { None = 0, Left = 1, Right = 2 }
        // Original nested-private Int32 enum 0200042c, zero method bodies.
        private enum ComicPanelState { None = 0, Entering = 1, Looping = 2, Exiting = 3 }

        // Original 06001888: activate/deactivate callbacks occur before the cutscene store.
        public void Initialise(ComicPanelBehaviour behaviour, ComicCutscene comicCutscene)
        {
            if (m_renderTextureOutput != null)
                m_renderTextureOutput.texture = behaviour.RenderTexture;
            m_enterAnim = behaviour.EnterAnim;
            m_loopAnim = behaviour.LoopAnim;
            m_exitAnim = behaviour.ExitAnim;
            m_state = ComicPanelState.None;
            m_animatingRoot.gameObject.SetActive(false);
            m_comicCutscene = comicCutscene;
        }

        // Original 06001889: state stores precede Stop/SampleAnimation faults.
        public void EnterExit(float inputWeight)
        {
            m_animatingRoot.gameObject.SetActive(true);
            if (m_state == ComicPanelState.None)
                m_state = ComicPanelState.Entering;
            else if (m_state == ComicPanelState.Looping)
            {
                m_state = ComicPanelState.Exiting;
                m_animatingRoot.Stop();
            }
            if (m_state == ComicPanelState.Entering)
                m_enterAnim.SampleAnimation(m_animatingRoot.gameObject, inputWeight);
            else if (m_state == ComicPanelState.Exiting)
                m_exitAnim.SampleAnimation(m_animatingRoot.gameObject, 1f - inputWeight);
        }

        // Original 0600188a: Looping is stored first; LastInputType is read twice.
        public void SetLooping()
        {
            if (m_state == ComicPanelState.Looping)
                return;
            m_state = ComicPanelState.Looping;
            m_animatingRoot.gameObject.SetActive(true);
            if (m_loopAnim != null)
                PlayAnimation(m_loopAnim);
            if (m_playerChoicePosition != PlayerChoicePosition.None)
            {
                m_lastInputType = ControlMapping.LastInputType;
                OnInputTypeChanged(ControlMapping.LastInputType);
                ControlMapping.OnUpdateLastInputType += OnInputTypeChanged;
            }
        }

        // Original 0600188b: callback faults leave m_lastInputType at its prior value.
        private void OnInputTypeChanged(InputType lastInputType)
        {
            if (lastInputType == InputType.Mouse || lastInputType == InputType.Touch || lastInputType == InputType.Keyboard)
            {
                if (m_lastInputType != lastInputType)
                    m_onReset?.Invoke();
            }
            else if (m_playerChoicePosition == PlayerChoicePosition.Left)
            {
                m_selectInput = GameInput.UILeft;
                m_deselectInput = GameInput.UIRight;
                SetSelected();
            }
            else if (m_playerChoicePosition == PlayerChoicePosition.Right)
            {
                m_selectInput = GameInput.UIRight;
                m_deselectInput = GameInput.UILeft;
                SetDeselected();
            }
            m_lastInputType = lastInputType;
        }

        // Original 0600188c: original unsubscribe/subscribe/event/glyph order retained.
        private void SetSelected(float _ = 0f)
        {
            ControlMapping.Unsubscribe(m_selectInput, SetSelected, -1);
            ControlMapping.Subscribe(m_deselectInput, SetDeselected, InputTrigger.Up, -1);
            ControlMapping.Subscribe(m_submitInput, TriggerButtonAction, InputTrigger.Up, -1);
            m_OnSelected?.Invoke();
            if (m_glyphView == null)
                return;
            m_glyphView.gameObject.SetActive(false);
        }

        // Original 0600188d: callback mutation can change the subsequent glyph/input reads.
        public void SetDeselected(float _ = 0f)
        {
            ControlMapping.Unsubscribe(m_deselectInput, SetDeselected, -1);
            ControlMapping.Unsubscribe(m_submitInput, TriggerButtonAction, -1);
            ControlMapping.Subscribe(m_selectInput, SetSelected, InputTrigger.Down, -1);
            m_OnDeselected?.Invoke();
            if (m_glyphView == null)
                return;
            m_glyphView.SetInput(m_selectInput, GameAction.None, GameAction.None);
            m_glyphView.gameObject.SetActive(true);
        }

        // Original 0600188e: capture clip.name before reading the animating root.
        private void PlayAnimation(AnimationClip clip)
        {
            string name = clip.name;
            m_animatingRoot.AddClip(clip, name);
            m_animatingRoot.Play(name);
        }

        // Original 0600188f: optional submitted event precedes an unguarded Button click.
        public void TriggerButtonAction(float _)
        {
            m_OnSubmitted?.Invoke();
            m_button.onClick.Invoke();
        }

        // Original 06001890: original cutscene selection result gates selection callbacks.
        public void Action_SelectOptionA()
        {
            if (m_comicCutscene.SelectCharacterOptionA(this))
                SetSelected();
        }

        // Original 06001891: original array option one, through the cutscene's genuine API.
        public void Action_SelectOptionB()
        {
            if (m_comicCutscene.SelectCharacterOptionB(this))
                SetSelected();
        }

        // Original 06001892: cleanup is conditional only on authored choice position.
        private void OnDestroy()
        {
            if (m_playerChoicePosition == PlayerChoicePosition.None)
                return;
            ControlMapping.Unsubscribe(m_selectInput, SetSelected, -1);
            ControlMapping.Unsubscribe(m_deselectInput, SetDeselected, -1);
            ControlMapping.Unsubscribe(m_submitInput, TriggerButtonAction, -1);
            ControlMapping.OnUpdateLastInputType -= OnInputTypeChanged;
        }

        // Original 06001893: UISubmit field initialization precedes the real base constructor.
        public ComicPanel() { }
    }
}
