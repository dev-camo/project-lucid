using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

// Original HLUnityUI.Runtime 0200000e (MethodDefs 0x06000076..7a). Spawns one
// HLPipsElement per pip under m_pipRoot and steps a single current pip without
// wrapping, raising each element's activate and deactivate events.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public sealed class HLPips : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Root to spawn pips under.")]
    private RectTransform m_pipRoot;

    [Tooltip("Pip element prefab.")]
    [SerializeField]
    private HLPipsElement m_pipsElementPrefab;

    private const int InvalidPipIndex = -1;

    private int m_pipCount;
    private int m_currentPipIndex;
    private int m_previousPipIndex = InvalidPipIndex;
    private List<HLPipsElement> m_pips = new List<HLPipsElement>();

    // 0x06000076 @0x1B3BAC0: existing pips are kept and appended to, and the
    // previous index is left alone.
    public void SetupPips(int pipCount, int activePipIndex = 0)
    {
        if (m_pipsElementPrefab == null)
            return;

        m_pipCount = pipCount;
        for (int i = 0; i < m_pipCount; i++)
        {
            HLPipsElement pip = Instantiate(m_pipsElementPrefab, m_pipRoot);
            (i == activePipIndex ? pip.OnInitialiseActive : pip.OnInitialiseInactive).Invoke();
            m_pips.Add(pip);
        }
        m_currentPipIndex = activePipIndex;
    }

    // 0x06000077 @0x1B3BC30: the count and indices are not reset.
    public void DestroyPips()
    {
        for (int i = m_pips.Count - 1; i >= 0; i--)
        {
            Destroy(m_pips[i].gameObject);
            m_pips.RemoveAt(i);
        }
    }

    // 0x06000078 @0x1B3BD10.
    public void Next()
    {
        if (m_currentPipIndex == m_pipCount - 1)
            return;

        m_previousPipIndex = m_currentPipIndex;
        m_currentPipIndex++;
        m_pips[m_previousPipIndex].OnDeactivate.Invoke();
        m_pips[m_currentPipIndex].OnActivate.Invoke();
    }

    // 0x06000079 @0x1B3BD90.
    public void Previous()
    {
        if (m_currentPipIndex == 0)
            return;

        m_previousPipIndex = m_currentPipIndex;
        m_currentPipIndex--;
        m_pips[m_previousPipIndex].OnDeactivate.Invoke();
        m_pips[m_currentPipIndex].OnActivate.Invoke();
    }

    public HLPips() { }
}
