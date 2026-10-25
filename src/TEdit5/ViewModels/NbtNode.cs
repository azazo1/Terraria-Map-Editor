using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using TEdit.Common.IO;

namespace TEdit5.ViewModels;

/// <summary>
/// NBT 树里的一项.
/// </summary>
public class NbtNode : ReactiveObject
{
    private bool _isExpanded = true;
    private string _editableValue = string.Empty;

    public NbtNode(string name, object? value, TagCompound? parent)
    {
        Name = name;
        Value = value;
        Parent = parent;
        Children = new ObservableCollection<NbtNode>();
        Build();
    }

    public string Name { get; }

    public object? Value { get; }

    /// <summary>所在父级, 用于写回修改.</summary>
    public TagCompound? Parent { get; }

    public ObservableCollection<NbtNode> Children { get; }

    public bool IsLeaf => Children.Count == 0;

    public bool CanEdit => Parent != null && Value is string or int or long or byte or short or float or double or bool;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public string TypeLabel => Value switch
    {
        null => "null",
        TagCompound => "compound",
        byte => "byte",
        short => "short",
        int => "int",
        long => "long",
        float => "float",
        double => "double",
        string => "string",
        bool => "bool",
        byte[] bytes => $"byte[{bytes.Length}]",
        int[] ints => $"int[{ints.Length}]",
        IList list => $"list[{list.Count}]",
        _ => Value.GetType().Name,
    };

    public string ValuePreview => Value switch
    {
        null => string.Empty,
        TagCompound compound => $"{compound.Count} 项",
        byte[] bytes => $"({bytes.Length} 字节)",
        int[] ints => $"({ints.Length} 项)",
        IList list => $"({list.Count} 项)",
        string text => text.Length > 60 ? text[..60] + "..." : text,
        _ => Value.ToString() ?? string.Empty,
    };

    public string EditableValue
    {
        get => Value?.ToString() ?? string.Empty;
        set
        {
            this.RaiseAndSetIfChanged(ref _editableValue, value);
            if (!CanEdit || Parent == null)
            {
                return;
            }

            Parent.Set(Name, ConvertValue(value));
            this.RaisePropertyChanged(nameof(ValuePreview));
        }
    }

    private object ConvertValue(string text)
        => Value switch
        {
            int => int.TryParse(text, out int i) ? i : 0,
            long => long.TryParse(text, out long l) ? l : 0,
            byte => byte.TryParse(text, out byte b) ? b : (byte)0,
            short => short.TryParse(text, out short s) ? s : (short)0,
            float => float.TryParse(text, out float f) ? f : 0f,
            double => double.TryParse(text, out double d) ? d : 0d,
            bool => bool.TryParse(text, out bool bv) && bv,
            _ => text,
        };

    private void Build()
    {
        if (Value is TagCompound compound)
        {
            foreach (var pair in compound)
            {
                Children.Add(new NbtNode(pair.Key, pair.Value, compound));
            }
        }
        else if (Value is IList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Children.Add(new NbtNode($"[{i}]", list[i], null));
            }
        }

        _editableValue = Value?.ToString() ?? string.Empty;
    }

    /// <summary>按名字筛选子树, 命中则保留.</summary>
    public bool Matches(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        return Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || ValuePreview.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
}
