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
├── .github/
│   ├── prompts/                  # AI Prompt 模板
│   ├── workflows/                # GitHub Actions CI/CD
│   └── copilot-instructions.md   # Copilot 开发指南
├── src/
│   ├── FlexComDotnet/            # WPF UI 层
│   │   ├── Converters/           # XAML 值转换器 (SerialConverters, NetworkConverters, ProtocolConverters, ScriptConverters, LogConverters)
│   │   ├── Features/
│   │   │   ├── AutoReply/Views/  # 自动回复视图 (AutoReplyView)
│   │   │   ├── Checksum/Views/   # 校验计算器视图 (ChecksumCalculatorWindow)
│   │   │   ├── Layout/Controls/  # 布局控件 (ActivityBar, CollapsiblePanel, FloatingPanelWindow, MultiZoneLayout)
│   │   │   ├── Logging/Views/    # 日志面板视图 (LogPanelView)
│   │   │   ├── Network/Views/    # 网络连接视图 (ConnectionConfigView)
│   │   │   ├── Protocol/Views/   # 协议定义视图 (ProtocolDefinitionWindow)
│   │   │   ├── Scripting/        # 脚本功能
│   │   │   │   ├── Completion/   # 智能补全 (FComCompletionData)
│   │   │   │   ├── Resources/    # 语法高亮定义 (LuaSyntax.xshd)
│   │   │   │   └── Views/        # 脚本视图 (ScriptingView, ScriptEditorWindow, ApiReferenceWindow, 对话框)
│   │   │   ├── Serial/Views/     # 串口视图 (Config/Communication/CommandList)
│   │   │   ├── Update/Views/     # 更新视图 (UpdateWindow)
│   │   │   └── Visualization/Views/ # 数据可视化视图 (DataVisualizationView)
│   │   ├── Fonts/                # 内嵌字体 (MapleMono-NF-CN)
│   │   ├── Services/             # UI 层服务 (ThemeService, ServiceCollectionExtensions)
│   │   ├── Themes/               # 主题资源字典 (DarkTheme.xaml, LightTheme.xaml)
│   │   ├── App.xaml(.cs)         # 应用入口
│   │   └── MainWindow.xaml(.cs)  # 主窗口 (含状态栏 RX/TX 统计)
│   │
│   └── FlexComDotnet.Core/       # 核心业务层 (无 UI 依赖)
│       └── Features/
│           ├── AutoReply/
│           │   ├── Models/       # 配置模型 (ReplyMode, MatchType, MatchRule, SequentialFrame, AutoReplyConfig)
│           │   ├── Services/     # 回复服务与处理器 (IAutoReplyService, IReplyHandler, Handlers/)
│           │   └── ViewModels/   # 自动回复 ViewModel (AutoReplyViewModel)
│           ├── Checksum/
│           │   ├── Models/       # 算法枚举 (ChecksumAlgorithmType)
│           │   ├── Services/     # 校验服务与算法策略 (IChecksumService, IChecksumAlgorithm, Algorithms/)
│           │   └── ViewModels/   # 计算器 ViewModel (ChecksumCalculatorViewModel)
│           ├── Layout/
│           │   ├── Models/       # 布局状态模型 (LayoutState, PanelInfo, PanelZone)
│           │   └── Services/     # 面板管理器 (IPanelManager)
│           ├── Logging/
│           │   ├── Models/       # 日志模型 (LogEntry, LogLevel, LogSource)
│           │   ├── Services/     # 日志服务 (ILoggingService, ILogPersistenceService, LoggingService, LogPersistenceService)
│           │   └── ViewModels/   # 日志 ViewModel (LogPanelViewModel)
│           ├── Network/
│           │   ├── Models/       # 连接模型 (ConnectionType, ConnectionState, NetworkConfig, ClientInfo)
│           │   ├── Services/     # 连接服务 (IConnection, ITcpClientService, ITcpServerService, IUdpService)
│           │   └── ViewModels/   # 连接配置 ViewModel (ConnectionConfigViewModel)
│           ├── Protocol/
│           │   ├── Models/       # 协议模型 (FrameDefinition, FieldDefinition, DataType, Endianness, ProtocolType, ParsedFrame)
│           │   │   └── Dlt645/   # DL/T 645 专用模型 (Dlt645ControlCode, Dlt645DataDictionary, Dlt645ErrorCode, Dlt645ParsedFrame)
│           │   ├── Services/     # 协议服务 (IProtocolParser, IProtocolParserService, ProtocolParserService)
│           │   │   └── Parsers/  # 策略模式解析器 (ConfigurableParser, Dlt645Parser)
│           │   └── ViewModels/   # 协议 ViewModel (ProtocolParserViewModel)
│           ├── Scripting/
│           │   ├── Models/       # 脚本模型 (HookType, ScriptState, ScriptFileInfo, ScriptLogEntry)
│           │   ├── Services/     # 脚本服务 (IScriptEngine, IScriptManager, IScriptHookService, IScriptApiBridge)
│           │   └── ViewModels/   # 脚本 ViewModel (ScriptingViewModel)
│           ├── Serial/
│           │   ├── Helpers/      # 工具类 (HexHelper, ChecksumHelper)
│           │   ├── Models/       # 数据模型 & 枚举 (AppConfig, SerialPortConfig, CommandItem, SerialEnums)
│           │   ├── Services/     # 串口/配置/存储服务 (ISerialPortService, IConfigurationService, ICommandStorageService)
│           │   └── ViewModels/   # MVVM ViewModel (SerialConfigViewModel, SerialCommunicationViewModel, CommandListViewModel)
│           ├── Update/
│           │   ├── Models/       # 版本信息模型 (VersionInfo, ReleaseInfo, UpdateCheckResult, DownloadProgress, InstallationType)
│           │   ├── Services/     # 更新服务 (IUpdateService, IVersionService, IGitHubReleaseService, IDownloadService)
│           │   └── ViewModels/   # 更新 ViewModel (UpdateViewModel)
│           └── Visualization/
│               ├── Models/       # 可视化模型 (ChartDataPoint, ChannelConfig, VisualizationConfig, VisualizationEventArgs)
│               ├── Services/     # 可视化服务 (IVisualizationService, VisualizationService)
│               └── ViewModels/   # 可视化 ViewModel (DataVisualizationViewModel)
│
└── tests/
    └── FlexComDotnet.Tests/      # 单元测试
        └── Features/
            ├── AutoReply/        # 自动回复测试
            ├── Checksum/         # 校验计算器测试
            ├── Layout/           # 布局功能测试
            ├── Logging/          # 日志功能测试
            ├── Network/          # 网络功能测试
            ├── Protocol/         # 协议解析测试
            ├── Scripting/        # 脚本功能测试
            ├── Serial/           # 串口功能测试
            ├── Update/           # 自动更新测试
            └── Visualization/    # 数据可视化测试
