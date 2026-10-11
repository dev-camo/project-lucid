// Original HLUnityUI.Runtime.dll global UISoundEffectManager02000004, all three methods.
// Complete dual native evidence: source receiver Unity-null check, pitch then volume then PlayOneShot.
// The ordinary method sets basePitch1.05946 rather than1; the semitone method converts signed Int32 to Single
// and calls original native powf before reloading the receiver for its pitch setter. The local name/Mathf.Pow
// C# spelling is inferred. No clipping, validation, AudioSource creation, clip-null check or pitch reset is added.
// Original spelling, optimizer/fault equivalence, supplied-provider algorithms, current emission and Engine
// behavior are held; no original/native/provider/service body was executed.
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[Il2CppSetOption(Option.NullChecks, false)]
public class UISoundEffectManager : MonoSingleton<UISoundEffectManager>
{
    private const float basePitch = 1.05946f;

    [SerializeField]
    private AudioSource m_audioSource;

    public void PlayAudioClip(AudioClip clip, float volume = 1f)
    {
        if (m_audioSource != null)
        {
            m_audioSource.pitch = basePitch;
            m_audioSource.volume = volume;
            m_audioSource.PlayOneShot(clip);
        }
    }

    public void PlayAudioClipWithSemitoneOffset(AudioClip clip, int semitoneChange, float volume = 1f)
    {
        if (m_audioSource != null)
        {
            float pitch = Mathf.Pow(basePitch, (float)semitoneChange);
            m_audioSource.pitch = pitch;
            m_audioSource.volume = volume;
            m_audioSource.PlayOneShot(clip);
        }
    }

    public UISoundEffectManager() { }
}
