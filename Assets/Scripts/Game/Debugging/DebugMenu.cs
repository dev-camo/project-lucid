using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime owner 020005ec. The shipping build's stripped bodies
    // are preserved; Conditional attributes do not authorize invented menu behavior.
    public class DebugMenu : MonoBehaviour, ISystem
    {
        public static Action<bool> OnDebugMenuToggle;
        public static Action<bool> OnCharacterInfoToggle;
        private static readonly List<DebugButton> s_allButtons = new List<DebugButton>();
        private static List<DebugButton> s_buttonsToDraw = new List<DebugButton>();
        private static string s_currentMenuPath;
        private static readonly float s_defaultButtonHeightPixels = 75f;

        [SerializeField] public float m_scrollBarWidthPixels = 25f;
        [SerializeField] private float m_panelScreenWidthPercent = 0.3f;
        [SerializeField] private GUISkin m_guiSkin;
        [SerializeField] private bool m_shouldToggleCursor;
        [SerializeField] private Vector2 m_referenceScreenDimensions = new Vector2(1920f, 1080f);
        [SerializeField] private Hardlight.InputSupplier m_toggleDebugMenuInput;
        private Vector2 m_scrollerPos = Vector2.zero;
        private float m_screenScaling = 1f;
        private bool m_shown;
        private int m_numFingersDown;
        private SystemRef<Hardlight.UIRuntimeConfiguration> m_uiRuntimeConfigRef;
        private Hardlight.InputBridge m_inputBridge;
        private bool m_previouslyHadGameplayControl;

        // 06001fc6 / 06001fc7.
        public static string CurrentMenuPath => s_currentMenuPath;
        public bool IsShown => m_shown;

        // 06001fc8: both complete shipping ranges return without work.
        [Conditional("BUILD_DEVELOPMENT")]
        public static void AddButton(string menuPath, string buttonText, UnityAction callback, float height = -1f, int priority = 0) { }

        // 06001fc9: original label-string overload also returns without work.
        [Conditional("BUILD_DEVELOPMENT")]
        public static void AddButton(string menuPath, string labelText, float height = -1f, int priority = 0) { }

        // 06001fca and actual predicates 06001fe8/9/a. Construct before querying the name;
        // replacing an existing button changes only its Callback.
        [Conditional("BUILD_DEVELOPMENT")]
        public static void AddButton(string menuPath, Func<string> buttonTextCallback, UnityAction callback, float height = -1f, int priority = 0)
        {
            if (height < 0f) height = s_defaultButtonHeightPixels;
            DebugButton button = new DebugButton(menuPath, buttonTextCallback, callback, height, priority);
            string buttonText = buttonTextCallback();
            DebugButton existing = s_allButtons.Find(btn => btn.MenuPath == menuPath && btn.ButtonName() == buttonText);
            if (existing != null)
            {
                existing.Callback = callback;
            }
            else
            {
                s_allButtons.Add(button);
                s_allButtons.Sort(DebugButton.Comparer);
                if (menuPath.Equals(s_currentMenuPath) && s_buttonsToDraw.Find(btn => btn.MenuPath == menuPath && btn.ButtonName() == buttonText) == null)
                {
                    s_buttonsToDraw.Add(button);
                    s_buttonsToDraw.Sort(DebugButton.Comparer);
                }
            }
            if (!string.IsNullOrEmpty(menuPath))
            {
                // The shipping tail executes this predicate search and discards its result.
                s_allButtons.Find(btn => btn.FullPath == menuPath);
            }
        }

        // 06001fcb: original empty body.
        [Conditional("BUILD_DEVELOPMENT")]
        public static void AddLabel(string menuPath, Func<string> labelTextCallback, float height = -1f, int priority = 0) { }

        // 06001fcc / 06001fec: remove the first identical path/name from both lists.
        [Conditional("BUILD_DEVELOPMENT")]
        public static void RemoveButton(string menuPath, string buttonText)
        {
            DebugButton button = s_allButtons.Find(btn => btn.MenuPath == menuPath && btn.ButtonName() == buttonText);
            if (button != null)
            {
                s_allButtons.Remove(button);
                s_buttonsToDraw.Remove(button);
            }
        }

        // 06001fcd: original empty body.
        [Conditional("BUILD_DEVELOPMENT")]
        public static void RemoveLabel(string menuPath, string labelText) { }

        // 06001fce / 06001fee/ff0: exact equality selects list removals;
        // only clearing the current menu uses StartsWith. Parent removal touches the all list only.
        [Conditional("BUILD_DEVELOPMENT")]
        public static void RemoveAllMenuPathButtons(string menuPath, bool removeParentButton)
        {
            List<DebugButton> buttons = s_allButtons.FindAll(btn => btn.MenuPath == menuPath);
            for (int i = 0; i < buttons.Count; i++)
            {
                s_allButtons.Remove(buttons[i]);
                s_buttonsToDraw.Remove(buttons[i]);
            }
            if (removeParentButton)
            {
                string pathWithLeadingSlash = "/" + menuPath;
                DebugButton button = s_allButtons.Find(btn => btn.FullPath == menuPath || btn.FullPath == pathWithLeadingSlash);
                if (button != null) s_allButtons.Remove(button);
            }
            if (s_currentMenuPath != null && s_currentMenuPath.StartsWith(menuPath))
            {
                s_buttonsToDraw.Clear();
                s_currentMenuPath = null;
            }
        }

        // 06001fcf: this shipping body splits the path and discards both outputs.
        [Conditional("BUILD_DEVELOPMENT")]
        private static void AddMenu(string menuPath, int priority)
        {
            Hardlight.Utils.ResourceUtils.SplitPathAndName(menuPath, out string path, out string name);
        }

        // 06001fd0 with 06001ff2 and genuine cached 06001fe3/4/5 callbacks.
        [Conditional("BUILD_DEVELOPMENT")]
        public static void ShowMenu(string fullMenuPath)
        {
            Hardlight.Utils.ResourceUtils.SplitPathAndName(fullMenuPath, out string path, out string name);
            s_currentMenuPath = fullMenuPath;
            s_buttonsToDraw = s_allButtons.FindAll(btn => btn.MenuPath == fullMenuPath);
            s_buttonsToDraw.Sort(DebugButton.Comparer);
            if (!string.IsNullOrEmpty(name))
            {
                s_buttonsToDraw.Insert(0, new DebugButton(fullMenuPath, () => "Back", GetMenuCallback(path), s_defaultButtonHeightPixels, -1));
            }
            else
            {
                s_buttonsToDraw.Insert(0, new DebugButton("", () => "Close", () => { }, s_defaultButtonHeightPixels, -1));
            }
        }

        // 06001fd1 / 06001fe6: authentic cached empty callback ignores its argument.
        public static UnityAction GetMenuCallback(string fullMenuPath) => () => { };

        // 06001fd2: state changes before system/storage lookup and before callback faults.
        [Conditional("BUILD_DEVELOPMENT")]
        public void ToggleMenu()
        {
            m_shown = !m_shown;
            if (m_shouldToggleCursor)
            {
                App app = ProcessManager.GetSystem<App>(null, true);
                if (m_shown)
                {
                    m_previouslyHadGameplayControl = app.Storage.GetValue<bool>(AppFSMKeys.GameplayControlActive, false, true);
                    app.SetHasGameplayControl(false, false);
                }
                else app.SetHasGameplayControl(m_previouslyHadGameplayControl, false);
            }
            OnDebugMenuToggle?.Invoke(m_shown);
        }

        // 06001fd3 / 06001fd4: both shipping bodies are empty.
        [Conditional("BUILD_DEVELOPMENT")]
        public void CloseMenu() { }
        private void OnToggleDebugMenu(float value) { }

        // 06001fd5 destroys this component, not its GameObject.
        private void Start() { UnityEngine.Object.Destroy(this); }
        private void OnDestroy() { }
        private void Update() { }

        // 06001fd8: original virtual InputBridge slot21 registers an OnUp listener.
        private void SetupInputBridge(Hardlight.UIRuntimeConfiguration uiRuntimeConfiguration)
        {
            m_inputBridge = uiRuntimeConfiguration.InputBridge;
            m_inputBridge.RegisterInputListenerOnUp(m_toggleDebugMenuInput, OnToggleDebugMenu, -1);
        }

        // 06001fd9: touches are captured once but live touchCount and phase are reread.
        [Conditional("BUILD_DEVELOPMENT")]
        private void CheckForTouches()
        {
            Touch touch = default;
            Touch[] touches = Input.touches;
            int numFingers = 0;
            for (int i = 0; i < Input.touchCount; i++)
            {
                touch = touches[i];
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) numFingers++;
            }
            if (numFingers != m_numFingersDown) m_numFingersDown = numFingers;
        }

        // 06001fda: original division has no zero or exceptional-value validation.
        private void CalculateScreenScaling() { m_screenScaling = Screen.height / m_referenceScreenDimensions.y; }

        // 06001fdb: normal-path restoration only; a name/callback/GUI fault retains its prefix.
        // Shipping floating-to-int exceptional conversion and actual Unity GUI execution remain held.
        private void OnGUI()
        {
            if (!m_shown) return;
            if (m_guiSkin == null) m_guiSkin = GUI.skin;
            GUISkin previousSkin = GUI.skin;
            GUI.skin = m_guiSkin;
            CalculateScreenScaling();
            Rect safeArea = Screen.safeArea;
            Rect panel = new Rect(safeArea.x, safeArea.y, safeArea.width * m_panelScreenWidthPercent, safeArea.height);
            GUI.Box(panel, string.Empty);
            GUILayout.BeginArea(panel);
            m_scrollerPos = GUILayout.BeginScrollView(new Vector2(0f, m_scrollerPos.y), false, false, GUILayout.Width(panel.width), GUILayout.Height(panel.height));
            int buttonFontSize = m_guiSkin.button.fontSize;
            m_guiSkin.button.fontSize = (int)(buttonFontSize * m_screenScaling);
            int labelFontSize = m_guiSkin.label.fontSize;
            m_guiSkin.label.fontSize = (int)(labelFontSize * m_screenScaling);
            for (int i = 0; i < s_buttonsToDraw.Count; i++)
            {
                DebugButton button = s_buttonsToDraw[i];
                int height = (int)(button.Height * m_screenScaling);
                string text = button.ButtonName();
                if (button.Callback != null)
                {
                    if (GUILayout.Button(text, m_guiSkin.button, GUILayout.Height(height))) button.Callback();
                }
                else GUILayout.Label(text, m_guiSkin.label, GUILayout.Height(height));
            }
            m_guiSkin.button.fontSize = buttonFontSize;
            m_guiSkin.label.fontSize = labelFontSize;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.skin = previousSkin;
        }

        // 06001fdc/06001fdd: exact ordered initializers above; no additional work.
        public DebugMenu() { }

        private class DebugButton
        {
            public readonly string MenuPath;
            public readonly Func<string> ButtonName;
            public UnityAction Callback;
            public readonly float Height;
            public readonly int Priority;

            // 06001fde: name callback executes before concatenation and may fault.
            public string FullPath => MenuPath + "/" + ButtonName();

            // 06001fdf: base constructor precedes these five ordered writes.
            public DebugButton(string menuPath, Func<string> name, UnityAction callback, float height, int priority)
            {
                MenuPath = menuPath;
                ButtonName = name;
                Callback = callback;
                Height = height;
                Priority = priority;
            }

            // 06001fe0: descending signed priority, then ordinal names (StringComparison4).
            public static int Comparer(DebugButton button0, DebugButton button1)
            {
                if (button0.Priority == button1.Priority) return string.Compare(button0.ButtonName(), button1.ButtonName(), StringComparison.Ordinal);
                return button1.Priority.CompareTo(button0.Priority);
            }
        }
    }
}
