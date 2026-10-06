using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Video;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class SplashScreen : MonoBehaviour, ISystem
    {
        [Serializable]
        public class AspectRatioSplashScreenData
        {
            [Tooltip("Screen aspect ratio within tolerance will be included.")]
            public Vector2 AspectRatio;
            [Tooltip("Minimum screen width required.  Leave as 0 to apply to all dimensions.")]
            public float Width;
            [Tooltip("List of splash screen videos.")]
            public List<VideoClip> Videos;
            [Tooltip("List of splash screen images.")]
            public List<Sprite> Sprites;
        }

        [SerializeField] private AspectRatioSplashScreenData[] m_aspectRatioSplashScreenData;
        [SerializeField] private float m_tolerance = 0.05f;
        [SerializeField] private List<VideoPlayer> m_targetVideoPlayers;
        [SerializeField] private List<UnityEngine.UI.Image> m_targetSplashScreens;
        [SerializeField] private PlayableDirector m_playableDirector;
        [SerializeField] private UnityEvent m_onDirectorStopped;

        // Original06000629/2a; original backing field precedes m_timer.
        public float Progress { get; private set; }
        private float m_timer;

        // Original0600062b, ARM0x511c24. Retain first-entry fallback and
        // signed width-difference selection, not an absolute-distance sort.
        public void BeginSplashScreen()
        {
            if (m_aspectRatioSplashScreenData.Length == 0) return;
            AspectRatioSplashScreenData selected = m_aspectRatioSplashScreenData[0];
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            int width = Screen.width;
            float aspectRatio = (float)screenWidth / screenHeight;
            float previousWidthDifference = float.MinValue;
            for (int i = 0; i < m_aspectRatioSplashScreenData.Length; i++)
            {
                AspectRatioSplashScreenData data = m_aspectRatioSplashScreenData[i];
                if (!MathUtilities.WithinTolerance(aspectRatio, data.AspectRatio.x / data.AspectRatio.y, m_tolerance)) continue;
                float difference = width - data.Width;
                if ((previousWidthDifference < difference && previousWidthDifference < -m_tolerance) ||
                    (difference < previousWidthDifference && m_tolerance < previousWidthDifference))
                {
                    selected = m_aspectRatioSplashScreenData[i];
                    previousWidthDifference = difference;
                }
            }
            for (int i = 0; i < selected.Videos.Count && i < m_targetVideoPlayers.Count; i++)
            {
                // Original selected clip getter precedes target-player getter.
                VideoClip clip = selected.Videos[i];
                m_targetVideoPlayers[i].clip = clip;
            }
            for (int i = 0; i < selected.Sprites.Count && i < m_targetSplashScreens.Count; i++)
            {
                Sprite sprite = selected.Sprites[i];
                m_targetSplashScreens[i].sprite = sprite;
            }
        }

        // Original0600062c, ARM0x511e90. Registration precedes subscriptions.
        private void Awake()
        {
            ProcessManager.RegisterSystem(this);
            m_playableDirector.stopped += OnDirectorStopped;
            for (int i = 0; i < m_targetVideoPlayers.Count; i++)
                m_targetVideoPlayers[i].errorReceived += VideoPlayerOnErrorReceived;
        }

        // Original0600062d, ARM0x512018. Keep timeline receiver reloads,
        // float subtraction/addition and double division; no deltaTime timer.
        private void Update()
        {
            bool timelineHasTime = m_playableDirector.time > 0.0;
            float timer = m_timer;
            if (timelineHasTime)
            {
                timer += (float)m_playableDirector.time - m_timer;
                m_timer = timer;
            }
            Progress = Mathf.Clamp01((float)((double)timer / m_playableDirector.duration));
        }

        // Original0600062e, ARM0x5120a8. The native body removes only video
        // listeners and releases target textures, including its null failures.
        private void OnDestroy()
        {
            for (int i = 0; i < m_targetVideoPlayers.Count; i++)
            {
                VideoPlayer player = m_targetVideoPlayers[i];
                player.errorReceived -= VideoPlayerOnErrorReceived;
                player.targetTexture.Release();
            }
        }

        // Original0600062f, ARM0x5121b0 is RET, a genuine empty error callback.
        private void VideoPlayerOnErrorReceived(VideoPlayer videoPlayer, string message) { }

        // Original06000630, ARM0x5121b4. No progress, timer or player mutation.
        private void OnDirectorStopped(PlayableDirector director) => m_onDirectorStopped?.Invoke();
        // Original06000631 initializes only tolerance0.05 before MonoBehaviour.
        // Original nested06000632 delegates Object ctor, with null list defaults.
    }
}
