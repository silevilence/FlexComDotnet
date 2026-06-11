namespace FlexComDotnet.Core.Features.Serial.Services;

/// <summary>
/// 帧定界器 — 基于字节间超时和最大帧长度将字节流切分为帧
/// </summary>
internal class FrameDelimiter
{
    private readonly int _frameIntervalMs;
    private readonly int _maxFrameBytes;
    private readonly List<byte> _buffer = [];
    private DateTime _lastByteTime = DateTime.MinValue;
    private bool _hasLastByteTime;

    /// <summary>
    /// 帧完成事件：当一帧组装完毕时触发
    /// </summary>
    public event Action<byte[]>? FrameCompleted;

    public FrameDelimiter(int frameIntervalMs, int maxFrameBytes)
    {
        _frameIntervalMs = frameIntervalMs;
        _maxFrameBytes = maxFrameBytes;
    }

    /// <summary>
    /// 追加一个字节到帧缓冲区
    /// </summary>
    public void AppendByte(byte b, DateTime timestamp)
    {
        // 检查字节间超时
        if (_hasLastByteTime && _buffer.Count > 0)
        {
            var interval = (timestamp - _lastByteTime).TotalMilliseconds;
            if (interval > _frameIntervalMs)
            {
                EmitFrame();
            }
        }

        _buffer.Add(b);
        _lastByteTime = timestamp;
        _hasLastByteTime = true;

        // 检查最大帧长度
        if (_buffer.Count >= _maxFrameBytes)
        {
            EmitFrame();
        }
    }

    /// <summary>
    /// 强制产出当前缓冲区中的帧并触发 FrameCompleted 事件
    /// </summary>
    public void Flush()
    {
        if (_buffer.Count > 0)
        {
            var frame = _buffer.ToArray();
            _buffer.Clear();
            FrameCompleted?.Invoke(frame);
        }
    }

    /// <summary>
    /// 取出当前缓冲区中的帧（不触发事件），由调用方自行处理
    /// </summary>
    public byte[]? TryFlush()
    {
        if (_buffer.Count == 0) return null;
        var frame = _buffer.ToArray();
        _buffer.Clear();
        return frame;
    }

    /// <summary>
    /// 重置定界器状态（清空缓冲区）
    /// </summary>
    public void Reset()
    {
        _buffer.Clear();
        _hasLastByteTime = false;
    }

    private void EmitFrame()
    {
        if (_buffer.Count == 0) return;
        var frame = _buffer.ToArray();
        _buffer.Clear();
        FrameCompleted?.Invoke(frame);
    }
}
