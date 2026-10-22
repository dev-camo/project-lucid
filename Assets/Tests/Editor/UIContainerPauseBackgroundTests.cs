using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerPauseBackground (Game.Runtime 0x02000902; OnEnable 0x671B80, Update 0x671D90, Initialise 0x671C80,
    // InitialiseFromCamera 0x671EC0, RenderBackgroundImage 0x671DC0, OnDisable 0x672090) as a real component on an owned
    // inactive host over the CameraManagerClientSupport rig: a real CinemachineCameraManager with a brain on a real camera
    // whose render target gives it a known pixel size. The manager's main camera is not ready in these cases (readiness
    // comes from its Awake coroutine), which is the state where RenderBackgroundImage returns before it renders.
    //
    // Not covered: the render itself (RenderBackgroundImage 0x671E00..0x671EA5 and the OnDisable release), which runs
    // Camera.Render on the owned inactive camera host.
    public sealed class UIContainerPauseBackgroundTests
    {
        private const int CullingMask = 0x2D;
        private static readonly Vector2Int CameraSize = new Vector2Int(320, 200);

        private sealed class Rig
        {
            internal Owned Owned;
            internal CameraManagerClientSupport.Rig Cameras;
            internal UIContainerPauseBackground Background;
        }

        // The quality feature sits on the background as the engine serializer delivers it: the profile's level for the
        // scalable feature selects the first row of that level (PerformanceFeature.TryGet).
        private static void Run(bool useRenderTexture, Action<Rig> body, Func<Owned, PerformanceFeature<UIContainerPauseBackground.QualityBackground>> quality = null, bool registerManager = true) =>
            Owned.Run(owned =>
            {
                if (quality == null) owned.LeaseRow<ScalablePerformance>(); // PerformanceFeature's field initialiser
                var rig = new Rig { Owned = owned };
                rig.Cameras = CameraManagerClientSupport.NewRig(owned, true, registerManager);
                if (!registerManager) owned.LeaseRow<CinemachineCameraManager>(); // the empty row the background's reference waits on
                rig.Cameras.Camera.cullingMask = CullingMask;
                rig.Cameras.Camera.targetTexture = owned.Track(new RenderTexture(CameraSize.x, CameraSize.y, 0));
                PerformanceFeature<UIContainerPauseBackground.QualityBackground> feature = quality?.Invoke(owned);
                rig.Background = owned.Host("Lucid pause background", false).AddComponent<UIContainerPauseBackground>();
                Set(rig.Background, "m_useRenderTexture", useRenderTexture); // serialized in the original
                Set(rig.Background, "m_qualitySettings", feature ?? new PerformanceFeature<UIContainerPauseBackground.QualityBackground>()); // Reflection: serialized m_qualitySettings the editor assigns
                body(rig);
            });

        private static Delegate[] Subscribers(Rig rig) => rig.Cameras.Manager.OnMainCameraReady.GetInvocationList();

        private static bool IsSubscribed(Rig rig) =>
            Subscribers(rig).Any(handler => handler.Method.Name == "InitialiseFromCamera" && ReferenceEquals(handler.Target, rig.Background));

        // OnEnable 0x671B80 without the render texture switches the component off (Behaviour.set_enabled(false) at 0x671C26)
        // and does nothing else.
        [Test]
        public void WithoutTheRenderTextureEnablingSwitchesTheComponentOff()
        {
            Run(false, rig =>
            {
                Assert.That(rig.Background.enabled, Is.True);

                Call(rig.Background, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(rig.Background.enabled, Is.False);
                Assert.That(Get<CinemachineCameraManager>(rig.Background, "m_cinemachineCameraManager") == null, Is.True);
                Assert.That(IsSubscribed(rig), Is.False);
            });
        }

        // With the render texture and the manager already registered (TryGet true at 0x671BF2) OnEnable keeps the manager and
        // runs Initialise, which does not initialise yet (the main camera is not ready, the test at 0x671CCC) but subscribes
        // to the manager's main-camera event (the Delegate.Combine at 0x671D2A).
        [Test]
        public void EnablingWithTheRenderTextureKeepsTheManagerAndSubscribesToTheMainCameraEvent()
        {
            Run(true, rig =>
            {
                Call(rig.Background, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(rig.Background.enabled, Is.True);
                Assert.That(Get<CinemachineCameraManager>(rig.Background, "m_cinemachineCameraManager"), Is.SameAs(rig.Cameras.Manager));
                Assert.That(IsSubscribed(rig), Is.True);
                Assert.That(Get<Vector2Int>(rig.Background, "m_screenSize"), Is.EqualTo(Vector2Int.zero), "no camera seen yet");
            });
        }

        // With the manager absent (TryGet false at 0x671BF2) OnEnable hands Initialise to InvokeOnValid (0x671C68): nothing is
        // kept or subscribed until the manager registers, and then the callback runs Initialise.
        [Test]
        public void EnablingBeforeTheManagerRegistersInitialisesWhenItDoes()
        {
            Run(true, rig =>
            {
                Call(rig.Background, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(Get<CinemachineCameraManager>(rig.Background, "m_cinemachineCameraManager") == null, Is.True);
                Assert.That(IsSubscribed(rig), Is.False);

                // The row was leased empty and now holds the background's deferred callback, so the manager goes into the leased row itself.
                rig.Owned.RegisterIntoLeasedRow(rig.Cameras.Manager);

                Assert.That(Get<CinemachineCameraManager>(rig.Background, "m_cinemachineCameraManager"), Is.SameAs(rig.Cameras.Manager));
                Assert.That(IsSubscribed(rig), Is.True);
            }, registerManager: false);
        }

        // InitialiseFromCamera 0x671EC0 unsubscribes from the manager's event (Delegate.Remove at 0x671F75), takes the camera's
        // pixel size (get_pixelWidth 0x671FD3, get_pixelHeight 0x671FE0) and caches its culling mask (get_cullingMask 0x672067).
        // It then calls RenderBackgroundImage, which returns here because the main camera is not ready.
        [Test]
        public void InitialisingFromACameraTakesItsSizeAndCullingMaskAndStopsListening()
        {
            Run(true, rig =>
            {
                Call(rig.Background, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Assert.That(IsSubscribed(rig), Is.True);

                Call(rig.Background, "InitialiseFromCamera", rig.Cameras.Camera); // Reflection: private UIContainerPauseBackground.InitialiseFromCamera

                Assert.That(Get<Vector2Int>(rig.Background, "m_screenSize"), Is.EqualTo(CameraSize));
                Assert.That(Get<int>(rig.Background, "m_cachedCullingMask"), Is.EqualTo(CullingMask));
                Assert.That(IsSubscribed(rig), Is.False);
                Assert.That(Get<RenderTexture>(rig.Background, "m_renderTexture") == null, Is.True, "not ready: nothing rendered");
                Assert.That(rig.Cameras.Camera.cullingMask, Is.EqualTo(CullingMask), "the camera is left alone");
            });
        }

        private static PerformanceFeature<UIContainerPauseBackground.QualityBackground> Quality(Owned owned, Vector2 max)
        {
            var feature = owned.Track(ScriptableObject.CreateInstance<ScalableFeature>());
            feature.ID = "lucid-pause-background-quality";
            var profile = owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>());
            Set(profile, "m_attributes", new List<PerformanceAttribute> // Reflection: serialized m_attributes the editor assigns
            {
                new PerformanceAttribute { Feature = feature, Level = PerformanceProfile.QualityLevel.High },
            }); // serialized in the original
            var performance = owned.Register(owned.Host("Lucid pause background performance", false).AddComponent<ScalablePerformance>());
            performance.SetPerformanceProfile(profile);

            var quality = new PerformanceFeature<UIContainerPauseBackground.QualityBackground>();
            Set(quality, "m_scalableFeature", feature); // serialized in the original
            Type rowType = Field(typeof(PerformanceFeature<UIContainerPauseBackground.QualityBackground>), "m_qualityAttributes")
                .FieldType.GetGenericArguments()[0];
            IList rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType));
            foreach ((PerformanceProfile.QualityLevel level, Vector2 limit) in new[]
            {
                (PerformanceProfile.QualityLevel.Low, new Vector2(1f, 1f)),
                (PerformanceProfile.QualityLevel.High, max),
                (PerformanceProfile.QualityLevel.High, new Vector2(2f, 2f)),
            })
            {
                object row = Activator.CreateInstance(rowType);
                // Reflection: the row is a private nested type of authored serialized data.
                Field(rowType, "QualityLevel").SetValue(row, level);
                Field(rowType, "QualitySetting").SetValue(row, new UIContainerPauseBackground.QualityBackground { ResolutionMax = limit });
                rows.Add(row);
            }
            Set(quality, "m_qualityAttributes", rows); // serialized in the original
            return quality;
        }

        // The size is halved (integer division, so a 1 becomes 0) while the width is above the maximum width or the height
        // above the maximum height (the loop at 0x672020..0x672060 tests ucomiss ja on each); equal does not exceed. Each row
        // exceeds in a different way. The quality row used is the first of the active profile's level (High): the
        // lower level's and the later High row's maxima would give other sizes.
        [Test]
        public void TheSizeIsHalvedWhileEitherDimensionExceedsTheQualityMaximum()
        {
            foreach ((Vector2 max, Vector2Int expected) in new[]
            {
                (new Vector2(320f, 200f), new Vector2Int(320, 200)),
                (new Vector2(319f, 200f), new Vector2Int(160, 100)),
                (new Vector2(320f, 199f), new Vector2Int(160, 100)),
                (new Vector2(100f, 1000f), new Vector2Int(80, 50)),
                (new Vector2(1000f, 100f), new Vector2Int(160, 100)),
                (new Vector2(159f, 99f), new Vector2Int(80, 50)),
                (new Vector2(0f, 0f), new Vector2Int(0, 0)),
            })
            {
                Run(true, rig =>
                {
                    Call(rig.Background, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                    Call(rig.Background, "InitialiseFromCamera", rig.Cameras.Camera); // Reflection: private UIContainerPauseBackground.InitialiseFromCamera

                    Assert.That(Get<Vector2Int>(rig.Background, "m_screenSize"), Is.EqualTo(expected), "maximum " + max);
                }, owned => Quality(owned, max));
            }
        }

        // RenderBackgroundImage 0x671DC0 returns at once while the main camera is not ready (0x671DF1), leaving the camera and
        // the texture alone.
        [Test]
        public void RenderingWhileTheCameraIsNotReadyDoesNothing()
        {
            Run(true, rig =>
            {
                Call(rig.Background, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Call(rig.Background, "RenderBackgroundImage"); // Reflection: private UIContainerPauseBackground.RenderBackgroundImage

                Assert.That(Get<RenderTexture>(rig.Background, "m_renderTexture") == null, Is.True);
                Assert.That(rig.Cameras.Camera.cullingMask, Is.EqualTo(CullingMask));
                Assert.That(rig.Cameras.Camera.targetTexture, Is.Not.Null, "the case's own target is still assigned");
            });
        }

        // Update 0x671D90 renders only with the render texture on and the update flag set (0x671D96, 0x671D9F), and clears
        // the flag after rendering (0x671DAD). The flag has no setter in the original, so each row writes it.
        [Test]
        public void UpdateRendersAndClearsTheFlagOnlyWithTheRenderTextureAndTheFlag()
        {
            foreach ((bool useRenderTexture, bool flag, bool expectedFlag) in new[]
            {
                (true, true, false),
                (false, true, true),
                (true, false, false),
                (false, false, false),
            })
            {
                Run(useRenderTexture, rig =>
                {
                    // Reflection: nothing in the original sets it; the case supplies each state.
                    Set(rig.Background, "m_updateBackground", flag);
                    // The render step reads the manager, which OnEnable takes only with the render texture on.
                    Set(rig.Background, "m_cinemachineCameraManager", rig.Cameras.Manager);

                    Call(rig.Background, "Update"); // Reflection: Unity sends Update; Edit Mode does not

                    Assert.That(Get<bool>(rig.Background, "m_updateBackground"), Is.EqualTo(expectedFlag), "render texture " + useRenderTexture + ", flag " + flag);
                });
            }
        }

        // OnDisable 0x672090 gives the culling mask back and releases the texture only with the render texture on and a ready
        // camera (0x672096, 0x6720A3); without the render texture it does nothing even when there is no manager.
        [Test]
        public void DisablingLeavesTheCameraAloneWhenThereIsNoRenderTextureOrTheCameraIsNotReady()
        {
            Run(false, rig =>
            {
                Assert.DoesNotThrow(() => Call(rig.Background, "OnDisable")); // Reflection: Unity sends OnDisable; Edit Mode does not
            });

            Run(true, rig =>
            {
                Call(rig.Background, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not
                Call(rig.Background, "InitialiseFromCamera", rig.Cameras.Camera); // Reflection: private UIContainerPauseBackground.InitialiseFromCamera
                rig.Cameras.Camera.cullingMask = 5;

                Call(rig.Background, "OnDisable"); // Reflection: Unity sends OnDisable; Edit Mode does not

                Assert.That(rig.Cameras.Camera.cullingMask, Is.EqualTo(5), "not restored while the camera is not ready");
            });
        }

        // The quality settings class 0x672160 is a plain serializable class whose constructor only calls the base: no limit.
        [Test]
        public void AFreshQualitySettingHasNoMaximum()
        {
            Assert.That(new UIContainerPauseBackground.QualityBackground().ResolutionMax, Is.EqualTo(Vector2.zero));
        }
    }
}