```

**关键文件说明：**
- `README.md`: 项目说明文档
- `ROADMAP.md`: 功能开发计划与进度
- `CHANGELOG.md`: 版本更新日志
- `FlexComDotnet.slnx`: 解决方案文件
- `.editorconfig`: 代码风格配置
- `.gitignore`: Git 忽略文件

## 3. 常用命令 (Dotnet CLI)
所有命令默认在项目根目录下执行。

### 构建与运行
- **构建项目**: `dotnet build`
- **运行应用**: `dotnet run --project src/FlexComDotnet/FlexComDotnet.csproj`
- **清理项目**: `dotnet clean`
- **构建无警告**: `dotnet build --warnaserror`

### 测试
- **运行所有测试**: `dotnet test`
- **运行特定测试项目**: `dotnet test tests/FlexComDotnet.Tests/FlexComDotnet.Tests.csproj`
- **运行特定测试类**: `dotnet test --filter "FullyQualifiedName~SerialPortServiceTests"`
- **运行特定测试方法**: `dotnet test --filter "FullyQualifiedName~SerialPortServiceTests.NewService_ShouldNotBeConnected"`
- **测试覆盖率**: `dotnet test --collect:"XPlat Code Coverage"`

### 依赖管理
- **添加 NuGet 包**: `dotnet add src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [PackageName]`
- **移除 NuGet 包**: `dotnet remove src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [PackageName]`
- **列出依赖**: `dotnet list package`

### 项目管理
- **创建新解决方案**: `dotnet new sln`
- **添加项目到解决方案**: `dotnet sln add [项目路径]`
- **检查 SDK 版本**: `dotnet --version`
- **清理 NuGet 缓存**: `dotnet nuget locals all --clear`

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

## 6. 项目状态与技术栈

### 技术栈详情
**核心框架：**
- **.NET SDK**: 10.0.102 (通过 global.json 配置)
- **目标框架**: net10.0-windows
- **UI 框架**: WPF (UseWPF=true) + Panuon.WPF.UI 1.3.0.2
- **架构模式**: MVVM with CommunityToolkit.Mvvm

**核心功能库：**
- **串口通信**: System.IO.Ports 10.0.2
- **WMI 查询**: System.Management 10.0.2
- **本地存储**: LiteDB 5.0.21 (文档数据库)
- **脚本引擎**: NLua 1.7.8 (Lua 脚本支持)
- **代码编辑器**: AvalonEdit 6.3.1.120 (语法高亮、代码补全)
- **图表库**: ScottPlot.WPF 5.1.57 (实时数据可视化)
- **配置管理**: Microsoft.Extensions.Configuration.Json 10.0.2
- **依赖注入**: Microsoft.Extensions.DependencyInjection 10.0.2
- **MVVM 工具包**: CommunityToolkit.Mvvm 8.4.0

**开发工具：**
- **测试框架**: xUnit 2.9.3
- **断言库**: FluentAssertions 8.8.0
- **模拟框架**: Moq 4.20.72
- **代码覆盖率**: coverlet.collector 6.0.4
- **测试 SDK**: Microsoft.NET.Test.Sdk 17.14.1
- **测试运行器**: xunit.runner.visualstudio 3.1.4

### 代码质量配置
- **可为空引用类型**: 启用
- **隐式 using**: 启用
- **语言版本**: latest
- **代码分析**: 启用 .NET 分析器，警告视为错误
- **代码风格**: 通过 .editorconfig 配置

### 开发环境要求
1. **.NET SDK**: 10.0.102 或更高版本
2. **操作系统**: Windows 10/11 (WPF 应用)
3. **开发工具**:
   - VS Code with C# Dev Kit
4. **Git**: 版本控制

### 项目进度跟踪
- **ROADMAP.md**: 功能开发计划与优先级
- **README.md**: 项目说明和快速开始指南
- **Git 提交**: 遵循 Emoji 提交规范
- **测试覆盖**: 所有功能必须包含单元测试

## 7. 开发最佳实践

### 命名规范
- **项目命名**: `FlexComDotnet.{Layer}` (如 Core, Tests)
- **命名空间**: `FlexComDotnet.{Layer}.Features.{Feature}.{SubFolder}`
- **接口**: `I{Name}` (如 `ISerialPortService`)
- **ViewModel**: `{Feature}ViewModel` (如 `SerialConfigViewModel`)
- **测试类**: `{ClassName}Tests` (如 `SerialPortServiceTests`)
- **私有字段**: `_camelCase` (如 `_serialPortService`)
- **静态字段**: `s_camelCase` (如 `s_instance`)

### 功能开发流程
1. **需求分析**: 参考 ROADMAP.md 确定功能优先级
2. **TDD 实施**: 先写测试，再实现功能
3. **模块化设计**: 在 Features/ 目录下创建功能模块
4. **依赖注入**: 通过 DI 容器注册服务 (见 `ServiceCollectionExtensions.cs`)
5. **UI 绑定**: 使用 MVVM 模式分离业务逻辑和界面
6. **测试覆盖**: 确保所有公共 API 都有测试
7. **文档更新**: 更新相关文档和示例

### 代码组织原则
1. **单一职责**: 每个类/方法只做一件事
2. **开放封闭**: 对扩展开放，对修改封闭
3. **依赖倒置**: 依赖抽象，不依赖具体实现
4. **接口隔离**: 客户端不应依赖不需要的接口
5. **里氏替换**: 子类可以替换父类

### 主题系统开发规范
- **DynamicResource**: UI 元素颜色必须使用 `{DynamicResource BrushName}` 绑定
- **主题资源**: 新增颜色需同时在 `DarkTheme.xaml` 和 `LightTheme.xaml` 中定义
- **控件样式**: 全局控件样式覆盖定义在主题文件底部
- **命名规范**: 颜色用 `{Name}Color`，画刷用 `{Name}Brush`

### 错误处理策略
1. **结构化异常**: 使用 try-catch-finally
2. **日志记录**: 记录详细错误信息和上下文
3. **用户反馈**: 提供友好的错误提示
4. **资源清理**: 确保异常时资源正确释放
5. **恢复机制**: 提供错误恢复选项

### 性能优化指南
1. **异步操作**: 使用 async/await 避免 UI 阻塞
2. **内存管理**: 及时释放非托管资源
3. **数据绑定**: 使用 ObservableCollection 优化 UI 更新
4. **缓存策略**: 合理缓存频繁访问的数据
5. **延迟加载**: 按需加载资源和数据

## 8. 策略模式应用规范

### 何时使用策略模式
当功能涉及多种可互换算法或行为时（如校验算法、协议解析、回复规则），应采用策略模式：

1. **定义接口**: 创建 `I{Feature}Algorithm` 或 `I{Feature}Handler` 接口
2. **实现策略**: 每种算法/行为作为独立类实现接口
3. **策略注册**: 通过枚举或工厂管理策略映射
4. **服务封装**: 创建 `I{Feature}Service` 作为策略选择器

### 示例：校验算法策略
```csharp
// 接口定义
public interface IChecksumAlgorithm
{
    ChecksumAlgorithmType Type { get; }
    string DisplayName { get; }
    string Description { get; }
    int ResultLength { get; }
    byte[] Calculate(byte[] data);
    string CalculateAsHexString(byte[] data);
}

