using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterState_Ability<TAbility> : ActorState_Ability<TAbility> where TAbility : CharacterAbility
    {
        protected CharacterState_Ability(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Character character = user.GetAs<Character>();
            TAbility ability = GetAbility(character);
            ability.SetDefinitionOverride();
            ability.SetInvulnerability();
            ability.ResetStaminaRecharge();
            ability.SetCameraOverrides();
            SetCameraProxyTargetSettings(character, ability);
            character.UpdateAbilityCameraOverride(ability.CameraTypeOverride, ability.CameraSettingsOverride, ability.CameraHeadingOverride);
            SetGravityMultiplier(character, ability.GravityMultiplier);
            SetFreeLookHeadingOverride(character, ability);
            SetBlobShadowEnabled(character, ability.CharacterAbilityDefinition, true);
            SetSwitchAvailable(character, ability.SwitchAvailable);
            character.SetModifierFormLocked(ability.DefinitionBase.FormIsLocked);
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Character character = user.GetAs<Character>();
            TAbility ability = GetAbility(character);
            ability.ApplyStaminaRecharge(updateContext.DeltaTime);
            ability.ApplyInputModifier();
            UpdateAbilityHeading(character, ability);
            AdjustGravityMultiplier(character, ability.GravityMultiplier);
            UpdateFreeLookHeadingOverride(character, ability);
            character.UpdateAbilityCameraOverride(ability.CameraTypeOverride, ability.CameraSettingsOverride, ability.CameraHeadingOverride);
            if (ability.CharacterAbilityDefinition.ContinuesBoost) character.BoostStamina.ContinueFromAbility();
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Character character = user.GetAs<Character>();
            TAbility ability = GetAbility(character);
            if (ability.DefinitionBase.FormIsLocked) character.SetModifierFormLocked(false);
            ClearGravityMultiplier(character);
            ClearFreeLookHeadingOverride(character);
            // Even when inactive the original evaluates the virtual definition first.
            SetBlobShadowEnabled(character, ability.CharacterAbilityDefinition, false);
            ClearSwitchAvailable(character);
        }

        protected virtual void UpdateRemainTimer(Character character, float deltaTime)
        {
            if (!character.Storage.TryGetValue(ActorFSMKeys.StateRemainTimer, out float remain)) return;
            remain -= deltaTime;
            if (remain > 0f) character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, remain);
            else character.Storage.RemoveValue<float>(ActorFSMKeys.StateRemainTimer);
        }

        private void UpdateAbilityHeading(Character character, TAbility ability)
        {
            if (ability.ValidateContactsActive && !ability.OrientateColliderHit)
                character.Storage.SetValue(ActorFSMKeys.AbilityHeading, character.WorldVelocity);
        }

        protected bool MaintainHeading(TAbility ability) =>
            !ability.OrientateHeadingToPlane && (!ability.ValidateContactsActive || ability.OrientateColliderHit);

        protected Vector3 CachedHeading(Character character)
        {
            Vector3 heading = character.Storage.GetValue(ActorFSMKeys.AbilityHeading, Vector3.zero, true);
            Vector3 local = character.WorldToLocalRotation * heading;
            local.y = character.LocalVelocity.y;
            return local;
        }

        protected virtual void SetCameraProxyTargetSettings(Character character, TAbility ability)
        {
            if (TryGetCameraProxySettingsFromAbility(ability, out CameraProxyTargetSettings settings))
                CameraUtilities.SetCharacterCameraProxyTargetSettings(character, settings);
        }

        private bool TryGetCameraProxySettingsFromAbility(TAbility ability, out CameraProxyTargetSettings cameraProxyTargetSettings)
        {
            CharacterAbilityDefinition definition = ability.DefinitionBase as CharacterAbilityDefinition;
            if (object.ReferenceEquals(definition, null))
            {
                cameraProxyTargetSettings = default(CameraProxyTargetSettings);
                return false;
            }
            cameraProxyTargetSettings = definition.CameraProxyTargetSettings;
            return true;
        }

        private CameraRecenterHeadingDefinition_FreeLook GetFreeLookHeadingOverride(Character character, CharacterAbility ability)
        {
            CameraRecenterHeadingOverrides overrides = ability.GetFreeLookHeadingOverrides();
            if (overrides == null) return null;
            overrides.TryGetValue(character.SaveDataSettings.CameraRecenterHeadingType, out CameraRecenterHeadingDefinition_FreeLook result);
            return result;
        }

        private void SetFreeLookHeadingOverride(Character character, TAbility ability)
        {
            CameraRecenterHeadingDefinition_FreeLook definition = GetFreeLookHeadingOverride(character, ability);
            StackableDataHandle handle = character.AddModifierOverride(0x64d5d8bb, definition);
            character.Storage.SetValue(ActorFSMKeys.FreeLookHeadingOverrideHandle, handle);
        }

        private void UpdateFreeLookHeadingOverride(Character character, TAbility ability)
        {
            if (!character.Storage.TryGetValue(ActorFSMKeys.FreeLookHeadingOverrideHandle, out StackableDataHandle handle)) return;
            CameraRecenterHeadingDefinition_FreeLook definition = GetFreeLookHeadingOverride(character, ability);
            character.AdjustModifierOverrides(handle, 0x64d5d8bb, definition);
        }

        private void ClearFreeLookHeadingOverride(Character character)
        {
            if (!character.Storage.TryGetValue(ActorFSMKeys.FreeLookHeadingOverrideHandle, out StackableDataHandle handle)) return;
            character.RemoveModifierOverrides(handle);
            character.Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.FreeLookHeadingOverrideHandle);
        }

        private void SetBlobShadowEnabled(Character character, CharacterAbilityDefinition abilityDefinition, bool stateActive)
        {
            if (character.BlobShadowObject == null) return;
            character.BlobShadowObject.SetActive(!stateActive || !abilityDefinition.DisableBlobShadow);
        }

        private void SetSwitchAvailable(Character character, bool switchAvailable)
        {
            if (!switchAvailable) return;
            StackableDataHandle handle = character.AddModifierOverride(0x35e87f40, true);
            character.Storage.SetValue(ActorFSMKeys.SwitchAvailableOverrideHandle, handle);
        }

        private void ClearSwitchAvailable(Character character)
        {
            if (!character.Storage.TryGetValue(ActorFSMKeys.SwitchAvailableOverrideHandle, out StackableDataHandle handle)) return;
            character.RemoveModifierOverrides(handle);
            character.Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.SwitchAvailableOverrideHandle);
        }
    }
}
