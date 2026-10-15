using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;

// Test-only closed definition group. Its two abstract callbacks record when the
// genuine DefinitionDataType.GetData body calls them; the base body is unchanged.
// This source file matches its class name so its MonoScript identity is explicit.
public sealed class ProjectLucidRecordingDefinitionGroup : DefinitionDataType<string, FiniteStateMachineScriptableObject>
{
    public readonly List<string> Calls = new List<string>();
    public Func<FiniteStateMachineScriptableObject, string> Key;
    public IEqualityComparer<string> Comparer;

    protected override string GetElementKey(FiniteStateMachineScriptableObject data)
    {
        Calls.Add("key");
        return Key(data);
    }

    protected override IEqualityComparer<string> GetKeyComparer()
    {
        Calls.Add("comparer");
        return Comparer;
    }
}
