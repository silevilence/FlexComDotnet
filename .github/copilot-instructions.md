# FlexComDotnet Project Guidelines

## 1. 项目概况
- **项目名称**: FlexComDotnet
- **核心语言**: C#
- **框架版本**: .NET 10
- **UI 框架**: WPF (Windows Presentation Foundation) + Panuon.WPF.UI
- **架构模式**: MVVM
- **核心库**: CommunityToolkit.Mvvm

## 2. 目录结构
```
FlexComDotnet/
├── src/
│   ├── FlexComDotnet/            # WPF UI 层
│   │   ├── Converters/           # XAML 值转换器 (Serial, Network, Protocol, Script, Log)
│   │   ├── Features/
│   │   │   ├── AutoReply/Views/
│   │   │   ├── Checksum/Views/
│   │   │   ├── EmojiSupport/     # Behaviors/ + Controls/ (Emoji 补全与渲染)
│   │   │   ├── Layout/Controls/  # ActivityBar, CollapsiblePanel, FloatingPanelWindow, MultiZoneLayout
│   │   │   ├── Logging/Views/
│   │   │   ├── Network/Views/
│   │   │   ├── Protocol/Views/   # ProtocolDefinitionWindow, RxProtocolParseWindow, TxProtocolBuildWindow
│   │   │   ├── Scripting/        # Completion/ + Resources/ + Views/
│   │   │   ├── Serial/Views/     # SerialConfigView, SerialCommunicationView, CommandListView
│   │   │   ├── Settings/Views/   # SettingsWindow, DebugToolsWindow
│   │   │   ├── Update/Views/
│   │   │   └── Visualization/Views/
│   │   ├── Fonts/                # MapleMono-NF-CN
│   │   ├── Services/             # ThemeService, ServiceCollectionExtensions (DI 注册)
│   │   └── Themes/               # DarkTheme.xaml, LightTheme.xaml
│   │
│   └── FlexComDotnet.Core/       # 核心业务层 (无 UI 依赖)
│       └── Features/
│           ├── AutoReply/        # Models / Services (Handlers/) / ViewModels
│           ├── Checksum/         # Models / Services (Algorithms/) / ViewModels
│           ├── EmojiSupport/     # Models / Services
│           ├── Layout/           # Models / Services
│           ├── Logging/          # Models / Services / ViewModels
│           ├── Network/          # Models / Services / ViewModels
│           ├── Protocol/         # Models (Dlt645/) / Services (Parsers/) / ViewModels
│           ├── Scripting/        # Models / Services / ViewModels
│           ├── Serial/           # Helpers / Models / Services / ViewModels
│           ├── Settings/         # Models / ViewModels
│           ├── Update/           # Models / Services / ViewModels
│           └── Visualization/    # Models / Services / ViewModels
│
└── tests/
    └── FlexComDotnet.Tests/      # 单元测试 (xUnit + FluentAssertions + Moq)
        └── Features/             # 按功能模块对应测试
```

## 3. 常用命令
```bash
dotnet build                           # 构建
dotnet run --project src/FlexComDotnet/FlexComDotnet.csproj  # 运行
dotnet test                            # 运行所有测试
dotnet test --filter "FullyQualifiedName~ClassName"         # 特定测试类
dotnet test --filter "FullyQualifiedName~Class.Method"      # 特定测试方法
dotnet add src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [Name]  # 添加包
dotnet remove src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [Name]  # 移除包
```

## 4. 开发流程 (Development Workflow)

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
- **完成检查**: 在提示用户尝试或提交代码片段之前，**必须**运行相关测试并确保通过 (`dotnet test`)。

## 5. Agent 行为准则 (Behavior Guidelines)

### CLI First 原则
- **依赖管理**: 添加/移除 NuGet 包**必须**使用 `dotnet add/remove package` 命令，**严禁**直接编辑 `.csproj` 文件。
- **项目操作**: 创建项目、添加引用等操作优先使用 `dotnet` CLI。

