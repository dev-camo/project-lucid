using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The declaration shape of the chaos aim and chaos dash types (definitions, abilities, states), the camera
    // override definition the aim ability reads and the target indicator controller: type flags, base, attribute
    // order, and every declared field and method with its flags in metadata order. The expected rows are the
    // original DummyDll ECMA-335 tables (this is declaration shape, not behaviour).
    public sealed class CharacterAirChaosDeclarationTests
    {
        private static Type Nested(Type owner, string name) => owner.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);

        // Serializable is TypeDef flag 0x2000 (checked with the type flags), not a CustomAttribute row, but
        // Mono's reflection reports it among the attributes.
        private static string[] AttributeNames(Type type) => type.GetCustomAttributesData()
            .Where(data => data.AttributeType != typeof(SerializableAttribute))
            .Select(data => data.AttributeType.Name == "Il2CppSetOptionAttribute"
                ? "Il2CppSetOption:" + Convert.ToInt32(data.ConstructorArguments[0].Value)
                : data.AttributeType.Name.Replace("Attribute", "")).ToArray();

        private static void AssertShape(Type type, int typeFlags, Type baseType, string[] interfaces, string[] attributes,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            Type actualBase = type.BaseType.IsGenericType ? type.BaseType.GetGenericTypeDefinition() : type.BaseType;
            Assert.That(actualBase, Is.EqualTo(baseType), type.FullName + " base");
            string[] declared = type.GetInterfaces().Select(i => i.Name.Split('`')[0]).ToArray();
            Assert.That(declared, Is.SupersetOf(interfaces), type.FullName + " interfaces");
            Assert.That(AttributeNames(type), Is.EqualTo(attributes), type.FullName + " attributes");
        }

        [Test]
        public void DefinitionsKeepTheOriginalShape()
        {
            AssertShape(typeof(CharacterAbilityDefinition_AirChaosAim), 0x00100001, typeof(CharacterAbilityDefinition_AirChaos),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "CreateAssetMenu" },
                new[] { ("RaycastDistance", 0x0006), ("GroundDistance", 0x0006), ("LedgeAngleMin", 0x0006), ("LedgeAngleMax", 0x0006), ("LedgeDistance", 0x0006), ("LedgeOffset", 0x0006), ("WorldDistance", 0x0006), ("OffsetDistance", 0x0006), ("TrackMask", 0x0006), ("CollisionMask", 0x0006), ("HomingAttackPrefab", 0x0006), ("MappingHomingToAimTargetTypes", 0x0006), ("HomingTargetDefinitions", 0x0006), ("HomingTargetScreenBorders", 0x0006), ("HomingTargetZoneOffset", 0x0006), ("HomingTargetProjectDistance", 0x0006), ("HomingTargetProjectConeAngle", 0x0006), ("HomingRailProjectDistance", 0x0006), ("HomingRailProjectConeAngle", 0x0006), ("HomingRailProjectDistanceStep", 0x0006), ("AimLerpSmoothTime", 0x0006), ("AimLerpMaxSpeed", 0x0006), ("AimLerpScaleAnim", 0x0006), ("CameraTargetDelayOnEnter", 0x0006), ("CameraTargetDelayOnStay", 0x0006), ("CameraTargetHorizontalInputValue", 0x0006), ("CameraTargetVerticalInputValue", 0x0006), ("TargetTypes", 0x0006), ("<HomingTargetCone>k__BackingField", 0x0001), ("<HomingTargetProjectDistanceSqr>k__BackingField", 0x0001), ("<HomingTargetProjectConeCosAngle>k__BackingField", 0x0001), ("<HomingTargetZoneSize>k__BackingField", 0x0001), ("<HomingRailProjectConeTangents>k__BackingField", 0x0001), ("<LedgeCosAngleMin>k__BackingField", 0x0001), ("<LedgeCosAngleMax>k__BackingField", 0x0001) },
                new[] { ("get_ScriptType", 0x08c6), ("get_HomingTargetCone", 0x0886), ("set_HomingTargetCone", 0x0881), ("get_HomingTargetProjectDistanceSqr", 0x0886), ("set_HomingTargetProjectDistanceSqr", 0x0881), ("get_HomingTargetProjectConeCosAngle", 0x0886), ("set_HomingTargetProjectConeCosAngle", 0x0881), ("get_HomingTargetZoneSize", 0x0886), ("set_HomingTargetZoneSize", 0x0881), ("get_HomingRailProjectConeTangents", 0x0886), ("set_HomingRailProjectConeTangents", 0x0881), ("get_LedgeCosAngleMin", 0x0886), ("set_LedgeCosAngleMin", 0x0881), ("get_LedgeCosAngleMax", 0x0886), ("set_LedgeCosAngleMax", 0x0881), ("CalculateCachedValues", 0x00c6), (".ctor", 0x1886) });
            AssertShape(Nested(typeof(CharacterAbilityDefinition_AirChaosAim), "HomingTargetAimDefinition"), 0x00102002, typeof(object),
                new string[0],
                new string[0],
                new[] { ("TargetType", 0x0006), ("OffsetLocal", 0x0006), ("EnemyTypes", 0x0006), ("HomingTypes", 0x0006) },
                new[] { (".ctor", 0x1886) });
            AssertShape(Nested(typeof(CharacterAbilityDefinition_AirChaosAim), "ChaosAimTarget"), 0x00102002, typeof(object),
                new string[0],
                new string[0],
                new[] { ("BeamTransformType", 0x0006), ("DiscTransformType", 0x0006), ("RingTransformType", 0x0006), ("TargetRotation", 0x0006), ("ArrowsTransformType", 0x0006), ("ArrowsRotation", 0x0006), ("Arrow0Enabled", 0x0006), ("Arrow0PositionAnimation", 0x0006), ("Arrow0RotationAnimation", 0x0006), ("Arrow1Enabled", 0x0006), ("Arrow1PositionAnimation", 0x0006), ("Arrow1RotationAnimation", 0x0006), ("RequiresLerpToTarget", 0x0006), ("CameraTargetDefinition", 0x0006), ("CameraSlopeOverrides", 0x0006), ("CameraOverrideMetadataKey", 0x0006), ("RaycastGraceDistance", 0x0006), ("OrientateCharacterOnExit", 0x0006), ("AnimationDefinition", 0x0006), ("m_arrow0AnimTimeTotal", 0x0001), ("m_arrow1AnimTimeTotal", 0x0001) },
                new[] { ("CalculateCachedValues", 0x0086), ("UpdateAnimation", 0x0086), ("GetCameraOverride", 0x0086), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbilityDefinition_AirChaosDash), 0x00100001, typeof(CharacterAbilityDefinition_AirChaos),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "CreateAssetMenu" },
                new[] { ("MinimumTime", 0x0006), ("OffsetAngleToWorldUp", 0x0006), ("OffsetDistance", 0x0006), ("DashSpeed", 0x0006), ("ExitSpeedBySlopeCurve", 0x0006), ("<OffsetCosineAngleToWorldUp>k__BackingField", 0x0001) },
                new[] { ("get_ScriptType", 0x08c6), ("get_OffsetCosineAngleToWorldUp", 0x0886), ("set_OffsetCosineAngleToWorldUp", 0x0881), ("CalculateCachedValues", 0x00c6), (".ctor", 0x1886) });
            AssertShape(typeof(CameraAbilityOverrideDefinition), 0x00100001, typeof(CameraAbilityDefinition),
                new string[0],
                new[] { "CreateAssetMenu", "Il2CppSetOption:2", "Il2CppSetOption:1" },
                new[] { ("m_axisAlignment", 0x0001), ("m_reverseByDirection", 0x0001) },
                new[] { ("get_AxisAlignment", 0x0886), ("get_ReverseByDirection", 0x0886), (".ctor", 0x1886) });
        }

        [Test]
        public void AbilitiesKeepTheOriginalShape()
        {
            AssertShape(typeof(CharacterAbility_AirChaosAim), 0x00100001, typeof(CharacterAbility_AirChaos<>),
                new[] { "IHomingAttack" },
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new[] { ("ForwardToNormalRotation", 0x0021), ("AimLerpDistanceSqrTolerance", 0x8051), ("m_cone", 0x0001), ("m_targetedLocation", 0x0001), ("m_controllerMovementHasBeenActive", 0x0001), ("m_aimTransformPositionLastFrame", 0x0001), ("m_homingAttack", 0x0001), ("m_homingTargetAimDefinitions", 0x0021), ("m_homingTargets", 0x0021), ("m_homingTargetsDirty", 0x0001), ("m_railDefinition", 0x0001), ("m_transporterDefinition", 0x0001), ("m_hitCollisionData", 0x0001), ("m_aimType", 0x0001), ("m_aimTransform", 0x0001), ("m_aimPosition", 0x0001), ("m_aimOffset", 0x0001), ("m_aimNormal", 0x0001), ("m_aimRotation", 0x0001), ("m_groundType", 0x0001), ("m_groundPosition", 0x0001), ("m_groundRotation", 0x0001), ("m_debugTargetObject", 0x0001), ("m_cameraTargetTimerActive", 0x0001), ("m_cameraTargetTimer", 0x0001), ("m_aimLerpActive", 0x0001), ("m_aimLerpPosition", 0x0001), ("m_aimLerpVelocity", 0x0001), ("m_aimLerpScaleActive", 0x0001), ("m_aimLerpScaleTime", 0x0001), ("m_targetImpulse", 0x0001), ("m_targetImpulsePrevious", 0x0001), ("m_abilityAirChaosDash", 0x0001), ("m_abilityKnockback", 0x0001), ("m_cameraManagerRef", 0x0021) },
                new[] { ("Initialise", 0x00c6), ("Close", 0x00c6), ("DoOnEnter", 0x00c4), ("DoOnUpdate", 0x00c4), ("DoOnLateUpdate", 0x00c4), ("DoOnActivation", 0x00c6), ("DoOnDeactivation", 0x00c6), ("RaycastHitIsValid", 0x0081), ("TryGetHitCollider", 0x0081), ("ControllerMovementActive", 0x0081), ("UpdateTarget", 0x0081), ("OnTargetTypeEnter", 0x0081), ("OnTargetTypeUpdate", 0x0081), ("OnTargetTypeLeave", 0x0081), ("TryGetLedgeHit", 0x0081), ("TryGetRailHit", 0x0081), ("IsSurfaceTargetableRail", 0x0081), ("TryGetTransporterHit", 0x0081), ("IsSurfaceTargetableTransporter", 0x0081), ("ProjectToGround", 0x0081), ("SetTransformFromHit", 0x0081), ("GetCameraOverride", 0x0081), ("SetAirActive", 0x00c4), ("UpdateUI", 0x0086), ("ApplyTargetingImpulse", 0x0081), ("SetTargetObject", 0x0081), ("get_TargetZoneSize", 0x09e6), ("get_TargetZoneOffset", 0x09e6), ("AddTarget", 0x01e6), ("RemoveTarget", 0x01e6), ("IsHomingTargetValid", 0x01e6), ("OnHomingTargetHit", 0x01e6), ("OnTriggerEnter", 0x01e6), ("OnTriggerExit", 0x01e6), ("RemoveHomingTargetChildren", 0x0081), (".ctor", 0x1886) });
            AssertShape(typeof(CharacterAbility_AirChaosDash), 0x00100001, typeof(CharacterAbility_AirChaos<>),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new[] { ("m_targetTransform", 0x0001), ("m_targetPosition", 0x0001), ("m_targetRotation", 0x0001), ("m_startRotation", 0x0001), ("m_snapToTarget", 0x0001) },
                new[] { ("Initialise", 0x00c6), ("Activate", 0x0086), ("GetTargetPositionAndRotation", 0x0081), ("UpdateDirection", 0x0081), ("SetCameraOverrides", 0x00c4), ("OnCharacterPositionSnap", 0x0081), ("SetAirActive", 0x00c4), ("DoOnEnter", 0x00c4), ("DoOnUpdate", 0x00c4), ("DoOnLeave", 0x00c4), (".ctor", 0x1886) });
        }

        [Test]
        public void StatesKeepTheOriginalShape()
        {
            AssertShape(typeof(CharacterState_AirChaos<,>), 0x00100081, typeof(CharacterState_AirAbility),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new (string, int)[0],
                new[] { (".ctor", 0x1884), ("DoOnEnter", 0x00c4), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4), ("TryGetAbility", 0x0081) });
            AssertShape(typeof(CharacterState_AirChaosAim), 0x00100001, typeof(CharacterState_AirChaos<,>),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2", "GraphNodeDefaultName", "GraphNodeMenuFormat" },
                new (string, int)[0],
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096) });
            AssertShape(typeof(CharacterState_AirChaosDash), 0x00100001, typeof(CharacterState_AirChaos<,>),
                new string[0],
                new[] { "GraphNodeMenuFormat", "Il2CppSetOption:1", "Il2CppSetOption:2", "GraphNodeDefaultName" },
                new (string, int)[0],
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096) });
        }

        [Test]
        public void TargetIndicatorControllerKeepsTheOriginalShape()
        {
            AssertShape(typeof(UITargetIndicatorController), 0x00102001, typeof(object),
                new string[0],
                new[] { "Il2CppSetOption:1", "Il2CppSetOption:2" },
                new[] { ("LocalToWorldUp", 0x0021), ("m_beam", 0x0001), ("m_disc", 0x0001), ("m_ring", 0x0001), ("m_arrowsParent", 0x0001), ("m_arrow0", 0x0001), ("m_arrow1", 0x0001), ("m_arrow0Scaler", 0x0001), ("m_arrow1Scaler", 0x0001), ("m_arrowsAimVisual", 0x0001), ("m_projectionAim", 0x0001), ("m_projectionLocation", 0x0001), ("m_projectionLocationVisual", 0x0001), ("m_projectionLocationRenderer", 0x0001), ("m_projectionGround", 0x0001), ("m_projectionTarget", 0x0001), ("m_projectionTime", 0x0001), ("m_projectionSpeedMin", 0x0001), ("m_projectionSpeedMax", 0x0001), ("m_projections", 0x0001), ("m_aimPosition", 0x0001), ("m_aimRotation", 0x0001), ("m_groundPosition", 0x0001), ("m_groundRotation", 0x0001), ("m_overrideRotation", 0x0001), ("m_orientateToCamera", 0x0001), ("m_arrow0AnimTime", 0x0001), ("m_arrow1AnimTime", 0x0001), ("m_arrowScaleMultiplier", 0x0001), ("m_projectionTotalTime", 0x0001), ("m_aimTarget", 0x0001), ("m_character", 0x0001) },
                new[] { ("Initialise", 0x0086), ("Set", 0x0086), ("UpdateTarget", 0x0086), ("ResetProjections", 0x0081), ("UpdateProjections", 0x0081), ("SetProjectionLocation", 0x0081), ("SetProjectionTarget", 0x0081), ("IsTransformTypeGround", 0x0081), ("SetTransform", 0x0081), ("SetTransformActive", 0x0081), ("SetTransform", 0x0081), ("SetTransformAnimation", 0x0081), (".ctor", 0x1886) });
        }

        // The authored strings: the asset menu entries of the three definitions and the node names and menu
        // format of the two concrete states (attribute blobs in the DummyDll).
        [Test]
        public void AuthoredAttributeStringsMatchTheOriginalMetadata()
        {
            AssertMenu(typeof(CharacterAbilityDefinition_AirChaosAim), "CharacterAbilityDefinition_AirChaosAim", "HardlightProject/DefinitionData/AbilityDef/AirChaosAim", 1);
            AssertMenu(typeof(CharacterAbilityDefinition_AirChaosDash), "CharacterAbilityDefinition_AirChaosDash", "HardlightProject/DefinitionData/AbilityDef/AirChaosDash", 1);
            AssertMenu(typeof(CameraAbilityOverrideDefinition), "CameraAbilityOverrideDefinition", "HardlightProject/DefinitionData/Definitions/CameraAbilityOverrideDefinition", 0);
            Assert.That(typeof(CharacterState_AirChaosAim).GetCustomAttribute<GraphNodeDefaultNameAttribute>().DefaultName, Is.EqualTo("AirChaosAim"));
            Assert.That(typeof(CharacterState_AirChaosDash).GetCustomAttribute<GraphNodeDefaultNameAttribute>().DefaultName, Is.EqualTo("AirChaosDash"));
            Assert.That(typeof(CharacterState_AirChaosAim).GetCustomAttribute<GraphNodeMenuFormatAttribute>().Format, Is.EqualTo("Character/{0}"));
            Assert.That(typeof(CharacterState_AirChaosDash).GetCustomAttribute<GraphNodeMenuFormatAttribute>().Format, Is.EqualTo("Character/{0}"));
        }

        private static void AssertMenu(Type type, string fileName, string menuName, int order)
        {
            var menu = type.GetCustomAttribute<UnityEngine.CreateAssetMenuAttribute>();
            Assert.That((menu.fileName, menu.menuName, menu.order), Is.EqualTo((fileName, menuName, order)), type.FullName);
        }
    }
}
