using System;
using System.Collections.Generic;
using Hardlight;
using Source = HLButtonHighlightCustomisations.ButtonHighlightChangeSource;

namespace ProjectLucid.Tests
{
    // Test-only concrete ModifiedOnUISelection attached with AddComponent. Each
    // callback appends "<Label> <Name> <args>" to the shared Sink, then runs the
    // optional AfterCall hook so a test can mutate state between callbacks.
    public sealed class PlayModeRecordingSelectionModification : ModifiedOnUISelection
    {
        public string Label = "?";
        public List<string> Sink;
        public Func<Source, string> Describe = source => source == null ? "null" : "source";
        public Action<string> AfterCall;

        private void Record(string entry)
        {
            Sink.Add(Label + " " + entry);
            AfterCall?.Invoke(entry);
        }

        public override void OnActivate(Source source) => Record("Activate " + Describe(source));
        public override void OnDeactivate(Source source) => Record("Deactivate " + Describe(source));
        public override void OnHighlightBegin(Source previousSource, Source currentSource) =>
            Record("Begin " + Describe(previousSource) + " " + Describe(currentSource));
        public override void OnHighlightUpdated(Source previousSource, Source currentSource, float lerpValue) =>
            Record("Updated " + Describe(previousSource) + " " + Describe(currentSource) + " " +
                lerpValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        public override void OnHighlightEnd(Source previousSource, Source currentSource) =>
            Record("End " + Describe(previousSource) + " " + Describe(currentSource));
    }
}