// 具体策略 (位于 Services/Algorithms/ 目录)
public class Crc16ModbusAlgorithm : ChecksumAlgorithmBase { ... }
public class Md5Algorithm : ChecksumAlgorithmBase { ... }

// 服务封装
public interface IChecksumService
{
    IEnumerable<IChecksumAlgorithm> GetAllAlgorithms();
    IChecksumAlgorithm GetAlgorithm(ChecksumAlgorithmType type);
}
```

### 示例：自动回复处理器策略
```csharp
// 接口定义
public interface IReplyHandler
{
    ReplyMode Mode { get; }
    string DisplayName { get; }
    string Description { get; }
    ReplyResult Process(byte[] receivedData, AutoReplyConfig config);
    void Reset(AutoReplyConfig config);
}

// 具体策略 (位于 Services/Handlers/ 目录)
public class MatchReplyHandler : IReplyHandler { ... }      // 匹配回复
public class SequentialReplyHandler : IReplyHandler { ... } // 顺序回复
public class ScriptReplyHandler : IReplyHandler { ... }     // 脚本回复

// 服务封装
public interface IAutoReplyService
{
    void Start();
    void Stop();
    void UpdateConfig(AutoReplyConfig config);
    event EventHandler<ReplyEventArgs>? ReplyTriggered;
}
```

### 扩展新算法/处理器
1. 在对应枚举中添加新类型 (如 `ChecksumAlgorithmType`, `ReplyMode`, `ProtocolType`)
2. 创建实现对应接口的策略类
3. 在服务构造函数中注册新策略
4. 无需修改 ViewModel 或 UI 层代码

### 示例：协议解析器策略
```csharp
// 接口定义
public interface IProtocolParser
{
    ProtocolType Type { get; }
    bool TryExtractFrame(byte[] buffer, out byte[] frame, out int consumed);
    bool Validate(byte[] frame);
    ParsedFrame Parse(byte[] frame, FrameDefinition definition);
}

