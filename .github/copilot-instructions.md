# FlexComDotnet Project Guidelines — 跨语言通用规范

## 1. 项目概况
- **项目名称**: FlexComDotnet
- **语言**: C# (.NET 10) + Rust (驱动层)
- **平台**: Windows 10/11
- **架构**: C# WPF 应用 + Rust 内核驱动，通过 IOCTL 通信

## 2. 目录结构
```
FlexComDotnet/
├── src/
│   ├── FlexComDotnet/            # WPF UI 层 (C#)
│   └── FlexComDotnet.Core/       # 核心业务层 (C#, 无 UI 依赖)
├── driver/
│   └── serial-monitor/           # 串口监控内核驱动 (Rust)
├── tests/
│   └── FlexComDotnet.Tests/      # C# 单元测试
├── docs/
│   ├── specs/                    # 功能设计文档
│   └── plans/                    # 实现计划
├── .github/
│   ├── instructions/             # 分语言开发规范
│   │   ├── dotnet.md             # C# / WPF / MVVM 规范
│   │   ├── rust.md               # Rust 编码规范
│   │   └── driver.md             # 驱动开发约束
│   ├── prompts/                  # Agent 提示词模板
│   └── copilot-instructions.md   # 跨语言通用规范（本文件）
├── CHANGELOG.md                  # 版本更新日志
├── ROADMAP.md                    # 产品路线图
└── README.md                     # 项目说明
```

## 3. 开发流程 (Development Workflow)

### TDD 原则 (Test-Driven Development)
在编写任何功能代码之前，**必须**遵循 TDD 流程：
1. **Red**: 编写一个失败的测试用例，定义预期的行为。
2. **Green**: 编写最少量的代码让测试通过。
3. **Refactor**: 在保证测试通过的前提下优化代码结构。

### Bug 修复流程
1. 创建一个能够复现 Bug 的测试用例（此时测试应失败）。
2. 修复代码。
3. 验证测试通过。

### 交付标准
- **C#**: 在提示用户尝试或提交代码片段之前，**必须**运行相关测试并确保通过 (`dotnet test`)。
- **Rust**: 在提示用户尝试或提交代码片段之前，**必须**运行 `cargo test` 并确保通过；驱动代码需通过 `cargo build` 无警告编译。

## 4. Agent 行为准则 (Behavior Guidelines)

### CLI First 原则
- **C# 依赖管理**: 添加/移除 NuGet 包**必须**使用 `dotnet add/remove package` 命令，**严禁**直接编辑 `.csproj` 文件。
- **Rust 依赖管理**: 添加/移除 crate **必须**使用 `cargo add/remove` 命令，**严禁**直接编辑 `Cargo.toml` 的 `[dependencies]`。
- **项目操作**: 创建项目、添加引用等操作优先使用对应语言的 CLI (`dotnet` / `cargo`)。

### Git 操作
- **被动模式**: 禁止主动执行 git commit, push, pull 等操作。
- **显式触发**: 仅在用户明确使用相关 Skill 或指令要求进行版本控制操作时执行。

### 文档更新
- **被动模式**: 禁止主动更新 `ROADMAP.md` 或其他文档文件。
- **显式触发**: 仅在用户明确要求更新文档任务状态或计划时执行。

## 5. CHANGELOG 规范

### 格式要求
```markdown
# v{Major}.{Minor}.{Patch}

## ✨ 新功能
- 功能描述（用户视角，非技术实现）

## 🚀 优化
- 优化描述

## 🐛 Bug 修复
- 修复描述
```

### 重要规则
- 一级标题为版本号 (如 `# v1.2.0`)
- 二级标题固定为 `✨ 新功能`、`🚀 优化`、`🐛 Bug 修复`
- 内容必须以**用户视角**描述变更价值
- CI 发布时会自动提取对应版本内容，未找到则构建失败

## 6. 变更安全检查清单

### 模型属性变更
当向 Model/Config 类添加或删除属性时，**必须**全局搜索 `new {ClassName}` 和 `= new {ClassName}` 或其属性初始化器，在所有手动构造/复制该类的位置同步属性，包括但不限于：
- `ToModel()` 序列化方法
- `FromModel()` 反序列化方法
- `CancelRule()` / 备份还原逻辑
- 深拷贝 / 克隆方法

### 端到端数据流验证
对关键数据流（如 UI 输入 → 保存 → 持久化 → 加载 → Handler 消费 → 输出），编写 round-trip 测试验证数据在完整链路中的保真性，不要只测试单个环节。

### 多需求任务处理
接到包含多个修复/功能点的任务时，先用 todo list 列出**全部**需求点并逐一标记，避免因复杂修复导致遗漏其他需求项。

### 协议偏移处理
涉及固定前缀/偏移的协议解析或构建时，使用命名常量表达偏移含义，并编写测试覆盖"固定字段 + 自定义字段共存"的场景。

### C# ↔ Rust 跨语言接口变更
修改 IOCTL 编号、共享数据结构 (`#[repr(C)]`) 或通信协议时，**必须**同步更新 C# 端的 P/Invoke 定义与 Rust 端的结构体定义，确保内存布局一致。
