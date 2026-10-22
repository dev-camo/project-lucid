// Original Apple.Arcade.SplashScreen.dll:Apple.Arcade.SplashScreen.SplashScreenPlayer.
// TypeDef 0x02000006, methods 0x0600000b..0x06000010, with the async state machine <Start>d__5
// (0x02000007, MoveNext 0x06000011 at x86_64 0x425ed0..0x4262d0).
// x86_64 ranges: Start 0x425c10, OnVideoPlayerReady 0x425cb0, Action_OnVideoCompleted 0x425d60,
// OnLoopPointReached 0x425e40, OnVideoCompleted 0x425dd0, .ctor 0x425eb0.
// The three completion methods have three separate identical native bodies.
//
// Hook H-056 (Start): see the comment in Start. The original code below it is unchanged.
using ProjectLucid.Offline;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace Apple.Arcade.SplashScreen
{
    [RequireComponent(typeof(VideoPlayer))]
    public class SplashScreenPlayer : MonoBehaviour
    {
        [SerializeField]
        private VideoPlayer _videoPlayer;

        [SerializeField]
        private AudioSource _audioSource;

        [SerializeField]
        private int m_targetFPS = 60;

        [SerializeField]
        private UnityEvent m_onPlayerLoopPointReached;

        private int m_cachedTargetFrameRate;

        private async void Start()
        {
            // Offline hook H-056: the original loaded the Apple Arcade splash video from Resources and
            // played it, firing m_onPlayerLoopPointReached when the video ended. On every desktop target
            // (owner decision D7) the splash is skipped instead: the same frame-rate restore and the same
            // event of the original completion path run at once, so the scene's persistent listener
            // (UnloadSceneAfterFirstBoot.Action_Unload) unloads s_apple and the boot graph leaves
            // WaitForApple through its own AppleUnloaded transition. No video is loaded or played and no
            // event is invented. Other targets keep the original behaviour.
            if (OfflineAdapters.Active && OfflineDesktopPlatform.IsCurrent)
            {
                m_cachedTargetFrameRate = Application.targetFrameRate;
                OnVideoCompleted();
                return;
            }
            m_cachedTargetFrameRate = Application.targetFrameRate;
            _videoPlayer.clip = await new ResourcesSplashScreenVideoProvider().GetSplashScreenVideo();
            Application.targetFrameRate = m_targetFPS;
            _videoPlayer.loopPointReached += OnLoopPointReached;
            _videoPlayer.sendFrameReadyEvents = true;
            _videoPlayer.Play();
            _videoPlayer.Pause();
            _videoPlayer.frameReady += OnVideoPlayerReady;
        }

        // The first decoded frame: stop listening, start the video and its audio together.
        private void OnVideoPlayerReady(VideoPlayer source, long frameidx)
        {
            source.frameReady -= OnVideoPlayerReady;
            _videoPlayer.sendFrameReadyEvents = false;
            source.Play();
            _audioSource.Play();
        }

        public void Action_OnVideoCompleted()
        {
            Application.targetFrameRate = m_cachedTargetFrameRate;
            m_onPlayerLoopPointReached?.Invoke();
        }

        private void OnLoopPointReached(VideoPlayer _)
        {
            Application.targetFrameRate = m_cachedTargetFrameRate;
            m_onPlayerLoopPointReached?.Invoke();
        }

        private void OnVideoCompleted()
        {
            Application.targetFrameRate = m_cachedTargetFrameRate;
            m_onPlayerLoopPointReached?.Invoke();
        }

        public SplashScreenPlayer() { }
    }
}
