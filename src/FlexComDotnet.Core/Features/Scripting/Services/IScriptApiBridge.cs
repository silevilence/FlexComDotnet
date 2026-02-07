using FlexComDotnet.Core.Features.Scripting.Models;

namespace FlexComDotnet.Core.Features.Scripting.Services;

/// <summary>
/// 脚本 API 桥接接口 - 暴露给 Lua 脚本的 FCom 全局对象
/// </summary>
public interface IScriptApiBridge
{
    /// <summary>
    /// 日志输出事件
    /// </summary>
    event EventHandler<ScriptLogEntry>? LogOutput;

    /// <summary>
    /// 发送数据（Hex 字符串）
    /// </summary>
    /// <param name="hexData">十六进制字符串 (如 "FF 01 02")</param>
    /// <returns>是否发送成功</returns>
    bool Send(string hexData);

    /// <summary>
    /// 发送原始字节数据
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <returns>是否发送成功</returns>
    bool SendBytes(byte[] data);

    /// <summary>
    /// 发送文本数据
    /// </summary>
    /// <param name="text">文本内容</param>
    /// <returns>是否发送成功</returns>
    bool SendText(string text);

    /// <summary>
    /// 输出日志消息
    /// </summary>
    /// <param name="message">日志消息</param>
    void Log(string message);

    /// <summary>
    /// 输出调试日志
    /// </summary>
    /// <param name="message">调试消息</param>
    void LogDebug(string message);

    /// <summary>
    /// 输出警告日志
    /// </summary>
    /// <param name="message">警告消息</param>
    void LogWarning(string message);

    /// <summary>
    /// 输出错误日志
    /// </summary>
    /// <param name="message">错误消息</param>
    void LogError(string message);

    /// <summary>
    /// 延时（毫秒）
    /// </summary>
    /// <param name="milliseconds">延时毫秒数</param>
    void Delay(int milliseconds);

    /// <summary>
    /// 计算 CRC16-Modbus
    /// </summary>
    /// <param name="hexData">十六进制字符串</param>
    /// <returns>CRC16 结果 (十六进制字符串)</returns>
    string Crc16(string hexData);

    /// <summary>
    /// 计算 CRC32
    /// </summary>
    /// <param name="hexData">十六进制字符串</param>
    /// <returns>CRC32 结果 (十六进制字符串)</returns>
    string Crc32(string hexData);

    /// <summary>
    /// 计算 Checksum (Sum8)
    /// </summary>
    /// <param name="hexData">十六进制字符串</param>
    /// <returns>Checksum 结果 (十六进制字符串)</returns>
    string Checksum(string hexData);

    /// <summary>
    /// 获取当前时间戳（毫秒）
    /// </summary>
    /// <returns>Unix 时间戳（毫秒）</returns>
    long GetTimestamp();

    /// <summary>
    /// 将十六进制字符串转换为字节数组
    /// </summary>
    /// <param name="hexString">十六进制字符串</param>
    /// <returns>字节数组</returns>
    byte[] HexToBytes(string hexString);

    /// <summary>
    /// 将字节数组转换为十六进制字符串
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <returns>十六进制字符串</returns>
    string BytesToHex(byte[] data);

    /// <summary>
    /// 设置脚本名称（供日志使用）
    /// </summary>
    /// <param name="scriptName">脚本名称</param>
    void SetScriptName(string scriptName);

    /// <summary>
    /// 设置取消令牌（供 delay 等操作检查中断）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    void SetCancellationToken(CancellationToken cancellationToken);
}
