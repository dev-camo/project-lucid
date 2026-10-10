using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HardlightProject
{
    [RequireComponent(typeof(RawImage))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameInputGlyphView : MonoBehaviour
    {
        [SerializeField] [InspectorReadOnly] private RawImage m_rawImage;
        [SerializeField] private RawImage m_rawDecoratorImage;
        [SerializeField] private Image m_separatorImage;
        [SerializeField] private RawImage m_rawComboImage;
        [SerializeField] private GameInput m_gameInput = GameInput.None;
        [SerializeField] private GameAction m_gameAction;
        [SerializeField] private GameAction m_gameActionCombo;
        [SerializeField] private bool m_preserveAspectRatio = true;
        private InputType m_inputType;
        private Vector2 m_cachedSizeDelta;
        private static readonly SystemRef<DataManager> m_dataManagerRef =
            ProcessManager.GetSystemRef<DataManager>(null, true);

        // Game.Runtime 0x06003606: the authored main image is required here.
        private void Awake()
        {
            m_cachedSizeDelta = m_rawImage.rectTransform.sizeDelta;
        }

        // 0x06003607: clear, refresh from the last input type, then subscribe.
        // A fault in either earlier operation prevents the subscription.
        private void OnEnable()
        {
            SetImage(null, null);
            OnInputTypeChanged(ControlMapping.LastInputType);
            ControlMapping.OnUpdateLastInputType += OnInputTypeChanged;
        }

        // 0x06003608: remove one matching callback; image and state are retained.
        private void OnDisable()
        {
            ControlMapping.OnUpdateLastInputType -= OnInputTypeChanged;
        }

        // 0x06003609: mouse changes leave the previous input type and image intact.
        private void OnInputTypeChanged(InputType inputType)
        {
            if (inputType == InputType.Mouse)
                return;
            m_inputType = inputType;
            SetInput(m_gameInput, m_gameAction, m_gameActionCombo);
        }

        // 0x0600360a: None does not clear the previous selection.
        public void SetInput(GameInput input)
        {
            if (input == GameInput.None)
                return;
            SetInput(input, GameAction.None, GameAction.None);
        }

        // 0x0600360b: None is ignored, rather than resetting the action.
        public void SetAction(GameAction action)
        {
            if (action == GameAction.None)
                return;
            SetInput(GameInput.None, action, GameAction.None);
        }

        // 0x0600360c: a combo also requires an existing main action.
        public void SetActionCombo(GameAction actionCombo)
        {
            if (actionCombo == GameAction.None || m_gameAction == GameAction.None)
                return;
            SetInput(GameInput.None, m_gameAction, actionCombo);
        }

        // 0x0600360d: an absent combo image also skips the separator. Field reads
        // after SetActive are intentionally live because lifecycle callbacks can run.
        private void SetComboImage(bool comboEnabled, Texture2D comboGlyph)
        {
            if (m_rawComboImage == null)
                return;
            m_rawComboImage.gameObject.SetActive(comboEnabled);
            m_rawComboImage.texture = comboGlyph;
            if (m_separatorImage == null)
                return;
            m_separatorImage.gameObject.SetActive(comboEnabled);
        }

        // 0x0600360e: texture is assigned before the Unity null test sets enabled.
        private void SetDecoratorImage(Texture2D decoratorGlyph)
        {
            if (m_rawDecoratorImage == null)
                return;
            m_rawDecoratorImage.texture = decoratorGlyph;
            m_rawDecoratorImage.enabled = decoratorGlyph != null;
        }

        // 0x0600360f: combo changes precede main-image changes. A null glyph disables
        // the main image without replacing its texture or cached size. The original
        // aspect calculation divides cached Y by width/height; it has no zero guard.
        private void SetImage(Texture2D glyph, Texture2D decoratorGlyph, Texture2D comboGlyph = null)
        {
            SetComboImage(glyph != null && comboGlyph != null, comboGlyph);
            if (glyph == null)
            {
                m_rawImage.enabled = false;
                SetDecoratorImage(null);
                return;
            }
            m_rawImage.texture = glyph;
            m_rawImage.enabled = true;
            if (m_preserveAspectRatio)
            {
                float aspect = (float)glyph.width / glyph.height;
                m_rawImage.rectTransform.sizeDelta =
                    new Vector2(m_cachedSizeDelta.x, m_cachedSizeDelta.y / aspect);
            }
            SetDecoratorImage(decoratorGlyph);
        }

        // 0x06003610: publish all selection fields before any provider lookup. The
        // decorator and combo use live field reads after the preceding lookup.
        public void SetInput(GameInput input, GameAction actionMain, GameAction actionCombo = GameAction.None)
        {
            m_gameInput = input;
            m_gameAction = actionMain;
            m_gameActionCombo = actionCombo;
            Texture2D glyph = GetGlyph(input, actionMain);
            Texture2D decoratorGlyph = GetDecoratorForGameAction(m_gameAction);
            Texture2D comboGlyph = GetGlyph(GameInput.None, m_gameActionCombo);
            SetImage(glyph, decoratorGlyph, comboGlyph);
        }

        // 0x06003611: raw input type zero blocks lookups. Input glyphs precede
        // action-to-input mapping, then the genuine Game action-glyph fallback.
        private Texture2D GetGlyph(GameInput gameInput, GameAction gameAction)
        {
            if (m_inputType == 0)
                return null;
            Texture2D glyph = null;
            if (TryGetGlyphForGameInput(gameInput, ref glyph))
                return glyph;
            GameInput mappedInput;
            if (TryGetGameInputForGameAction(gameAction, out mappedInput)
                && TryGetGlyphForGameInput(mappedInput, ref glyph))
                return glyph;
            return GetGlyphForGameAction(gameAction);
        }

        // 0x06003612: own glyph parameter is ref, and an early false retains it.
        // The original provider's parameter is out. EventSystem.current is required;
        // only the cast module and its plain managed lookup object are guarded.
        private bool TryGetGlyphForGameInput(GameInput gameInput, ref Texture2D glyph)
        {
            if (gameInput == GameInput.None)
                return false;
            HLInputModule inputModule = EventSystem.current.currentInputModule as HLInputModule;
            return inputModule != null && inputModule.GlyphLookupSystem != null
                && inputModule.GlyphLookupSystem.TryGetGlyphForGameInput(gameInput, m_inputType, out glyph);
        }

        // 0x06003613: publish the authentic None literal before checking the action.
        private bool TryGetGameInputForGameAction(GameAction gameAction, out GameInput gameInput)
        {
            gameInput = GameInput.None;
            CharacterControlsMappingDefinition controlsMapping;
            return gameAction != GameAction.None && TryGetControlsMapping(out controlsMapping)
                && controlsMapping.TryGetGameInput(m_inputType, gameAction, out gameInput);
        }

        // 0x06003614: action None skips DataManager. A present dictionary entry
        // still dereferences its value without guarding a null glyph map.
        private Texture2D GetGlyphForGameAction(GameAction gameAction)
        {
            Texture2D glyph = null;
            GameActionGlyphMap glyphMap;
            if (gameAction == GameAction.None)
                return null;
            return m_dataManagerRef.Get().GameActionGlyphMapDefinitions.TryGetValue(m_inputType, out glyphMap)
                && glyphMap.TryGetGlyph(gameAction, out glyph) ? glyph : null;
        }

        // 0x06003615: no action-None guard is added to the decorator lookup.
        private Texture2D GetDecoratorForGameAction(GameAction gameAction)
        {
            CharacterControlsMappingDefinition controlsMapping;
            Texture2D glyph;
            return TryGetControlsMapping(out controlsMapping)
                && controlsMapping.TryGetDecoratorImage(m_inputType, gameAction, out glyph) ? glyph : null;
        }

        // 0x06003616: the required Character dictionary indexer may throw. A wrong
        // or Unity-null constant returns false, while a null mapping on a valid
        // constant is returned with true and may fault in the caller.
        private bool TryGetControlsMapping(out CharacterControlsMappingDefinition controlsMapping)
        {
            GlobalConstantDefinition_Character definition =
                m_dataManagerRef.Get().GlobalConstantDefinitions[GlobalConstantType.Character]
                    as GlobalConstantDefinition_Character;
            if (definition == null)
            {
                controlsMapping = null;
                return false;
            }
            controlsMapping = definition.ControlsMappingDefinition;
            return true;
        }

        // 0x06003617: the two authored initializers run before MonoBehaviour's
        // constructor. 0x06003618 is emitted from the readonly SystemRef initializer
        // above, preserving the original BeforeFieldInit source pattern.
        public GameInputGlyphView() { }
    }
}
