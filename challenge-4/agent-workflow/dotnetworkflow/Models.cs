namespace FactoryWorkflow;

/// <summary>
/// Request payload for the /api/analyze_machine endpoint.
/// </summary>
/// <param name="machine_id">The machine identifier to analyze</param>
/// <param name="telemetry">Raw telemetry data from the machine sensors</param>
public record AnalyzeRequest(string machine_id, System.Text.Json.JsonElement telemetry);

/// <summary>
/// Complete workflow response containing results from all agents in the pipeline.
/// </summary>
public class WorkflowResponse
{
    /// <summary>
    /// Results from each agent in execution order.
    /// </summary>
    public List<AgentStepResult> AgentSteps { get; set; } = new();

    /// <summary>
    /// Final output message from the last agent in the workflow.
    /// </summary>
    public string? FinalMessage { get; set; }
}

/// <summary>
/// Execution details for a single agent in the workflow pipeline.
/// </summary>
public class AgentStepResult
{
    /// <summary>
    /// Name of the agent that executed this step.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Tool/function calls made by this agent during execution.
    /// </summary>
    public List<ToolCallInfo> ToolCalls { get; set; } = new();

    /// <summary>
    /// Accumulated text output from the agent.
    /// </summary>
    public string TextOutput { get; set; } = string.Empty;

    /// <summary>
    /// Final message from this agent (passed to the next agent in the workflow).
    /// </summary>
    public string? FinalMessage { get; set; }
}

/// <summary>
/// Information about a tool/function call made by an agent.
/// </summary>
public class ToolCallInfo
{
    /// <summary>
    /// Name of the tool that was called.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public string? CallId { get; set; }

    /// <summary>
    /// Arguments passed to the tool (serialized).
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// Result returned by the tool (truncated for response size).
    /// </summary>
    public string? Result { get; set; }
}
