using System;
using Hardlight;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000864; bodies 0x612490..0x612860 (1.10.1 x86-64).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerCharacterAbilityChaos : UIContainerCharacterAbility
    {
        [SerializeField]
        private UITargetIndicatorController m_transforms;
        private CharacterAbility_AirChaosAim m_ability;
        public readonly Bindable<bool> ShouldShow = new Bindable<bool>();

        // 06003095: the aim ability field is written even when the type test fails (null); the
        // indicators are only set up, a frame later, for a character that has the ability.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            if (TryGetAbility(out m_ability))
                CoroutineUtils.OnNextFrame(InitialiseTransforms);
        }

        // 06003096: start speed is the chaos dash speed, end speed the dash exit curve at zero and the
        // end gravity multiplier is the air movement's gravity (1 without it) relative to the aim's.
        private void InitialiseTransforms()
        {
            if (!TryGetCharacter(out Character character))
                return;
            bool hasDash = character.TryGetAbility(ActorAbilityType.Character_AirChaosDash, out CharacterAbility_AirChaosDash dash);
            bool hasAir = character.TryGetAbility(ActorAbilityType.Character_MovementAir, out CharacterAbility_MovementAir air);
            float startSpeed = 0f;
            float endSpeed = 0f;
            if (hasDash)
            {
                startSpeed = dash.DefinitionAirChaos.DashSpeed;
                endSpeed = dash.DefinitionAirChaos.ExitSpeedBySlopeCurve.Evaluate(0f);
            }
            float airGravity = hasAir ? air.CharacterAbilityDefinition.GravityMultiplier : 1f;
            float endGravityMultiplier = airGravity / m_ability.CharacterAbilityDefinition.GravityMultiplier;
            m_transforms.Initialise(character, startSpeed, endSpeed, 0f, endGravityMultiplier, OrientateToCamera);
        }

        // 06003097
        private void LateUpdate()
        {
            if (!TryGetCharacter(out _))
                return;
            float deltaTime = Time.deltaTime;
            ShouldShow.Value = m_ability.UpdateUI(deltaTime, m_transforms, OrientateToCamera);
        }

        // 06003098
        public UIContainerCharacterAbilityChaos() { }
    }
}
