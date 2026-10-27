using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection.PortableExecutable;
using FlexComDotnet.Core.Features.ProcessMonitor.Models;

namespace FlexComDotnet.Core.Features.ProcessMonitor.Services;

public sealed class ProcessMonitorTargets(string? nativeRoot = null)
{
    public string NativeRoot { get; } = nativeRoot ?? Path.Combine(AppContext.BaseDirectory, "ProcessHook");

    public ProcessMonitorTarget GetTarget(int processId)
    {
        using var process = Process.GetProcessById(processId);
        if (!IsWow64Process2(process.Handle, out var machine, out var native)) throw new Win32Exception();
        var architecture = (machine == 0 ? native : machine) switch { 0x14c => "x86", 0x8664 => "x64", _ => "不支持" };
        return new(processId, process.StartTime.ToUniversalTime().ToFileTimeUtc(), process.ProcessName, architecture);
    }

    public async Task<ProcessMonitorLaunch> PrepareLaunchAsync(string executable, string arguments, CancellationToken cancellationToken = default)
    {
        executable = Path.GetFullPath(executable);
        using var stream = File.OpenRead(executable);
        using var image = new PEReader(stream);
        var architecture = image.PEHeaders.CoffHeader.Machine switch
        { Machine.Amd64 => "x64", Machine.I386 => "x86", _ => throw new NotSupportedException("仅支持 x86/x64 可执行文件。") };
        // AnyCPU managed applications run using the OS architecture unless they explicitly prefer 32-bit.
        var cor = image.PEHeaders.CorHeader;
        if (architecture == "x86" && cor is not null && (cor.Flags & (CorFlags.Requires32Bit | CorFlags.Prefers32Bit)) == 0 && Environment.Is64BitOperatingSystem) architecture = "x64";
        var helperPath = Path.Combine(NativeRoot, architecture, "FlexCom.ProcessHook.Helper.exe");
        var start = new ProcessStartInfo(helperPath) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "--launch", executable, arguments, Path.Combine(NativeRoot, architecture, "FlexCom.ProcessHook.dll") }) start.ArgumentList.Add(argument);
        var helper = Process.Start(start) ?? throw new IOException("无法启动采集助手。");
        try
        {
            var line = await helper.StandardOutput.ReadLineAsync(cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            var parts = line?.Split('|');
            if (parts is not { Length: 2 } || !int.TryParse(parts[0], out var pid) || !long.TryParse(parts[1], out var time)) throw new IOException("准备启动失败：" + await helper.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false));
            return new(helper, new(pid, time, Path.GetFileNameWithoutExtension(executable), architecture));
        }
        catch { helper.StandardInput.Close(); await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false); helper.Dispose(); throw; }
    }

    public IReadOnlyList<ProcessMonitorTarget> Enumerate()
    {
        var results = new List<ProcessMonitorTarget>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try { results.Add(GetTarget(process.Id)); }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or ArgumentException)
                { /* Inaccessible and exited processes are not offered as valid targets. */ }
            }
        }
        return results.OrderBy(p => p.Name).ThenBy(p => p.ProcessId).ToArray();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);
}
