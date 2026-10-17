// Original HLUnityUI.Runtime HLEnumSelector`1 0200000b (global namespace), from the fully
// shared x86-64 bodies 0x1392370..0x1392F80 and the type RGCTX. Wrapping uses the cached
// m_valueCount, not m_values.Length.
using Hardlight;
using TMPro;
using UnityEngine;

public abstract class HLEnumSelector<T> : MonoBehaviour
{
    [SerializeField]
    private T m_startingValue;
    [SerializeField]
    protected TMP_Text m_label;
    protected T m_currentValue;
    protected T[] m_values;
    private int m_valueCount;
    private int m_currentIndex;

    public T Value => m_currentValue;

    public void Action_Increment()
    {
        m_currentIndex++;
        if (m_currentIndex >= m_valueCount)
            m_currentIndex = 0;
        m_currentValue = m_values[m_currentIndex];
        Validate();
    }

    public void Action_Decrement()
    {
        m_currentIndex--;
        if (m_currentIndex < 0)
            m_currentIndex = m_valueCount - 1;
        m_currentValue = m_values[m_currentIndex];
        Validate();
    }

    // 0x06000066: the value is kept even when no entry matches; the index then stays put.
    public void SetCurrentValue(T value)
    {
        m_currentValue = value;
        for (int i = 0; i < m_valueCount; i++)
        {
            if (Compare(m_values[i], m_currentValue))
            {
                m_currentIndex = i;
                break;
            }
        }
        Validate();
    }

    private void OnEnable()
    {
        Init();
    }

    private void OnValidate()
    {
        Init();
    }

    public void Init()
    {
        m_values = EnumUtilities.GetValues<T>();
        m_valueCount = m_values.Length;
        SetStartingValue();
        Validate();
    }

    protected abstract void Validate();

    protected abstract bool Compare(T a, T b);

    // 0x0600006c: falls back to the first value when nothing matches.
    private void SetStartingValue()
    {
        m_currentIndex = 0;
        if (m_startingValue != null)
        {
            for (int i = 0; i < m_valueCount; i++)
            {
                if (Compare(m_values[i], m_startingValue))
                {
                    m_currentIndex = i;
                    break;
                }
            }
        }
        m_currentValue = m_values[m_currentIndex];
    }
}
