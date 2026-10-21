using System.Collections;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0200087b; bodies 0x61A4F0..0x61B1E0 (1.10.1 x86-64).
    // Boss health pips: one object and fill image per health point, refilled as the boss health changes.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerBossHealth : UIContainer
    {
        public readonly Bindable<bool> Visible = new Bindable<bool>();
        public readonly Bindable<Strings> BossName = new Bindable<Strings>();
        [SerializeField]
        private float m_lerpTimeInSeconds = 0.5f;
        [SerializeField]
        private bool m_useUnscaledTime;
        [SerializeField]
        private GameObject[] m_healthPoolObjects;
        [SerializeField]
        private Image[] m_healthFills;
        private SystemRef<BossManager> m_bossManagerRef;
        private BossManager m_bossManager;
        private int m_totalHealth = -1;
        private int m_currentHealthTarget;
        private float m_lerpTimeCounter;
        private Coroutine m_lerpRoutine;
        private readonly WaitForEndOfFrame m_waitForEndOfFrame = new WaitForEndOfFrame();

        // 0600310a
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            Visible.Value = false;
            m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);
            m_bossManagerRef.InvokeOnValid(OnBossManagerValid);
        }

        // 0600310b: the container shows only while a boss encounter is active; pips beyond the
        // total are hidden and the filled ones follow the current health.
        private void OnBossManagerValid(BossManager bossManager)
        {
            m_bossManager = bossManager;
            gameObject.SetActive(m_bossManager?.BossManagerActive ?? false);
            m_totalHealth = m_bossManager.BossHealthTotal;
            m_currentHealthTarget = m_bossManager.BossHealthCurrent;
            BossName.Value = m_bossManager.BossName;
            for (int i = 0; i < m_healthPoolObjects.Length; i++)
            {
                m_healthPoolObjects[i].SetActive(i < m_totalHealth);
                m_healthFills[i].fillAmount = i < m_currentHealthTarget ? 1f : 0f;
            }
            m_bossManager.OnBossHealthPoolChange += OnHealthChange;
            m_bossManager.OnBossHealthTotalUpdate += OnBossHealthTotalUpdate;
            Visible.Value = true;
        }

        // 0600310c: a rise fills pips upwards; any other change drains the pip at the new health.
        private void OnHealthChange(int health)
        {
            int oldHealthTarget = m_currentHealthTarget;
            m_currentHealthTarget = health;
            if (m_lerpRoutine != null)
            {
                StopCoroutine(m_lerpRoutine);
            }
            m_lerpTimeCounter = 0f;
            if (oldHealthTarget < health)
            {
                m_lerpRoutine = StartCoroutine(LerpUIUp(m_currentHealthTarget, oldHealthTarget));
            }
            else
            {
                m_lerpRoutine = StartCoroutine(LerpUIDown(m_healthFills[m_currentHealthTarget]));
            }
        }

        // 0600310d: a new total shows that many pips, all full.
        private void OnBossHealthTotalUpdate(int totalHealth)
        {
            if (m_lerpRoutine != null)
            {
                StopCoroutine(m_lerpRoutine);
            }
            m_totalHealth = totalHealth;
            for (int i = 0; i < m_healthPoolObjects.Length; i++)
            {
                m_healthPoolObjects[i].SetActive(i < totalHealth);
                m_healthFills[i].fillAmount = 1f;
            }
            m_currentHealthTarget = totalHealth;
            BossName.Value = m_bossManager.BossName;
        }

        // 0600310e / MoveNext 0600311a: the pip count is currentHealthTarget - 1, not the size of
        // the change, and pips start at the old health.
        private IEnumerator LerpUIUp(int currentHealthTarget, int oldHealthTarget)
        {
            for (int i = 0; i < currentHealthTarget - 1; i++)
            {
                Image fillArea = m_healthFills[i + oldHealthTarget];
                float fillAmount = 0f;
                m_lerpTimeCounter = 0f;
                while (fillAmount < 1f)
                {
                    m_lerpTimeCounter += m_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    fillAmount = Mathf.Lerp(0f, 1f, m_lerpTimeCounter / m_lerpTimeInSeconds);
                    fillArea.fillAmount = fillAmount;
                    yield return m_waitForEndOfFrame;
                }
            }
        }

        // 0600310f / MoveNext 06003114
        private IEnumerator LerpUIDown(Image fillArea)
        {
            float fillAmount = 1f;
            while (fillAmount > 0f)
            {
                m_lerpTimeCounter += m_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                fillAmount = Mathf.Lerp(1f, 0f, m_lerpTimeCounter / m_lerpTimeInSeconds);
                fillArea.fillAmount = fillAmount;
                yield return m_waitForEndOfFrame;
            }
        }

        // 06003110
        private void OnDestroy()
        {
            if (m_lerpRoutine != null)
            {
                StopCoroutine(m_lerpRoutine);
            }
            if (m_bossManager == null)
            {
                return;
            }
            m_bossManager.OnBossHealthPoolChange -= OnHealthChange;
            m_bossManager.OnBossHealthTotalUpdate -= OnBossHealthTotalUpdate;
        }
    }
}
