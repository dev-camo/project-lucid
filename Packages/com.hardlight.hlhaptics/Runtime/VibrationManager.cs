using System.Collections;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class VibrationManager : MonoBehaviour, ISystem
    {
        [SerializeField] private VibrationEventData[] m_vibrationEventDefinitions;
        [SerializeField] private bool m_debugInEditor;
        private bool m_vibrationEnabledInSettings;

        // Original06000007/08/09: offsets0x29/0x2a and private setter.
        public bool VibrationEnabledInSettings => m_vibrationEnabledInSettings;
        public bool IsAvailable { get; private set; }

        // Original0600000a: replacement registration precedes availability;
        // the player then clears the debug flag even when it was initially true.
        private void Awake()
        {
            ProcessManager.RegisterSystem(this, null, true, false);
            IsAvailable = IsFeatureAvailable();
            m_debugInEditor = false;
        }

        // Original0600000b unregisters all genuine identity matches.
        private void OnDestroy() => ProcessManager.UnregisterSystem(this);

        // Original0600000c reads the data's event before the enum overload.
        // Do not add an Engine-object/null check or use the argument's events.
        public void TriggerEvent(VibrationEventData vibrationEventData)
        {
            TriggerEvent(vibrationEventData.m_vibrationEvent);
        }

        // Original0600000d starts a coroutine for EVERY matching definition.
        // A match does not stop scanning; reread the authored array each step.
        public void TriggerEvent(VibrationEvent vibrationEvent)
        {
            // Preserve the native read/failure order: availability/settings
            // precede the None check, and the fallback debug flag follows it.
            if (IsAvailable && VibrationEnabledInSettings)
            {
                if (vibrationEvent == VibrationEvent.None) return;
            }
            else
            {
                if (vibrationEvent == VibrationEvent.None) return;
                if (!m_debugInEditor) return;
            }
            for (int i = 0; i < m_vibrationEventDefinitions.Length; ++i)
            {
                if (m_vibrationEventDefinitions[i].m_vibrationEvent == vibrationEvent)
                    StartCoroutine(Vibrate(m_vibrationEventDefinitions[i].m_vibrationEvents));
            }
        }

        // Original0600000e creates the complete natural <Vibrate>d__13 owner
        // (06000013..18). The observed counter begins at -1 and increments
        // before delay/yield. Preserve the float comparison, NaN and fractional
        // repeat behavior, original array rereads, and CoroutineUtils wait.
        private IEnumerator Vibrate(VibrationEventData.Vibrations[] vibrations)
        {
            for (int j = 0; j < vibrations.Length; ++j)
            {
                int repeatCount = -1;
                while (repeatCount < vibrations[j].m_repeat)
                {
                    Vibrate(vibrations[j].m_vibrationType);
                    ++repeatCount;
                    if (vibrations[j].m_delay > 0f)
                        yield return StartCoroutine(CoroutineUtils.WaitForRealSeconds(vibrations[j].m_delay));
                }
            }
        }

        // Original0600000f: the Loading callback's inlined store has this field.
        public void SetVibrationFromSettings(bool setting) => m_vibrationEnabledInSettings = setting;

        // Original06000010 reads only m_debugInEditor in this supplied player.
        // Any pre-compilation platform conditional/API form remains unresolved.
        private bool IsFeatureAvailable() => m_debugInEditor;

        // Original06000011 is an empty native body in BOTH supplied CPUs.
        // This preserves the shipped macOS behavior, not an offline replacement.
        // Missing iOS implementation and native file contents remain held.
        public void Vibrate(VibrationType vibrationType) { }

        // Original06000012 delegates to MonoBehaviour only.
    }
}
