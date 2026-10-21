using Hardlight;
using TMPro;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Original Game.Runtime 0200044b; native 0x7A6100.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SubtitleMixer : PlayableBehaviour
    {
        private string m_currentText = string.Empty;
        private float m_currentAlpha;
        private Color m_backgroundColour = new Color(0f, 0f, 0f, 0.7f);
        private int m_inputCount;
        private GameObject m_backingPanel;
        private bool m_anyTextShowing;
        private bool m_subtitlesEnabled = true;

        // Original06001955: the saved preference is sampled each time the graph starts.
        public override void OnGraphStart(Playable playable)
        {
            m_subtitlesEnabled = ProcessManager.GetSystem<SaveManager>().GetSaveDataSettings().SubtitlesEnabled;
            base.OnGraphStart(playable);
        }

        // Original06001956: the last clip with positive weight (later inputs win) supplies the text, alpha and
        // background; the text is shown at that alpha and the backing panel (the text's parent) follows
        // whether any clip was active. A non-positive weight is skipped, but a NaN weight is not.
        public override void ProcessFrame(Playable playable, FrameData _, object playerData)
        {
            TextMeshProUGUI text = playerData as TextMeshProUGUI;
            m_anyTextShowing = false;
            if (text == null || !m_subtitlesEnabled) return;
            m_inputCount = playable.GetInputCount();
            for (int i = 0; i < m_inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                if (inputWeight <= 0f) continue;
                ScriptPlayable<SubtitleBehaviour> input = (ScriptPlayable<SubtitleBehaviour>)playable.GetInput(i);
                SubtitleBehaviour behaviour = input.GetBehaviour();
                m_currentText = behaviour.SubtitleText;
                m_anyTextShowing = true;
                m_currentAlpha = inputWeight;
                m_backgroundColour = behaviour.BackgroundColour;
            }
            text.text = m_currentText;
            text.color = new Color(1f, 1f, 1f, m_currentAlpha);
            if (m_backingPanel == null) m_backingPanel = text.transform.parent.gameObject;
            m_backingPanel.SetActive(m_anyTextShowing);
        }

        // Implicit original06001957: empty text, translucent black background and enabled subtitles.
    }
}
