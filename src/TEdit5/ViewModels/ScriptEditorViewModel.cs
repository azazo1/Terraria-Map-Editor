using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>脚本列表里的一项.</summary>
public class ScriptFileInfo
{
    public ScriptFileInfo(string name, string path)
    {
        Name = name;
        Path = path;
    }

    public string Name { get; }

    public string Path { get; }
}

/// <summary>
/// 脚本面板. 移植自 Windows 版的 ScriptingSidebarViewModel, 保留脚本浏览, 编辑与保存;
/// 执行脚本需要脚本宿主, 这里暂未接线.
/// </summary>
public partial class ScriptEditorViewModel : ReactiveObject
{
    private static readonly string[] EngineNamesInternal = { "JavaScript (Jint)", "Lua" };
    private static readonly string[] Extensions = { ".js", ".lua" };

    private int _selectedEngineIndex;
    private ScriptFileInfo? _selectedScript;
    private string _scriptCode = string.Empty;
    private bool _showApiReference;
    private string _log = string.Empty;

    public ScriptEditorViewModel()
    {
        ScriptsDirectory = System.IO.Path.Combine(AppPaths.ApplicationData, "scripts");
        Directory.CreateDirectory(ScriptsDirectory);

        NewScriptCommand = ReactiveCommand.Create(NewScript);
        SaveScriptCommand = ReactiveCommand.Create(SaveScript);
        ToggleApiReferenceCommand = ReactiveCommand.Create(ToggleApiReference);
        GenerateDocsCommand = ReactiveCommand.Create(GenerateDocs);
        RefreshCommand = ReactiveCommand.Create(LoadScriptFiles);
        RunScriptCommand = ReactiveCommand.Create(RunScript);
        RunLuaScriptCommand = ReactiveCommand.Create(RunLuaScript);

        LoadScriptFiles();
        LoadApiReference();
    }

    public string ScriptsDirectory { get; }

    public ObservableCollection<ScriptFileInfo> Scripts { get; } = new();

    public string[] EngineNames => EngineNamesInternal;

    public ObservableCollection<string> ApiReference { get; } = new();

    public ReactiveCommand<RxVoid, RxVoid> NewScriptCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SaveScriptCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ToggleApiReferenceCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> GenerateDocsCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RefreshCommand { get; }

    /// <summary>运行脚本. 宿主还没接, 目前只给出提示.</summary>
    public ReactiveCommand<RxVoid, RxVoid> RunScriptCommand { get; }

    /// <summary>用 Lua 引擎运行当前脚本.</summary>
    public ReactiveCommand<RxVoid, RxVoid> RunLuaScriptCommand { get; }

