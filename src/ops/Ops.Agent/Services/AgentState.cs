using Ops.Shared.Config;

namespace Ops.Agent.Services;

public sealed class AgentState
{
    public OpsConfig Config { get; private set; }

    public AgentState(OpsConfig config)
    {
        Config = config;
    }

    public void Update(OpsConfig config) => Config = config;
}
