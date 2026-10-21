using System;
using System.Collections.Generic;
using Hardlight.UI.Binding;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000607; bodies translated from the 1.10.1 x86-64 binary, 0x5645D0..0x564AC0.
    // Debug frame information texts. The CPU and GPU texts show the timer frequency (not a frame time), rounded to
    // three decimals; the frame rate text is one over the average of the last 60 frame deltas, rounded to two.
    public class ViewFrameInformation : MonoBehaviour
    {
        public readonly Bindable<string> Framerate = new Bindable<string>();
        public readonly Bindable<string> FramerateCPU = new Bindable<string>();
        public readonly Bindable<string> FramerateGPU = new Bindable<string>();
        private readonly Queue<float> m_framerateSamplesMs = new Queue<float>(60);

        // 0600205f (1.10.1 RVA 0x5645D0)
        private void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            FramerateCPU.Value = "CPU: " + Math.Round((double)FrameTimingManager.GetCpuTimerFrequency(), 3, MidpointRounding.ToEven).ToString() + "ms";
            FramerateGPU.Value = "GPU: " + Math.Round((double)FrameTimingManager.GetGpuTimerFrequency(), 3, MidpointRounding.ToEven).ToString() + "ms";
            Framerate.Value = string.Format("Framerate: {0} FPS",
                Math.Round((double)(float)(1f / GetAverageFramerateMs(Time.deltaTime)), 2, MidpointRounding.ToEven));
        }

        // 06002060 (1.10.1 RVA 0x564940): the sample queue keeps the newest 60 values; the sample passed in is the raw
        // delta time in seconds, not milliseconds.
        private float GetAverageFramerateMs(float currentDeltaTimeMs)
        {
            m_framerateSamplesMs.Enqueue(currentDeltaTimeMs);
            if (m_framerateSamplesMs.Count >= 61)
                m_framerateSamplesMs.Dequeue();
            float sum = 0f;
            foreach (float sample in m_framerateSamplesMs)
                sum += sample;
            return sum / m_framerateSamplesMs.Count;
        }

        // 06002061 (1.10.1 RVA 0x564AC0)
        public ViewFrameInformation()
        {
        }
    }
}