    public int SelectedEngineIndex
    {
        get => _selectedEngineIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedEngineIndex, value);
    }

    public ScriptFileInfo? SelectedScript
    {
        get => _selectedScript;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedScript, value);
            if (value != null)
            {
                LoadSelectedScript();
            }
        }
    }

    public string ScriptCode
    {
        get => _scriptCode;
        set => this.RaiseAndSetIfChanged(ref _scriptCode, value);
    }

    public bool ShowApiReference
    {
        get => _showApiReference;
        set => this.RaiseAndSetIfChanged(ref _showApiReference, value);
    }

    public string Log
    {
        get => _log;
        private set => this.RaiseAndSetIfChanged(ref _log, value);
    }

    /// <summary>执行脚本所用的世界.</summary>
    public TEdit.Terraria.World? World { get; set; }

    /// <summary>脚本改完图格后的重绘回调.</summary>
    public Action? Refresh { get; set; }

    /// <summary>执行前保存撤销快照.</summary>
    public Action? SaveUndoSnapshot { get; set; }

    /// <summary>读取当前选区范围, 返回 x,y,w,h 形式.</summary>
    public Func<string>? SelectionProvider { get; set; }

    /// <summary>设定选区范围.</summary>
    public Action<int, int, int, int>? SelectionSetter { get; set; }

    private void RunScript()
    {
        string code = ScriptCode;
        SaveUndoSnapshot?.Invoke();
        string result = Services.ScriptRunner.Run(code, World, Refresh, SelectionProvider, SelectionSetter);
        AppendLog(result);
    }

    private void RunLuaScript()
    {
        SaveUndoSnapshot?.Invoke();
        string result = Services.ScriptRunner.RunLua(ScriptCode, World, Refresh, SelectionProvider, SelectionSetter);
        AppendLog(result);
    }

    private void ToggleApiReference()
    {
        ShowApiReference = !ShowApiReference;
    }

    private void NewScript()
    {
        ScriptCode = string.Empty;
        SelectedScript = null;
        AppendLog("新建脚本");
    }

    private void SaveScript()
    {
        string? path = _selectedScript?.Path;
        if (string.IsNullOrEmpty(path))
        {
            string extension = Extensions[Math.Clamp(SelectedEngineIndex, 0, Extensions.Length - 1)];
            path = System.IO.Path.Combine(ScriptsDirectory, $"script_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
        }

        File.WriteAllText(path, ScriptCode);
        AppendLog($"已保存: {System.IO.Path.GetFileName(path)}");
        LoadScriptFiles();
    }

    private void LoadSelectedScript()
    {
        if (_selectedScript == null || !File.Exists(_selectedScript.Path))
        {
            return;
        }

        ScriptCode = File.ReadAllText(_selectedScript.Path);

        string extension = System.IO.Path.GetExtension(_selectedScript.Path).ToLowerInvariant();
        int index = Array.IndexOf(Extensions, extension);
        if (index >= 0)
        {
            SelectedEngineIndex = index;
        }
    }

    private void LoadScriptFiles()
    {
        Scripts.Clear();
        if (!Directory.Exists(ScriptsDirectory))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(ScriptsDirectory, "*.*")
                     .Where(f => f.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(f => f))
        {
            Scripts.Add(new ScriptFileInfo(System.IO.Path.GetFileName(file), file));
        }

        AppendLog($"脚本目录共 {Scripts.Count} 个文件");
    }

    private void LoadApiReference()
    {
        foreach (string api in new[]
                 {
                     "tileWidth / tileHeight: 世界宽高, 单位是格",
                     "getSelection(): 当前选区, 返回 x,y,w,h, 无选区返回空串",
                     "setSelection(x, y, w, h): 设定选区范围",
                     "getWorldTitle(): 世界标题",
                     "getWorldTime(): 世界时间, 0 到 1, 0 是凌晨",
                     "getTile(x, y): 读该格的方块编号, 空格返回 0",
                     "setTile(x, y, id): 写该格的方块编号, id 传 0 表示清除",
                     "fill(x, y, w, h, id): 批量填一片矩形",
                     "setWall(x, y, id): 写墙体编号, 传 0 表示清除",
                     "getWall(x, y): 读墙体编号, 无墙返回 0",
                     "countTile(id): 统计某种方块在世界里的数量",
                     "countWall(id): 统计某种墙体在世界里的数量",
                     "setLiquid(x, y, amount, type): 写液体, type 1 水 2 岩浆 3 蜂蜜, amount 传 0 清空",
                     "getNpcCount(): NPC 数量",
                     "getNpcName(index): 读 NPC 名字",
                     "getNpcPosition(index): 读 NPC 坐标, 返回 x,y",
                     "removeNpc(index): 删除该 NPC",
                     "addNpc(name, spriteId, x, y): 新增 NPC",
                     "getChestCount(): 箱子数量",
                     "getChestItemCount(index): 该箱子的物品数",
                     "getChestItemId(chest, slot): 该箱子里某格物品的编号",
                     "setChestItem(chest, slot, id, stack): 往箱子某格写物品",
                     "getSignCount(): 告示牌数量",
                     "getSignText(index): 读告示牌文本",
                     "addSign(x, y, text): 新增告示牌",
                     "distance(x1, y1, x2, y2): 两点距离",
                     "rectContains(x, y, w, h, px, py): 点是否在矩形内, 返回布尔",
                     "log(text): 输出到脚本日志",
                     "JS 与 Lua 两个引擎暴露的函数同名同义",
                 })
        {
            ApiReference.Add(api);
        }
    }

    private void GenerateDocs()
    {
        string path = System.IO.Path.Combine(ScriptsDirectory, "API_REFERENCE.md");
        File.WriteAllLines(path, new[] { "# 脚本 API 参考", string.Empty }
            .Concat(ApiReference.Select(a => "- " + a)));
        AppendLog("已生成 API_REFERENCE.md");
    }

    private void AppendLog(string message)
    {
        Log = $"{DateTime.Now:HH:mm:ss} {message}";
        LogManager.Info($"脚本: {message}");
    }
}
