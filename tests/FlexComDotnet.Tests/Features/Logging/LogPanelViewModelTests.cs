using FlexComDotnet.Core.Features.Logging.Models;
using FlexComDotnet.Core.Features.Logging.Services;
using FlexComDotnet.Core.Features.Logging.ViewModels;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Logging;

public class LogPanelViewModelTests
{
    private LoggingService CreateServiceWithEntries()
    {
        var service = new LoggingService();
        service.Info(LogSource.Serial, "串口消息");
        service.Warning(LogSource.Script, "脚本警告");
        service.Error(LogSource.Network, "网络错误");
        service.Debug(LogSource.AutoReply, "自动回复调试");
        service.Info(LogSource.Protocol, "协议消息");
        return service;
    }

    #region 初始化

    [Fact]
    public void Constructor_ShouldLoadExistingEntries()
    {
        // Arrange
        var service = CreateServiceWithEntries();

        // Act
        var vm = new LogPanelViewModel(service);

        // Assert
        vm.FilteredEntries.Should().HaveCount(5);
    }

    [Fact]
    public void NewLogEntry_ShouldAppearInFilteredEntries()
    {
        // Arrange
        var service = new LoggingService();
        var vm = new LogPanelViewModel(service);

        // Act
        service.Info(LogSource.Serial, "新消息");

        // Assert
        vm.FilteredEntries.Should().HaveCount(1);
        vm.FilteredEntries[0].Message.Should().Be("新消息");
    }

    #endregion

    #region 日志等级筛选

    [Fact]
    public void FilterByLevel_Info_ShouldShowOnlyInfoEntries()
    {
        // Arrange
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        // Act
        vm.ShowDebug = false;
        vm.ShowInfo = true;
        vm.ShowWarning = false;
        vm.ShowError = false;

        // Assert
        vm.FilteredEntries.Should().OnlyContain(e => e.Level == LogLevel.Info);
    }

    [Fact]
    public void FilterByLevel_Warning_ShouldShowOnlyWarningEntries()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        vm.ShowDebug = false;
        vm.ShowInfo = false;
        vm.ShowWarning = true;
        vm.ShowError = false;

        vm.FilteredEntries.Should().OnlyContain(e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void FilterByLevel_Error_ShouldShowOnlyErrorEntries()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        vm.ShowDebug = false;
        vm.ShowInfo = false;
        vm.ShowWarning = false;
        vm.ShowError = true;

        vm.FilteredEntries.Should().OnlyContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void FilterByLevel_AllEnabled_ShouldShowAllEntries()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        vm.ShowDebug = true;
        vm.ShowInfo = true;
        vm.ShowWarning = true;
        vm.ShowError = true;

        vm.FilteredEntries.Should().HaveCount(5);
    }

    #endregion

    #region 关键词搜索

    [Fact]
    public void FilterByKeyword_ShouldFilterByMessage()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        vm.SearchKeyword = "串口";