// 具体策略 (位于 Services/Parsers/ 目录)
public class ConfigurableParser : IProtocolParser { ... }   // 通用可配置解析器
public class Dlt645Parser : IProtocolParser { ... }         // DL/T 645-2007 协议

// 服务封装
public interface IProtocolParserService
{
    IProtocolParser GetParser(ProtocolType type);
    IEnumerable<ProtocolType> GetSupportedTypes();
}
```

## 9. 测试规范

### 测试文件组织
- 测试文件位于 `tests/FlexComDotnet.Tests/Features/{Feature}/`
- 测试类命名: `{ClassName}Tests.cs`
- 测试方法命名: `{Method}_When{Condition}_Should{ExpectedBehavior}` 或 `{Method}_Should{ExpectedBehavior}`

### 测试模式
```csharp
[Fact]
public void MethodName_ShouldExpectedBehavior()
{
    // Arrange
    var service = new SomeService();
    
    // Act
    var result = service.DoSomething();
    
    // Assert
    result.Should().BeTrue();
}
```

### 常用断言 (FluentAssertions)
```csharp
result.Should().BeTrue();
result.Should().BeFalse();
result.Should().BeNull();
result.Should().NotBeNull();
result.Should().Be(expected);
result.Should().BeEquivalentTo(expected);
collection.Should().HaveCount(3);
collection.Should().Contain(item);
action.Should().Throw<ArgumentException>();
action.Should().NotThrow();
```

### Mock 使用 (Moq)
```csharp
var mockService = new Mock<ISerialPortService>();
mockService.Setup(s => s.IsConnected).Returns(true);
mockService.Setup(s => s.Send(It.IsAny<byte[]>())).Returns(true);
mockService.Verify(s => s.Send(It.IsAny<byte[]>()), Times.Once);
```

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

### 服务注册位置
所有服务在 `src/FlexComDotnet/Services/ServiceCollectionExtensions.cs` 中注册。

### 生命周期规则
- **Singleton**: 服务类 (如 `ISerialPortService`, `IConfigurationService`, `IScriptEngine`)
- **Transient**: ViewModel 类 (如 `SerialConfigViewModel`, `AutoReplyViewModel`)

### 注册示例
```csharp
// 单例服务
services.AddSingleton<ISerialPortService, SerialPortService>();

