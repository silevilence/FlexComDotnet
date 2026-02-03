namespace FlexComDotnet.Core.Features.AutoReply.Models;

/// <summary>
/// 顺序回复配置
/// </summary>
public class SequentialReplyConfig
{
    /// <summary>
    /// 预设帧列表
    /// </summary>
    public List<SequentialFrame> Frames { get; set; } = [];

    /// <summary>
    /// 是否循环回复
    /// </summary>
    public bool EnableLoop { get; set; } = true;

    /// <summary>
    /// 当前回复索引
    /// </summary>
    public int CurrentIndex { get; set; }
}
