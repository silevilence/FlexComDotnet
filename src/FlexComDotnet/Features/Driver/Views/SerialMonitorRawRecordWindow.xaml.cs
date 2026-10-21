using System.Windows;
using FlexComDotnet.Core.Features.Driver.ViewModels;

namespace FlexComDotnet.Features.Driver.Views;

public partial class SerialMonitorRawRecordWindow : Window
{
    public SerialMonitorRawRecordWindow(SerialMonitorRecordViewModel record)
    {
        InitializeComponent();
        DataContext = record;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