// 带工厂的单例
services.AddSingleton<IScriptEngine>(sp =>
{
    var engine = new ScriptEngine();
    var bridge = sp.GetRequiredService<IScriptApiBridge>();
    engine.RegisterApiBridge(bridge);
    return engine;
});

// 瞬态 ViewModel
services.AddTransient<SerialConfigViewModel>();
```

## 12. 常见问题与注意事项

### XAML 缩进
- XAML 文件使用 2 空格缩进 (见 .editorconfig)
- C# 文件使用 4 空格缩进

### 跨线程 UI 更新
WPF 要求 UI 更新必须在主线程执行：
```csharp
Application.Current.Dispatcher.Invoke(() =>
{
    // UI 更新代码
});
```

### 可为空引用类型
项目启用了 `<Nullable>enable</Nullable>`，注意：
- 使用 `?` 标记可空类型
- 使用 `!` 断言非空（谨慎使用）
- 优先使用 null 检查或 null 合并运算符

### CommunityToolkit.Mvvm 特性
```csharp
// 自动生成属性和通知
[ObservableProperty]
private string _name;

// 关联命令可执行状态
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
private bool _isValid;

// 自动生成命令
[RelayCommand]
private void Save() { ... }

[RelayCommand(CanExecute = nameof(CanSave))]
private void Save() { ... }
```
