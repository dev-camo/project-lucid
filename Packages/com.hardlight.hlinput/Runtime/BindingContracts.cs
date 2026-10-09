using System.Collections.Generic;

namespace Hardlight
{
    // Original HLInput.Runtime contracts: actual abstract declarations only, no replacement implementation.
    public interface IBaseInputBindingProvider
    {
        int JoystickIndex { get; } // 06000123
        InputType InputTypeOverride { get; } // 06000124
        InputType BindingInputType { get; } // 06000125
    }
    public interface IInputAxisProvider<TAxisBiding> : IBaseInputBindingProvider where TAxisBiding : BaseBinding
    {
        IReadOnlyList<IAxisBindingProvider<TAxisBiding>> AxisProviders { get; }
    }
    public interface IInputButtonProvider<TButtonBinding> : IBaseInputBindingProvider where TButtonBinding : BaseBinding
    {
        IReadOnlyList<IButtonBindingProvider<TButtonBinding>> ButtonProviders { get; }
    }
    public interface IBaseBindingDataProvider { GameInput GameInput { get; } } // 0600003b
    public interface IAxisBindingProvider<TAxisBinding> : IBaseBindingDataProvider where TAxisBinding : BaseBinding
    {
        IReadOnlyList<TAxisBinding> AxisBindings { get; } // 0600003a
    }
    public interface IButtonBindingProvider<TButtonBinding> : IBaseBindingDataProvider where TButtonBinding : BaseBinding
    {
        IReadOnlyList<TButtonBinding> ButtonBindings { get; } // 0600003c
    }
    public interface ICurrentBaseInputBindingsProvider { } // 02000008: genuine zero-local-API stripped marker.
    public interface IBaseControllerProvider
    {
        IReadOnlyList<string> GetControllerNames(); // 0600005f
        FastAction OnControllerConnectionUpdate { get; set; } // 06000060/61
    }
}
