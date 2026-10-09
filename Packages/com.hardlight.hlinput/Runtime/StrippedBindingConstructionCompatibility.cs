using System.Collections.Generic;

namespace Hardlight
{
    // ProjectLucid interoperability: shipping metadata contains no constructor
    // for either type. These private methods only make preserved source compile.
    // They are additional compatibility methods with zero original-method credit.
    // Actual Unity asset loading, Instantiate and JSON routes require engine tests.
    public partial class SwipeBinding
    {
        private SwipeBinding(List<InputModifier> modifiers) : base(modifiers)
        {
        }
    }

    public partial class VectorisedBinding
    {
        private VectorisedBinding(List<InputModifier> modifiers) : base(modifiers)
        {
        }
    }
}
