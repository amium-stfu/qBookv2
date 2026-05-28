using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HornetStudio.Editor.Helpers;

namespace HornetStudio.Editor.Models;

/// <summary>
/// Describes the detected target value shape for one SetValue interaction.
/// </summary>
public enum SetValueTargetKind
{
    Unknown,
    Numeric,
    String,
    Boolean
}

/// <summary>
/// Describes the supported structured SetValue operations.
/// </summary>
public enum SetValueOperationKind
{
    SetLiteral,
    SetFromItem,
    IncrementBy,
    DecrementBy,
    IncrementOne,
    DecrementOne,
    AppendText,
    SetTrue,
    SetFalse
}

/// <summary>
/// Represents one SetValue interaction operation.
/// </summary>
public sealed class SetValueOperation
{
    /// <summary>
    /// Gets or sets the operation kind.
    /// </summary>
    public SetValueOperationKind Kind { get; init; } = SetValueOperationKind.SetLiteral;

    /// <summary>
    /// Gets or sets the literal value used by literal-based operations.
    /// </summary>
    public string LiteralValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the source item path used by item-copy operations.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional separator inserted between existing and appended text.
    /// </summary>
    public string Separator { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the operation originated from a legacy free-text argument.
    /// </summary>
    public bool IsLegacyLiteral { get; init; }
}

/// <summary>
/// Represents the parsing result for a SetValue argument.
/// </summary>
public sealed class SetValueOperationParseResult
{
    /// <summary>
    /// Gets or sets a value indicating whether parsing succeeded.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the argument used the structured SetValue format.
    /// </summary>
    public bool IsStructured { get; init; }

    /// <summary>
    /// Gets or sets the parsed operation.
    /// </summary>
    public SetValueOperation Operation { get; init; } = new();

    /// <summary>
    /// Gets or sets the parse error message when parsing failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;
}

/// <summary>
/// Represents the validation result for a SetValue operation.
/// </summary>
public sealed class SetValueOperationValidationResult
{
    /// <summary>
    /// Gets a successful validation result.
    /// </summary>
    public static SetValueOperationValidationResult Success { get; } = new() { IsValid = true };

    /// <summary>
    /// Gets or sets a value indicating whether the operation is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets or sets the validation message when validation failed.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;
}

/// <summary>
/// Describes one inline SetValue operation option.
/// </summary>
public sealed class SetValueInlineOperationOption
{
    /// <summary>
    /// Gets or sets the persisted operation kind.
    /// </summary>
    public SetValueOperationKind Kind { get; init; }

    /// <summary>
    /// Gets or sets the short display text shown in the inline editor.
    /// </summary>
    public string DisplayText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the option uses a source item picker instead of a literal editor.
    /// </summary>
    public bool UsesSourceItem { get; init; }
}

/// <summary>
/// Encodes, parses, summarizes, and validates structured SetValue interaction arguments.
/// </summary>
public static class SetValueOperationCodec
{
    private const string StructuredPrefixValue = "sv1:";

    /// <summary>
    /// Gets the prefix used by structured SetValue arguments.
    /// </summary>
    public static string StructuredPrefix => StructuredPrefixValue;

    /// <summary>
    /// Determines whether the supplied argument uses the structured SetValue prefix.
    /// </summary>
    /// <param name="rawArgument">The persisted argument text.</param>
    /// <returns><see langword="true"/> when the argument starts with the structured prefix; otherwise <see langword="false"/>.</returns>
    public static bool IsStructuredArgument(string? rawArgument)
        => !string.IsNullOrWhiteSpace(rawArgument)
           && rawArgument.Trim().StartsWith(StructuredPrefixValue, StringComparison.Ordinal);

