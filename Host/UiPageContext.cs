using System;
using System.Collections.Generic;
using Amium.Items;

namespace Amium.Host;

public sealed class UiPageContext : IDisposable
{
    private readonly List<AttachedItemLink> _links = [];
    private readonly string _pagePath;

    public UiPageContext(string pageName, string? bookName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageName);

        PageName = pageName.Trim();
        BookName = string.IsNullOrWhiteSpace(bookName) ? null : bookName.Trim();
        _pagePath = string.IsNullOrWhiteSpace(BookName) ? PageName : $"{BookName}/{PageName}";
    }

    public string PageName { get; }
    public string? BookName { get; }
    public string PagePath => _pagePath;

    public Item Attach(Item source, string? alias = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var itemName = string.IsNullOrWhiteSpace(alias) ? source.Name : alias.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);

        var targetPath = $"{_pagePath}/{itemName}";

        foreach (var link in _links)
        {
            if (link.Matches(source, targetPath))
            {
                return link.AttachedItem;
            }
        }

        var attached = source.Clone().Repath(targetPath);
        _links.Add(new AttachedItemLink(source, attached, targetPath));
        return attached;
    }

    public HostCommand CreateCommand(string name, Action action, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(action);

        var commandPath = $"{_pagePath}/Commands/{name.Trim()}";
        return new HostCommand(commandPath, _ => action(), description: description);
    }

    public HostCommand AttachCommand(string name, Action action, string? description = null)
        => CreateCommand(name, action, description);

    public void Dispose()
    {
        foreach (var link in _links)
        {
            link.Dispose();
        }

        _links.Clear();
    }

    private sealed class AttachedItemLink : IDisposable
    {
        private readonly Item _attachedItem;
        private readonly Item _source;
        private bool _isSyncingFromSource;
        private bool _isSyncingFromTarget;
        private readonly string _targetPath;

        public AttachedItemLink(Item source, Item attachedItem, string targetPath)
        {
            _source = source;
            _attachedItem = attachedItem;
            _targetPath = targetPath;
            _source.Changed += OnSourceChanged;
            HostRegistries.Data.ItemChanged += OnTargetChanged;
        }

        public Item AttachedItem => _attachedItem;

        public bool Matches(Item source, string targetPath)
            => ReferenceEquals(_source, source)
                && string.Equals(_targetPath, targetPath, StringComparison.Ordinal);

        public void Dispose()
        {
            _source.Changed -= OnSourceChanged;
            HostRegistries.Data.ItemChanged -= OnTargetChanged;
            HostRegistries.Data.Remove(_targetPath);
        }

        private void OnSourceChanged(object? sender, ItemChangedEventArgs e)
        {
            if (_isSyncingFromTarget)
            {
                return;
            }

            if (!HostRegistries.Data.TryGet(_targetPath, out var target) || target is null)
            {
                return;
            }

            _isSyncingFromSource = true;
            try
            {
                var parameterName = e.ParameterName;
                if (string.Equals(parameterName, "Value", StringComparison.Ordinal))
                {
                    var valueTimestamp = _source.Params.Has("Value") ? _source.Params["Value"].LastUpdate : (ulong?)null;
                    HostRegistries.Data.UpdateValue(_targetPath, _source.Value, valueTimestamp);
                    return;
                }

                if (_source.Params.Has(parameterName) && target.Params.Has(parameterName))
                {
                    var sourceParameter = _source.Params[parameterName];
                    HostRegistries.Data.UpdateParameter(_targetPath, parameterName, sourceParameter.Value, sourceParameter.LastUpdate);
                    return;
                }

                var snapshot = _source.Clone().Repath(_targetPath);
                HostRegistries.Data.UpsertSnapshot(_targetPath, snapshot, pruneMissingMembers: true);
            }
            finally
            {
                _isSyncingFromSource = false;
            }
        }

        private void OnTargetChanged(object? sender, DataChangedEventArgs e)
        {
            if (_isSyncingFromSource || !string.Equals(e.Key, _targetPath, StringComparison.Ordinal))
            {
                return;
            }

            _isSyncingFromTarget = true;
            try
            {
                if (string.Equals(e.ParameterName, "Value", StringComparison.Ordinal) || e.ChangeKind == DataChangeKind.ValueUpdated)
                {
                    _source.Value = e.Item.Value;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(e.ParameterName) && e.Item.Params.Has(e.ParameterName))
                {
                    _source.Params[e.ParameterName].Value = e.Item.Params[e.ParameterName].Value;
                    return;
                }

                var snapshot = e.Item.Clone();
                foreach (var parameterEntry in snapshot.Params.GetDictionary())
                {
                    _source.Params[parameterEntry.Key].Value = parameterEntry.Value.Value;
                }
            }
            finally
            {
                _isSyncingFromTarget = false;
            }
        }
    }
}
