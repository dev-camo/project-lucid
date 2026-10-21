using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace HardlightProject
{
    // Original Game.Runtime VideoPlayerWrapper (TypeDef 0x02000a40, x86-64 0x6B97E0..0x6B99CF): plays a video on
    // start and raises the completed event when it ends or is skipped.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class VideoPlayerWrapper : MonoBehaviour
    {
        [SerializeField] private VideoPlayer m_videoPlayer;
        [SerializeField] private UnityEvent m_onVideoStarted;
        [SerializeField] private UnityEvent m_onVideoCompleted;

        private void Start() // 06003a9c
        {
            m_videoPlayer.loopPointReached += OnVideoComplete;
            m_videoPlayer.Play();
            if (m_onVideoStarted != null) m_onVideoStarted.Invoke();
        }

        private void OnDestroy() { m_videoPlayer.loopPointReached -= OnVideoComplete; } // 06003a9d

        // 06003a9e, 06003a9f and 06003aa0 share one native body: stop the player, then raise the completed event.
        private void OnVideoComplete(VideoPlayer videoPlayer) { SkipVideo(); }

        private void SkipVideo()
        {
            m_videoPlayer.Stop();
            if (m_onVideoCompleted != null) m_onVideoCompleted.Invoke();
        }

        public void Action_SkipVideo() { SkipVideo(); }

        public VideoPlayerWrapper() { } // 06003aa1
    }
}
