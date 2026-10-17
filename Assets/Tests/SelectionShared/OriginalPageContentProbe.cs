using System;
using System.Collections.Generic;
using Hardlight;

namespace ProjectLucid.Tests
{
    // Records the GuiPageContent virtual slots 6 (OnShow) and 8 (OnHide) that
    // Hardlight.DynamicPageLoad dispatches to its entries. The overrides do not call
    // the base bodies, so an entry needs no UI runtime configuration.
    // Editor-only assemblies cannot supply attachable components, so it lives in this runtime test assembly.
    public sealed class OriginalPageContentProbe : GuiPageContent
    {
        [NonSerialized] public readonly List<GuiPageParameters> Shown = new List<GuiPageParameters>();
        [NonSerialized] public readonly List<(GuiPageParameters parameters, UISystemClosureReason reason)> Hidden =
            new List<(GuiPageParameters parameters, UISystemClosureReason reason)>();

        public override void OnShow(GuiPageParameters parameters)
        {
            Shown.Add(parameters);
        }

        public override void OnHide(GuiPageParameters parameters, UISystemClosureReason reasonForClosure)
        {
            Hidden.Add((parameters, reasonForClosure));
        }
    }
}
