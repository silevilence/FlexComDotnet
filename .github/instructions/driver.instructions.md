---
applyTo: "driver/**"
---

# 驱动开发约束

## 1. 概述
本项目的驱动层为 Windows 内核模式过滤驱动 (Kernel-Mode Filter Driver)，用于透明监控串口通信数据。驱动 Attach 到目标串口设备栈，拦截 IRP 进行数据捕获，不影响原始通信。

## 2. IRP 处理规范

### 必须遵守的原则
- **透传优先**: 驱动仅作为过滤存在，不得修改、阻塞或丢弃原始 IRP 数据
- **完成例程 (Completion Routine)**: 读操作必须设置完成例程以捕获返回数据，完成例程中**禁止**执行阻塞操作
- **IRQL 限制**:
  - `PASSIVE_LEVEL`: 可执行任何操作
  - `APC_LEVEL`: 不可等待内核对象
  - `DISPATCH_LEVEL`: 禁止页面错误、禁止等待、禁止分配分页内存
- **IRP 传递**: 未处理的 IRP 必须无条件传递给下层驱动 (`IoCallDriver`)，禁止吞掉任何 IRP

### 需要拦截的 IRP
| IRP 类型 | 用途 | 处理方式 |
|---|---|---|
| `IRP_MJ_READ` | 捕获 RX 数据 | 设置完成例程，在完成后复制数据 |
| `IRP_MJ_WRITE` | 捕获 TX 数据 | 在下发前复制 buffer 数据 |
| `IRP_MJ_DEVICE_CONTROL` | 监控串口配置变更 | 选择性捕获波特率等配置 IOCTL |

## 3. 性能约束

- **内核缓冲区**: 使用固定大小的环形缓冲区 (Ring Buffer) 存储捕获数据，避免频繁的内存分配/释放
- **自旋锁**: 高 IRQL 路径中使用 SpinLock 保护共享资源，持锁时间必须极短（微秒级）
- **内存分配**: 内核中优先使用非分页池 (NonPagedPool)，分页池仅用于低 IRQL 的初始化路径
- **数据拷贝**: 最小化数据拷贝次数，在完成例程中直接写入环形缓冲区

## 4. 安全性要求

- **输入验证**: IOCTL 输入缓冲区的长度和内容必须严格校验，防止内核态缓冲区溢出
- **权限控制**: 设备对象的 ACL 必须限制为管理员 (Administrator) 访问
- **符号链接**: 使用 `\DosDevices\SerialMonitor` 暴露给用户态，命名不得与系统设备冲突
- **卸载安全**: 驱动卸载时必须确保：
  1. 所有挂起的 IRP 已完成或取消
  2. 过滤设备已从设备栈中 Detach
  3. 所有内核资源（内存、锁、事件对象）已释放
  4. 符号链接和设备对象已删除

## 5. IOCTL 接口定义

| IOCTL | 方向 | 描述 |
|---|---|---|
| `IOCTL_START_MONITOR` | 用户 → 驱动 | 启动对指定串口的监控 |
| `IOCTL_STOP_MONITOR` | 用户 → 驱动 | 停止监控 |
| `IOCTL_GET_DATA` | 驱动 → 用户 | 读取捕获的数据缓冲区 |

### 共享数据结构约束
- 所有 C# ↔ Rust 共享结构体必须标记 `#[repr(C)]`
- 使用固定宽度类型，禁止 `usize` / `isize`
- 字段对齐需与 C# `[StructLayout(LayoutKind.Sequential)]` 一致
- 增删字段时必须双端同步更新

## 6. 调试与日志

- 使用 `DbgPrint` / WDK 日志宏输出内核调试信息
- 日志输出需包含函数名和行号，便于 WinDbg 定位
- Release 构建中移除或降级非必要的日志输出，避免性能影响
- 蓝屏 (BSOD) 场景必须有意义的 BugCheck 参数，便于 crash dump 分析

## 7. 构建与签名

- 构建工具链: WDK (Windows Driver Kit) + Rust `windows-drivers-rs`
- Debug 构建使用测试签名 (`/testsigning on`)
- Release 构建需要正式的代码签名证书（EV 证书或 WHQL）
- CI 产物: `.sys` 驱动文件 + 可选 `.inf` 安装描述文件
