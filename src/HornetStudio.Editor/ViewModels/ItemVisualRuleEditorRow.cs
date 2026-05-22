using System;
using System.Collections.ObjectModel;

namespace HornetStudio.Editor.ViewModels;

public sealed class ItemVisualRuleEditorRow : ObservableObject
{
    private string _sourceKind = nameof(Models.VisualRuleSourceKind.MonitorRule);
    private string _sourcePath = string.Empty;
    private string _target = nameof(Models.VisualRuleTarget.Body);
    private string _propertyName = nameof(Models.VisualRuleProperty.BodyBackColor);
    private string _effect = nameof(Models.VisualRuleEffect.None);
    private string _activeValue = string.Empty;
    private string _inactiveValue = string.Empty;

    public string SourceKind
    {
        get => _sourceKind;
        set => SetProperty(ref _sourceKind, string.IsNullOrWhiteSpace(value) ? nameof(Models.VisualRuleSourceKind.MonitorRule) : value);
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value ?? string.Empty);
    }

    public string Target
    {
        get => _target;
        set => SetProperty(ref _target, string.IsNullOrWhiteSpace(value) ? nameof(Models.VisualRuleTarget.Body) : value);
    }

    public string PropertyName
    {
        get => _propertyName;
        set => SetProperty(ref _propertyName, string.IsNullOrWhiteSpace(value) ? nameof(Models.VisualRuleProperty.BodyBackColor) : value);
    }

    public string Effect
    {
        get => _effect;
        set => SetProperty(ref _effect, string.IsNullOrWhiteSpace(value) ? nameof(Models.VisualRuleEffect.None) : value);
    }

    public string ActiveValue
    {
        get => _activeValue;
        set => SetProperty(ref _activeValue, value ?? string.Empty);
    }

    public string InactiveValue
    {
        get => _inactiveValue;
        set => SetProperty(ref _inactiveValue, value ?? string.Empty);
    }

    public ObservableCollection<string> SourceKindOptions { get; } = [];

    public ObservableCollection<string> SourceOptions { get; } = [];

    public ObservableCollection<string> TargetOptions { get; } = [];

    public ObservableCollection<string> PropertyOptions { get; } = [];

    public ObservableCollection<string> EffectOptions { get; } = [];

    public bool HasInactiveValue => !string.IsNullOrWhiteSpace(InactiveValue);
}