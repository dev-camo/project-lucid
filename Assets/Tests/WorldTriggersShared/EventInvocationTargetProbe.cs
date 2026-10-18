using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectLucid.Tests.WorldTriggersShared
{
    // Test-only Behaviour whose public methods EventInvocationInfo reaches through reflection.
    // Each call records its name and argument; editor-only assemblies cannot supply
    // attachable components.
    public sealed class EventInvocationTargetProbe : MonoBehaviour
    {
        public enum ProbeState
        {
            Zero = 0,
            One = 1,
            Two = 2
        }

        public readonly List<string> Calls = new List<string>();
        public readonly List<object> Arguments = new List<object>();

        private void Record(string name, object argument)
        {
            Calls.Add(name);
            Arguments.Add(argument);
        }

        public void NoArguments() => Record(nameof(NoArguments), null);
        public void TakeString(string value) => Record(nameof(TakeString), value);
        public void TakeInt(int value) => Record(nameof(TakeInt), value);
        public void TakeFloat(float value) => Record(nameof(TakeFloat), value);
        public void TakeBool(bool value) => Record(nameof(TakeBool), value);
        public void TakeState(ProbeState value) => Record(nameof(TakeState), value);
        public void TakeDouble(double value) => Record(nameof(TakeDouble), value);
        public void TakeObject(object value) => Record(nameof(TakeObject), value);

        public void Fail()
        {
            Record(nameof(Fail), null);
            throw new InvalidOperationException("probe failure");
        }
    }
}
