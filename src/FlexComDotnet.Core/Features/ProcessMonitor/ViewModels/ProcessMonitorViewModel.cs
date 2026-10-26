using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.ProcessMonitor.ViewModels;

public partial class ProcessMonitorViewModel : ObservableObject
{
    private readonly IConfigurationService _configuration;
    [ObservableProperty] private string _executablePath = string.Empty;
    [ObservableProperty] private string _arguments = string.Empty;
    [ObservableProperty] private string _portFilter = string.Empty;
    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private string _operationStatus = "API Hook 采集组件尚未通过可行性验收，采集不可用。";

    public ProcessMonitorViewModel(IConfigurationService configuration)
    {
        _configuration = configuration;
        var config = configuration.Load().ProcessMonitor;
        ExecutablePath = config.ExecutablePath;
        Arguments = config.Arguments;
        PortFilter = config.PortFilter;
    }

    [RelayCommand]
    private void SaveConfiguration()
    {
        var config = _configuration.Load();
        config.ProcessMonitor.ExecutablePath = ExecutablePath;
        config.ProcessMonitor.Arguments = Arguments;
        config.ProcessMonitor.PortFilter = PortFilter;
        OperationStatus = _configuration.Save(config) ? "进程监控偏好已保存。" : "保存失败，请检查配置目录权限。";
    }
}
