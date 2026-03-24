---
applyTo: "driver/**"
---

# Rust 编码规范

## 1. 项目结构
```
driver/
└── serial-monitor/           # 串口监控内核驱动
    ├── Cargo.toml
    ├── build.rs              # WDK 构建脚本
    └── src/
        ├── lib.rs            # 入口 (DriverEntry)
        ├── device.rs         # 设备对象创建与管理
        ├── filter.rs         # 过滤驱动 IRP 拦截逻辑
        ├── ioctl.rs          # IOCTL 定义与用户态通信
        └── shared.rs         # 共享数据结构 (#[repr(C)])
```

## 2. 常用命令
```bash
cd driver/serial-monitor
cargo build                   # 构建
cargo test                    # 运行测试
cargo clippy                  # 静态分析
cargo fmt                     # 格式化
cargo add <crate>             # 添加依赖
cargo remove <crate>          # 移除依赖
```

## 3. 编码规范

### 命名规范
- **模块/文件**: `snake_case`
- **类型/Trait**: `PascalCase`
- **函数/变量**: `snake_case`
- **常量/静态**: `SCREAMING_SNAKE_CASE`
- **缩进**: 4 空格

### unsafe 使用原则
- `unsafe` 块必须附带注释说明安全性不变量 (safety invariant)
- 最小化 `unsafe` 的范围，在 `unsafe` 块中只包含真正需要的操作
- 对外暴露的函数尽量提供 safe wrapper
- FFI 函数声明集中在专用模块中 (如 `ffi.rs` 或 `sys.rs`)

### 错误处理
- 驱动内核代码使用 `NTSTATUS` 返回码
- 用户态工具代码使用 `Result<T, E>` + `thiserror` 自定义错误类型
- 禁止在驱动代码中使用 `panic!` / `unwrap()` / `expect()`，除非在绝对不可能失败的场景且有注释说明

### 内存安全
- 内核内存分配必须检查返回值，处理分配失败的情况
- 释放资源时使用 RAII 模式（通过 `Drop` trait）
- 指针操作必须检查空指针
- Buffer 操作必须检查边界

### FFI 与跨语言接口
- 所有与 C# 共享的结构体必须标记 `#[repr(C)]`
- IOCTL 编号定义需与 C# 端 P/Invoke 常量保持严格一致
- 共享数据结构的字段类型使用固定宽度整数 (`u8`, `u16`, `u32`, `u64`)
- 修改共享结构体时**必须**同步更新 C# 端定义（参见通用规范中的"跨语言接口变更"清单）

## 4. 代码质量
- 所有代码必须通过 `cargo clippy` 无警告
- 所有代码必须通过 `cargo fmt` 格式化
- 公共 API 必须有文档注释 (`///`)
- 驱动入口函数 (`DriverEntry`) 和 IRP 处理函数需有完整的安全性文档

## 5. 测试规范
- 单元测试放在对应模块文件的 `#[cfg(test)]` 块中
- 集成测试放在 `tests/` 目录
- 驱动逻辑中可测试的纯函数（如协议解析、数据转换）必须有单元测试
- 与硬件交互的逻辑通过 trait 抽象以便 mock 测试
