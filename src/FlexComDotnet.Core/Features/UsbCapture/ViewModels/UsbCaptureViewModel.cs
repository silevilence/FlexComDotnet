using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.UsbCapture.ViewModels;

public partial class UsbCaptureViewModel : ObservableObject
{
    private readonly IConfigurationService _configuration;
    [ObservableProperty] private string _deviceFilter = string.Empty;
    public bool IsCapturing => false;
    [ObservableProperty] private string _operationStatus = "USB 抓包尚未实现。实时采集需要 USBPcap 捕获驱动；不会安装 SerialMonitor 驱动。";

    public UsbCaptureViewModel(IConfigurationService configuration)
    {
        _configuration = configuration;
        DeviceFilter = configuration.Load().UsbCapture.DeviceFilter;
    }

    [RelayCommand]
    private void SaveConfiguration()
    {
        var config = _configuration.Load();
        config.UsbCapture.DeviceFilter = DeviceFilter;
        OperationStatus = _configuration.Save(config) ? "USB 设备偏好已保存。" : "保存失败，请检查配置目录权限。";
    }
}
