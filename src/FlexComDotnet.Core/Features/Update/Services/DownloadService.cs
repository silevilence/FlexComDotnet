using System.Diagnostics;
using FlexComDotnet.Core.Features.Update.Models;

namespace FlexComDotnet.Core.Features.Update.Services;

/// <summary>
/// 下载服务实现
/// </summary>
public class DownloadService : IDownloadService
{
    private const int BufferSize = 81920; // 80KB buffer
    private readonly HttpClient _httpClient;

    public DownloadService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    public async Task<bool> DownloadFileAsync(
        string url,
        string destinationPath,
        Action<DownloadProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 确保目标目录存在
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var response = await _httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            var bytesReceived = 0L;
            var lastProgressTime = Stopwatch.GetTimestamp();
            var lastBytesReceived = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous);

            var buffer = new byte[BufferSize];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesReceived += bytesRead;

                // 计算下载速度 (每 500ms 更新一次)
                var currentTime = Stopwatch.GetTimestamp();
                var elapsedSeconds = (double)(currentTime - lastProgressTime) / Stopwatch.Frequency;

                if (elapsedSeconds >= 0.5 || bytesReceived == totalBytes)
                {
                    var bytesPerSecond = (long)((bytesReceived - lastBytesReceived) / elapsedSeconds);
                    lastProgressTime = currentTime;
                    lastBytesReceived = bytesReceived;

                    progressCallback?.Invoke(new DownloadProgress
                    {
                        BytesReceived = bytesReceived,
                        TotalBytes = totalBytes,
                        BytesPerSecond = bytesPerSecond
                    });
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            // 取消下载时删除不完整的文件
            TryDeleteFile(destinationPath);
            return false;
        }
        catch (Exception)
        {
            TryDeleteFile(destinationPath);
            return false;
        }
    }

    /// <summary>
    /// 获取临时下载目录
    /// </summary>
    public string GetDownloadDirectory()
    {
        var downloadDir = Path.Combine(Path.GetTempPath(), "FlexComDotnet", "Downloads");

        if (!Directory.Exists(downloadDir))
        {
            Directory.CreateDirectory(downloadDir);
        }

        return downloadDir;
    }

    /// <summary>
    /// 尝试删除文件
    /// </summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 忽略删除失败
        }
    }

    /// <summary>
    /// 创建默认的 HttpClient
    /// </summary>
    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30) // 大文件下载超时设置
        };
        client.DefaultRequestHeaders.Add("User-Agent", "FlexComDotnet-Updater");
        return client;
    }
}
