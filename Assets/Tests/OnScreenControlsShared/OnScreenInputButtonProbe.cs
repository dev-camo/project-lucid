using Hardlight;
using HardlightProject;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only concrete OnScreenInputButton that exposes the protected raise and supply entry points.
    public sealed class OnScreenInputButtonProbe : OnScreenInputButton
    {
        public void RaiseDown(GameInput gameInput, float value, InputType inputType) => OnButtonDown(gameInput, value, inputType);

        public void RaiseHeld(GameInput gameInput, float value, InputType inputType) => OnButtonHeld(gameInput, value, inputType);

        public void RaiseUp(GameInput gameInput, float value, InputType inputType) => OnButtonUp(gameInput, value, inputType);

        public void Supply(GameInput gameInput, InputType inputType) => SupplyGameInputSequence(gameInput, inputType);
    }
}
