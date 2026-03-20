using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace UiEditor.Items
{
    public sealed class ItemChangedEventArgs : EventArgs
    {
        public ItemChangedEventArgs(Item item, string parameterName)
        {
            Item = item;
            ParameterName = parameterName;
        }

        public Item Item { get; }
        public string ParameterName { get; }
    }

    public sealed class Parameter
    {
        public event EventHandler? Changed;

        public string Name { get; }
        public string Path { get; set; } = string.Empty;
        private object? _value;
        public ulong LastUpdate;
        public dynamic Value
        {
            get => _value!;
            set
            {
                if (value is null)
                {
                    if (_value is not null && _value.GetType().IsValueType)
                        throw new InvalidCastException($"Cannot assign null to parameter '{Name}' of type '{_value.GetType().FullName}'.");
                    _value = null;
                    LastUpdate = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    Changed?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (value.GetType() != _value?.GetType() && _value is not null)
                    throw new InvalidCastException($"Cannot assign value of type '{value.GetType().FullName}' to parameter '{Path.Replace("/", ".")}' of type '{_value?.GetType()}'.");

                _value = value;
                LastUpdate = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public Parameter(string name, object? value, string path = "")
        {

            Name = name;
            Path = path;
            _value = value;
            LastUpdate = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public override string ToString()
        {
            return $"Path: {Path.Replace("/", ".")}.{Name}: {_value} (Type: {_value?.GetType().FullName ?? "UnknownType"})";
        }
    }

    public class Item : ItemDictionary
    {
        public event EventHandler<ItemChangedEventArgs>? Changed;

        public ParameterDictionary Params { get; set; }
        public string? Name => Params["Name"].Value?.ToString();
        public string? Path => Params["Path"].Value?.ToString();

        public Item(string name)
        {
            _path = _path + "/" + name;
            Params = new ParameterDictionary(_path, OnParameterChanged);
            Params["Name"].Value = name;
            Params["Path"].Value = _path;
            Params["Value"].Value = null!;
        }

        public void AddItem(string name)
        {
            var item = this[name];

        }


        public Item(string name, object? value = null, string? path = null)
        {
            if (path == null)
            {
                _path = name;
            }
            else
            {
                _path = path + "/" + name;
            }
            Params = new ParameterDictionary(_path, OnParameterChanged);
            Params["Name"].Value = name;
            Params["Path"].Value = _path;
            Params["Value"].Value = value!;

        }

        public dynamic Value
        {
            get
            {
                return Params["Value"].Value;
            }

            set
            {
                Params["Value"].Value = value!;
            }
        }

        public dynamic GetParamter(string paramName)
        {
            if (!Params.Has(paramName))
                throw new KeyNotFoundException($"Parameter '{paramName}' not found in item '{Name}'.");
            return Params[paramName].Value;
        }

        private void OnParameterChanged(Parameter parameter)
        {
            Changed?.Invoke(this, new ItemChangedEventArgs(this, parameter.Name));
        }
    }



    public class ItemDictionary
    {

        internal ConcurrentDictionary<string, Item> Dictionary = new ConcurrentDictionary<string, Item>();

        public ConcurrentDictionary<string, Item> GetDictionary() => new ConcurrentDictionary<string, Item>(Dictionary);
        public void SetDictionary(ConcurrentDictionary<string, Item> newDictionary)
        {
            Dictionary = newDictionary;
        }


        internal string _path = "";

        public ItemDictionary() { }

        public ItemDictionary(string path)
        {
            _path = path;
        }

        public void Clear()
        {
            Dictionary.Clear();
        }


        public bool Has(string id)
        {
            return Dictionary.ContainsKey(id);
        }
        public void Remove(string id)
        {
            Dictionary.TryRemove(id, out _);
        }

        private void Add(string id, Item item)
        {
            if (!Dictionary.TryAdd(id, item))
                throw new ArgumentException($"An element with the key '{id}' already exists.", nameof(id));
        }

        public Item this[string id]
        {
            get
            {
                var path = _path;
                return Dictionary.GetOrAdd(id, key => new Item(key, path: path));
            }
            set
            {
                if (value == null)
                {
                    return;
                }
                Dictionary[id] = value;

            }
        }
    }

    public class ParameterDictionary
    {
        internal string _path = "";
        internal ConcurrentDictionary<string, Parameter> Dictionary = new ConcurrentDictionary<string, Parameter>();
        private readonly Action<Parameter>? _onParameterChanged;

        public void Clear()
        {
            Dictionary.Clear();
        }

        public ConcurrentDictionary<string, Parameter> GetDictionary() => new ConcurrentDictionary<string, Parameter>(Dictionary);
        public void SetDictionary(ConcurrentDictionary<string, Parameter> newDictionary)
        {
            foreach (var parameter in Dictionary.Values)
            {
                parameter.Changed -= OnParameterChanged;
            }

            Dictionary = newDictionary;

            foreach (var parameter in Dictionary.Values)
            {
                parameter.Changed += OnParameterChanged;
            }
        }

        public ParameterDictionary(string path, Action<Parameter>? onParameterChanged = null)
        {
            _path = path;
            _onParameterChanged = onParameterChanged;
        }

        public bool Has(string id)
        {
            return Dictionary.ContainsKey(id);
        }

        public void Remove(string id)
        {
            Dictionary.TryRemove(id, out _);
        }


        public Parameter this[string id]
        {
            get
            {
                string path = _path + "/" + id;
                return Dictionary.GetOrAdd(id, key =>
                {
                    var parameter = new Parameter(key, null, path);
                    parameter.Changed += OnParameterChanged;
                    return parameter;
                });
            }
            set
            {
                if (value == null)
                {
                    return;
                }

                value.Changed -= OnParameterChanged;
                value.Changed += OnParameterChanged;
                Dictionary[id] = value;
            }
        }

        private void OnParameterChanged(object? sender, EventArgs e)
        {
            if (sender is Parameter parameter)
            {
                _onParameterChanged?.Invoke(parameter);
            }
        }
    }


}
