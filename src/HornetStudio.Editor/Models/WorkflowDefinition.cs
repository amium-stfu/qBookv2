using System;
using System.Collections.Generic;

namespace HornetStudio.Editor.Models;

/// <summary>
/// Represents the runtime state of one workflow execution.
/// </summary>
public enum FunctionState
{
    Idle,
    Running,
    Stopping,
    Done,
    Failed,
    Canceled
}

/// <summary>
/// Identifies the supported workflow step types.
/// </summary>
public enum FunctionStepType
{
    SetValue,
    Delay,
    IfThenElse,
    While,
    Log
}

/// <summary>
/// Identifies the catalog kind of one callable function entry.
/// </summary>
public enum FunctionCatalogKind
{
    Declarative,
    Python
}

/// <summary>
/// Identifies where one function catalog entry originates.
/// </summary>
public enum FunctionCatalogSource
{
    FunctionsDirectory,
    LegacyWorkflowDirectory,
    PythonApplication
}

/// <summary>
/// Represents one function registry entry that can be listed by the Functions widget.
/// </summary>
public sealed class FunctionCatalogEntry
{
    /// <summary>
    /// Gets or sets the stable registry reference.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the function.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the function kind.
    /// </summary>
    public FunctionCatalogKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the logical source classification.
    /// </summary>
    public FunctionCatalogSource Source { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific source identifier.
    /// </summary>
    public string SourceIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-facing source label.
    /// </summary>
    public string DisplaySource { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the entry can be edited from Functions.
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets or sets whether the entry can be deleted from Functions.
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Gets or sets whether the entry is callable.
    /// </summary>
    public bool CanRun { get; set; }

    /// <summary>
    /// Gets or sets whether the registry entry is valid.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Gets or sets the optional status detail shown to the user.
    /// </summary>
    public string StatusText { get; set; } = string.Empty;
}

/// <summary>
/// Describes one persisted workflow definition.
/// </summary>
public sealed class FunctionDefinition
{
    public string Name { get; set; } = string.Empty;

    public List<FunctionStepDefinition> Steps { get; set; } = [];
}

/// <summary>
/// Base type for all workflow steps.
/// </summary>
public abstract class FunctionStepDefinition
{
    public abstract FunctionStepType Type { get; }
}

/// <summary>
/// Writes one configured value to a target path.
/// </summary>
public sealed class FunctionSetValueStepDefinition : FunctionStepDefinition
{
    public override FunctionStepType Type => FunctionStepType.SetValue;

    /// <summary>
    /// Gets or sets the target item path to write.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the persisted SetValue payload.
    /// This may contain a legacy literal scalar value or a structured SetValue operation encoded with the <c>sv1:</c> prefix.
    /// When <see cref="ValueFrom"/> is configured, that legacy source path still takes precedence for backward compatibility.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source item path whose current value is read at runtime and written to <see cref="Target"/>.
    /// When non-empty, this legacy compatibility field takes precedence over <see cref="Value"/>.
    /// Newly edited structured SetValue steps should persist through <see cref="Value"/> instead of this field.
    /// </summary>
    public string ValueFrom { get; set; } = string.Empty;
}

/// <summary>
/// Waits for the configured duration.
/// </summary>
public sealed class FunctionDelayStepDefinition : FunctionStepDefinition
{
    public override FunctionStepType Type => FunctionStepType.Delay;

    public int Milliseconds { get; set; }
}

/// <summary>
/// Evaluates a boolean condition and executes one branch.
/// </summary>
public sealed class FunctionIfThenElseStepDefinition : FunctionStepDefinition
{
    public override FunctionStepType Type => FunctionStepType.IfThenElse;

    public string Condition { get; set; } = string.Empty;

    public List<BooleanConditionVariableDefinition> Variables { get; set; } = [];

    public List<FunctionStepDefinition> Then { get; set; } = [];

    public List<FunctionStepDefinition> Else { get; set; } = [];
}

/// <summary>
/// Evaluates a boolean condition and executes the body while it remains true.
/// </summary>
public sealed class FunctionWhileStepDefinition : FunctionStepDefinition
{
    public override FunctionStepType Type => FunctionStepType.While;

    /// <summary>
    /// Gets or sets the boolean loop condition.
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reusable condition variables resolved before each iteration.
    /// </summary>
    public List<BooleanConditionVariableDefinition> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets the loop body steps.
    /// </summary>
    public List<FunctionStepDefinition> Steps { get; set; } = [];
}

/// <summary>
/// Describes one reusable boolean condition variable definition.
/// </summary>
public sealed class BooleanConditionVariableDefinition
{
    /// <summary>
    /// Gets or sets the variable name used in the formula.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source path that is resolved at runtime.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;
}

/// <summary>
/// Writes one log entry to a configured process log.
/// </summary>
public sealed class FunctionLogStepDefinition : FunctionStepDefinition
{
    public override FunctionStepType Type => FunctionStepType.Log;

    public string TargetLog { get; set; } = string.Empty;

    public MonitorLogLevel Level { get; set; } = MonitorLogLevel.Info;

    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Describes one validation error in workflow content.
/// </summary>
/// <param name="Path">The logical workflow path that failed validation.</param>
/// <param name="Message">The validation message.</param>
public sealed record FunctionValidationError(string Path, string Message);

/// <summary>
/// Collects workflow validation results.
/// </summary>
public sealed class FunctionValidationResult
{
    public FunctionValidationResult(IReadOnlyList<FunctionValidationError> errors)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public IReadOnlyList<FunctionValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}