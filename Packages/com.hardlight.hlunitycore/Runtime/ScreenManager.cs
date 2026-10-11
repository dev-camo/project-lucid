// Preserved Sonic Dream Team 1.10.1 original HLUnityCore.Runtime declarations.
// Original MethodDef to ARM64/x86_64 evidence addresses, including natural iterator roles.
// 0x06000dd0 0x1b17e94 / 0x1b130f0 .ctor
// 0x06000dd1 0x1b17f98 / 0x1b131e0 System.IDisposable.Dispose
// 0x06000dd2 0x1b17f9c / 0x1b131f0 MoveNext
// 0x06000dd3 0x1b1803c / 0x1b13280 System.Collections.Generic.IEnumerator<System.Object>.get_Current
// 0x06000dd4 0x1b18044 / 0x1b13290 System.Collections.IEnumerator.Reset
// 0x06000dd5 0x1b18084 / 0x1b132d0 System.Collections.IEnumerator.get_Current
// 0x06000dd6 0x1b17e68 / 0x1b130d0 .ctor
// 0x06000dd7 0x1b1808c / 0x1b132e0 System.IDisposable.Dispose
// 0x06000dd8 0x1b18090 / 0x1b132f0 MoveNext
// 0x06000dd9 0x1b18118 / 0x1b13360 System.Collections.Generic.IEnumerator<System.Object>.get_Current
// 0x06000dda 0x1b18120 / 0x1b13370 System.Collections.IEnumerator.Reset
// 0x06000ddb 0x1b18160 / 0x1b133b0 System.Collections.IEnumerator.get_Current
// 0x06000db6 0x1b17064 / 0x1b12430 get_Orientation
// 0x06000db7 0x1b1706c / 0x1b12440 set_Orientation
// 0x06000db8 0x1b17074 / 0x1b12450 get_DeviceOrientation
// 0x06000db9 0x1b1707c / 0x1b12460 set_DeviceOrientation
// 0x06000dba 0x1b17084 / 0x1b12470 add_OnSafeAreaChange
// 0x06000dbb 0x1b17138 / 0x1b12520 remove_OnSafeAreaChange
// 0x06000dbc 0x1b171ec / 0x1b125d0 Awake
// 0x06000dbd 0x1b173c4 / 0x1b12790 GetScreenOrientation
// 0x06000dbe 0x1b176f4 / 0x1b12a60 OnDestroy
// 0x06000dbf 0x1b1751c / 0x1b128c0 Initialise
// 0x06000dc0 0x1b17770 / 0x1b12ad0 Shutdown
// 0x06000dc1 0x1b17880 / 0x1b12bc0 GetUIOrientationFromScreenSize
// 0x06000dc2 0x1b176bc / 0x1b12a30 GetScreenOrientationFromScreenSize
// 0x06000dc3 0x1b178b8 / 0x1b12bf0 OnPropertyStoreSave
// 0x06000dc4 0x1b17978 / 0x1b12c90 OnPropertyStoreLoad
// 0x06000dc5 0x1b17b0c / 0x1b12e00 ChangeOrientation
// 0x06000dc6 0x1b179f8 / 0x1b12d00 ForceOrientation
// 0x06000dc7 0x1b17ca8 / 0x1b12f50 ToggleOrientationLock
// 0x06000dc8 0x1b17d30 / 0x1b12fc0 SetOrientationLock
// 0x06000dc9 0x1b174ac / 0x1b12860 ControlOrientationLock
// 0x06000dca 0x1b17e60 / 0x1b130c0 IsOrientationLocked
// 0x06000dcb 0x1b179c0 / 0x1b12cd0 ConvertScreenToUIOrientation
// 0x06000dcc 0x1b17c30 / 0x1b12ee0 SetScreenToAutoRotate
// 0x06000dcd 0x1b17644 / 0x1b129c0 PollScreenRotate
// 0x06000dce 0x1b17ec0 / 0x1b13110 ScreenHasSafeArea
// 0x06000dcf 0x1b17ec8 / 0x1b13120 .ctor
// Original HLUnityCore.Runtime.dll four-owner reconstruction from complete ARM64/x86_64 evidence.
// C# spelling/local names and generated iterator names are inferred; compilation, provider algorithms,
// Unity/platform behavior and native fault equivalence remain held. No service body was executed.
// The orientation cache is never reset by any selected original method; polling yields before its first read.
// Load callbacks ignore the supplied list/new-file flag. Registration flags change only after callbacks return.
// Shutdown removes property callbacks; OnDestroy subsequently stops both coroutines before base teardown.
// OnOrientationChange remains null until a genuine subscriber assigns it; static FastAction.Invoke is genuine.
// UIOrientation/out-of-range enum values and square-screen Portrait selection are preserved without validation.
using System;
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ScreenManager : MonoSingleton<ScreenManager>
    {
        private const string DesiredOrientationsSaveKey = "desired_screen_orientation";
        private const string AutoRotateSaveKey = "user_locked_auto_rotate";
        private const float AutoRotationCheckTime = 0.25f;
        private Coroutine m_reapplyAutoRotation;
        private readonly WaitForEndOfFrame m_wait = new WaitForEndOfFrame();
        private bool m_userLockedAutoRotate;
        private bool m_initialised;
        private ScreenOrientation m_lastScreenOrientation;
        private bool m_lastScreenOrientationUpdated;
        private readonly WaitForSeconds m_timedWait = new WaitForSeconds(AutoRotationCheckTime);
        private Coroutine m_pollScreenRotate;
        public UIOrientation Orientation { get; private set; }
        public ScreenOrientation DeviceOrientation { get; private set; }
        public FastAction OnOrientationChange;
        public event Action<bool> OnSafeAreaChange;
        [SerializeField] private ScreenOrientation m_editorOrientation;

        protected override void Awake()
        {
            base.Awake();
            DeviceOrientation = GetScreenOrientation();
            bool isLandscape = DeviceOrientation.IsLandscape();
            Screen.autorotateToLandscapeLeft = isLandscape;
            Screen.autorotateToLandscapeRight = isLandscape;
            Screen.autorotateToPortrait = !isLandscape;
            Screen.autorotateToPortraitUpsideDown = !isLandscape;
            Initialise();
            m_pollScreenRotate = StartCoroutine(PollScreenRotate());
            this.SubscribeToAction(SystemAction.Initialise, Initialise);
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
        }

        private ScreenOrientation GetScreenOrientation(bool skipScreenOrientationUpdate = false)
        {
            ScreenOrientation orientation;
            if (m_lastScreenOrientationUpdated)
                orientation = m_lastScreenOrientation;
            else
            {
                orientation = GetScreenOrientationFromScreenSize();
                if (m_lastScreenOrientationUpdated && orientation == m_lastScreenOrientation)
                    orientation = m_lastScreenOrientation;
                else if (orientation.IsObsolete())
                {
                    orientation = GetScreenOrientationFromScreenSize();
                    if (!skipScreenOrientationUpdate) Screen.orientation = orientation;
                }
            }
            m_lastScreenOrientation = orientation;
            m_lastScreenOrientationUpdated = true;
            return orientation;
        }

        protected override void OnDestroy()
        {
            Shutdown();
            this.SafeStopCoroutine(ref m_pollScreenRotate);
            this.SafeStopCoroutine(ref m_reapplyAutoRotation);
            base.OnDestroy();
        }

        private void Initialise(object context = null)
        {
            if (m_initialised) return;
            HLPropertyStore.AddLoadHandler(OnPropertyStoreLoad, true);
            HLPropertyStore.AddSaveHandler(OnPropertyStoreSave);
            m_initialised = true;
        }

        private void Shutdown(object context = null)
        {
            if (!m_initialised) return;
            HLPropertyStore.RemoveLoadHandler(OnPropertyStoreLoad);
            HLPropertyStore.RemoveSaveHandler(OnPropertyStoreSave);
            m_initialised = false;
        }

        private UIOrientation GetUIOrientationFromScreenSize()
        {
            return Screen.width > Screen.height ? UIOrientation.Landscape : UIOrientation.Portrait;
        }

        private ScreenOrientation GetScreenOrientationFromScreenSize()
        {
            return Screen.width > Screen.height ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
        }

        private void OnPropertyStoreSave(HLPropertyList propertyList)
        {
            propertyList.AddProperty<int>(DesiredOrientationsSaveKey, (int)Orientation);
            propertyList.AddProperty<bool>(AutoRotateSaveKey, m_userLockedAutoRotate);
        }

        private void OnPropertyStoreLoad(HLPropertyList propertyList, bool isNewFile)
        {
            Orientation = UIOrientation.Uninitialised;
            Orientation = GetUIOrientationFromScreenSize();
            ForceOrientation();
        }

        public void ChangeOrientation(UIOrientation newOrientation, Action onOrientationChangedCallback = null)
        {
            if (newOrientation == UIOrientation.Uninitialised || newOrientation == Orientation) return;
            Orientation = newOrientation;
            ForceOrientation();
            if (!ProcessManager.IsSystemNull<HLPropertyStore>()) HLPropertyStore.SaveDelayed(false);
            if (onOrientationChangedCallback != null) onOrientationChangedCallback();
        }

        private void ForceOrientation()
        {
            UIOrientation orientation = Orientation;
            if (orientation == UIOrientation.Uninitialised) return;
            bool isLandscape = orientation == UIOrientation.Landscape;
            Screen.orientation = isLandscape ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            if (m_userLockedAutoRotate) return;
            Screen.autorotateToLandscapeLeft = isLandscape;
            Screen.autorotateToLandscapeRight = isLandscape;
            Screen.autorotateToPortrait = !isLandscape;
            Screen.autorotateToPortraitUpsideDown = !isLandscape;
            this.SafeStopCoroutine(ref m_reapplyAutoRotation);
            m_reapplyAutoRotation = StartCoroutine(SetScreenToAutoRotate());
        }

        public void ToggleOrientationLock(bool setLock)
        {
            if (m_userLockedAutoRotate) return;
            ControlOrientationLock(setLock);
        }

        public void SetOrientationLock(bool setLock)
        {
            ControlOrientationLock(setLock);
            m_userLockedAutoRotate = setLock;
            if (!ProcessManager.IsSystemNull<HLPropertyStore>()) HLPropertyStore.SaveDelayed(false);
        }

        private void ControlOrientationLock(bool setLock)
        {
            bool isLandscape = DeviceOrientation.IsLandscape();
            Screen.autorotateToLandscapeLeft = isLandscape || !setLock;
            Screen.autorotateToLandscapeRight = isLandscape || !setLock;
            Screen.autorotateToPortrait = !(isLandscape && setLock);
            Screen.autorotateToPortraitUpsideDown = !(isLandscape && setLock);
        }

        public bool IsOrientationLocked() { return m_userLockedAutoRotate; }

        private UIOrientation ConvertScreenToUIOrientation()
        {
            return Screen.width > Screen.height ? UIOrientation.Landscape : UIOrientation.Portrait;
        }

        private IEnumerator SetScreenToAutoRotate()
        {
            yield return m_wait;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        private IEnumerator PollScreenRotate()
        {
            while (true)
            {
                yield return m_timedWait;
                ScreenOrientation orientation = GetScreenOrientation();
                if (orientation != DeviceOrientation)
                {
                    DeviceOrientation = orientation;
                    FastAction.Invoke(OnOrientationChange);
                }
            }
        }

        public bool ScreenHasSafeArea() { return HLUnityCore.DoesThisDeviceHaveASafeArea(); }
        public ScreenManager() { }
    }
}
