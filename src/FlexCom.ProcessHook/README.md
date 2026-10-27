# 进程串口监控（API Hook，实验性）

此组件观察所选进程的串口 Win32 API，不自动跟踪子进程。它不依赖 FlexCom 的 SerialMonitor 内核驱动；串口设备本身仍须有正常工作的设备驱动。原串口监控、此功能与 USB 抓包的配置、会话和启停互相独立。

## 使用

1. 在“进程串口监控（API Hook）”页面选择程序与启动参数并启动，或者刷新进程列表、选择 PID 后附加。
2. 同时观察该进程中能够识别的串口句柄。串口名称、方向与搜索框只过滤显示，不缩小注入范围。
3. 原始调用保留请求长度、实际完成长度、成功标志、原调用 LastError、写入尝试、实际数据和完整性。成功调用的 LastError 可能是旧值，不应单独据此判断失败。
4. 组帧需显式启用并指定已知的固定帧长。协议定义来自应用的协议配置。组帧处理全部保留记录，不受原始记录显示筛选影响；按会话、进程、句柄代次和方向隔离，遇到缺口、截断或失败会断开部分帧。
5. 可导出当前可见的原始记录 JSON，或导出帧、原始记录引用与解析字段。导出仅含内存中仍保留的内容，并非无限期落盘录制。
6. 停止监控不结束目标程序。DLL 为了在途 I/O 和回调安全会驻留到目标退出；升级组件后应重启目标。采集应用正常退出会释放会话；异常退出后，目标下次串口调用时会定期检查采集端并停止观察。

API 完成时间不等于线路逐字节时间，不能用于推断精确的物理串口帧间隔。附加前已打开的串口可以在首次调用时识别，但 COM 名称可能未知；请按句柄和代次区分，名称未知的记录不会匹配指定的 COM 显示筛选。

## 范围与诊断

- 目标范围为 Windows 10/11 上可访问的 x86/x64 普通进程。控制端按目标架构选择对应助手与 DLL。
- 权限不足时会返回失败，进程列表不提供无法读取身份的进程；不会自动提权、关闭系统保护或安装驱动。受保护进程、ARM64、跨进程句柄复制、直接 NT 调用、线程池 I/O 回调及未覆盖的驱动 IOCTL 不属于已验证范围。
- 实现覆盖 CreateFileA/W、CloseHandle、ReadFile/WriteFile、GetOverlappedResult/Ex、GetQueuedCompletionStatus/Ex、CreateIoCompletionPort 关联、ReadFileEx/WriteFileEx，以及串口配置、超时、EscapeCommFunction、PurgeComm、CancelIo/Ex。实现存在不代表已经通过真实设备验收，见下方状态。
- 仅直接等待事件而从不调用已覆盖的完成查询函数、直接检查 OVERLAPPED.Internal 等路径，无法保证观察到完成；不能用于声称完整捕获。
- 若先查询完成，再在旧 IOCP 包尚未取出时复用同一个 OVERLAPPED，包本身不足以可靠区分请求。此时显示“完成来源不明确”，不读取可能属于新请求的缓冲区；该地址的 IOCP 记录需重启目标后恢复。常规“完成包取出后再复用”不受此限制。
- 记录中只列出已观察到的配置参数，未列出的 DCB 字段和设备状态为未知，不推断物理控制线状态。DTR/RTS 记录代表 API 调用结果。
- 缺少架构对应组件、进程启动身份改变、已有其他监控会话、传输版本不一致或驻留 DLL 与磁盘版本不同均会阻止附加并给出诊断。

## 有界缓冲

每个目标最多追踪 1024 个串口句柄、1024 个挂起请求和 1024 个待确认完成包地址；耗尽时保留业务调用并报告采集缺口。完成包地址追踪耗尽后，不能可靠归因的 IOCP 完成会被标为不可用，需重启目标恢复。

共享队列最多 256 条记录；每条最多保留 1024 字节实际载荷和 1024 字节写入尝试。原始请求/完成长度仍保留，超长数据标为截断。队列锁采用不等待的生产方式，消费者变慢会丢采集记录，丢弃计数随下一条存活记录携带，以便在正确位置断开组帧。

