using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Serial.ViewModels;

/// <summary>
/// 用于 UI 绑定的指令项包装类
/// </summary>
public partial class CommandItemViewModel : ObservableObject
{
    private readonly CommandItem _model;

    public CommandItemViewModel(CommandItem model)
    {
        _model = model;
    }

    /// <summary>
    /// 原始模型
    /// </summary>
    public CommandItem Model => _model;

    /// <summary>
    /// ID
    /// </summary>
    public int Id => _model.Id;

    /// <summary>
    /// 指令名称
    /// </summary>
    public string Name
    {
        get => _model.Name;
        set
        {
            if (_model.Name != value)
            {
                _model.Name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 指令内容
    /// </summary>
    public string Content
    {
        get => _model.Content;
        set
        {
            if (_model.Content != value)
            {
                _model.Content = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 指令描述
    /// </summary>
    public string Description
    {
        get => _model.Description;
        set
        {
            if (_model.Description != value)
            {
                _model.Description = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 是否使用 Hex 模式
    /// </summary>
    public bool IsHexMode
    {
        get => _model.IsHexMode;
        set
        {
            if (_model.IsHexMode != value)
            {
                _model.IsHexMode = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled
    {
        get => _model.IsEnabled;
        set
        {
            if (_model.IsEnabled != value)
            {
                _model.IsEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder
    {
        get => _model.SortOrder;
        set
        {
            if (_model.SortOrder != value)
            {
                _model.SortOrder = value;
                OnPropertyChanged();
            }
        }
    }
}

/// <summary>
/// 指令列表 ViewModel
/// </summary>
public partial class CommandListViewModel : ObservableObject, IDisposable
{
    private readonly ICommandStorageService _storageService;
    private readonly ISerialPortService _serialPortService;
    private bool _disposed;

    /// <summary>
    /// 指令列表
    /// </summary>
    public ObservableCollection<CommandItemViewModel> Commands { get; } = [];

    /// <summary>
    /// 当前选中的指令
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommandCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendCommandCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private CommandItemViewModel? _selectedCommand;

    /// <summary>
    /// 是否处于编辑模式
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelEditCommand))]
    private bool _isEditing;

    /// <summary>
    /// 编辑中的指令名称
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditCommand))]
    private string _editName = string.Empty;

    /// <summary>
    /// 编辑中的指令内容
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditCommand))]
    private string _editContent = string.Empty;

    /// <summary>
    /// 编辑中的指令描述
    /// </summary>
    [ObservableProperty]
    private string _editDescription = string.Empty;

    /// <summary>
    /// 编辑中的 Hex 模式
    /// </summary>
    [ObservableProperty]
    private bool _editIsHexMode;

    /// <summary>
    /// 是否创建新指令（而非编辑现有指令）
    /// </summary>
    [ObservableProperty]
    private bool _isCreating;

    /// <summary>
    /// 编辑/创建面板标题
    /// </summary>
    public string EditPanelTitle => IsCreating ? "添加指令" : "编辑指令";

    /// <summary>
    /// 是否已连接串口
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommandCommand))]
    private bool _isConnected;

    /// <summary>
    /// 发送状态消息
    /// </summary>
    [ObservableProperty]
    private string _sendStatus = string.Empty;

    /// <summary>
    /// 发送数据事件，用于通知主 ViewModel 发送数据
    /// </summary>
    public event EventHandler<byte[]>? SendDataRequested;

    public CommandListViewModel(ICommandStorageService storageService, ISerialPortService serialPortService)
    {
        _storageService = storageService;
        _serialPortService = serialPortService;

        // 订阅连接状态变化
        _serialPortService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = _serialPortService.IsConnected;

        // 加载指令列表
        LoadCommands();
    }

    /// <summary>
    /// 加载指令列表
    /// </summary>
    private void LoadCommands()
    {
        Commands.Clear();
        var items = _storageService.GetAll();
        foreach (var item in items)
        {
            Commands.Add(new CommandItemViewModel(item));
        }
    }

    /// <summary>
    /// 添加新指令
    /// </summary>
    [RelayCommand]
    private void AddCommand()
    {
        EditName = string.Empty;
        EditContent = string.Empty;
        EditDescription = string.Empty;
        EditIsHexMode = false;
        IsCreating = true;
        IsEditing = true;
        OnPropertyChanged(nameof(EditPanelTitle));
    }

    /// <summary>
    /// 编辑选中的指令
    /// </summary>
    [RelayCommand]
    private void EditCommand()
    {
        if (SelectedCommand == null) return;

        EditName = SelectedCommand.Name;
        EditContent = SelectedCommand.Content;
        EditDescription = SelectedCommand.Description;
        EditIsHexMode = SelectedCommand.IsHexMode;
        IsCreating = false;
        IsEditing = true;
        OnPropertyChanged(nameof(EditPanelTitle));
    }

    /// <summary>
    /// 保存编辑
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveEdit))]
    private void SaveEdit()
    {
        if (IsCreating)
        {
            // 创建新指令
            var newItem = new CommandItem
            {
                Name = EditName,
                Content = EditContent,
                Description = EditDescription,
                IsHexMode = EditIsHexMode
            };

            var id = _storageService.Add(newItem);
            newItem.Id = id;
            Commands.Add(new CommandItemViewModel(newItem));
        }
        else if (SelectedCommand != null)
        {
            // 更新现有指令
            SelectedCommand.Name = EditName;
            SelectedCommand.Content = EditContent;
            SelectedCommand.Description = EditDescription;
            SelectedCommand.IsHexMode = EditIsHexMode;
            _storageService.Update(SelectedCommand.Model);
        }

        IsEditing = false;
    }

