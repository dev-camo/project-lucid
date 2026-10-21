using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000994; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class UserEditableUIBase : MonoBehaviour
    {
        public readonly Bindable<bool> EditModeActive = new Bindable<bool>();
        [Header("UI events")]
        [SerializeField] private UIModernEvent m_editStartEvent;
        [SerializeField] private UIModernEvent m_editCancelEvent;
        [SerializeField] private UIModernEvent m_editConfirmEvent;
        [SerializeField] private UIModernEvent m_editFlipEvent;
        [SerializeField] private UIModernEvent m_editResetEvent;
        private readonly UIModernMessage m_uiModernMessage = new UIModernMessage(UIModernEventType.OnClick);
        private MessageExchangeBoundCallbackArg<UIModernMessage> m_messageExchange;

        // 06003750..06003757 (1.10.1 RVA 0x697070..0x6970F0): compiler-generated accessors.
        protected bool m_initialised { get; private set; }
        protected bool m_editMode { get; private set; }
        protected SaveDataSettings m_saveDataSettings { get; private set; }
        protected bool m_isFlipped { get; private set; }

        // 06003758 (RVA 0x697100): all five edit events are bound to their own UIModernEvent assets.
        private void Awake()
        {
            m_initialised = false;
            m_editMode = false;
            EditModeActive.Value = false;
            m_messageExchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            m_messageExchange.SubscribeToMessage<UIModernEvent>(in m_uiModernMessage, in m_editStartEvent, OnEditStart);
            m_messageExchange.SubscribeToMessage<UIModernEvent>(in m_uiModernMessage, in m_editCancelEvent, OnEditCancel);
            m_messageExchange.SubscribeToMessage<UIModernEvent>(in m_uiModernMessage, in m_editConfirmEvent, OnEditConfirm);
            m_messageExchange.SubscribeToMessage<UIModernEvent>(in m_uiModernMessage, in m_editFlipEvent, OnEditFlip);
            m_messageExchange.SubscribeToMessage<UIModernEvent>(in m_uiModernMessage, in m_editResetEvent, OnEditReset);
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(OnSaveManagerValid);
        }

        // 06003759 (RVA 0x6973B0)
        protected virtual void OnValidate()
        {
            gameObject.CanValidate();
        }

        // 0600375a (RVA 0x6973D0)
        private void OnDestroy()
        {
            m_initialised = false;
            if (m_messageExchange == null) return;
            m_messageExchange.UnsubscribeFromMessage<UIModernEvent>(in m_uiModernMessage, in m_editStartEvent, OnEditStart);
            m_messageExchange.UnsubscribeFromMessage<UIModernEvent>(in m_uiModernMessage, in m_editCancelEvent, OnEditCancel);
            m_messageExchange.UnsubscribeFromMessage<UIModernEvent>(in m_uiModernMessage, in m_editConfirmEvent, OnEditConfirm);
            m_messageExchange.UnsubscribeFromMessage<UIModernEvent>(in m_uiModernMessage, in m_editFlipEvent, OnEditFlip);
            m_messageExchange.UnsubscribeFromMessage<UIModernEvent>(in m_uiModernMessage, in m_editResetEvent, OnEditReset);
        }

        // 0600375b (RVA 0x6975C0): initialises lazily once the save settings have arrived.
        private void Update()
        {
            if (m_initialised)
            {
                if (m_editMode) DoUpdate();
            }
            else if (m_saveDataSettings != null)
            {
                Initialise();
                if (m_editMode) DoUpdate();
            }
        }

        // 0600375c (RVA 0x6977D0)
        protected virtual void DoUpdate()
        {
        }

        // 0600375d (RVA 0x697610): the stored ControlsEditMode flag arrives inverted into m_editMode.
        private void Initialise()
        {
            m_editMode = !ProcessManager.GetSystem<App>().Storage.GetValue<bool>(AppFSMKeys.ControlsEditMode);
            EditModeActive.Value = m_editMode;
            m_isFlipped = m_saveDataSettings.OnScreenControlsFlipped;
            DoInitialise();
            if (m_editMode)
            {
                ExitEditMode();
            }
            else
            {
                UIModernEvent noEvent = null;
                OnEditStart(in noEvent);
            }
            m_initialised = true;
        }

        // 0600375e (RVA 0x6977E0)
        protected virtual void DoInitialise()
        {
        }

        // 0600375f (RVA 0x6977F0)
        protected virtual void OnSaveManagerValid(SaveManager saveManager)
        {
            m_saveDataSettings = saveManager.GetSaveDataSettings();
        }

        // 06003760 (RVA 0x697810)
        protected virtual void OnEditStart(in UIModernEvent uiModernEvent)
        {
            m_editMode = true;
            EditModeActive.Value = true;
        }

        // 06003761 (RVA 0x697840)
        protected virtual void OnEditCancel(in UIModernEvent uiModernEvent)
        {
            ExitEditMode();
        }

        // 06003762 (RVA 0x697860): publishes the settings back to the app storage, then leaves edit mode.
        protected virtual void OnEditConfirm(in UIModernEvent uiModernEvent)
        {
            ProcessManager.GetSystem<App>().Storage.SetValue<SaveDataSettings>(AppFSMKeys.GameSettings, m_saveDataSettings);
            ExitEditMode();
        }

        // 06003763 (RVA 0x6979D0)
        protected virtual void OnEditFlip(in UIModernEvent uiModernEvent)
        {
            m_isFlipped = !m_isFlipped;
        }

        // 06003764 (RVA 0x6979E0)
        protected virtual void OnEditReset(in UIModernEvent uiModernEvent)
        {
            m_isFlipped = false;
        }

        // 06003765 (RVA 0x6979F0)
        protected virtual void ExitEditMode()
        {
            m_editMode = false;
            EditModeActive.Value = false;
        }

        // 06003766 (RVA 0x697A20)
        protected UserEditableUIBase()
        {
        }
    }
}
