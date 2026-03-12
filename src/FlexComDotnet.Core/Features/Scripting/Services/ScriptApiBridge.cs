using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Protocol.Services;
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
    private readonly IProtocolParserService? _protocolParserService;
    private string _scriptName = string.Empty;
    private CancellationToken _cancellationToken;

    /// <inheritdoc />
    public event EventHandler<ScriptLogEntry>? LogOutput;

    public ScriptApiBridge(ISerialPortService serialPortService, IChecksumService checksumService, IProtocolParserService? protocolParserService = null)
    {
        _serialPortService = serialPortService;
        _checksumService = checksumService;
        _protocolParserService = protocolParserService;
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

    #region 协议 API

    /// <inheritdoc />
    public string[] GetProtocols()
    {
        if (_protocolParserService == null)
            return [];

        return _protocolParserService.GetAllDefinitions()
            .Select(d => d.Name)
            .ToArray();
    }

    /// <inheritdoc />
    public Dictionary<string, object>[] GetProtocolFields(string protocolName)
    {
        if (_protocolParserService == null || string.IsNullOrEmpty(protocolName))
            return [];

        var parser = _protocolParserService.GetParser(protocolName);
        if (parser == null)
            return [];

        var definition = parser.Definition;
        var result = new List<Dictionary<string, object>>();

        foreach (var field in definition.Fields.Where(f => f.IsEnabled))
        {
            result.Add(new Dictionary<string, object>
            {
                ["name"] = field.Name,
                ["description"] = field.Description,
                ["dataType"] = field.DataType.ToString(),
                ["length"] = field.Length,
                ["startIndex"] = field.StartIndex
            });
        }

        return result.ToArray();
    }

    /// <inheritdoc />
    public Dictionary<string, object> Parse(string protocolName, string hexFrame)
    {
        var errorResult = new Dictionary<string, object>();

        if (_protocolParserService == null)
        {
            errorResult["error"] = "协议服务不可用";
            return errorResult;
        }

        if (string.IsNullOrEmpty(protocolName) || string.IsNullOrEmpty(hexFrame))
        {
            errorResult["error"] = "协议名称或帧数据不能为空";
            return errorResult;
        }

        try
        {
            var bytes = HexHelper.HexStringToBytes(hexFrame);
            if (bytes.Length == 0)
            {
                errorResult["error"] = "无效的十六进制数据";
                return errorResult;
            }

            var parsed = _protocolParserService.Parse(protocolName, bytes);
            if (!parsed.IsValid)
            {
                errorResult["error"] = parsed.ErrorMessage ?? "解析失败";
                return errorResult;
            }

            var result = new Dictionary<string, object>
            {
                ["isValid"] = true,
                ["checksumValid"] = parsed.ChecksumValid,
                ["protocolName"] = parsed.ProtocolName
            };

            foreach (var field in parsed.Fields)
            {
                result[field.Name] = field.Value ?? field.DisplayValue;
            }

            return result;
        }
        catch (Exception ex)
        {
            errorResult["error"] = ex.Message;
            return errorResult;
        }
    }

    /// <inheritdoc />
    public string Build(string protocolName, NLua.LuaTable fieldValuesTable)
    {
        if (_protocolParserService == null || string.IsNullOrEmpty(protocolName))
            return string.Empty;

        try
        {
            // 将 LuaTable 转换为 Dictionary
            var fieldValues = new Dictionary<string, object>();
            foreach (var key in fieldValuesTable.Keys)
            {
                var value = fieldValuesTable[key];
                if (key is string keyStr && value != null)
                {
                    fieldValues[keyStr] = value;
                }
            }

            var parser = _protocolParserService.GetParser(protocolName);
            if (parser == null)
                return string.Empty;

            var frame = parser.BuildFrame(fieldValues);
            return HexHelper.BytesToHexString(frame);
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion
}