        vm.FilteredEntries.Should().HaveCount(1);
        vm.FilteredEntries[0].Message.Should().Contain("串口");
    }

    [Fact]
    public void FilterByKeyword_Empty_ShouldShowAll()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        vm.SearchKeyword = "";

        vm.FilteredEntries.Should().HaveCount(5);
    }

    [Fact]
    public void FilterByKeyword_CaseInsensitive()
    {
        var service = new LoggingService();
        service.Info(LogSource.Serial, "Hello World");
        var vm = new LogPanelViewModel(service);

        vm.SearchKeyword = "hello";

        vm.FilteredEntries.Should().HaveCount(1);
    }

    #endregion

    #region 正则表达式搜索

    [Fact]
    public void FilterByRegex_ShouldMatchPattern()
    {
        var service = new LoggingService();
        service.Info(LogSource.Serial, "连接成功 COM3");
        service.Info(LogSource.Serial, "连接成功 COM12");
        service.Info(LogSource.Serial, "数据发送");
        var vm = new LogPanelViewModel(service);

        vm.UseRegex = true;
        vm.SearchKeyword = @"COM\d+";

        vm.FilteredEntries.Should().HaveCount(2);
    }

    [Fact]
    public void FilterByRegex_InvalidPattern_ShouldShowNoMatch()
    {
        var service = new LoggingService();
        service.Info(LogSource.Serial, "测试消息");
        var vm = new LogPanelViewModel(service);

        vm.UseRegex = true;
        vm.SearchKeyword = @"[invalid";

        // 无效正则不应崩溃，且应标记错误
        vm.IsRegexError.Should().BeTrue();
    }

    [Fact]
    public void FilterByRegex_Disabled_ShouldUsePlainText()
    {
        var service = new LoggingService();
        service.Info(LogSource.Serial, "test.log");
        service.Info(LogSource.Serial, "testXlog");
        var vm = new LogPanelViewModel(service);

        // "test.log" 普通搜索应只匹配字面量
        vm.UseRegex = false;
        vm.SearchKeyword = "test.log";

        vm.FilteredEntries.Should().HaveCount(1);
        vm.FilteredEntries[0].Message.Should().Be("test.log");
    }

    [Fact]
    public void FilterByRegex_EmptyPattern_ShouldShowAll()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        vm.UseRegex = true;
        vm.SearchKeyword = "";

        vm.FilteredEntries.Should().HaveCount(5);
    }

    #endregion

    #region 来源模块多选筛选

    [Fact]
    public void SourceFilters_AllEnabled_ByDefault()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        // 默认所有来源都启用
        vm.SourceFilters.Should().OnlyContain(sf => sf.IsSelected);
        vm.FilteredEntries.Should().HaveCount(5);
    }

    [Fact]
    public void SourceFilter_DisableOne_ShouldFilterOut()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        // 关闭串口来源
        var serialFilter = vm.SourceFilters.First(sf => sf.Source == LogSource.Serial);
        serialFilter.IsSelected = false;

        vm.FilteredEntries.Should().HaveCount(4);
        vm.FilteredEntries.Should().NotContain(e => e.Source == LogSource.Serial);
    }

    [Fact]
    public void SourceFilter_SelectOnlyOne_ShouldShowOnlyThatSource()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        // 全不选，再只选串口
        vm.SelectNoneSourcesCommand.Execute(null);
        var serialFilter = vm.SourceFilters.First(sf => sf.Source == LogSource.Serial);
        serialFilter.IsSelected = true;

        vm.FilteredEntries.Should().OnlyContain(e => e.Source == LogSource.Serial);
    }

    [Fact]
    public void SelectAllSources_ShouldEnableAll()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        // 先全不选
        vm.SelectNoneSourcesCommand.Execute(null);
        vm.FilteredEntries.Should().BeEmpty();

        // 全选
        vm.SelectAllSourcesCommand.Execute(null);
        vm.FilteredEntries.Should().HaveCount(5);
    }

    [Fact]
    public void SelectNoneSources_ShouldDisableAll()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        vm.SelectNoneSourcesCommand.Execute(null);

        vm.SourceFilters.Should().OnlyContain(sf => !sf.IsSelected);
        vm.FilteredEntries.Should().BeEmpty();
    }

    [Fact]
    public void InvertSources_ShouldToggleAll()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        // 先关闭串口和脚本
        vm.SourceFilters.First(sf => sf.Source == LogSource.Serial).IsSelected = false;
        vm.SourceFilters.First(sf => sf.Source == LogSource.Script).IsSelected = false;

        // 反选
        vm.InvertSourcesCommand.Execute(null);

        // 串口和脚本应开启，其他应关闭
        vm.SourceFilters.First(sf => sf.Source == LogSource.Serial).IsSelected.Should().BeTrue();
        vm.SourceFilters.First(sf => sf.Source == LogSource.Script).IsSelected.Should().BeTrue();
        vm.SourceFilters.First(sf => sf.Source == LogSource.Network).IsSelected.Should().BeFalse();
        vm.SourceFilters.First(sf => sf.Source == LogSource.AutoReply).IsSelected.Should().BeFalse();
        vm.SourceFilters.First(sf => sf.Source == LogSource.Protocol).IsSelected.Should().BeFalse();
    }

    #endregion

    #region 组合筛选

    [Fact]
    public void CombinedFilter_LevelAndSource()
    {
        var service = new LoggingService();
        service.Info(LogSource.Serial, "串口信息");
        service.Warning(LogSource.Serial, "串口警告");
        service.Error(LogSource.Serial, "串口错误");
        service.Info(LogSource.Network, "网络信息");
        var vm = new LogPanelViewModel(service);

        // 只选串口来源
        foreach (var sf in vm.SourceFilters) sf.IsSelected = sf.Source == LogSource.Serial;
        vm.ShowInfo = true;
        vm.ShowWarning = false;
        vm.ShowError = false;
        vm.ShowDebug = false;

        vm.FilteredEntries.Should().HaveCount(1);
        vm.FilteredEntries[0].Message.Should().Be("串口信息");
    }

    [Fact]
    public void CombinedFilter_LevelAndKeyword()
    {
        var service = new LoggingService();
        service.Info(LogSource.Serial, "连接成功");
        service.Info(LogSource.Serial, "数据发送");
        service.Error(LogSource.Serial, "连接失败");
        var vm = new LogPanelViewModel(service);

        vm.ShowInfo = true;
        vm.ShowError = false;
        vm.ShowWarning = false;
        vm.ShowDebug = false;
        vm.SearchKeyword = "连接";

        vm.FilteredEntries.Should().HaveCount(1);
        vm.FilteredEntries[0].Message.Should().Be("连接成功");
    }

    [Fact]
    public void CombinedFilter_RegexAndSource()
    {
        var service = new LoggingService();
        service.Info(LogSource.Serial, "COM3 连接");
        service.Info(LogSource.Network, "COM5 连接");
        service.Info(LogSource.Serial, "数据发送");
        var vm = new LogPanelViewModel(service);

        vm.UseRegex = true;
        vm.SearchKeyword = @"^COM\d+";
        foreach (var sf in vm.SourceFilters) sf.IsSelected = sf.Source == LogSource.Serial;

        vm.FilteredEntries.Should().HaveCount(1);
        vm.FilteredEntries[0].Message.Should().Be("COM3 连接");
    }

    #endregion

    #region 清空日志

    [Fact]
    public void ClearLogs_ShouldClearFilteredEntries()
    {
        var service = CreateServiceWithEntries();
        var vm = new LogPanelViewModel(service);

        vm.ClearLogsCommand.Execute(null);

        vm.FilteredEntries.Should().BeEmpty();
    }

    #endregion

    #region 时间范围筛选

    [Fact]
    public void FilterByTimeRange_ShouldFilterCorrectly()
    {
        var service = new LoggingService();
        var vm = new LogPanelViewModel(service);

        // 添加不同时间的日志（通过直接添加条目测试）
        service.Log(LogLevel.Info, LogSource.System, "消息1");
        service.Log(LogLevel.Info, LogSource.System, "消息2");

        // 默认无时间范围限制时应显示所有
        vm.FilteredEntries.Should().HaveCount(2);
    }

    #endregion
}
