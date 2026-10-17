using System;
using System.Collections.Generic;
using Hardlight;

namespace ProjectLucid.Tests
{
    // Records the GuiPageContent virtual slots 6 (OnShow) and 8 (OnHide) that
    // Hardlight.DynamicPageLoad dispatches to its entries. The overrides do not call
    // the base bodies, so an entry needs no UI runtime configuration. OnShow also
    // records the entry's activeSelf and, when ReadActiveContent is set, the loader's
    // active content at call time.
    // Editor-only assemblies cannot supply attachable components, so it lives in this runtime test assembly.
    public sealed class OriginalPageContentProbe : GuiPageContent
    {
        [NonSerialized] public readonly List<GuiPageParameters> Shown = new List<GuiPageParameters>();
        [NonSerialized] public readonly List<(GuiPageParameters parameters, UISystemClosureReason reason)> Hidden =
            new List<(GuiPageParameters parameters, UISystemClosureReason reason)>();
        [NonSerialized] public readonly List<(bool activeSelf, GuiPageContent activeContent)> ShownState =
            new List<(bool activeSelf, GuiPageContent activeContent)>();
        [NonSerialized] public Func<GuiPageContent> ReadActiveContent;

        public override void OnShow(GuiPageParameters parameters)
        {
            Shown.Add(parameters);
            ShownState.Add((gameObject.activeSelf, ReadActiveContent?.Invoke()));
        }

        public override void OnHide(GuiPageParameters parameters, UISystemClosureReason reasonForClosure)
        {
            Hidden.Add((parameters, reasonForClosure));
        }
    }
}
