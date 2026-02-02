using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FlexComDotnet.Core.Features.Serial.ViewModels;

namespace FlexComDotnet;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // 设置串口配置面板的 DataContext
        SerialConfigPanel.DataContext = App.Services.GetRequiredService<SerialConfigViewModel>();
        
        // 设置收发区域的 DataContext
        SerialCommunicationPanel.DataContext = App.Services.GetRequiredService<SerialCommunicationViewModel>();
    }
}