    /// <summary>
    /// Parses one persisted SetValue argument into either a structured operation or a legacy literal fallback.
    /// </summary>
    /// <param name="rawArgument">The persisted argument text.</param>
    /// <returns>The parse result.</returns>
    public static SetValueOperationParseResult Parse(string? rawArgument)
    {
        var normalizedArgument = rawArgument?.Trim() ?? string.Empty;
        if (!IsStructuredArgument(normalizedArgument))
        {
            return new SetValueOperationParseResult
            {
                IsValid = true,
                IsStructured = false,
                Operation = new SetValueOperation
                {
                    Kind = SetValueOperationKind.SetLiteral,
                    LiteralValue = normalizedArgument,
                    IsLegacyLiteral = true
                }
            };
        }

        var payload = normalizedArgument[StructuredPrefixValue.Length..];
        if (string.IsNullOrWhiteSpace(payload))
        {
            return CreateInvalidStructuredResult("Structured SetValue operation payload is missing.");
        }

        try
        {
            if (JsonNode.Parse(payload) is not JsonObject root)
            {
                return CreateInvalidStructuredResult("Structured SetValue operation payload must be a JSON object.");
            }

            var operationText = root["op"]?.GetValue<string>();
            if (!Enum.TryParse<SetValueOperationKind>(operationText, ignoreCase: true, out var kind))
            {
                return CreateInvalidStructuredResult("Structured SetValue operation kind is missing or invalid.");
            }

            var literalValue = root["value"]?.GetValue<string>() ?? string.Empty;
            var sourcePath = TargetPathHelper.NormalizeConfiguredTargetPath(root["source"]?.GetValue<string>());
            var separator = root["separator"]?.GetValue<string>() ?? string.Empty;

            return new SetValueOperationParseResult
            {
                IsValid = true,
                IsStructured = true,
                Operation = new SetValueOperation
                {
                    Kind = kind,
                    LiteralValue = literalValue,
                    SourcePath = sourcePath,
                    Separator = separator,
                    IsLegacyLiteral = false
                }
            };
        }
        catch (JsonException)
        {
            return CreateInvalidStructuredResult("Structured SetValue operation payload is not valid JSON.");
        }
    }

    /// <summary>
    /// Serializes one structured SetValue operation into the persisted argument field.
    /// </summary>
    /// <param name="operation">The operation to serialize.</param>
    /// <returns>The persisted argument string.</returns>
    public static string Serialize(SetValueOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.IsLegacyLiteral)
        {
            return operation.LiteralValue ?? string.Empty;
        }

        var payload = new JsonObject
        {
            ["op"] = operation.Kind.ToString()
        };

        if (UsesLiteralValue(operation.Kind))
        {
            payload["value"] = operation.LiteralValue ?? string.Empty;
        }

        if (UsesSourcePath(operation.Kind))
        {
            payload["source"] = TargetPathHelper.NormalizeConfiguredTargetPath(operation.SourcePath);
        }

        if (UsesSeparator(operation.Kind) && !string.IsNullOrEmpty(operation.Separator))
        {
            payload["separator"] = operation.Separator;
        }

        return StructuredPrefixValue + payload.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    /// <summary>
    /// Creates a user-facing summary for one persisted SetValue argument.
    /// </summary>
    /// <param name="rawArgument">The persisted argument text.</param>
    /// <param name="targetKind">The detected target kind.</param>
    /// <returns>A readable operation summary.</returns>
    public static string GetSummary(string? rawArgument, SetValueTargetKind targetKind)
    {
        var parsed = Parse(rawArgument);
        return !parsed.IsValid
            ? "Invalid structured SetValue operation"
            : GetSummary(parsed.Operation, targetKind: targetKind);
    }

    /// <summary>
    /// Creates a user-facing summary for one SetValue operation.
    /// </summary>
    /// <param name="operation">The operation to summarize.</param>
    /// <param name="targetKind">The detected target kind.</param>
    /// <returns>A readable operation summary.</returns>
    public static string GetSummary(SetValueOperation operation, SetValueTargetKind targetKind)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.IsLegacyLiteral)
        {
            return string.IsNullOrWhiteSpace(operation.LiteralValue)
                ? "Legacy literal (empty)"
                : $"Legacy literal {FormatQuotedValue(operation.LiteralValue, targetKind: targetKind)}";
        }

