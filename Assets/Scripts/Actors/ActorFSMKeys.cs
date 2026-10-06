using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000058. Complete 151-key initialization from
    // supplied ARM0x4f63b8 and x86 counterpart; only the original cctor is a body.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class ActorFSMKeys
    {
        // Original 0x040001af; native static offset 0x0.
        public static readonly GraphStorageKey AbilitiesInUse = new GraphStorageKey("AbilitiesInUse", 0, 0);
        // Original 0x040001b0; native static offset 0x10.
        public static readonly GraphStorageKey AbilityHeading = new GraphStorageKey("AbilityHeading", 0, 0);
        // Original 0x040001b1; native static offset 0x20.
        public static readonly GraphStorageKey StateRemainTimer = new GraphStorageKey("StateRemainTimer", 0, 0);
        // Original 0x040001b2; native static offset 0x30.
        public static readonly GraphStorageKey IsCollectingOrb = new GraphStorageKey("IsCollectingOrb", 0, 0);
        // Original 0x040001b3; native static offset 0x40.
        public static readonly GraphStorageKey OutroSequenceType = new GraphStorageKey("OutroSequenceType", 0, 0);
        // Original 0x040001b4; native static offset 0x50.
        public static readonly GraphStorageKey SkipLevelEnter = new GraphStorageKey("SkipLevelEnter", 0, 0);
        // Original 0x040001b5; native static offset 0x60.
        public static readonly GraphStorageKey IsEnteringLevel = new GraphStorageKey("IsEnteringLevel", 0, 0);
        // Original 0x040001b6; native static offset 0x70.
        public static readonly GraphStorageKey IsExitingLevel = new GraphStorageKey("IsExitingLevel", 0, 0);
        // Original 0x040001b7; native static offset 0x80.
        public static readonly GraphStorageKey SwapInIsInProgress = new GraphStorageKey("SwapInIsInProgress", 0, 0);
        // Original 0x040001b8; native static offset 0x90.
        public static readonly GraphStorageKey SwapOutIsInProgress = new GraphStorageKey("SwapOutIsInProgress", 0, 0);
        // Original 0x040001b9; native static offset 0xa0.
        public static readonly GraphStorageKey InterruptDyingState = new GraphStorageKey("InterruptDyingState", 0, 0);
        // Original 0x040001ba; native static offset 0xb0.
        public static readonly GraphStorageKey FullscreenEffectDictionary = new GraphStorageKey("FullscreenEffectDictionary", 0, 0);
        // Original 0x040001bb; native static offset 0xc0.
        public static readonly GraphStorageKey SwitchAvailableOverrideHandle = new GraphStorageKey("SwitchAvailableOverrideHandle", 0, 0);
        // Original 0x040001bc; native static offset 0xd0.
        public static readonly GraphStorageKey AbilityDefinitionOverride = new GraphStorageKey("AbilityDefinitionOverride", 0, 0);
        // Original 0x040001bd; native static offset 0xe0.
        public static readonly GraphStorageKey StaminaUIDisabled = new GraphStorageKey("StaminaUIDisabled", 0, 0);
        // Original 0x040001be; native static offset 0xf0.
        public static readonly GraphStorageKey FormLockedOverrideHandle = new GraphStorageKey("FormLockedOverrideHandle", 0, 0);
        // Original 0x040001bf; native static offset 0x100.
        public static readonly GraphStorageKey SpeedLockCanBeExceeded = new GraphStorageKey("SpeedLockCanBeExceeded", 0, 0);
        // Original 0x040001c0; native static offset 0x110.
        public static readonly GraphStorageKey ActiveAnimationLookup = new GraphStorageKey("ActiveAnimationLookup", 0, 0);
        // Original 0x040001c1; native static offset 0x120.
        public static readonly GraphStorageKey BoostSwitchStamina = new GraphStorageKey("BoostSwitchStamina", 0, 0);
        // Original 0x040001c2; native static offset 0x130.
        public static readonly GraphStorageKey BoostSwitchRatio = new GraphStorageKey("BoostSwitchRatio", 0, 0);
        // Original 0x040001c3; native static offset 0x140.
        public static readonly GraphStorageKey BoostSwitchAbility = new GraphStorageKey("BoostSwitchAbility", 0, 0);
        // Original 0x040001c4; native static offset 0x150.
        public static readonly GraphStorageKey AirProjectedBufferDisabled = new GraphStorageKey("AirProjectedBufferDisabled", 0, 0);
        // Original 0x040001c5; native static offset 0x160.
        public static readonly GraphStorageKey AirProjectedBufferTimer = new GraphStorageKey("AirProjectedBufferTimer", 0, 0);
        // Original 0x040001c6; native static offset 0x170.
        public static readonly GraphStorageKey AirDecelerationDisabled = new GraphStorageKey("AirDecelerationDisabled", 0, 0);
        // Original 0x040001c7; native static offset 0x180.
        public static readonly GraphStorageKey AirControlsLockTimeEnd = new GraphStorageKey("AirControlsLockTimeEnd", 0, 0);
        // Original 0x040001c8; native static offset 0x190.
        public static readonly GraphStorageKey AirAbilitiesReset = new GraphStorageKey("AirAbilitiesReset", 0, 0);
        // Original 0x040001c9; native static offset 0x1a0.
        public static readonly GraphStorageKey AirAbilityActive = new GraphStorageKey("AirAbilityActive", 0, 0);
        // Original 0x040001ca; native static offset 0x1b0.
        public static readonly GraphStorageKey AirAbilityBlendVelocity = new GraphStorageKey("AirAbilityBlendVelocity", 0, 0);
        // Original 0x040001cb; native static offset 0x1c0.
        public static readonly GraphStorageKey AirAbilityBlendGravity = new GraphStorageKey("AirAbilityBlendGravity", 0, 0);
        // Original 0x040001cc; native static offset 0x1d0.
        public static readonly GraphStorageKey AirAbilityDirectionOverrideAnimationElapsedSeconds = new GraphStorageKey("AirAbilityDirectionOverrideAnimationElapsedSeconds", 0, 0);
        // Original 0x040001cd; native static offset 0x1e0.
        public static readonly GraphStorageKey AirAbilityInterruptTimer = new GraphStorageKey("AirAbilityInterruptTimer", 0, 0);
        // Original 0x040001ce; native static offset 0x1f0.
        public static readonly GraphStorageKey AirAbilityElapsedTime = new GraphStorageKey("AirAbilityElapsedTime", 0, 0);
        // Original 0x040001cf; native static offset 0x200.
        public static readonly GraphStorageKey AirAbilityActiveCount = new GraphStorageKey("AirAbilityActiveCount", 0, 0);
        // Original 0x040001d0; native static offset 0x210.
        public static readonly GraphStorageKey AirLedgeBufferTimer = new GraphStorageKey("AirLedgeBufferTimer", 0, 0);
        // Original 0x040001d1; native static offset 0x220.
        public static readonly GraphStorageKey AirLedgeBufferFromRail = new GraphStorageKey("AirLedgeBufferFromRail", 0, 0);
        // Original 0x040001d2; native static offset 0x230.
        public static readonly GraphStorageKey LastRespawnPoint = new GraphStorageKey("LastRespawnPoint", 0, 0);
        // Original 0x040001d3; native static offset 0x240.
        public static readonly GraphStorageKey IsOutOfBounds = new GraphStorageKey("IsOutOfBounds", 0, 0);
        // Original 0x040001d4; native static offset 0x250.
        public static readonly GraphStorageKey InHardFailureState = new GraphStorageKey("InHardFailureState", 0, 0);
        // Original 0x040001d5; native static offset 0x260.
        public static readonly GraphStorageKey TriggerDyingState = new GraphStorageKey("TriggerDyingState", 0, 0);
        // Original 0x040001d6; native static offset 0x270.
        public static readonly GraphStorageKey InDyingStateTime = new GraphStorageKey("InDyingStateTime", 0, 0);
        // Original 0x040001d7; native static offset 0x280.
        public static readonly GraphStorageKey RespawnTeleportRequired = new GraphStorageKey("RespawnTeleportRequired", 0, 0);
        // Original 0x040001d8; native static offset 0x290.
        public static readonly GraphStorageKey RespawnTeleportToAir = new GraphStorageKey("RespawnTeleportToAir", 0, 0);
        // Original 0x040001d9; native static offset 0x2a0.
        public static readonly GraphStorageKey RespawnEffectsComplete = new GraphStorageKey("RespawnEffectsComplete", 0, 0);
        // Original 0x040001da; native static offset 0x2b0.
        public static readonly GraphStorageKey RespawnRetainVelocity = new GraphStorageKey("RespawnRetainVelocity", 0, 0);
        // Original 0x040001db; native static offset 0x2c0.
        public static readonly GraphStorageKey RespawnInvulnerableDuration = new GraphStorageKey("RespawnInvulnerableDuration", 0, 0);
        // Original 0x040001dc; native static offset 0x2d0.
        public static readonly GraphStorageKey RespawnAnimationPlaying = new GraphStorageKey("RespawnAnimationPlaying", 0, 0);
        // Original 0x040001dd; native static offset 0x2e0.
        public static readonly GraphStorageKey ShieldDamageHitPoints = new GraphStorageKey("ShieldDamageHitPoints", 0, 0);
        // Original 0x040001de; native static offset 0x2f0.
        public static readonly GraphStorageKey JumpElapsedTime = new GraphStorageKey("JumpElapsedTime", 0, 0);
        // Original 0x040001df; native static offset 0x300.
        public static readonly GraphStorageKey IsJumping = new GraphStorageKey("IsJumping", 0, 0);
        // Original 0x040001e0; native static offset 0x310.
        public static readonly GraphStorageKey JumpDirection = new GraphStorageKey("JumpDirection", 0, 0);
        // Original 0x040001e1; native static offset 0x320.
        public static readonly GraphStorageKey JumpingExitedGround = new GraphStorageKey("JumpingExitedGround", 0, 0);
        // Original 0x040001e2; native static offset 0x330.
        public static readonly GraphStorageKey JumpAngleExtra = new GraphStorageKey("JumpAngleExtra", 0, 0);
        // Original 0x040001e3; native static offset 0x340.
        public static readonly GraphStorageKey JumpOnRailTracker = new GraphStorageKey("JumpOnRailTracker", 0, 0);
        // Original 0x040001e4; native static offset 0x350.
        public static readonly GraphStorageKey JumpOnRailCanTurnAround = new GraphStorageKey("JumpOnRailCanTurnAround", 0, 0);
        // Original 0x040001e5; native static offset 0x360.
        public static readonly GraphStorageKey JumpOnRailMagnetism = new GraphStorageKey("JumpOnRailMagnetism", 0, 0);
        // Original 0x040001e6; native static offset 0x370.
        public static readonly GraphStorageKey JumpOnRailDistanceMax = new GraphStorageKey("JumpOnRailDistanceMax", 0, 0);
        // Original 0x040001e7; native static offset 0x380.
        public static readonly GraphStorageKey JumpOnRailAngleCosineMax = new GraphStorageKey("JumpOnRailAngleCosineMax", 0, 0);
        // Original 0x040001e8; native static offset 0x390.
        public static readonly GraphStorageKey JumpDecelerationDisabled = new GraphStorageKey("JumpDecelerationDisabled", 0, 0);
        // Original 0x040001e9; native static offset 0x3a0.
        public static readonly GraphStorageKey CanQueueJump = new GraphStorageKey("CanQueueJump", 0, 0);
        // Original 0x040001ea; native static offset 0x3b0.
        public static readonly GraphStorageKey CanQueueBoost = new GraphStorageKey("CanQueueBoost", 0, 0);
        // Original 0x040001eb; native static offset 0x3c0.
        public static readonly GraphStorageKey JumpFaceVelocityDirection = new GraphStorageKey("JumpFaceVelocityDirection", 0, 0);
        // Original 0x040001ec; native static offset 0x3d0.
        public static readonly GraphStorageKey TurnInputInverted = new GraphStorageKey("TurnInputInverted", 0, 0);
        // Original 0x040001ed; native static offset 0x3e0.
        public static readonly GraphStorageKey TurnTrackerActive = new GraphStorageKey("TurnTrackerActive", 0, 0);
        // Original 0x040001ee; native static offset 0x3f0.
        public static readonly GraphStorageKey TurnCameraActive = new GraphStorageKey("TurnCameraActive", 0, 0);
        // Original 0x040001ef; native static offset 0x400.
        public static readonly GraphStorageKey TurnCameraRotation = new GraphStorageKey("TurnCameraRotation", 0, 0);
        // Original 0x040001f0; native static offset 0x410.
        public static readonly GraphStorageKey TurnCameraStickyControls = new GraphStorageKey("TurnCameraStickyControls", 0, 0);
        // Original 0x040001f1; native static offset 0x420.
        public static readonly GraphStorageKey TurnCameraInput = new GraphStorageKey("TurnCameraInput", 0, 0);
        // Original 0x040001f2; native static offset 0x430.
        public static readonly GraphStorageKey TurnCameraInputRelative = new GraphStorageKey("TurnCameraInputRelative", 0, 0);
        // Original 0x040001f3; native static offset 0x440.
        public static readonly GraphStorageKey GroundGripMotion = new GraphStorageKey("GroundGripMotion", 0, 0);
        // Original 0x040001f4; native static offset 0x450.
        public static readonly GraphStorageKey GroundGripTime = new GraphStorageKey("GroundGripTime", 0, 0);
        // Original 0x040001f5; native static offset 0x460.
        public static readonly GraphStorageKey GroundGripValue = new GraphStorageKey("GroundGripValue", 0, 0);
        // Original 0x040001f6; native static offset 0x470.
        public static readonly GraphStorageKey GroundGripRestoreTimer = new GraphStorageKey("GroundGripRestoreTimer", 0, 0);
        // Original 0x040001f7; native static offset 0x480.
        public static readonly GraphStorageKey GroundBoostRestoreTimer = new GraphStorageKey("GroundBoostRestoreTimer", 0, 0);
        // Original 0x040001f8; native static offset 0x490.
        public static readonly GraphStorageKey HomingTargetType = new GraphStorageKey("HomingTargetType", 0, 0);
        // Original 0x040001f9; native static offset 0x4a0.
        public static readonly GraphStorageKey HomingTargetTime = new GraphStorageKey("HomingTargetTime", 0, 0);
        // Original 0x040001fa; native static offset 0x4b0.
        public static readonly GraphStorageKey HomingTargetInnerVolumeOnly = new GraphStorageKey("HomingTargetInnerVolumeOnly", 0, 0);
        // Original 0x040001fb; native static offset 0x4c0.
        public static readonly GraphStorageKey HomingAttackActionTimestamp = new GraphStorageKey("HomingAttackActionTimestamp", 0, 0);
        // Original 0x040001fc; native static offset 0x4d0.
        public static readonly GraphStorageKey HomingAttackOneShotActive = new GraphStorageKey("HomingAttackOneShotActive", 0, 0);
        // Original 0x040001fd; native static offset 0x4e0.
        public static readonly GraphStorageKey HomingRailSwitchObject = new GraphStorageKey("HomingRailSwitchObject", 0, 0);
        // Original 0x040001fe; native static offset 0x4f0.
        public static readonly GraphStorageKey HomingRailSwitchSpeed = new GraphStorageKey("HomingRailSwitchSpeed", 0, 0);
        // Original 0x040001ff; native static offset 0x500.
        public static readonly GraphStorageKey RailActive = new GraphStorageKey("RailActive", 0, 0);
        // Original 0x04000200; native static offset 0x510.
        public static readonly GraphStorageKey RailActiveLastTime = new GraphStorageKey("RailActiveLastTime", 0, 0);
        // Original 0x04000201; native static offset 0x520.
        public static readonly GraphStorageKey RailSwitchNeighbour = new GraphStorageKey("RailSwitchNeighbour", 0, 0);
        // Original 0x04000202; native static offset 0x530.
        public static readonly GraphStorageKey RailSwitchTimeElapsed = new GraphStorageKey("RailSwitchTimeElapsed", 0, 0);
        // Original 0x04000203; native static offset 0x540.
        public static readonly GraphStorageKey RailSwitchStartPosition = new GraphStorageKey("RailSwitchStartPosition", 0, 0);
        // Original 0x04000204; native static offset 0x550.
        public static readonly GraphStorageKey RailSwitchStartVelocity = new GraphStorageKey("RailSwitchStartVelocity", 0, 0);
        // Original 0x04000205; native static offset 0x560.
        public static readonly GraphStorageKey RailSwitchResumeSpeed = new GraphStorageKey("RailSwitchResumeSpeed", 0, 0);
        // Original 0x04000206; native static offset 0x570.
        public static readonly GraphStorageKey RailIsValid = new GraphStorageKey("RailIsValid", 0, 0);
        // Original 0x04000207; native static offset 0x580.
        public static readonly GraphStorageKey RailTransferYVelocity = new GraphStorageKey("RailTransferYVelocity", 0, 0);
        // Original 0x04000208; native static offset 0x590.
        public static readonly GraphStorageKey RailTurnAroundInProgress = new GraphStorageKey("RailTurnAroundInProgress", 0, 0);
        // Original 0x04000209; native static offset 0x5a0.
        public static readonly GraphStorageKey RailTurnAroundCooldown = new GraphStorageKey("RailTurnAroundCooldown", 0, 0);
        // Original 0x0400020a; native static offset 0x5b0.
        public static readonly GraphStorageKey RailAutoAttach = new GraphStorageKey("RailAutoAttach", 0, 0);
        // Original 0x0400020b; native static offset 0x5c0.
        public static readonly GraphStorageKey RailTargetTime = new GraphStorageKey("RailTargetTime", 0, 0);
        // Original 0x0400020c; native static offset 0x5d0.
        public static readonly GraphStorageKey RailTargetCancelled = new GraphStorageKey("RailTargetCancelled", 0, 0);
        // Original 0x0400020d; native static offset 0x5e0.
        public static readonly GraphStorageKey RailToCamera = new GraphStorageKey("RailToCamera", 0, 0);
        // Original 0x0400020e; native static offset 0x5f0.
        public static readonly GraphStorageKey TransporterActive = new GraphStorageKey("TransporterActive", 0, 0);
        // Original 0x0400020f; native static offset 0x600.
        // Supplied-release quirk: this key shares the RailIsValid name.
        public static readonly GraphStorageKey TransporterIsValid = new GraphStorageKey("RailIsValid", 0, 0);
        // Original 0x04000210; native static offset 0x610.
        public static readonly GraphStorageKey TransporterAutoAttach = new GraphStorageKey("TransporterAutoAttach", 0, 0);
        // Original 0x04000211; native static offset 0x620.
        public static readonly GraphStorageKey RollingActive = new GraphStorageKey("RollingActive", 0, 0);
        // Original 0x04000212; native static offset 0x630.
        public static readonly GraphStorageKey GravityDisabledModifierHandle = new GraphStorageKey("GravityDisabledModifierHandle", 0, 0);
        // Original 0x04000213; native static offset 0x640.
        public static readonly GraphStorageKey GravityMultiplierModifierHandle = new GraphStorageKey("GravityMultiplierModifierHandle", 0, 0);
        // Original 0x04000214; native static offset 0x650.
        public static readonly GraphStorageKey ClimbingEdgePassed = new GraphStorageKey("ClimbingEdgePassed", 0, 0);
        // Original 0x04000215; native static offset 0x660.
        public static readonly GraphStorageKey ClimbingNonMountableEdgeNormal = new GraphStorageKey("ClimbingNonMountableEdgeNormal", 0, 0);
        // Original 0x04000216; native static offset 0x670.
        public static readonly GraphStorageKey ClimbingLastKnownPosition = new GraphStorageKey("ClimbingLastKnownPosition", 0, 0);
        // Original 0x04000217; native static offset 0x680.
        public static readonly GraphStorageKey ClimbingLastKnownRotation = new GraphStorageKey("ClimbingLastKnownRotation", 0, 0);
        // Original 0x04000218; native static offset 0x690.
        public static readonly GraphStorageKey ClimbingLastCollider = new GraphStorageKey("ClimbingLastCollider", 0, 0);
        // Original 0x04000219; native static offset 0x6a0.
        public static readonly GraphStorageKey ClimbingEnterWhenDirectionOverrideEnds = new GraphStorageKey("ClimbingEnterWhenDirectionOverrideEnds", 0, 0);
        // Original 0x0400021a; native static offset 0x6b0.
        public static readonly GraphStorageKey ClimbingMaintainCurrentVelocityOnEnter = new GraphStorageKey("ClimbingMaintainCurrentVelocityOnEnter", 0, 0);
        // Original 0x0400021b; native static offset 0x6c0.
        public static readonly GraphStorageKey ClimbingWorldVelocityAtTerrainEnter = new GraphStorageKey("ClimbingWorldVelocityAtTerrainEnter", 0, 0);
        // Original 0x0400021c; native static offset 0x6d0.
        public static readonly GraphStorageKey ClimbingEnterCollisionNormal = new GraphStorageKey("ClimbingEnterCollisionNormal", 0, 0);
        // Original 0x0400021d; native static offset 0x6e0.
        public static readonly GraphStorageKey ClimbingInvalid = new GraphStorageKey("ClimbingInvalid", 0, 0);
        // Original 0x0400021e; native static offset 0x6f0.
        public static readonly GraphStorageKey ClimbingLastTimestamp = new GraphStorageKey("ClimbingLastTimestamp", 0, 0);
        // Original 0x0400021f; native static offset 0x700.
        public static readonly GraphStorageKey LedgeMountTimeElapsed = new GraphStorageKey("LedgeMountTimeElapsed", 0, 0);
        // Original 0x04000220; native static offset 0x710.
        public static readonly GraphStorageKey LedgeMountFinished = new GraphStorageKey("LedgeMountFinished", 0, 0);
        // Original 0x04000221; native static offset 0x720.
        public static readonly GraphStorageKey HalfPipeColliderActive = new GraphStorageKey("HalfPipeColliderActive", 0, 0);
        // Original 0x04000222; native static offset 0x730.
        public static readonly GraphStorageKey HalfPipeColliderUpdate = new GraphStorageKey("HalfPipeColliderUpdate", 0, 0);
        // Original 0x04000223; native static offset 0x740.
        public static readonly GraphStorageKey HalfPipeColliderPosition = new GraphStorageKey("HalfPipeColliderPosition", 0, 0);
        // Original 0x04000224; native static offset 0x750.
        public static readonly GraphStorageKey HalfPipeColliderNormal = new GraphStorageKey("HalfPipeColliderNormal", 0, 0);
        // Original 0x04000225; native static offset 0x760.
        public static readonly GraphStorageKey HalfPipeColliderSpeedForward = new GraphStorageKey("HalfPipeColliderSpeedForward", 0, 0);
        // Original 0x04000226; native static offset 0x770.
        public static readonly GraphStorageKey HalfPipeColliderEntrySide = new GraphStorageKey("HalfPipeColliderEntrySide", 0, 0);
        // Original 0x04000227; native static offset 0x780.
        public static readonly GraphStorageKey HalfPipeGravityOnEnter = new GraphStorageKey("HalfPipeGravityOnEnter", 0, 0);
        // Original 0x04000228; native static offset 0x790.
        public static readonly GraphStorageKey HalfPipeAirMotionIndex = new GraphStorageKey("HalfPipeAirMotionIndex", 0, 0);
        // Original 0x04000229; native static offset 0x7a0.
        public static readonly GraphStorageKey HalfPipeAirMotionNormal = new GraphStorageKey("HalfPipeAirMotionNormal", 0, 0);
        // Original 0x0400022a; native static offset 0x7b0.
        public static readonly GraphStorageKey HalfPipeAirLeaveTime = new GraphStorageKey("HalfPipeAirLeaveTime", 0, 0);
        // Original 0x0400022b; native static offset 0x7c0.
        public static readonly GraphStorageKey HalfPipeTransferActive = new GraphStorageKey("HalfPipeTransferActive", 0, 0);
        // Original 0x0400022c; native static offset 0x7d0.
        public static readonly GraphStorageKey HalfPipeTransferNormalStart = new GraphStorageKey("HalfPipeTransferNormalStart", 0, 0);
        // Original 0x0400022d; native static offset 0x7e0.
        public static readonly GraphStorageKey HalfPipeTransferRotationTime = new GraphStorageKey("HalfPipeTransferRotationTime", 0, 0);
        // Original 0x0400022e; native static offset 0x7f0.
        public static readonly GraphStorageKey GroundInputDirection = new GraphStorageKey("GroundInputDirection", 0, 0);
        // Original 0x0400022f; native static offset 0x800.
        public static readonly GraphStorageKey GroundCameraSettingsSet = new GraphStorageKey("GroundCameraSettingsSet", 0, 0);
        // Original 0x04000230; native static offset 0x810.
        public static readonly GraphStorageKey GroundLandingSpeed = new GraphStorageKey("GroundLandingSpeed", 0, 0);
        // Original 0x04000231; native static offset 0x820.
        public static readonly GraphStorageKey GroundLandingTime = new GraphStorageKey("GroundLandingTime", 0, 0);
        // Original 0x04000232; native static offset 0x830.
        public static readonly GraphStorageKey GroundLedgePosition = new GraphStorageKey("GroundLedgePosition", 0, 0);
        // Original 0x04000233; native static offset 0x840.
        public static readonly GraphStorageKey GroundLedgeTime = new GraphStorageKey("GroundLedgeTime", 0, 0);
        // Original 0x04000234; native static offset 0x850.
        public static readonly GraphStorageKey FreeLookHeadingOverrideHandle = new GraphStorageKey("FreeLookHeadingOverrideHandle", 0, 0);
        // Original 0x04000235; native static offset 0x860.
        public static readonly GraphStorageKey CameraProxySettingHandle = new GraphStorageKey("CameraProxySettingHandle", 0, 0);
        // Original 0x04000236; native static offset 0x870.
        public static readonly GraphStorageKey CameraHeadingOverrideDisabled = new GraphStorageKey("CameraHeadingOverrideDisabled", 0, 0);
        // Original 0x04000237; native static offset 0x880.
        public static readonly GraphStorageKey HoverHeadingDirection = new GraphStorageKey("HoverHeadingDirection", 0, 0);
        // Original 0x04000238; native static offset 0x890.
        public static readonly GraphStorageKey HoverDampingVelocity = new GraphStorageKey("HoverDampingVelocity", 0, 0);
        // Original 0x04000239; native static offset 0x8a0.
        public static readonly GraphStorageKey IsIdle = new GraphStorageKey("IsIdle", 0, 0);
        // Original 0x0400023a; native static offset 0x8b0.
        public static readonly GraphStorageKey InputOverrideModifierHandle = new GraphStorageKey("InputOverrideModifierHandle", 0, 0);
        // Original 0x0400023b; native static offset 0x8c0.
        public static readonly GraphStorageKey EnemyDeadTimer = new GraphStorageKey("EnemyDeadTimer", 0, 0);
        // Original 0x0400023c; native static offset 0x8d0.
        public static readonly GraphStorageKey EnemyFireProjectileTurnComplete = new GraphStorageKey("EnemyFireProjectileTurnComplete", 0, 0);
        // Original 0x0400023d; native static offset 0x8e0.
        public static readonly GraphStorageKey EnemyFireProjectileWindUpComplete = new GraphStorageKey("EnemyFireProjectileWindUpComplete", 0, 0);
        // Original 0x0400023e; native static offset 0x8f0.
        public static readonly GraphStorageKey LightspeedDashTarget = new GraphStorageKey("LightspeedDashTarget", 0, 0);
        // Original 0x0400023f; native static offset 0x900.
        public static readonly GraphStorageKey LightspeedDashInvulnerable = new GraphStorageKey("LightspeedDashInvulnerable", 0, 0);
        // Original 0x04000240; native static offset 0x910.
        public static readonly GraphStorageKey BurstAttackDurationTimer = new GraphStorageKey("BurstAttackDurationTimer", 0, 0);
        // Original 0x04000241; native static offset 0x920.
        public static readonly GraphStorageKey CatchReleaseActive = new GraphStorageKey("CatchReleaseActive", 0, 0);
        // Original 0x04000242; native static offset 0x930.
        public static readonly GraphStorageKey ChaosControlActive = new GraphStorageKey("ChaosControlActive", 0, 0);
        // Original 0x04000243; native static offset 0x940.
        public static readonly GraphStorageKey OneShotCanTriggerJump = new GraphStorageKey("OneShotCanTriggerJump", 0, 0);
        // Original 0x04000244; native static offset 0x950.
        public static readonly GraphStorageKey OneShotCanTriggerBoost = new GraphStorageKey("OneShotCanTriggerBoost", 0, 0);
        // Original 0x04000245; native static offset 0x960.
        public static readonly GraphStorageKey SpinDashChargeTime = new GraphStorageKey("SpinDashChargeTime", 0, 0);
    }
}
