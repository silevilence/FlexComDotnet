# 用户态串口 API Hook 可行性记录

## 方案与依赖

选择 [Microsoft Detours](https://github.com/microsoft/Detours) v4.0.1，固定提交 `e4bfd6b03e50de46b47abfbd1e46b384f0c5f833`。官方以源码发布，MIT 许可；源码下载包 SHA-256 为 `FEF0B17C5C3C9356FB8BA7366A3B196149DAADD131FB6A92ABEDA1CB4D3B9B85`。随验证包保留原许可。使用 VS C++ x86/x64 工具链及 Windows SDK，测试代码使用静态 CRT，不需要 .NET 或自研内核驱动。构建脚本检查源码哈希，组件必须与目标位数匹配。

不使用早期仅限 x86 的 Detours Express 授权资料作为依据。[官方跨位数说明](https://github.com/microsoft/Detours/wiki/OverviewHelpers)要求匹配架构的辅助组件。当前验证器每种架构各自启动/附加同架构受控工作进程；这不证明跨位数控制器已经实现。

验证入口：`tools/ProcessMonitor.Probe/Build-Probe.ps1 -Architecture x64`（或 x86），再执行 `ProbeTests.ps1`。`--self-test` 不打开串口；`--serial COM10 COM20` 仅用于专用配对端口，会清空端口缓冲并发送已知测试字节。图形版本默认测试 COM10/COM20，便于通过远程桌面运行；它直接执行程序内测试，不调用终端或脚本。

## 本轮证据与限制

- 开发机：Windows 11 build 26200，.NET SDK 10.0.301，VS 18 C++ 工具链。x86/x64 构建成功，启动注入、运行中附加、非串口 NUL 写入排除自检通过。
- 用户指定测试机：Hyper-V，通过 RDP 连接，Windows 10 专业版 build 19041 系列，桌面显示测试模式；虚拟串口为 COM10↔COM20、COM11↔COM21、COM12↔COM22。
- 用户已明确允许先在现有环境验证，关闭测试签名且无自研驱动的干净环境验收留待完成。本轮不改变系统安全设置或安装/卸载任何驱动。
- x86/x64 图形验证：2026-10-26 分别在 COM10↔COM20 上得到 PASS。包括无 Hook 基线、启动 Hook、运行中附加；附加时工作进程已经打开串口。双向字节 `00 01 02 7F 80 FE FF`、实际完成长度、零长度写入、非串口排除均符合预期。每种架构在每个 Hook 模式得到 5 条记录、0 条丢弃；样例保存在 [x86](samples/process-hook/2026-10-26-win10-x86.txt) 与 [x64](samples/process-hook/2026-10-26-win10-x64.txt)。其中 LastError 在成功时保留原调用状态，不被解读为失败。
- 串口识别使用 `GetFileType` 与 `GetCommState` 的交集，因此可以在首次读写时识别附加前已打开的串口句柄；此时不能仅凭句柄确定友好 COM 名，显示未知，不伪造名称。

## 首期承诺与实现门槛

目标为 Windows 10/11 x86/x64 普通进程，同用户/同等权限或明确足够权限。受保护进程、ARM64、直接 NT 调用、内核发起操作、未拦截的第三方驱动 IOCTL、跨进程句柄操作不在首期支持范围。没有通过实测的路径不得显示为“已支持”。

当前验证器只覆盖同步 ReadFile/WriteFile；它不是可发布的监控后端。异步读取绝不在挂起时复制业务缓冲区。后续必须独立验证：

- `GetOverlappedResult`/`GetOverlappedResultEx` 以及 IOCP 的单项/批量完成；立即完成和重复查询只产生一个完成记录。
- `ReadFileEx`/`WriteFileEx` 回调参数、线程及一次性调用语义；取消、关闭、超时、OVERLAPPED 复用和停止后的迟到完成。
- 记录缓存有界且消费者延迟不阻塞目标；无法入队或截断时暴露计数和完整性缺口。
- 停止不结束目标、不强行卸载 DLL；再次附加要隔离会话、进程启动实例及句柄世代。

依据：[ReadFileEx](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-readfileex)、[完成回调生命周期](https://learn.microsoft.com/windows/win32/api/minwinbase/nc-minwinbase-lpoverlapped_completion_routine)、[GetQueuedCompletionStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-getqueuedcompletionstatusex)。

发布前性能验收阈值：以同一受控程序无 Hook 基线为对照，115200 波特率连续双向 30 分钟无未报告数据缺口；采集内存上限明确，停止后无持续增长；吞吐下降不超过 10%，API 调用延迟 p99 增量不超过 1 ms。阈值尚未实测，不能据此宣称性能达标。

## 状态

同步最小链路已部分实测；完整可行性验收、生产会话后端、异步/控制路径、完整采集界面及最终打包尚未完成。ROADMAP 中对应未完成条目继续保留。
