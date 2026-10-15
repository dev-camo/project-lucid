using System;
using System.Collections.Generic;
using HardlightProject;

// Test-only logic definition. The genuine EntityActivationLogicDefinition
// factory creates its logic, which records each lifecycle call into the
// definition's trace. This source file matches its class name.
public sealed class ProjectLucidEntityActivationProbeDefinition : EntityActivationLogicDefinition
{
    [NonSerialized] public List<string> Trace = new List<string>();
    public string Label;

    public override EntityActivationLogicType Type => default;
    protected override Type ScriptType => typeof(ProbeLogic);

    private sealed class ProbeLogic : EntityActivationLogic
    {
        private ProjectLucidEntityActivationProbeDefinition m_definition;

        // The base finalizer calls Shutdown; keep collection out of the trace.
        public ProbeLogic() { GC.SuppressFinalize(this); }

        public override void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity) =>
            m_definition = (ProjectLucidEntityActivationProbeDefinition)definition;

        public override void OnStart() { base.OnStart(); m_definition.Trace.Add("start " + m_definition.Label); }
        public override void OnEnd() { base.OnEnd(); m_definition.Trace.Add("end " + m_definition.Label); }
        public override void Shutdown() => m_definition.Trace.Add("shutdown " + m_definition.Label);
    }
}
