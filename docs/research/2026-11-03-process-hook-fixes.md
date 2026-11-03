# API Hook 实机问题排查与修复（2026-11-03）

## 诊断基线

沿用 Windows 10 19045 Hyper-V 虚拟机、专用 COM10↔20、COM11↔21、COM12↔22；不改变安全配置和驱动。时间以报告中的 UTC 为准。先运行仅添加诊断的版本，保留原有零等待锁策略和容量。

115200、64 字节、双向四次调用、每轮 Sleep(6)，30 秒诊断基线（不是 30 分钟验收）：

| 架构 | 业务完成 | 采集完成 | 丢弃增量 | 队列峰值 | 读取会话争锁累计 | 发布争锁累计 |
|---|---:|---:|---:|---:|---:|---:|
| x64 | 7440 | 6826 | 614 | 19/256 | 375 | 276 |
| x86 | 7444 | 7423 | 21 | 24/256 | 19 | 3 |

分类为本会话累计值，包含此前 1 秒预热；预热分别丢弃 37、1，故 651−37=614、22−1=21。两架构均满足完成记录加丢弃增量等于业务完成，字节错误为零。队列容量、跟踪容量和异步歧义均为零。与旧报告的架构高低相反，证据支持“消费者与生产者锁竞争受调度影响”，不支持“x86 天生更易丢失”或“队列太小”。

短测 Hook/无 Hook 吞吐比 x64 1.0331、x86 1.0275，p99 增量 119、152 μs。这是虚拟串口节奏测试，不代表物理线路吞吐。原生完整回归均通过；真实 .NET 单轮 x64 四条/28 字节通过，x86 三条/21 字节失败，正确性仍未验收。

原始报告在 [baseline](samples/process-hook/2026-11-03/baseline/)；历史 2026-10-28 失败样本不修改。

## 统计口径与复跑

共享 ABI 升至 v3，记录体仍为 2464 字节，头部从 64 扩为 128 字节；旧驻留 DLL 必须重启目标。`NativeProcessMonitorTransport.Diagnostics` 提供累计快照，VmTests 按周期导出，附 QPC 频率。

- `SnapshotContention`、`PublishContention`、`CompletionContention` 分别统计会话快照、发布、完成会话校验的锁失败；`QueueFull`、`HandleCapacity`、`PendingCapacity` 为对应观察丢失；`AmbiguousCompletion` 为无法归属的观察丢失。静止后七项之和等于总丢失，运行中跨字段采样可能暂时不一致。
- `RetirementCapacity` 是跟踪退化警告，不是额外业务完成丢失。`NotificationLoss` 仅表示诊断通知未入队，原观察已计数，禁止再次增加总丢失。异步丢失是观察计数，不能直接当成业务完成数。
- `QueueHighWater` 是会话最高占用；`PublishCalls/PublishTicks/MaxPublishTicks` 统计取得队列锁后的发布调用及其包含锁尝试的处理时间。未取得锁的调用单独由争锁计数表示。只调用两次 QPC，无日志、堆分配或每字节计时。

构建使用 `Build-Hook.ps1` 或已生成的 Visual C++ `artifacts/{x64,x86}/build.cmd`；执行两个架构的 `HookContractTests.exe --diagnostics` 和无参数完整合约，再运行 `dotnet run --project tools/ProcessMonitor.HostTests -- src/FlexCom.ProcessHook/artifacts`，以及 `dotnet test tests/FlexComDotnet.Tests/FlexComDotnet.Tests.csproj`。

诊断合约分别复现三个锁失败点、消费者暂停/突发、八线程同步写入、句柄/挂起/完成地址容量耗尽、异步复用及诊断通知丢失。新增通知测试曾明确失败于重复计数，修复后必须通过。原生与 .NET 宿主合约验证新增诊断布局。

VmTests 包根目录存在 `diagnostic-baseline.txt` 时使用 30 秒采样；不存在时仍为 30 分钟。短测不得标为长期验收。