### Git 操作
- **被动模式**: 禁止主动执行 git commit, push, pull 等操作。
- **显式触发**: 仅在用户明确使用相关 Skill 或指令要求进行版本控制操作时执行。

### 文档更新
- **被动模式**: 禁止主动更新 `ROADMAP.md` 或其他文档文件。
- **显式触发**: 仅在用户明确要求更新文档任务状态或计划时执行。

## 6. 技术栈

**核心依赖:**
- .NET 10.0.102 | WPF (net10.0-windows) | Panuon.WPF.UI 1.3.0.2
- CommunityToolkit.Mvvm 8.4.0 | Microsoft.Extensions.DependencyInjection 10.0.2
- System.IO.Ports 10.0.2 | System.Management 10.0.2 | LiteDB 5.0.21
- NLua 1.7.8 | AvalonEdit 6.3.1.120 | ScottPlot.WPF 5.1.57 | Emoji.Wpf 0.3.4

**测试:** xUnit 2.9.3 + FluentAssertions 8.8.0 + Moq 4.20.72

**环境:** Windows 10/11 | Nullable enable | 隐式 using | .NET 分析器启用

## 7. 命名规范与主题系统

### 命名规范
- **命名空间**: `FlexComDotnet.{Layer}.Features.{Feature}.{SubFolder}`
- **接口**: `I{Name}` | **ViewModel**: `{Feature}ViewModel` | **测试类**: `{ClassName}Tests`
- **私有字段**: `_camelCase` | **静态字段**: `s_camelCase`
- **XAML 缩进**: 2 空格 | **C# 缩进**: 4 空格

### 主题系统
- UI 元素颜色必须使用 `{DynamicResource BrushName}` 绑定
- 新增颜色需同时在 `DarkTheme.xaml` 和 `LightTheme.xaml` 中定义
- 命名: 颜色用 `{Name}Color`，画刷用 `{Name}Brush`

## 8. 策略模式应用规范

当功能涉及多种可互换算法或行为时（校验算法、协议解析、回复规则），采用策略模式：
1. 定义 `I{Feature}Algorithm` / `I{Feature}Handler` 接口
2. 每种算法/行为作为独立类实现接口，放在 `Services/Algorithms/`、`Services/Handlers/` 或 `Services/Parsers/`
3. 通过枚举管理策略映射，`I{Feature}Service` 作为策略选择器
4. 扩展时只需添加实现类 + 注册枚举，无需修改 ViewModel 或 UI

**已有策略实例:** `IChecksumAlgorithm` (11 种算法)、`IReplyHandler` (匹配/顺序/协议/脚本)、`IProtocolParser` (通用/DL\T645)

## 9. 测试规范

- 测试文件: `tests/FlexComDotnet.Tests/Features/{Feature}/{ClassName}Tests.cs`
- 测试方法命名: `{Method}_When{Condition}_Should{ExpectedBehavior}` 或 `{Method}_Should{ExpectedBehavior}`
- 使用 Arrange-Act-Assert 模式，FluentAssertions 断言，Moq 模拟依赖

## 10. CHANGELOG 规范

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

## 11. 依赖注入配置

- 服务注册: `src/FlexComDotnet/Services/ServiceCollectionExtensions.cs`
- **Singleton**: 服务类 | **Transient**: ViewModel 类

## 12. 注意事项

- **跨线程 UI 更新**: 必须使用 `Application.Current.Dispatcher.Invoke(() => { ... })`
- **CommunityToolkit.Mvvm**: 使用 `[ObservableProperty]` 生成属性通知，`[RelayCommand]` 生成命令

## 13. 变更安全检查清单

### 模型属性变更
当向 Model/Config 类（如 `ProtocolResponseConfig`、`ProtocolRuleConfig`）添加或删除属性时，**必须**全局搜索 `new {ClassName}` 和 `= new {ClassName}` 或其属性初始化器，在所有手动构造/复制该类的位置同步属性，包括但不限于：
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
