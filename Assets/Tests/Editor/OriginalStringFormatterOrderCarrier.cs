using System;
using System.Collections.Generic;
using Hardlight.UI.Binding;
using UnityEngine;

// Owned binding source for the StringFormatter evaluation-order case. Each member is
// declared as a genuine Bindable<string> so the original BindReference lookup accepts
// it; the assigned instances record every Value read in a shared list.
public sealed class OriginalStringFormatterOrderCarrier : ScriptableObject
{
    [NonSerialized] public readonly List<string> Reads = new List<string>();
    [NonSerialized] public Bindable<string> Format;
    [NonSerialized] public Bindable<string> First;
    [NonSerialized] public Bindable<string> Second;
    [NonSerialized] public Bindable<string> Third;
    [NonSerialized] public Bindable<string> Fourth;

    public Bindable<string> Recording(string name, string value) => new RecordingBindable(Reads, name, value);

    private sealed class RecordingBindable : Bindable<string>
    {
        private readonly List<string> reads;
        private readonly string name;

        public RecordingBindable(List<string> reads, string name, string value) : base(value)
        {
            this.reads = reads;
            this.name = name;
        }

        public override string Value
        {
            get
            {
                reads.Add(name);
                return base.Value;
            }
        }
    }
}
