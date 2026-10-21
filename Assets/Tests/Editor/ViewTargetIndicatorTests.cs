using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldSurfaceShared;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // ViewTargetIndicatorParameters (Game.Runtime 0x02000856, constructor x86-64 0x60EE10) and ViewTargetIndicator (0x02000855,
    // 0x60E610..0x60ED90) as a real component on an inactive owned host over a real UI Image, an owned Canvas and an owned target
    // parent object. The serialized image, canvas, target parent and tracking types are authored data; the Unity messages Awake and
    // Reset and the protected members are called directly. The base class ViewTarget is tested through its own cases. Not covered:
    // the per-frame LateUpdate that applies the position, which needs the camera manager.
    public sealed class ViewTargetIndicatorTests
    {
        private const float Tolerance = 1e-4f;
        private const BindingFlags Backing = BindingFlags.Instance | BindingFlags.NonPublic;

        private sealed class Rig
        {
            public Owned Owned;
            public ViewTargetIndicator View;
            public Image Image;
            public Canvas Canvas;
            public GameObject TargetParent;
        }

        private sealed class AbilityProbe : IHomingAbility
        {
            public bool CanActivate { get; set; }
            public bool IsTriggered { get; set; }
            public bool OverridesTargeting => false;
            public CharacterAbilityDefinition_Targeting TargetingDefinition => null;
            public void Reset() { }
            public bool ShouldProjectFromCamera() => false;
            public float GetAdditionalSpeed(float time) => 0f;
        }

        private static void Run(Action<Rig> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<CinemachineCameraManager>(); // ViewTarget's field initialiser
            var rig = new Rig { Owned = owned };
            rig.View = owned.Host("Lucid view target indicator", false).AddComponent<ViewTargetIndicator>();
            rig.Image = owned.Host("Lucid target image", false).AddComponent<Image>();
            rig.Canvas = owned.Host("Lucid target canvas", false).AddComponent<Canvas>();
            rig.TargetParent = owned.Host("Lucid target parent");
            Set(rig.View, "m_targetImage", rig.Image);
            Set(rig.View, "m_canvas", rig.Canvas);
            Set(rig.View, "m_targetParent", rig.TargetParent);
            body(rig);
        });

        private static void WithHomingTargets(Action<Rig> body) => Owned.Run(owned =>
        {
            // HomingTarget caches a CharacterManager reference, and its Editor OnValidate reads the time lookup cache; the
            // TimeScaledComponent base initialiser takes the TimeManager row (as in the other homing target fixtures).
            owned.LeaseRow<TimeManager>();
            owned.LeaseRow<CharacterManager>();
            FieldInfo config = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = config.GetValue(null);
            try
            {
                config.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                Run(body);
            }
            finally
            {
                config.SetValue(null, prior);
            }
        });

        private static void Allow(Rig rig, HomingTargetType type) => Get<List<HomingTargetType>>(rig.View, "m_showForHomingTargetTypes").Add(type);

        private static void Track(Rig rig, HomingTargetType type) => Get<List<HomingTargetType>>(rig.View, "m_continuousTrackingTargetTypes").Add(type);

        private static T GetProperty<T>(Rig rig, string name) => (T)Call(rig.View, "get_" + name);

        private static void SetProperty(Rig rig, string name, object value) => Call(rig.View, "set_" + name, value);

        private static HomingTargetUpdateMessage Message(bool canActivate, HomingTargetType type, GameObject target, Vector3 position,
            TargetObjectMetaData metaData = null) => new HomingTargetUpdateMessage
        {
            IsValid = true,
            TargetValue = new CharacterHomingPool.PooledTarget
            {
                Abilities = new List<IHomingAbility> { new AbilityProbe { CanActivate = canActivate } },
                Object = new CharacterTargetObjectData { Target = target, WorldPosition = position, Type = type, MetaData = metaData },
            },
        };

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance), "z");
        }

        // dump.cs TypeDefIndex 2133 and the DummyDll rows (field 0x04002008, constructor 0x0600305a).
        [Test]
        public void TheParametersMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(ViewTargetIndicatorParameters), 0x00100001,
                new (string, int)[] { ("TargetIndicator", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(ViewTargetIndicatorParameters), 1, 2);
            Assert.That(typeof(ViewTargetIndicatorParameters).BaseType, Is.EqualTo(typeof(ViewTargetParameters)));
            Assert.That(new ViewTargetIndicatorParameters().TargetIndicator == null, Is.True);
        }

        // dump.cs TypeDefIndex 2132 and the DummyDll rows (fields 0x04002003..0x04002007, methods 0x0600304b..0x06003059).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(ViewTargetIndicator), 0x00100001,
                new (string, int)[]
                {
                    ("m_targetImage", 0x0001), ("m_canvas", 0x0001), ("m_continuousTrackingTargetTypes", 0x0001), ("m_targetParent", 0x0001),
                    ("m_targetImageTransform", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x00c4), ("Reset", 0x0081), ("OnSetup", 0x00c4), ("get_TargetPosition", 0x08c4), ("set_TargetPosition", 0x08c4),
                    ("get_TargetScale", 0x08c4), ("set_TargetScale", 0x08c4), ("get_TargetColour", 0x08c4), ("set_TargetColour", 0x08c4),
                    ("ShowTarget", 0x00c4), ("get_ProjectToScreenSpace", 0x08c4), ("TargetInRange", 0x00c4),
                    ("UpdateFromDetectorMessage", 0x00c6), ("UpdateFromTargetMessage", 0x00c6), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(ViewTargetIndicator), 2, 1);
            Assert.That(typeof(ViewTargetIndicator).BaseType, Is.EqualTo(typeof(ViewTarget)));
            foreach (string field in new[] { "m_targetImage", "m_canvas", "m_targetParent" })
                Assert.That(AttributeTypes(typeof(ViewTargetIndicator).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
            FieldInfo tracking = typeof(ViewTargetIndicator).GetField("m_continuousTrackingTargetTypes", Declared);
            Assert.That(AttributeTypes(tracking), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(tracking), Is.EqualTo("Indicator will keep tracking the transform of the target for these homing target types."));
        }

        // The constructor 0x60ED90 builds the tracking type list before the ViewTarget constructor.
        [Test]
        public void ConstructionStartsWithNoTrackedTypes()
        {
            Run(rig => Assert.That(Get<List<HomingTargetType>>(rig.View, "m_continuousTrackingTargetTypes"), Is.Empty));
        }

        // Awake 0x60E610 hides the target through the virtual ShowTarget (slot 20) and takes the image's transform as a
        // RectTransform (the cast at 0x60E673).
        [Test]
        public void AwakeHidesTheTargetAndTakesTheImageTransform()
        {
            Run(rig =>
            {
                Call(rig.View, "Awake");

                Assert.That(rig.TargetParent.activeSelf, Is.False);
                Assert.That(Get<RectTransform>(rig.View, "m_targetImageTransform"), Is.SameAs(rig.Image.rectTransform));
            });
        }

        // Reset 0x60E6B0 stores GetComponent<Canvas>() (Component.GetComponent, null when there is none).
        [Test]
        public void ResetTakesTheCanvasOfTheSameObject()
        {
            Run(rig =>
            {
                Call(rig.View, "Reset");
                Assert.That(Get<Canvas>(rig.View, "m_canvas") == null, Is.True, "no canvas on the indicator's own object");

                var own = rig.View.gameObject.AddComponent<Canvas>();
                Call(rig.View, "Reset");
                Assert.That(Get<Canvas>(rig.View, "m_canvas"), Is.SameAs(own));
            });
        }

        // set_TargetPosition 0x60E880 divides all three components by the canvas scale factor (divps on x and y, divss on z) and
        // writes the anchored position of the image's rect transform; get_TargetPosition 0x60E860 reads it back.
        [Test]
        public void ThePositionIsSetInCanvasUnits()
        {
            Run(rig =>
            {
                rig.Canvas.scaleFactor = 2f;
                Call(rig.View, "Awake");

                SetProperty(rig, "TargetPosition", new Vector3(200f, 100f, 30f));

                AssertNear(rig.Image.rectTransform.anchoredPosition3D, new Vector3(100f, 50f, 15f));
                AssertNear(GetProperty<Vector3>(rig, "TargetPosition"), new Vector3(100f, 50f, 15f));
            });
        }

        // The scale (0x60E8F0, 0x60E8D0) is the image's local scale and the colour (0x60E950, 0x60E930) is the image's colour.
        [Test]
        public void TheScaleAndColourBelongToTheImage()
        {
            Run(rig =>
            {
                SetProperty(rig, "TargetScale", new Vector3(2f, 3f, 4f));
                SetProperty(rig, "TargetColour", new Color(0.25f, 0.5f, 0.75f, 0.5f));

                Assert.That(rig.Image.transform.localScale, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Assert.That(rig.Image.color, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 0.5f)));
                Assert.That(GetProperty<Vector3>(rig, "TargetScale"), Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Assert.That(GetProperty<Color>(rig, "TargetColour"), Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 0.5f)));
            });
        }

        // ShowTarget 0x60E970 activates or deactivates the target parent; get_ProjectToScreenSpace 0x60E990 is constant true.
        [Test]
        public void ShowTargetTogglesTheTargetParentAndTheIndicatorProjectsToScreenSpace()
        {
            Run(rig =>
            {
                Call(rig.View, "ShowTarget", false);
                Assert.That(rig.TargetParent.activeSelf, Is.False);
                Call(rig.View, "ShowTarget", true);
                Assert.That(rig.TargetParent.activeSelf, Is.True);
                Assert.That(GetProperty<bool>(rig, "ProjectToScreenSpace"), Is.True);
            });
        }

        // TargetInRange 0x60E9A0: a depth below the minimum render distance (ucomiss min, z; ja) or above the maximum
        // (ucomiss z, max; jbe taken when not above) hides the target and answers false; depths on the limits are in range. A depth
        // that is not a number fails both tests (unordered compare), so it is in range. Limits 10 and 100.
        [TestCase(9.5f, false, TestName = "ADepthBelowTheMinimumIsOutOfRange")]
        [TestCase(10f, true, TestName = "ADepthOnTheMinimumIsInRange")]
        [TestCase(55f, true, TestName = "ADepthBetweenTheLimitsIsInRange")]
        [TestCase(100f, true, TestName = "ADepthOnTheMaximumIsInRange")]
        [TestCase(100.5f, false, TestName = "ADepthAboveTheMaximumIsOutOfRange")]
        [TestCase(float.NaN, true, TestName = "ADepthThatIsNotANumberIsInRange")]
        public void TheRenderDistanceDecidesWhetherTheTargetIsInRange(float depth, bool inRange)
        {
            Run(rig =>
            {
                Set(rig.View, "m_minRenderDistance", 10f); // Reflection: serialized limit of the base class
                Set(rig.View, "m_maxRenderDistance", 100f); // Reflection: serialized limit of the base class
                Assert.That(rig.TargetParent.activeSelf, Is.True, "set-up: the target is shown");

                bool result = (bool)Call(rig.View, "TargetInRange", new Vector3(1f, 2f, depth));

                Assert.That(result, Is.EqualTo(inRange));
                Assert.That(rig.TargetParent.activeSelf, Is.EqualTo(inRange), "an out of range target is hidden, an in range one is left alone");
            });
        }

        // OnSetup 0x60E710 runs the base setup (the world target and the scale are taken), then replaces the image's sprite only
        // when the parameters carry one (op_Inequality at 0x60E826).
        [Test]
        public void SetupTakesTheTargetAndReplacesTheSpriteOnlyWhenOneIsGiven()
        {
            Run(rig =>
            {
                Sprite original = NewSprite(rig.Owned);
                Sprite indicator = NewSprite(rig.Owned);
                rig.Image.sprite = original;

                Call(rig.View, "OnSetup", new ViewTargetIndicatorParameters { WorldspaceTarget = new Vector3(1f, 2f, 3f) });
                Assert.That(rig.Image.sprite, Is.SameAs(original), "no indicator sprite was given");
                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));

                Call(rig.View, "OnSetup", new ViewTargetIndicatorParameters { WorldspaceTarget = new Vector3(4f, 5f, 6f), TargetIndicator = indicator });
                Assert.That(rig.Image.sprite, Is.SameAs(indicator));
                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            });
        }

        // The parameters are type-checked, not cast, against the indicator's own type (0x60E7F3 jb and 0x60E801 jne both go to the
        // plain return at 0x60E84B; only the base cast at 0x60E793 reaches RaiseInvalidCastException). Plain parameters still
        // give the world target and leave the sprite alone.
        [Test]
        public void SetupWithPlainTargetParametersLeavesTheSpriteAlone()
        {
            Run(rig =>
            {
                Sprite original = NewSprite(rig.Owned);
                rig.Image.sprite = original;
                var plain = new ViewTargetParameters { WorldspaceTarget = new Vector3(7f, 8f, 9f) };

                Assert.DoesNotThrow(() => Call(rig.View, "OnSetup", plain));

                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(new Vector3(7f, 8f, 9f)));
                Assert.That(rig.Image.sprite, Is.SameAs(original));
            });
        }

        // UpdateFromDetectorMessage 0x60E9E0 always clears the target first (collider, transform and world position), then pins the
        // surface position when the message has a surface (the interface reference at +0x8 is not null) and its type is listed.
        [Test]
        public void ASurfaceDetectionOfAListedTypePinsTheSurfacePosition()
        {
            Run(rig =>
            {
                Allow(rig, HomingTargetType.Rail);
                var surface = rig.Owned.Host("Lucid detected surface", false).AddComponent<FixedNearestSurfaceProbe>();
                rig.View.SetTargetPosition(new Vector3(9f, 9f, 9f));

                rig.View.UpdateFromDetectorMessage(new SurfaceDetectorUpdateMessage
                {
                    Type = HomingTargetType.Rail,
                    SurfaceLocation = new SurfaceLocation { m_surface = surface, m_worldPosition = new Vector3(1f, 2f, 3f) },
                });

                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            });
        }

        [Test]
        public void ADetectionThatDoesNotMatchClearsTheTarget()
        {
            Run(rig =>
            {
                Allow(rig, HomingTargetType.Rail);
                var surface = rig.Owned.Host("Lucid detected surface", false).AddComponent<FixedNearestSurfaceProbe>();
                var followed = rig.Owned.Host("Lucid followed object", false).transform;

                rig.View.SetTargetTransform(followed);
                rig.View.UpdateFromDetectorMessage(new SurfaceDetectorUpdateMessage
                {
                    Type = HomingTargetType.Enemy,
                    SurfaceLocation = new SurfaceLocation { m_surface = surface, m_worldPosition = new Vector3(1f, 2f, 3f) },
                });
                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(Vector3.zero), "an unlisted type pins nothing");
                // Reflection: the base class keeps the followed transform in a private auto-property.
                Assert.That(typeof(ViewTarget).GetField("<m_targetTransform>k__BackingField", Backing).GetValue(rig.View), Is.Null);

                rig.View.SetTargetPosition(new Vector3(9f, 9f, 9f));
                rig.View.UpdateFromDetectorMessage(new SurfaceDetectorUpdateMessage
                {
                    Type = HomingTargetType.Rail,
                    SurfaceLocation = new SurfaceLocation { m_worldPosition = new Vector3(1f, 2f, 3f) },
                });
                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(Vector3.zero), "a detection without a surface pins nothing");
            });
        }

        // UpdateFromTargetMessage 0x60EAD0: a message that cannot activate (HomingTargetUpdateMessage.CanActivate 0x59C3D0) or whose
        // type is not listed clears the target.
        [Test]
        public void AMessageThatCannotActivateOrIsNotListedClearsTheTarget()
        {
            Run(rig =>
            {
                Allow(rig, HomingTargetType.Enemy);
                GameObject target = rig.Owned.Host("Lucid homing object");

                rig.View.SetTargetPosition(new Vector3(9f, 9f, 9f));
                rig.View.UpdateFromTargetMessage(Message(false, HomingTargetType.Enemy, target, new Vector3(1f, 2f, 3f)));
                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(Vector3.zero), "no ability can activate");

                rig.View.SetTargetPosition(new Vector3(9f, 9f, 9f));
                rig.View.UpdateFromTargetMessage(Message(true, HomingTargetType.Rail, target, new Vector3(1f, 2f, 3f)));
                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(Vector3.zero), "the type is not listed");
            });
        }

        // An untracked type is pinned at the position the message carried (WorldPosition, +0x8), not at the object.
        [Test]
        public void AnUntrackedTypeIsPinnedAtTheMessagePosition()
        {
            Run(rig =>
            {
                Allow(rig, HomingTargetType.Enemy);
                GameObject target = rig.Owned.Host("Lucid homing object");
                target.transform.position = new Vector3(40f, 50f, 60f);

                rig.View.UpdateFromTargetMessage(Message(true, HomingTargetType.Enemy, target, new Vector3(1f, 2f, 3f)));

                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            });
        }

        // A tracked type without homing metadata (TryGetMetaDataAsType false, 0x60EC44) follows the object's transform.
        [Test]
        public void ATrackedTypeWithoutHomingMetadataFollowsTheObjectTransform()
        {
            Run(rig =>
            {
                Allow(rig, HomingTargetType.Enemy);
                Track(rig, HomingTargetType.Enemy);
                GameObject target = rig.Owned.Host("Lucid homing object");
                target.transform.position = new Vector3(40f, 50f, 60f);

                rig.View.UpdateFromTargetMessage(Message(true, HomingTargetType.Enemy, target, new Vector3(1f, 2f, 3f)));

                Assert.That(rig.View.TargetWorldPosition, Is.EqualTo(new Vector3(40f, 50f, 60f)));
            });
        }

        // A tracked type with homing metadata follows the collider of its HomingTarget (HomingTarget +0xB0, SetTargetCollider): the
        // world position is the centre of the collider's bounds.
        [Test]
        public void ATrackedTypeWithHomingMetadataFollowsTheTargetsCollider()
        {
            WithHomingTargets(rig =>
            {
                Allow(rig, HomingTargetType.Enemy);
                Track(rig, HomingTargetType.Enemy);
                GameObject host = rig.Owned.Host("Lucid homing target");
                host.transform.position = new Vector3(3f, 4f, 5f);
                var collider = host.AddComponent<BoxCollider>();
                var homing = host.AddComponent<HomingTarget>();
                Set(homing, "m_collider", collider); // Reflection: serialized reference
                Physics.SyncTransforms();

                rig.View.UpdateFromTargetMessage(Message(true, HomingTargetType.Enemy, host, new Vector3(1f, 2f, 3f),
                    new HomingObjectMetaData { HomingTarget = homing }));

                AssertNear(rig.View.TargetWorldPosition, new Vector3(3f, 4f, 5f));
            });
        }
    }
}
