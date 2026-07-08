namespace FlexComDotnet.Core.Features.Driver.Services;

/// <summary>
/// 驱动生命周期操作结果。
/// </summary>
public sealed record DriverLifecycleResult(bool Succeeded, string Message)
{
    public static DriverLifecycleResult Success(string message) => new(true, message);

    public static DriverLifecycleResult Failure(string message) => new(false, message);
}
