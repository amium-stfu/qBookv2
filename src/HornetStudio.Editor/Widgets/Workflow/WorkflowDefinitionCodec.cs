using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HornetStudio.Editor.Helpers;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.Widgets.Common;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HornetStudio.Editor.Widgets.Workflow;

/// <summary>
/// Loads and saves function definitions from YAML files.
/// </summary>
public static class FunctionDefinitionCodec
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Gets the folder-local function directory below one folder UI directory.
    /// </summary>
    /// <param name="folderDirectory">The directory that contains Folder.yaml.</param>
    /// <returns>The function directory path.</returns>
    public static string GetFunctionDirectory(string folderDirectory)
    {
        if (string.IsNullOrWhiteSpace(folderDirectory))
        {
            return string.Empty;
        }

        return Path.Combine(folderDirectory, "Scripts", "Functions");
    }

    /// <summary>
    /// Enumerates function YAML files below the current folder, including legacy workflow files.
    /// </summary>
    /// <param name="folderDirectory">The directory that contains Folder.yaml.</param>
    /// <returns>The discovered function file paths.</returns>
    public static IReadOnlyList<string> EnumerateFunctionFiles(string folderDirectory)
    {
        var discoveredFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddDirectory(GetFunctionDirectory(folderDirectory));
        AddDirectory(Path.Combine(folderDirectory, "Scripts", "Workflows"));

        return discoveredFiles.Values.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();

        void AddDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.yml").Concat(Directory.EnumerateFiles(directoryPath, "*.yaml")))
            {
                var key = Path.GetFileNameWithoutExtension(filePath);
                if (!discoveredFiles.ContainsKey(key))
                {
                    discoveredFiles[key] = filePath;
                }
            }
        }
    }

    /// <summary>
    /// Loads one function definition from a YAML file.
    /// </summary>
    /// <param name="filePath">The function YAML file path.</param>
    /// <param name="definition">The parsed function definition.</param>
    /// <param name="validation">The validation result.</param>
    /// <returns><see langword="true"/> when parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryLoadFromFile(string filePath, out FunctionDefinition? definition, out FunctionValidationResult validation)
    {
        if (!File.Exists(filePath))
        {
            definition = null;
            validation = new FunctionValidationResult([new FunctionValidationError("function", $"Function file '{filePath}' was not found.")]);
            return false;
        }

        return TryParse(File.ReadAllText(filePath), Path.GetFileName(filePath), out definition, out validation);
    }

    /// <summary>
    /// Saves one function definition to a YAML file and creates the parent directory when needed.
    /// </summary>
    /// <param name="filePath">The function YAML file path.</param>
    /// <param name="definition">The function definition to persist.</param>
    public static void SaveToFile(string filePath, FunctionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, Serialize(definition));
    }

    /// <summary>
    /// Parses one function definition from YAML text.
    /// </summary>
    /// <param name="raw">The YAML text.</param>
    /// <param name="sourceName">The logical source name used for diagnostics.</param>
    /// <param name="definition">The parsed function definition.</param>
    /// <param name="validation">The validation result.</param>
    /// <returns><see langword="true"/> when parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? raw, string? sourceName, out FunctionDefinition? definition, out FunctionValidationResult validation)
    {
        var errors = new List<FunctionValidationError>();
        definition = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            validation = new FunctionValidationResult([new FunctionValidationError("function", "Function YAML is empty.")]);
            return false;
        }

        YamlMappingNode root;
        try
        {
            using var reader = new StringReader(raw);
            var yaml = new YamlStream();
            yaml.Load(reader);
            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
            {
                validation = new FunctionValidationResult([new FunctionValidationError("function", "Function YAML must contain a root mapping.")]);
                return false;
            }

            root = mapping;
        }
        catch (Exception ex)
        {
            validation = new FunctionValidationResult([new FunctionValidationError("function", $"Function YAML could not be parsed: {ex.Message}")]);
            return false;
        }

        var name = GetRequiredScalar(root, "name", "function.name", errors);
        var stepsNode = GetSequence(root, "steps");
        if (stepsNode is null)
        {
            errors.Add(new FunctionValidationError("function.steps", "Function requires a steps sequence."));
        }

        var steps = new List<FunctionStepDefinition>();
        if (stepsNode is not null)
        {
            for (var index = 0; index < stepsNode.Children.Count; index++)
            {
                if (stepsNode.Children[index] is not YamlMappingNode stepNode)
                {
                    errors.Add(new FunctionValidationError($"function.steps[{index}]", "Function step must be a mapping."));
                    continue;
                }

                if (TryParseStep(stepNode, $"function.steps[{index}]", errors, out var step))
                {
                    steps.Add(step!);
                }
            }
        }

        validation = new FunctionValidationResult(errors);
        if (!validation.IsValid)
        {
            return false;
        }

        definition = new FunctionDefinition
        {
            Name = string.IsNullOrWhiteSpace(name) ? (sourceName ?? string.Empty) : name.Trim(),
            Steps = steps
        };

        AddStepValidationErrors(definition.Steps, path: "function.steps", errors);
        validation = new FunctionValidationResult(errors);
        if (!validation.IsValid)
        {
            definition = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Serializes one function definition into YAML text.
    /// </summary>
    /// <param name="definition">The function definition to serialize.</param>
    /// <returns>The YAML text.</returns>
    public static string Serialize(FunctionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var root = new Dictionary<string, object?>
        {
            ["name"] = definition.Name?.Trim() ?? string.Empty,
            ["steps"] = definition.Steps.Select(ToSerializableStep).ToList()
        };

        return Serializer.Serialize(root);
    }

    private static bool TryParseStep(YamlMappingNode node, string path, List<FunctionValidationError> errors, out FunctionStepDefinition? step)
    {
        step = null;
        var typeText = GetRequiredScalar(node, "type", $"{path}.type", errors);
        if (!Enum.TryParse<FunctionStepType>(typeText, ignoreCase: true, out var type))
        {
            errors.Add(new FunctionValidationError($"{path}.type", $"Unsupported function step type '{typeText}'."));
            return false;
        }

        switch (type)
        {
            case FunctionStepType.SetValue:
                step = new FunctionSetValueStepDefinition
                {
                    Target = GetRequiredScalar(node, "target", $"{path}.target", errors),
                    Value = GetOptionalScalar(node, "value") ?? string.Empty,
                    ValueFrom = GetOptionalScalar(node, "valueFrom") ?? string.Empty
                };
                return true;

            case FunctionStepType.Delay:
                if (!TryGetRequiredInt(node, "milliseconds", $"{path}.milliseconds", errors, out var milliseconds))
                {
                    return false;
                }

                step = new FunctionDelayStepDefinition
                {
                    Milliseconds = milliseconds
                };
                return true;

            case FunctionStepType.IfThenElse:
                var thenSequence = GetSequence(node, "then");
                if (thenSequence is null)
                {
                    errors.Add(new FunctionValidationError($"{path}.then", "IfThenElse step requires a then sequence."));
                    return false;
                }

                var thenSteps = ParseStepSequence(thenSequence, $"{path}.then", errors);
                var elseSteps = GetSequence(node, "else") is { } elseSequence
                    ? ParseStepSequence(elseSequence, $"{path}.else", errors)
                    : [];
                step = new FunctionIfThenElseStepDefinition
                {
                    Condition = GetRequiredScalar(node, "condition", $"{path}.condition", errors),
                    Variables = ParseConditionVariables(node, path, errors),
                    Then = thenSteps,
                    Else = elseSteps
                };
                return true;

            case FunctionStepType.While:
                var bodySequence = GetSequence(node, "steps");
                if (bodySequence is null)
                {
                    errors.Add(new FunctionValidationError($"{path}.steps", "While step requires a steps sequence."));
                    return false;
                }

                var bodySteps = ParseStepSequence(bodySequence, $"{path}.steps", errors);
                step = new FunctionWhileStepDefinition
                {
                    Condition = GetRequiredScalar(node, "condition", $"{path}.condition", errors),
                    Variables = ParseConditionVariables(node, path, errors),
                    Steps = bodySteps
                };
                return true;

            case FunctionStepType.Log:
                var levelText = GetOptionalScalar(node, "level");
                var logLevel = MonitorLogLevel.Info;
                var parsedLogLevel = MonitorLogLevel.Info;
                if (!string.IsNullOrWhiteSpace(levelText) && !Enum.TryParse<MonitorLogLevel>(levelText, ignoreCase: true, out parsedLogLevel))
                {
                    errors.Add(new FunctionValidationError($"{path}.level", $"Unsupported log level '{levelText}'."));
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(levelText))
                {
                    logLevel = parsedLogLevel;
                }

                step = new FunctionLogStepDefinition
                {
                    TargetLog = GetRequiredScalar(node, "targetLog", $"{path}.targetLog", errors),
                    Level = string.IsNullOrWhiteSpace(levelText) ? MonitorLogLevel.Info : logLevel,
                    Text = GetRequiredScalar(node, "text", $"{path}.text", errors)
                };
                return true;

            default:
                errors.Add(new FunctionValidationError(path, $"Function step type '{type}' is not supported."));
                return false;
        }
    }

    private static List<FunctionStepDefinition> ParseStepSequence(YamlSequenceNode sequence, string path, List<FunctionValidationError> errors)
    {
        var result = new List<FunctionStepDefinition>();
        for (var index = 0; index < sequence.Children.Count; index++)
        {
            if (sequence.Children[index] is not YamlMappingNode stepNode)
            {
                errors.Add(new FunctionValidationError($"{path}[{index}]", "Function step must be a mapping."));
                continue;
            }

            if (TryParseStep(stepNode, $"{path}[{index}]", errors, out var step))
            {
                result.Add(step!);
            }
        }

        return result;
    }

    private static object ToSerializableStep(FunctionStepDefinition step)
    {
        return step switch
        {
            FunctionSetValueStepDefinition setValue => BuildSetValueStep(setValue),
            FunctionDelayStepDefinition delay => new Dictionary<string, object?>
            {
                ["type"] = delay.Type.ToString(),
                ["milliseconds"] = delay.Milliseconds
            },
            FunctionIfThenElseStepDefinition conditional => BuildConditionalStep(conditional),
            FunctionWhileStepDefinition loop => BuildWhileStep(loop),
            FunctionLogStepDefinition log => new Dictionary<string, object?>
            {
                ["type"] = log.Type.ToString(),
                ["targetLog"] = log.TargetLog,
                ["level"] = log.Level.ToString(),
                ["text"] = log.Text
            },
            _ => throw new InvalidOperationException($"Unsupported function step type '{step.GetType().Name}'.")
        };
    }

    private static Dictionary<string, object?> BuildSetValueStep(FunctionSetValueStepDefinition setValue)
    {
        var result = new Dictionary<string, object?>
        {
            ["type"] = setValue.Type.ToString(),
            ["target"] = setValue.Target
        };

        if (!string.IsNullOrWhiteSpace(setValue.ValueFrom))
        {
            result["valueFrom"] = setValue.ValueFrom;
        }
        else
        {
            result["value"] = setValue.Value;
        }

        return result;
    }

    private static Dictionary<string, object?> BuildWhileStep(FunctionWhileStepDefinition loop)
    {
        var result = new Dictionary<string, object?>
        {
            ["type"] = loop.Type.ToString(),
            ["condition"] = loop.Condition,
            ["steps"] = loop.Steps.Select(ToSerializableStep).ToList()
        };

        if (loop.Variables.Count > 0)
        {
            result["variables"] = loop.Variables
                .Select(static variable => new Dictionary<string, object?>
                {
                    ["name"] = variable.Name,
                    ["sourcePath"] = variable.SourcePath
                })
                .ToList();
        }

        return result;
    }

    private static Dictionary<string, object?> BuildConditionalStep(FunctionIfThenElseStepDefinition conditional)
    {
        var result = new Dictionary<string, object?>
        {
            ["type"] = conditional.Type.ToString(),
            ["condition"] = conditional.Condition,
            ["then"] = conditional.Then.Select(ToSerializableStep).ToList()
        };

        if (conditional.Variables.Count > 0)
        {
            result["variables"] = conditional.Variables
                .Select(static variable => new Dictionary<string, object?>
                {
                    ["name"] = variable.Name,
                    ["sourcePath"] = variable.SourcePath
                })
                .ToList();
        }

        if (conditional.Else.Count > 0)
        {
            result["else"] = conditional.Else.Select(ToSerializableStep).ToList();
        }

        return result;
    }

    private static string GetRequiredScalar(YamlMappingNode node, string key, string path, List<FunctionValidationError> errors)
    {
        var value = GetOptionalScalar(node, key);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        errors.Add(new FunctionValidationError(path, $"{key} is required."));
        return string.Empty;
    }

    private static bool TryGetRequiredInt(YamlMappingNode node, string key, string path, List<FunctionValidationError> errors, out int value)
    {
        value = 0;
        var raw = GetOptionalScalar(node, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add(new FunctionValidationError(path, $"{key} is required."));
            return false;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            errors.Add(new FunctionValidationError(path, $"{key} must be an integer."));
            return false;
        }

        return true;
    }

    private static string? GetOptionalScalar(YamlMappingNode node, string key)
    {
        foreach (var entry in node.Children)
        {
            if (entry.Key is YamlScalarNode keyNode && string.Equals(keyNode.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value switch
                {
                    YamlScalarNode scalar => scalar.Value,
                    _ => null
                };
            }
        }

        return null;
    }

    private static YamlSequenceNode? GetSequence(YamlMappingNode node, string key)
    {
        foreach (var entry in node.Children)
        {
            if (entry.Key is YamlScalarNode keyNode && string.Equals(keyNode.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value as YamlSequenceNode;
            }
        }

        return null;
    }

    private static List<BooleanConditionVariableDefinition> ParseConditionVariables(YamlMappingNode node, string path, List<FunctionValidationError> errors)
    {
        var sequence = GetSequence(node, "variables");
        if (sequence is null)
        {
            return [];
        }

        var variables = new List<BooleanConditionVariableDefinition>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "value", "source" };
        for (var index = 0; index < sequence.Children.Count; index++)
        {
            if (sequence.Children[index] is not YamlMappingNode variableNode)
            {
                errors.Add(new FunctionValidationError($"{path}.variables[{index}]", "Condition variable must be a mapping."));
                continue;
            }

            var name = GetRequiredScalar(variableNode, "name", $"{path}.variables[{index}].name", errors);
            var sourcePath = GetRequiredScalar(variableNode, "sourcePath", $"{path}.variables[{index}].sourcePath", errors);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sourcePath))
            {
                continue;
            }

            if (!BooleanConditionEditorViewModel.IsValidVariableName(name))
            {
                errors.Add(new FunctionValidationError($"{path}.variables[{index}].name", $"Variable name '{name}' is invalid."));
                continue;
            }

            if (!usedNames.Add(name.Trim()))
            {
                errors.Add(new FunctionValidationError($"{path}.variables[{index}].name", $"Variable name '{name}' is used more than once."));
                continue;
            }

            variables.Add(new BooleanConditionVariableDefinition
            {
                Name = name.Trim(),
                SourcePath = TargetPathHelper.NormalizeConfiguredTargetPath(sourcePath)
            });
        }

        return variables;
    }

    private static void AddStepValidationErrors(IReadOnlyList<FunctionStepDefinition> steps, string path, List<FunctionValidationError> errors)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            AddStepValidationErrors(steps[index], $"{path}[{index}]", errors);
        }
    }

    private static void AddStepValidationErrors(FunctionStepDefinition step, string path, List<FunctionValidationError> errors)
    {
        switch (step)
        {
            case FunctionDelayStepDefinition:
            case FunctionLogStepDefinition:
            case FunctionSetValueStepDefinition:
                return;

            case FunctionIfThenElseStepDefinition conditional:
                if (string.IsNullOrWhiteSpace(conditional.Condition))
                {
                    errors.Add(new FunctionValidationError($"{path}.condition", "IfThenElse step requires a condition."));
                }

                if (conditional.Then.Count == 0)
                {
                    errors.Add(new FunctionValidationError($"{path}.then", "IfThenElse step requires at least one then step."));
                }

                AddStepValidationErrors(conditional.Then, $"{path}.then", errors);
                AddStepValidationErrors(conditional.Else, $"{path}.else", errors);
                return;

            case FunctionWhileStepDefinition loop:
                if (string.IsNullOrWhiteSpace(loop.Condition))
                {
                    errors.Add(new FunctionValidationError($"{path}.condition", "While step requires a condition."));
                }

                if (loop.Steps.Count == 0)
                {
                    errors.Add(new FunctionValidationError($"{path}.steps", "While step requires at least one body step."));
                }

                if (!HasPositiveDelayGuard(loop.Steps))
                {
                    errors.Add(new FunctionValidationError($"{path}.steps", "While step requires at least one positive Delay step in its body."));
                }

                AddStepValidationErrors(loop.Steps, $"{path}.steps", errors);
                return;

            default:
                errors.Add(new FunctionValidationError(path, $"Function step type '{step.Type}' is not supported."));
                return;
        }
    }

    private static bool HasPositiveDelayGuard(IEnumerable<FunctionStepDefinition> steps)
    {
        return steps.OfType<FunctionDelayStepDefinition>().Any(static step => step.Milliseconds > 0);
    }
}