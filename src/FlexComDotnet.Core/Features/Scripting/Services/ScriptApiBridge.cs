using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Scripting.Models;
using FlexComDotnet.Core.Features.Serial.Helpers;
using FlexComDotnet.Core.Features.Serial.Services;

namespace FlexComDotnet.Core.Features.Scripting.Services;

/// <summary>
/// 脚本 API 桥接实现 - 暴露给 Lua 脚本的 FCom 全局对象
/// </summary>
public class ScriptApiBridge : IScriptApiBridge
{
    private readonly ISerialPortService _serialPortService;
    private readonly IChecksumService _checksumService;
    private string _scriptName = string.Empty;
    private CancellationToken _cancellationToken;

    /// <inheritdoc />
    public event EventHandler<ScriptLogEntry>? LogOutput;

    public ScriptApiBridge(ISerialPortService serialPortService, IChecksumService checksumService)
    {
        _serialPortService = serialPortService;
        _checksumService = checksumService;
    }

    #region 数据发送

    /// <inheritdoc />
    public bool Send(string hexData)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hexData))
                return false;

            var bytes = HexHelper.HexStringToBytes(hexData);
            if (bytes.Length == 0)
                return false;

            return _serialPortService.Send(bytes);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool SendBytes(byte[] data)
    {
        try
        {
            return _serialPortService.Send(data);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool SendText(string text)
    {
        try
        {
            return _serialPortService.Send(text);
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 日志输出

    /// <inheritdoc />
    public void Log(string message)
    {
        EmitLog(message, ScriptLogLevel.Info);
    }

    /// <inheritdoc />
    public void LogDebug(string message)
    {
        EmitLog(message, ScriptLogLevel.Debug);
    }

    /// <inheritdoc />
    public void LogWarning(string message)
    {
        EmitLog(message, ScriptLogLevel.Warning);
    }

    /// <inheritdoc />
    public void LogError(string message)
    {
        EmitLog(message, ScriptLogLevel.Error);
    }

    private void EmitLog(string message, ScriptLogLevel level)
    {
        LogOutput?.Invoke(this, new ScriptLogEntry
        {
            Message = message,
            Level = level,
            ScriptName = _scriptName,
            Timestamp = DateTime.Now
        });
    }

    #endregion

    #region 延时

    /// <inheritdoc />
    public void Delay(int milliseconds)
    {
        if (milliseconds <= 0) return;

        // 使用可取消的 Task.Delay
        try
        {
            Task.Delay(milliseconds, _cancellationToken).Wait(_cancellationToken);
        }
        catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
        {
            throw new OperationCanceledException("脚本延时被取消", _cancellationToken);
        }
    }

    #endregion

    #region 校验计算

    /// <inheritdoc />
    public string Crc16(string hexData)
    {
        return CalculateChecksum(hexData, ChecksumAlgorithmType.Crc16Modbus);
    }

    /// <inheritdoc />
    public string Crc32(string hexData)
    {
        return CalculateChecksum(hexData, ChecksumAlgorithmType.Crc32);
    }

    /// <inheritdoc />
    public string Checksum(string hexData)
    {
        return CalculateChecksum(hexData, ChecksumAlgorithmType.Sum8);
    }

    private string CalculateChecksum(string hexData, ChecksumAlgorithmType type)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hexData))
                return string.Empty;

            var bytes = HexHelper.HexStringToBytes(hexData);
            if (bytes.Length == 0)
                return string.Empty;

            return _checksumService.CalculateAsHexString(type, bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion

    #region 工具方法

    /// <inheritdoc />
    public long GetTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <inheritdoc />
    public byte[] HexToBytes(string hexString)
    {
        return HexHelper.HexStringToBytes(hexString);
    }

    /// <inheritdoc />
    public string BytesToHex(byte[] data)
    {
        return HexHelper.BytesToHexString(data);
    }

    #endregion

    #region 配置方法

    /// <inheritdoc />
    public void SetScriptName(string scriptName)
    {
        _scriptName = scriptName;
    }

    /// <inheritdoc />
    public void SetCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    #endregion
}