        return operation.Kind switch
        {
            SetValueOperationKind.SetLiteral => $"Set to {FormatQuotedValue(operation.LiteralValue, targetKind: targetKind)}",
            SetValueOperationKind.SetFromItem => $"Set from {FormatSourcePath(operation.SourcePath)}",
            SetValueOperationKind.IncrementBy => $"Increase by {FormatNumericValue(operation.LiteralValue)}",
            SetValueOperationKind.DecrementBy => $"Decrease by {FormatNumericValue(operation.LiteralValue)}",
            SetValueOperationKind.IncrementOne => "Increase by 1",
            SetValueOperationKind.DecrementOne => "Decrease by 1",
            SetValueOperationKind.AppendText => FormatAppendSummary(operation),
            SetValueOperationKind.SetTrue => "Set true",
            SetValueOperationKind.SetFalse => "Set false",
            _ => operation.Kind.ToString()
        };
    }

    /// <summary>
    /// Validates one SetValue operation against the detected target kind.
    /// </summary>
    /// <param name="operation">The operation to validate.</param>
    /// <param name="targetKind">The detected target kind.</param>
    /// <param name="isCompatibleSourcePath">Optional callback that validates compatible source items.</param>
    /// <returns>The validation result.</returns>
    public static SetValueOperationValidationResult Validate(SetValueOperation operation, SetValueTargetKind targetKind, Func<string, bool>? isCompatibleSourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.IsLegacyLiteral)
        {
            return SetValueOperationValidationResult.Success;
        }

        if (UsesSourcePath(operation.Kind))
        {
            var normalizedSourcePath = TargetPathHelper.NormalizeConfiguredTargetPath(operation.SourcePath);
            if (string.IsNullOrWhiteSpace(normalizedSourcePath))
            {
                return CreateValidationError("A source item is required for this SetValue operation.");
            }

            if (isCompatibleSourcePath is not null && !isCompatibleSourcePath(normalizedSourcePath))
            {
                return CreateValidationError("The selected source item is not compatible with the target type.");
            }
        }

        return targetKind switch
        {
            SetValueTargetKind.Numeric => ValidateNumericOperation(operation),
            SetValueTargetKind.String => ValidateStringOperation(operation),
            SetValueTargetKind.Boolean => ValidateBooleanOperation(operation),
            _ => ValidateUnknownOperation(operation)
        };
    }

    /// <summary>
    /// Tries to parse one numeric literal using invariant culture.
    /// </summary>
    /// <param name="value">The literal text.</param>
    /// <param name="parsedValue">The parsed numeric value.</param>
    /// <returns><see langword="true"/> when parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParseNumericLiteral(string? value, out double parsedValue)
        => double.TryParse(value, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsedValue);

    /// <summary>
    /// Tries to parse one boolean-like literal using invariant rules.
    /// </summary>
    /// <param name="value">The literal text.</param>
    /// <param name="parsedValue">The parsed boolean value.</param>
    /// <returns><see langword="true"/> when parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParseBooleanLikeLiteral(string? value, out bool parsedValue)
    {
        if (bool.TryParse(value, out parsedValue))
        {
            return true;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
        {
            parsedValue = numericValue != 0;
            return true;
        }

        parsedValue = false;
        return false;
    }

    /// <summary>
    /// Classifies a SetValue target based on explicit target metadata, runtime type, and optional sample value.
    /// </summary>
    /// <param name="declaredType">The explicit target type metadata, if available.</param>
    /// <param name="targetType">The runtime target type.</param>
    /// <param name="sampleValue">An optional sample value.</param>
    /// <returns>The detected target kind.</returns>
    public static SetValueTargetKind ClassifyTargetKind(string? declaredType, Type? targetType, object? sampleValue)
        => TargetValueTypes.ToSetValueTargetKind(TargetValueTypes.Resolve(declaredType, targetType, sampleValue));

    /// <summary>
    /// Returns the supported inline editor operations for the detected target kind.
    /// </summary>
    /// <param name="targetKind">The detected SetValue target kind.</param>
    /// <returns>The inline operation options in display order.</returns>
    public static IReadOnlyList<SetValueInlineOperationOption> GetInlineOperationOptions(SetValueTargetKind targetKind)
        => targetKind switch
        {
            SetValueTargetKind.Numeric =>
            [
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.SetLiteral, DisplayText = "=", UsesSourceItem = false },
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.IncrementBy, DisplayText = "+", UsesSourceItem = false },
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.DecrementBy, DisplayText = "-", UsesSourceItem = false },
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.SetFromItem, DisplayText = "Item", UsesSourceItem = true }
            ],
            SetValueTargetKind.String =>
            [
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.SetLiteral, DisplayText = "=", UsesSourceItem = false },
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.AppendText, DisplayText = "+", UsesSourceItem = false },
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.SetFromItem, DisplayText = "Item", UsesSourceItem = true }
            ],
            SetValueTargetKind.Boolean =>
            [
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.SetTrue, DisplayText = "true", UsesSourceItem = false },
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.SetFalse, DisplayText = "false", UsesSourceItem = false },
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.SetFromItem, DisplayText = "Item", UsesSourceItem = true }
            ],
            _ =>
            [
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.SetLiteral, DisplayText = "=", UsesSourceItem = false },
                new SetValueInlineOperationOption { Kind = SetValueOperationKind.SetFromItem, DisplayText = "Item", UsesSourceItem = true }
            ]
        };

    /// <summary>
    /// Maps legacy or currently unsupported stored operations to the nearest inline editor representation.
    /// </summary>
    /// <param name="operation">The parsed SetValue operation.</param>
    /// <param name="targetKind">The detected SetValue target kind.</param>
    /// <returns>An operation shape that the inline editor can display and edit.</returns>
    public static SetValueOperation ToInlineEditorOperation(SetValueOperation operation, SetValueTargetKind targetKind = SetValueTargetKind.Unknown)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (targetKind == SetValueTargetKind.Boolean)
        {
            if (operation.Kind == SetValueOperationKind.SetLiteral
                && TryParseBooleanLikeLiteral(operation.LiteralValue, out var parsedBooleanValue))
            {
                return new SetValueOperation
                {
                    Kind = parsedBooleanValue ? SetValueOperationKind.SetTrue : SetValueOperationKind.SetFalse,
                    LiteralValue = operation.LiteralValue,
                    SourcePath = operation.SourcePath,
                    Separator = operation.Separator,
                    IsLegacyLiteral = operation.IsLegacyLiteral
                };
            }

            if (operation.Kind is SetValueOperationKind.SetTrue or SetValueOperationKind.SetFalse or SetValueOperationKind.SetFromItem)
            {
                return operation;
            }
        }

        return operation.Kind switch
        {
            SetValueOperationKind.IncrementOne => new SetValueOperation
            {
                Kind = SetValueOperationKind.IncrementBy,
                LiteralValue = "1",
                SourcePath = operation.SourcePath,
                Separator = operation.Separator,
                IsLegacyLiteral = operation.IsLegacyLiteral
            },
            SetValueOperationKind.DecrementOne => new SetValueOperation
            {
                Kind = SetValueOperationKind.DecrementBy,
                LiteralValue = "1",
                SourcePath = operation.SourcePath,
                Separator = operation.Separator,
                IsLegacyLiteral = operation.IsLegacyLiteral
            },
            SetValueOperationKind.SetTrue => new SetValueOperation
            {
                Kind = SetValueOperationKind.SetLiteral,
                LiteralValue = "true",
                SourcePath = operation.SourcePath,
                Separator = operation.Separator,
                IsLegacyLiteral = operation.IsLegacyLiteral
            },
            SetValueOperationKind.SetFalse => new SetValueOperation
            {
                Kind = SetValueOperationKind.SetLiteral,
                LiteralValue = "false",
                SourcePath = operation.SourcePath,
                Separator = operation.Separator,
                IsLegacyLiteral = operation.IsLegacyLiteral
            },
            _ => operation
        };
    }

    private static SetValueOperationParseResult CreateInvalidStructuredResult(string errorMessage)
        => new()
        {
            IsValid = false,
            IsStructured = true,
            ErrorMessage = errorMessage,
            Operation = new SetValueOperation()
        };

    private static SetValueOperationValidationResult ValidateNumericOperation(SetValueOperation operation)
        => operation.Kind switch
        {
            SetValueOperationKind.SetLiteral when TryParseNumericLiteral(operation.LiteralValue, out _) => SetValueOperationValidationResult.Success,
            SetValueOperationKind.SetFromItem => SetValueOperationValidationResult.Success,
            SetValueOperationKind.IncrementBy when TryParseNumericLiteral(operation.LiteralValue, out _) => SetValueOperationValidationResult.Success,
            SetValueOperationKind.DecrementBy when TryParseNumericLiteral(operation.LiteralValue, out _) => SetValueOperationValidationResult.Success,
            SetValueOperationKind.IncrementOne => SetValueOperationValidationResult.Success,
            SetValueOperationKind.DecrementOne => SetValueOperationValidationResult.Success,
            SetValueOperationKind.SetLiteral => CreateValidationError("Enter a valid numeric value using invariant format, for example 12 or 12.5."),
            SetValueOperationKind.IncrementBy or SetValueOperationKind.DecrementBy => CreateValidationError("Enter a valid numeric delta using invariant format, for example 1 or 0.5."),
            _ => CreateValidationError("This operation is not available for numeric targets.")
        };

    private static SetValueOperationValidationResult ValidateStringOperation(SetValueOperation operation)
        => operation.Kind switch
        {
            SetValueOperationKind.SetLiteral => SetValueOperationValidationResult.Success,
            SetValueOperationKind.SetFromItem => SetValueOperationValidationResult.Success,
            SetValueOperationKind.AppendText => SetValueOperationValidationResult.Success,
            _ => CreateValidationError("This operation is not available for string targets.")
        };

    private static SetValueOperationValidationResult ValidateBooleanOperation(SetValueOperation operation)
        => operation.Kind switch
        {
            SetValueOperationKind.SetTrue => SetValueOperationValidationResult.Success,
            SetValueOperationKind.SetFalse => SetValueOperationValidationResult.Success,
            SetValueOperationKind.SetFromItem => SetValueOperationValidationResult.Success,
            SetValueOperationKind.SetLiteral when TryParseBooleanLikeLiteral(operation.LiteralValue, out _) => SetValueOperationValidationResult.Success,
            SetValueOperationKind.SetLiteral => CreateValidationError("Enter true, false, 1, or 0 for boolean targets."),
            _ => CreateValidationError("This operation is not available for boolean targets.")
        };

    private static SetValueOperationValidationResult ValidateUnknownOperation(SetValueOperation operation)
        => operation.Kind switch
        {
            SetValueOperationKind.SetLiteral => SetValueOperationValidationResult.Success,
            SetValueOperationKind.SetFromItem => SetValueOperationValidationResult.Success,
            _ => CreateValidationError("The target type is unknown. Use a literal value or copy from another item.")
        };

    private static SetValueOperationValidationResult CreateValidationError(string errorMessage)
        => new()
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };

    private static bool UsesLiteralValue(SetValueOperationKind kind)
        => kind is SetValueOperationKind.SetLiteral
            or SetValueOperationKind.IncrementBy
            or SetValueOperationKind.DecrementBy
            or SetValueOperationKind.AppendText;

    private static bool UsesSourcePath(SetValueOperationKind kind)
        => kind == SetValueOperationKind.SetFromItem;

    private static bool UsesSeparator(SetValueOperationKind kind)
        => kind == SetValueOperationKind.AppendText;

    private static string FormatQuotedValue(string? value, SetValueTargetKind targetKind)
    {
        var normalized = value ?? string.Empty;
        if (targetKind == SetValueTargetKind.Numeric && TryParseNumericLiteral(normalized, out var numericValue))
        {
            return numericValue.ToString("0.############################", CultureInfo.InvariantCulture);
        }

        if (targetKind == SetValueTargetKind.Boolean && TryParseBooleanLikeLiteral(normalized, out var boolValue))
        {
            return boolValue ? "true" : "false";
        }

        return string.IsNullOrEmpty(normalized)
            ? "\"\""
            : $"\"{normalized}\"";
    }

    private static string FormatNumericValue(string? value)
        => TryParseNumericLiteral(value, out var numericValue)
            ? numericValue.ToString("0.############################", CultureInfo.InvariantCulture)
            : (value ?? string.Empty);

    private static string FormatAppendSummary(SetValueOperation operation)
    {
        var appendedValue = FormatQuotedValue(operation.LiteralValue, targetKind: SetValueTargetKind.String);
        if (string.IsNullOrEmpty(operation.Separator))
        {
            return $"Append {appendedValue}";
        }

        return $"Append {appendedValue} with separator {FormatQuotedValue(operation.Separator, targetKind: SetValueTargetKind.String)}";
    }

    private static string FormatSourcePath(string? sourcePath)
        => string.IsNullOrWhiteSpace(sourcePath)
            ? "selected item"
            : sourcePath.Trim();
}