    private bool CanSaveEdit() => IsEditing && !string.IsNullOrWhiteSpace(EditName) && !string.IsNullOrWhiteSpace(EditContent);

    /// <summary>
    /// 取消编辑
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelEdit))]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    private bool CanCancelEdit() => IsEditing;

    /// <summary>
    /// 删除指令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteCommand))]
    private void DeleteCommand()
    {
        if (SelectedCommand == null) return;

        _storageService.Delete(SelectedCommand.Id);
        Commands.Remove(SelectedCommand);
        SelectedCommand = null;
    }

    private bool CanDeleteCommand() => SelectedCommand != null;

    /// <summary>
    /// 发送指令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendCommand))]
    private void SendCommand()
    {
        if (SelectedCommand == null || !IsConnected) return;

        var command = SelectedCommand;
        byte[] data;

        if (command.IsHexMode)
        {
            if (!HexHelper.IsValidHexString(command.Content))
            {
                SendStatus = "发送失败: 无效的十六进制格式";
                return;
            }
            data = HexHelper.HexStringToBytes(command.Content);
        }
        else
        {
            data = HexHelper.AsciiStringToBytes(command.Content);
        }

        if (data.Length == 0)
        {
            SendStatus = "发送失败: 内容为空";
            return;
        }

        // 触发发送事件
        SendDataRequested?.Invoke(this, data);
        SendStatus = $"发送成功: {command.Name}";
    }

    private bool CanSendCommand() => SelectedCommand != null && IsConnected && SelectedCommand.IsEnabled;

    /// <summary>
    /// 双击发送指令
    /// </summary>
    public void SendCommandByDoubleClick(CommandItemViewModel command)
    {
        if (!IsConnected || !command.IsEnabled) return;

        SelectedCommand = command;
        if (CanSendCommand())
        {
            SendCommand();
        }
    }

    /// <summary>
    /// 上移指令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedCommand == null) return;

        var index = Commands.IndexOf(SelectedCommand);
        if (index <= 0) return;

        Commands.Move(index, index - 1);
        UpdateSortOrders();
    }

    private bool CanMoveUp() => SelectedCommand != null && Commands.IndexOf(SelectedCommand) > 0;

    /// <summary>
    /// 下移指令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedCommand == null) return;

        var index = Commands.IndexOf(SelectedCommand);
        if (index < 0 || index >= Commands.Count - 1) return;

        Commands.Move(index, index + 1);
        UpdateSortOrders();
    }

    private bool CanMoveDown() => SelectedCommand != null && Commands.IndexOf(SelectedCommand) < Commands.Count - 1;

    /// <summary>
    /// 更新排序顺序到数据库
    /// </summary>
    private void UpdateSortOrders()
    {
        for (int i = 0; i < Commands.Count; i++)
        {
            Commands[i].SortOrder = i + 1;
        }
        _storageService.UpdateSortOrder(Commands.Select(c => c.Model));
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _serialPortService.ConnectionStateChanged -= OnConnectionStateChanged;
            }
            _disposed = true;
        }
    }
}
