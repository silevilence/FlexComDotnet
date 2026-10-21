using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;
using FlexComDotnet.Core.Features.Driver.Services;

// Run only on an isolated Windows test machine with the v2 driver already
// registered/re-enumerated. Target and peer must be physically cross-connected.
// This program never installs a filter or restarts a device; --run transmits data.
if (args.Length == 1 && args[0] == "--self-test")
{
    var assembler = new MonitorFrameAssembler(3);
    if (assembler.Append(new(DateTime.Now, SerialMonitorDataDirection.Rx, [1, 2])).Count != 0
        || !assembler.Append(new(DateTime.Now, SerialMonitorDataDirection.Rx, [3])).Single().Data.SequenceEqual(new byte[] {1,2,3}))
        throw new InvalidOperationException("Frame assembler self-test failed.");
    Console.WriteLine("PASS: harness self-test (no driver/hardware accessed).");
    return;
}
if (args.Length < 3 || args[0] is not ("--run" or "--worker"))
{
    Console.WriteLine("Usage: SerialMonitor.SmokeTest --run COM3 COM4 [baud=115200]");
    Console.WriteLine("Requires two cross-connected ports, admin privileges, and the v2 filter already active.");
    Console.WriteLine("Checks existing open handles, >4096-byte TX/RX, target isolation, stop and restart.");
    Console.WriteLine("--self-test validates the harness without accessing any port.");
    return;
}
var target = args[1];
var peer = args[2];
if (target.Equals(peer, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use two different ports.");
var baud = args.Length > 3 ? int.Parse(args[3]) : 115200;
var tx = Pattern(8192, 17);
var rx = Pattern(8192, 91);
if (args[0] == "--worker")
{
    using var a = Open(target, baud);
    using var b = Open(peer, baud);
    for (var round = 0; round < 3; round++)
    {
        var aRead = ReadExactly(a.BaseStream, rx.Length);
        var bRead = ReadExactly(b.BaseStream, tx.Length);
        Console.WriteLine("READY"); // third-party handles/read requests exist before monitor start
        if (Console.ReadLine() != "RUN") return;
        await Task.WhenAll(a.BaseStream.WriteAsync(tx).AsTask(), b.BaseStream.WriteAsync(rx).AsTask());
        var received = await Task.WhenAll(aRead, bRead).WaitAsync(TimeSpan.FromSeconds(30));
        Require(received[0].SequenceEqual(rx) && received[1].SequenceEqual(tx), "Traffic changed or bytes were lost.");
        Console.WriteLine("DONE");
    }
    return;
}

var start = new ProcessStartInfo(Environment.ProcessPath!)
{
    UseShellExecute = false, CreateNoWindow = true,
    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
};
if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
    start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
foreach (var arg in new[] { "--worker", target, peer, baud.ToString() }) start.ArgumentList.Add(arg);
using var worker = Process.Start(start) ?? throw new InvalidOperationException("Cannot start traffic process.");
var stderr = worker.StandardError.ReadToEndAsync();
using var monitor = new SerialMonitorService();
var records = new ConcurrentQueue<SerialMonitorDataRecord>();
var errors = new ConcurrentQueue<string>();
monitor.DataReceived += (_, record) => records.Enqueue(record);
monitor.ErrorOccurred += (_, error) => errors.Enqueue(error);
try
{
    for (var round = 0; round < 3; round++)
    {
        await ExpectLine(worker, "READY");
        records.Clear();
        var enabled = round != 1;
        if (enabled)
        {
            var result = monitor.StartMonitoring(target);
            Require(result.Succeeded, result.Message);
        }
        var before = monitor.GetStatus() ?? throw new IOException("Driver status unavailable.");
        await worker.StandardInput.WriteLineAsync("RUN");
        await worker.StandardInput.FlushAsync();
        await ExpectLine(worker, "DONE");
        if (enabled)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(5)
                && (Bytes(records, SerialMonitorDataDirection.Tx).Length < tx.Length
                || Bytes(records, SerialMonitorDataDirection.Rx).Length < rx.Length))
                await Task.Delay(20);
            Require(errors.IsEmpty, string.Join("; ", errors));
            Require(Bytes(records, SerialMonitorDataDirection.Tx).SequenceEqual(tx), "Captured TX differs from target traffic (missing/mixed bytes).");
            Require(Bytes(records, SerialMonitorDataDirection.Rx).SequenceEqual(rx), "Captured RX differs from target traffic (missing/mixed bytes).");
            var status = monitor.GetStatus() ?? throw new IOException("Driver status unavailable.");
            Require(status.DroppedRecords == 0, "Capture queue overflowed.");
            Require(records.All(r => !r.DataUnavailable), "An I/O buffer could not be captured.");
            Require(monitor.StopMonitoring().Succeeded, "Cannot stop session.");
            Console.WriteLine($"PASS round {round}: third-party TX/RX matched, 8192 bytes each, no capture loss.");
        }
        else
        {
            var after = monitor.GetStatus() ?? throw new IOException("Driver status unavailable.");
            Require(after.State == SerialMonitorState.Stopped && after.TotalBytesTx == before.TotalBytesTx
                && after.TotalBytesRx == before.TotalBytesRx, "Stopped monitor continued capturing.");
            Console.WriteLine("PASS round 1: traffic unchanged while monitoring stopped; capture counters unchanged.");
        }
    }
    await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    Require(worker.ExitCode == 0, await stderr);
}
finally
{
    if (!worker.HasExited) worker.Kill(entireProcessTree: true); // only the child created above
}

static SerialPort Open(string name, int baud)
{
    var port = new SerialPort(name, baud, Parity.None, 8, StopBits.One)
    { ReadTimeout = 15000, WriteTimeout = 15000, Handshake = Handshake.None };
    port.Open();
    return port;
}
static byte[] Pattern(int count, int seed) => Enumerable.Range(0, count).Select(i => (byte)((i * 37 + seed) % 251)).ToArray();
static async Task<byte[]> ReadExactly(Stream stream, int length)
{
    var bytes = new byte[length];
    await stream.ReadExactlyAsync(bytes);
    return bytes;
}
static byte[] Bytes(IEnumerable<SerialMonitorDataRecord> records, SerialMonitorDataDirection direction) =>
    records.Where(r => r.Kind == 0 && r.Direction == direction && r.Status >= 0).SelectMany(r => r.Data).ToArray();
static async Task ExpectLine(Process process, string expected)
{
    var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(45));
    Require(line == expected, $"Worker expected '{expected}', got '{line}'. Check port wiring and worker errors.");
}
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
