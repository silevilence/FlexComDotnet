using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlexComDotnet.Core.Features.Capture.Models;
using FlexComDotnet.Core.Features.ProcessMonitor.Models;
using FlexComDotnet.Core.Features.ProcessMonitor.Services;

// Fixed-purpose acceptance runner; only owned children and the three dedicated port pairs.
internal static class Program
{
    private static string s_output = "";
    private static readonly object s_logLock = new();
    private static int s_failures;
    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private static string? s_reportUrl;
    private static readonly byte[] s_payload = [0, 1, 2, 0x7f, 0x80, 0xfe, 0xff];

    private static async Task<int> Main(string[] args)
    {
        if (args.FirstOrDefault() == "--managed-worker") return ManagedWorker(args[1]);
        if (args.FirstOrDefault() != "--run")
        {
            var destination = Path.Combine(Path.GetTempPath(), "FlexComVmTests-" + Guid.NewGuid().ToString("N"));
            var reports = Path.Combine(AppContext.BaseDirectory, "results-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(reports);
            File.WriteAllText(Path.Combine(reports, "progress.txt"), "Copying fixed test bundle to VM: " + destination + "\n");
            CopyDirectory(AppContext.BaseDirectory, destination);
            var start = new ProcessStartInfo(Path.Combine(destination, "ProcessMonitor.VmTests.exe")) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--run"); start.ArgumentList.Add(reports);
            Process.Start(start)?.Dispose();
            return 0;
        }
        s_output = args[1];
        var endpointFile = Path.Combine(AppContext.BaseDirectory, "report-url.txt");
        if (File.Exists(endpointFile)) s_reportUrl = File.ReadAllText(endpointFile).Trim();
        Log($"START UTC={DateTime.UtcNow:O} OS={Environment.OSVersion} architecture={RuntimeInformation.OSArchitecture} local={AppContext.BaseDirectory}");
        Log("Dedicated ports COM10/20, COM11/21, COM12/22; system security and drivers unchanged.");
        try
        {
            foreach (var architecture in new[] { "x64", "x86" })
                await Case($"native full serial {architecture}", () => RunProcess(Path.Combine(AppContext.BaseDirectory, "native", architecture, "NativeTests.exe"), ["--serial"], 90));
            foreach (var architecture in new[] { "x64", "x86" })
                await Case($".NET SerialPort {architecture}", () => ManagedCase(architecture));
            Log("SHORT SERIAL TESTS COMPLETE. Both architectures now run on separate port pairs concurrently.");
            await Task.WhenAll(Case("native lifecycle/performance x64", () => NativeCase("x64", "COM10", "COM20")),
                Case("native lifecycle/performance x86", () => NativeCase("x86", "COM11", "COM21")));
        }
        catch (Exception error) { Log("RUNNER ERROR " + error); Interlocked.Increment(ref s_failures); }
        Log($"FINISHED failures={s_failures} UTC={DateTime.UtcNow:O}");
        File.WriteAllText(Path.Combine(s_output, "finished.txt"), $"failures={s_failures}");
        foreach (var file in Directory.GetFiles(s_output))
            Upload(Path.GetFileName(file), File.ReadAllBytes(file));
        return s_failures == 0 ? 0 : 1;
    }
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var directory in Directory.GetDirectories(source))
            if (!Path.GetFileName(directory).StartsWith("results-")) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
    private static void Log(string message)
    {
        lock (s_logLock)
        {
            var line = $"{DateTime.UtcNow:HH:mm:ss} {message}\n";
            File.AppendAllText(Path.Combine(s_output, "progress.txt"), line);
            Upload("events.log", System.Text.Encoding.UTF8.GetBytes(line));
        }
    }
    private static void Upload(string name, byte[] data)
    {
        if (s_reportUrl is null) return;
        try { using var response = s_http.PostAsync(s_reportUrl + Uri.EscapeDataString(name), new ByteArrayContent(data)).GetAwaiter().GetResult(); }
        catch (HttpRequestException) { /* Local evidence remains available if the host link is lost. */ }
        catch (TaskCanceledException) { }
    }
    private static async Task Case(string name, Func<Task> action)
    {
        Log("BEGIN " + name);
        try { await action(); Log("PASS " + name); }
        catch (Exception error) { Interlocked.Increment(ref s_failures); Log("FAIL " + name + ": " + error); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static async Task RunProcess(string executable, string[] arguments, int seconds)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
        try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(seconds)); }
        finally { if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(); } }
        Log(Path.GetFileName(executable) + ": " + await output + await error);
        var nativeReport = Path.Combine(Path.GetDirectoryName(executable)!, "native-results.txt");
        if (File.Exists(nativeReport)) File.Copy(nativeReport, Path.Combine(s_output, "native-" + new DirectoryInfo(Path.GetDirectoryName(executable)!).Name + ".txt"), true);
        Require(child.ExitCode == 0, "test process exit=" + child.ExitCode);
    }
    private static async Task WaitFile(string directory, string filename, Process process, int seconds = 20)
    {
        var timer = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(directory, filename)))
        {
            if (File.Exists(Path.Combine(directory, "error.txt"))) throw new IOException(await File.ReadAllTextAsync(Path.Combine(directory, "error.txt")));
            Require(!process.HasExited, "workload exited");
            Require(timer.Elapsed.TotalSeconds < seconds, "timeout waiting for " + filename);
            await Task.Delay(20);
        }
        await Task.Delay(30);
    }
    private static async Task Signal(string directory, string filename, string content = "go")
    {
        var path = Path.Combine(directory, filename); await File.WriteAllTextAsync(path + ".tmp", content); File.Move(path + ".tmp", path);
    }
    private static int ManagedWorker(string directory)
    {
        try
        {
            using var target = new SerialPort("COM12", 115200) { ReadTimeout = 3000, WriteTimeout = 3000 };
            using var peer = new SerialPort("COM22", 115200) { ReadTimeout = 3000, WriteTimeout = 3000 };
            target.Open(); peer.Open(); target.DiscardInBuffer(); peer.DiscardInBuffer();
            File.WriteAllText(Path.Combine(directory, "ready.txt"), Environment.ProcessId.ToString());
            var wait = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(directory, "go.txt"))) { if (wait.Elapsed.TotalSeconds > 30) throw new TimeoutException(); Thread.Sleep(10); }
            var received = new byte[7];
            target.Write(s_payload, 0, 7); ReadExact(peer, received); Require(received.SequenceEqual(s_payload), "managed peer bytes");
            peer.Write(s_payload, 0, 7); ReadExact(target, received); Require(received.SequenceEqual(s_payload), "managed target bytes");
            File.WriteAllText(Path.Combine(directory, "done.txt"), "business bytes PASS");
            while (!File.Exists(Path.Combine(directory, "stop.txt"))) { if (wait.Elapsed.TotalSeconds > 60) break; Thread.Sleep(20); }
            return 0;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(directory, "error.txt"), error.ToString()); return 1; }
    }
    private static void ReadExact(SerialPort port, byte[] data)
    {
        var offset = 0; while (offset < data.Length) offset += port.Read(data, offset, data.Length - offset);
    }
    private static async Task ManagedCase(string architecture)
    {
        var directory = Path.Combine(Path.GetTempPath(), "FlexComManaged-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var executable = architecture == "x64" ? Environment.ProcessPath! : Path.Combine(AppContext.BaseDirectory, "worker-x86", "ProcessMonitor.VmTests.exe");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--managed-worker"); start.ArgumentList.Add(directory);
        using var child = Process.Start(start)!;
        await using var session = new ProcessMonitorSession(new NativeProcessMonitorTransport(Path.Combine(AppContext.BaseDirectory, "native")));
        try
        {
            await WaitFile(directory, "ready.txt", child);
            await session.AttachAsync(new ProcessMonitorTargets().GetTarget(child.Id));
            await Signal(directory, "go.txt"); await WaitFile(directory, "done.txt", child); await Task.Delay(250);
            var data = session.Records.Where(r => r.Api is "ReadFile" or "WriteFile" or "ReadFileEx" or "WriteFileEx").ToArray();
            await File.WriteAllTextAsync(Path.Combine(s_output, "managed-" + architecture + ".json"), JsonSerializer.Serialize(session.Records));
            Log($"managed {architecture}: business bytes PASS; captured completions={data.Length}, bytes={data.Sum(r => r.Data.Length)}");
            Require(data.Length == 4 && data.All(r => r.Succeeded && r.Data.SequenceEqual(s_payload)), ".NET SerialPort completion capture incomplete; do not declare this runtime supported");
        }
        finally
        {
            await session.StopAsync(); await Signal(directory, "stop.txt");
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { child.Kill(); await child.WaitForExitAsync(); }
        }
    }
    private sealed class CaptureCounter
    {
        public long Records, Invalid, Gaps;
        public void Receive(object? sender, ProcessMonitorRecord record)
        {
            if (record.Integrity == CaptureIntegrity.Gap) Interlocked.Exchange(ref Gaps, record.DroppedRecords);
            if (record.Api is not ("ReadFile" or "WriteFile")) return;
            Interlocked.Increment(ref Records);
            if (!record.Succeeded || record.CompletedLength != 64 || record.Data.Length != 64
                || record.Data.Where((value, index) => value != (byte)(record.Data[0] + index)).Any()) Interlocked.Increment(ref Invalid);
        }
    }
    private static async Task NativeCase(string architecture, string targetPort, string peerPort)
    {
        var directory = Path.Combine(Path.GetTempPath(), "FlexComTraffic-" + architecture + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var nativeRoot = Path.Combine(AppContext.BaseDirectory, "native");
        var executable = Path.Combine(nativeRoot, architecture, "NativeTests.exe");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--traffic", directory, @"\\.\" + targetPort, @"\\.\" + peerPort }) start.ArgumentList.Add(argument);
        using var child = Process.Start(start)!;
        var targets = new ProcessMonitorTargets(nativeRoot);
        await using var session = new ProcessMonitorSession(new NativeProcessMonitorTransport(nativeRoot));
        var counter = new CaptureCounter(); session.RecordReceived += counter.Receive;
        var command = 0;
        async Task<long[]> Cycle(int milliseconds, int pause, bool capture)
        {
            var beforeRecords = Interlocked.Read(ref counter.Records); var beforeInvalid = Interlocked.Read(ref counter.Invalid);
            var beforeGaps = Interlocked.Read(ref counter.Gaps); var id = ++command;
            await Signal(directory, $"go-{id}.txt", $"{milliseconds} {pause}");
            await WaitFile(directory, $"done-{id}.txt", child, milliseconds / 1000 + 15); await Task.Delay(150);
            var metrics = (await File.ReadAllTextAsync(Path.Combine(directory, $"done-{id}.txt"))).Split(' ').Select(long.Parse).ToArray();
            var records = Interlocked.Read(ref counter.Records) - beforeRecords; var gaps = Interlocked.Read(ref counter.Gaps) - beforeGaps;
            Log($"{architecture} cycle={id} capture={capture} cycles={metrics[0]} elapsedMs={metrics[1]} p99us={metrics[2]} records={records}/{metrics[3]} gaps={gaps} invalid={counter.Invalid - beforeInvalid}");
            Require(counter.Invalid == beforeInvalid, "captured payload or completion mismatch");
            if (capture && pause > 0) Require(records + gaps == metrics[3], "capture has unaccounted missing records");
            if (!capture) Require(records == 0, "capture continued after stop");
            return metrics;
        }
        try
        {
            await WaitFile(directory, "ready.txt", child);
            var baseline = await Cycle(3000, 6, false);
            await session.AttachAsync(targets.GetTarget(child.Id)); var monitored = await Cycle(3000, 6, true);
            var oldSession = session.SessionId;
            await session.StopAsync(); Require(!child.HasExited, "stop terminated target"); await Cycle(1000, 6, false);
            Interlocked.Exchange(ref counter.Gaps, 0);
            await session.AttachAsync(targets.GetTarget(child.Id)); Require(session.SessionId != oldSession, "reattach reused session"); await Cycle(1000, 6, true);
            Log($"{architecture} lifecycle and bytes PASS; entering 30-minute paced bidirectional run at configured 115200 baud.");
            var longRun = Cycle(1800000, 6, true); var samples = new List<object>(); var sampleIndex = 0;
            while (!longRun.IsCompleted)
            {
                await Task.WhenAny(longRun, Task.Delay(5000)); child.Refresh(); using var self = Process.GetCurrentProcess();
                samples.Add(new { utc = DateTime.UtcNow, targetPrivate = child.PrivateMemorySize64, collectorPrivate = self.PrivateMemorySize64,
                    retained = session.Records.Count, evicted = session.EvictedRecords, records = Interlocked.Read(ref counter.Records), gaps = Interlocked.Read(ref counter.Gaps) });
                if (++sampleIndex % 12 == 0) Log($"{architecture} long run minute={sampleIndex / 12} records={counter.Records} gaps={counter.Gaps} targetPrivate={child.PrivateMemorySize64}");
            }
            var duration = await longRun;
            await File.WriteAllTextAsync(Path.Combine(s_output, "memory-" + architecture + ".json"), JsonSerializer.Serialize(samples));
            await session.StopAsync(); await Cycle(3000, 6, false);
            var ratio = (double)monitored[0] / monitored[1] / ((double)baseline[0] / baseline[1]);
            Log($"{architecture} pacedThroughputRatio={ratio:F4}, p99IncreaseUs={monitored[2] - baseline[2]}, longCycles={duration[0]}; paced virtual-port result, not saturation benchmark.");
            Require(ratio >= .9 && monitored[2] - baseline[2] <= 1000, "paced performance threshold exceeded");
        }
        finally
        {
            await session.StopAsync(); await Signal(directory, "stop.txt");
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { child.Kill(); await child.WaitForExitAsync(); }
        }
        var startupDirectory = directory + "-startup"; Directory.CreateDirectory(startupDirectory);
        await using var launch = await targets.PrepareLaunchAsync(executable, $"--traffic \"{startupDirectory}\" \\\\.\\{targetPort} \\\\.\\{peerPort}");
        using var startupChild = Process.GetProcessById(launch.Target.ProcessId);
        try
        {
            await session.StartAsync(launch); await WaitFile(startupDirectory, "ready.txt", startupChild);
            await Signal(startupDirectory, "go-1.txt", "1000 6"); await WaitFile(startupDirectory, "done-1.txt", startupChild); await Task.Delay(150);
            Require(session.Records.Any(r => r.Api == "ReadFile" && r.Data.Length == 64), "startup serial data absent");
            await session.StopAsync(); Require(!startupChild.HasExited, "startup stop terminated target");
        }
        finally { await Signal(startupDirectory, "stop.txt"); await startupChild.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
    }
}
