using FlexComDotnet.Core.Features.Protocol.Models;
using FlexComDotnet.Core.Features.Visualization.Models;

namespace FlexComDotnet.Core.Features.Visualization.Services;

/// <summary>
/// 数据可视化服务接口
/// </summary>
public interface IVisualizationService
{
    #region 通道管理

    /// <summary>
    /// 添加通道
    /// </summary>
    void AddChannel(ChannelConfig channel);

    /// <summary>
    /// 移除通道
    /// </summary>
    bool RemoveChannel(string channelId);

    /// <summary>
    /// 更新通道配置
    /// </summary>
    void UpdateChannel(ChannelConfig channel);

    /// <summary>
    /// 获取所有通道配置
    /// </summary>
    IReadOnlyList<ChannelConfig> GetChannels();

    /// <summary>
    /// 获取指定通道配置
    /// </summary>
    ChannelConfig? GetChannel(string channelId);

    #endregion

    #region 数据管理

    /// <summary>
    /// 推送解析帧数据（从协议解析引擎接收）
    /// </summary>
    void PushData(ParsedFrame frame);

    /// <summary>
    /// 喂入原始字节数据，自动进行帧提取与解析
    /// </summary>
    void FeedRawData(byte[] data);

    /// <summary>
    /// 获取指定通道的数据点列表
    /// </summary>
    IReadOnlyList<ChartDataPoint> GetChannelData(string channelId);

    /// <summary>
    /// 获取所有通道的数据点
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<ChartDataPoint>> GetAllData();

    /// <summary>
    /// 清除所有数据
    /// </summary>
    void ClearData();

    /// <summary>
    /// 清除指定通道的数据
    /// </summary>
    void ClearChannelData(string channelId);

    #endregion

    #region 状态管理

    /// <summary>
    /// 是否正在采集数据
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 开始数据采集
    /// </summary>
    void Start();

    /// <summary>
    /// 停止数据采集
    /// </summary>
    void Stop();

    #endregion

    #region 数据导出

    /// <summary>
    /// 导出数据为 CSV 文件
    /// </summary>
    void ExportToCsv(string filePath);

    #endregion

    #region 配置

    /// <summary>
    /// 每通道最大数据点数
    /// </summary>
    int MaxDataPoints { get; set; }

    /// <summary>
    /// 选中的协议解析器名称
    /// </summary>
    string? SelectedParserName { get; set; }

    #endregion

    #region 事件

    /// <summary>
    /// 数据点新增事件
    /// </summary>
    event EventHandler<DataPointAddedEventArgs>? DataPointAdded;

    /// <summary>
    /// 数据清除事件
    /// </summary>
    event EventHandler? DataCleared;

    /// <summary>
    /// 状态变更事件
    /// </summary>
    event EventHandler<VisualizationStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 帧提取失败事件（接收到数据但协议不匹配）
    /// </summary>
    event EventHandler<ExtractionFailedEventArgs>? ExtractionFailed;

    #endregion
}
