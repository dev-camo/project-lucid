using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight.UI.Binding;
using UnityEngine;

// Owned binding source for the logic and math binder node cases. Each member is a
// genuine Bindable<T> field so the original BindReference lookup accepts it; values
// made with Recording log every Value read, by name, in a shared list.
public sealed class OriginalLogicMathNodeCarrier : ScriptableObject
{
    public enum Shade { Red, Green, Blue }

    [NonSerialized] public readonly List<string> Reads = new List<string>();
    [NonSerialized] public Bindable<bool> Bool1;
    [NonSerialized] public Bindable<bool> Bool2;
    [NonSerialized] public Bindable<bool> Bool3;
    [NonSerialized] public Bindable<bool> Bool4;
    [NonSerialized] public Bindable<bool> Bool5;
    [NonSerialized] public Bindable<bool> Bool6;
    [NonSerialized] public Bindable<bool> Bool7;
    [NonSerialized] public Bindable<object> Object1;
    [NonSerialized] public Bindable<object> Object2;
    [NonSerialized] public Bindable<object> Object3;
    [NonSerialized] public Bindable<object> Object4;
    [NonSerialized] public Bindable<object> Object5;
    [NonSerialized] public Bindable<int> Int1;
    [NonSerialized] public Bindable<int> Int2;
    [NonSerialized] public Bindable<long> Long1;
    [NonSerialized] public Bindable<long> Long2;
    [NonSerialized] public Bindable<float> Float1;
    [NonSerialized] public Bindable<float> Float2;
    [NonSerialized] public Bindable<string> Text;
    [NonSerialized] public Bindable<IList> List;
    [NonSerialized] public Bindable<DateTime> Start;
    [NonSerialized] public Bindable<DateTime> End;
    [NonSerialized] public Bindable<DateTime> Current;
    [NonSerialized] public Bindable<Shade> Colour;

    public Bindable<T> Recording<T>(string name, T value) => new RecordingBindable<T>(Reads, name, value);

    private sealed class RecordingBindable<T> : Bindable<T>
    {
        private readonly List<string> reads;
        private readonly string name;

        public RecordingBindable(List<string> reads, string name, T value) : base(value)
        {
            this.reads = reads;
            this.name = name;
        }

        public override T Value
        {
            get
            {
                reads.Add(name);
                return base.Value;
            }
        }
    }
}