应用原始历史最多 10000 条，派生帧最多 2000 帧，部分流最多 256 个，帧长最多 65536 字节。淘汰与丢弃会显示在界面。它们是容量上限，不是性能达标结论。

## 构建与自动测试

需要 .NET 10、Visual C++ x86/x64 工具和 Windows SDK。应用构建/发布会构建并复制两个架构的组件及许可证到 `ProcessHook/x86`、`ProcessHook/x64`。已自行构建并验证两个架构的组件时，可用 `-p:UseExistingProcessHookAssets=true` 只核验并复制现有产物；它不会重新编译原生代码。本机 Windows PowerShell 禁止加载脚本，未修改执行策略，本轮使用 Visual C++ 编译入口及此参数验证发布目录。开发时可用 `-p:BuildProcessHook=false` 跳过此组件，但产物将不保证具有监控能力；`BuildSerialMonitorDriver=false` 可独立跳过原内核驱动构建。

```powershell
& src/FlexCom.ProcessHook/Build-Hook.ps1 -Architecture x64
& src/FlexCom.ProcessHook/Build-Hook.ps1 -Architecture x86
& src/FlexCom.ProcessHook/artifacts/x64/HookContractTests.exe
& src/FlexCom.ProcessHook/artifacts/x86/HookContractTests.exe
dotnet run --project tools/ProcessMonitor.HostTests -- src/FlexCom.ProcessHook/artifacts
dotnet test tests/FlexComDotnet.Tests/FlexComDotnet.Tests.csproj
```

`HookContractTests` 使用生产 Hook 实现，将外部 Win32 串口 I/O 替换为受控返回值，并在本测试进程内通过 Detours 拦截；不访问串口、不注入业务程序。它验证字节/长度/错误码、异步完成去重、回调、控制事件、容量与缺口，不能证明实际串口驱动的时序或性能。

`ProcessMonitor.HostTests` 只启动专用无串口工作进程，验证真实 x86/x64 注入、启动/附加/停止、异常传输恢复、驻留版本拒绝与目标退出，并以合成 C++ 记录验证真实共享内存到 .NET 会话的 ABI、字节与缺口位置。合成记录不代表真实串口收发。

真实串口验证器 `NativeTests.Gui.exe` / `NativeTests.exe --serial` 固定使用专用 COM10↔COM20，并清空缓冲、发送测试数据。只能在专用测试环境运行；本次未执行。控制事件与业务完成分别断言，避免控制记录改变字节事件数量。

## 验收状态（2026-10-27）

基础可行性探针已在用户指定的测试模式虚拟机验证 x86/x64 启动与附加、附加前已打开串口、同步双向样例。早期生产实现的 x64 同步、事件、单项 IOCP 和普通回调路径也曾通过虚拟串口测试。

本次新增/修改的完整实现尚未进行虚拟机和真实串口回归；扩展完成、控制操作、异常取消、并发压力、.NET SerialPort 调用路径、完整界面交互与发布目录运行均须补验。此前虚拟机扩展测试在重新打开串口时失败，原因仍待定位。关闭测试签名且无自研驱动的干净环境验收，以及 30 分钟吞吐/内存/p99 延迟测试均未完成。保持实验性状态，不据自动测试结果声明可正式发布。

## 第三方许可与 API 依据

Microsoft Detours v4.0.1，固定提交 `e4bfd6b03e50de46b47abfbd1e46b384f0c5f833`，MIT 许可。构建脚本校验源码压缩包 SHA-256，分发时保留每个架构目录下的 `Detours-LICENSE.md`。[官方仓库](https://github.com/microsoft/Detours)。

完成结果语义参考 [GetOverlappedResultEx](https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-getoverlappedresultex)、[GetQueuedCompletionStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-getqueuedcompletionstatusex)、[OVERLAPPED_ENTRY](https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-overlapped_entry) 和 [RtlNtStatusToDosError](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-rtlntstatustodoserror)。等待超时不等于 I/O 完成；批量取出成功不等于每个 I/O 成功，不能读取保留的 OVERLAPPED_ENTRY.Internal 字段作为状态。
