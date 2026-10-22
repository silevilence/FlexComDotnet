# Windows 串口监控驱动签名研究与 OSSign 准备建议

研究日期：2026-10-22。仓库现状基线：`decda10`。政策、服务开放状态和价格可能变化，正式申请前必须重新核实。

## 1. 结论与适用范围

FlexComDotnet 的需求是在 Windows 10/11 上，通过自研 Rust 内核过滤驱动旁路观察其他程序的串口通信，并允许用户正常安装分发包。串口终端、虚拟串口转发和仅支持 USB 总线抓包的工具不能完整替代这一需求。

- 当前驱动使用本地测试证书；Release 构建配置不等于正式签名。
- 对现代 Windows 的新内核驱动，购买普通代码签名证书或给应用 EXE/MSIX 签名，不能替代微软硬件计划的驱动签名流程。[微软驱动签名政策](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/kernel-mode-code-signing-policy--windows-vista-and-later-)
- 未核实到中国大陆无注册经营主体的自然人，可以稳定完成“个人 EV 申请 → 微软硬件账户审核 → 正式驱动发布”的完整流程。此结论是证据不足，不是断言所有自然人在任何地区都不能申请。
- 未找到同时满足“开源、允许再分发、已验证适用于目标现代 Windows 配置的签名、透明串口旁路监控”的现成驱动。
- 优先保留现有驱动，准备向 OSSign 等合格发布方申请合作签名。OSSign 明确支持内核驱动，但当前暂停新申请，大陆个人项目资格未获其书面确认。
- 本研究未联系供应商、提交申请、购买证书或安装第三方驱动。后续计划中的申请和签名集成均未完成。

## 2. 签名路径和组织身份门槛

### 测试签名、Attestation 与 WHQL

| 路径 | 能解决什么 | 不能据此推断什么 |
| --- | --- | --- |
| 本地测试签名 | 开发及受控测试环境加载驱动 | 不能保证默认安全配置的普通用户机器可用 |
| 普通 CA 代码签名 | 证明软件发布者及文件完整性 | 不等于微软内核加载信任；应用签名不覆盖驱动签名 |
| 微软 Attestation | 不提交 HLK 测试日志的微软驱动签名路径 | 当前文档定位为测试用途，不代表 Windows Certified，也不等于正式 WHQL 发布认证 |
| WHQL / WHCP | 经适用测试和提交审核获得认证签名 | 仍需合格发布主体；签名本身不保证所有硬件、系统版本和 HVCI 配置兼容 |

微软当前的 [Driver signing offerings](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/driver-signing-offerings) 将 Attestation 描述为测试用途；[账户证书要求](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/code-signing-reqs)规定账户需关联有效 EV。应由合作发布方明确目标版本、用途和所需认证路径，不能把“免 HLK”写成“免账户审核”，也不能仅因不能经 Windows Update 零售分发，就推断所有 Attestation 驱动都需要 TESTSIGNING。

用户提供的 AccessPort `Apsm.sys` 截图显示签名者 `duanyong`、GlobalSign CodeSigning CA - G2、SHA1，以及 2012-04-22 时间戳。这只能证明该历史文件显示的签名信息，不能证明它是 EV、经过 WHQL，或今天所有新驱动仍可沿用同一方式。微软正在收紧旧交叉签名信任，不能依赖历史证书作为新项目发布路线。[微软驱动安全公告](https://techcommunity.microsoft.com/blog/windows-itpro-blog/advancing-windows-driver-security-removing-trust-for-the-cross-signed-driver-pro/4504818)

### 大陆个人申请 EV 与微软账号

SSL.com 的 [Sole Proprietor EV 产品页](https://www.ssl.com/products/software-integrity/code-signing/ev-sole-proprietor/)宣传个人验证及驱动用途，但其部分核验说明仍围绕组织或注册经营主体；取得证书不能单独证明微软会豁免业务审核。不能仅凭销售页面承诺完整通路，也不能把国外 sole proprietor 自动等同于中国大陆无营业执照的自然人。

[微软硬件计划注册说明](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/hardware-program-register)要求组织账户、EV 和代表组织签约的权限；[业务核验说明](https://learn.microsoft.com/en-us/partner-center/enroll/understand-the-verification-process)要求核验合法注册业务及地址等信息。硬件团队的[账户核验公告](https://techcommunity.microsoft.com/blog/hardware-dev-center/action-required-account-verification-for-windows-hardware-program-begins-october/4455452)将该核验流程用于硬件计划。Microsoft Store 的个人开发者账户不能据此视为硬件签名账户。

微软明确解释 EV 要求旨在核实身份、提交归属和提高可追责性，保护用户免受恶意驱动影响。[微软对 EV 要求的解释](https://techcommunity.microsoft.com/blog/windows-hardware-certification/update-to-ev-certificate-requirement-per-submission/364896)。微软也曾披露恶意驱动通过提交渠道获得签名，并改进准入和验证机制。[MSRC 事件说明](https://www.microsoft.com/en-us/msrc/blog/2021/06/investigating-and-mitigating-malicious-drivers)

研究判断：组织登记、地址和授权关系便于交叉核验，是微软采用的风控机制。没有找到官方充分解释为什么不能提供同等严格的纯自然人通道；不能把组织门槛说成密码学或技术上的必然，也不能据此判断个人开发者的能力。

## 3. 合作签名渠道

“有服务或咨询入口”不等于“已确认接受大陆个人项目并替其承担微软发布主体”。

| 机构 | 已证实的服务范围与开放状态 | 对本项目的结论 |
| --- | --- | --- |
| [OSSign](https://ossign.org/#faq) | 赞助公司是微软硬件计划成员；主要提供 Attestation，必要时可提供 WHQL；当前暂停新申请 | 最匹配的赞助路线；等待恢复并确认大陆作者、驱动类型、正式发布和再分发条件 |
| [SignPath Foundation](https://signpath.org/apply.html) | 保留免费开源签名申请入口；其人员在 2023 年公开说明不能直接提供微软驱动发布主体，需要另找赞助组织 | 可辅助应用签名或已有赞助方的签名管理，不能视为完整替代；未找到后来开放完整内核赞助的公告 |
| [Jungo](https://windriver.jungo.com/digital-driver-signing/) | 商业驱动签名及 Attestation 咨询；页面同时涉及所有驱动开发者和 WinDriver 客户 | 可询问，但未确认接收任意自研 Rust 过滤驱动、使用其账号发布或接受大陆个人；未找到免费开源计划 |
| [NicSRS / Racent](https://www.nicsrs.com/whql) | 商业 WHQL；FAQ 要求客户用 EV 创建自己的微软账号，套餐材料含营业执照 | 标准服务不能解决纯个人主体门槛 |
| [环度网信](https://www.ihuandu.com/whql.html) | 商业 WHQL；流程明确客户自行注册微软账号，套餐要求营业执照 | 未找到使用其主体赞助个人开源驱动的公开承诺 |
| [Certum](https://support.certum.eu/en/code-signing-required-documents/) | 个人普通代码签名与 EV 是不同产品，核验说明明确 EV 面向组织 | 普通个人开源证书不能解决硬件账户审核 |
| [ReactOS 历史签名计划](https://reactos.org/wiki/Driver_Signing) | 存在历史项目引用，当前页面访问受限，未取得继续受理的可靠证据 | 不能推荐为当前可申请方案 |

SignPath 的范围依据其工作人员的[原始回复](https://github.com/ImageMagick/ImageMagick/discussions/6826)；OSSign 的实际驱动合作案例见 [usbip-win2 签名仓库](https://github.com/OSSign/vadimgrn--usbip-win2)。这些证据不等于本项目已获接收。

如向服务商询问，需明确：大陆个人、无企业/EV/硬件账号、MIT 自研串口过滤驱动；询问是否由对方审核源码并控制构建，以对方作为发布主体完成微软签名，允许项目再分发，以及认证用途、更新费用和漏洞响应安排。仅协助购买自己的 EV 或注册自己的账号不解决本项目的当前门槛。

## 4. 现成驱动与替代技术调查

| 候选 | 功能、许可与签名证据 | 判断 |
| --- | --- | --- |
| [ENLYZE PortSniffer](https://github.com/enlyze/PortSniffer) | MIT，串口/并口过滤监控；官方 2.1 amd64 包内 `EnlyzePortSniffer.sys` 本次本地检查为 `NotSigned`，无 CAT；微软签名的 WDF 协安装 DLL 不能替代 SYS 签名 | 功能接近，但换用它仍需解决签名 |
| [LemonSerialMonitor](https://github.com/qingningmneg/LemonSerialMonitor) | MIT，串口监控；README 明确本地测试签名及 TESTSIGNING 要求 | 不能解决正式签名问题 |
| [IRPMon](https://github.com/MartinDrab/IRPMon/releases) | MIT，通用 IRP 监控；历史发布说明含个人 CA 签名和 Secure Boot 限制 | 未验证最新发布包满足现代微软签名要求，不能作为即插即用替代 |
| [USBPcap](https://github.com/desowin/usbpcap) | 驱动 GPLv2、命令行 BSD 2-Clause；[1.2.0.3 发布说明](https://github.com/desowin/usbpcap/releases/tag/1.2.0.3)明确微软 Attestation | 抓取 USB URB，需要串口芯片协议解析；不覆盖原生/PCIe 串口；未验证最新二进制和目标 Win11/HVCI 兼容性 |
| [HHD Serial Port Monitoring Control](https://hhdsoftware.com/serial-port-monitoring-control) | 商业监控控件和再分发许可方案 | 非开源，可另评估商业授权，不能作为已找到的开源组件 |

PortSniffer 检查对象为[官方 2.1 amd64 ZIP](https://github.com/enlyze/PortSniffer/releases/download/2.1/ENLYZE-PortSniffer-2.1-amd64.zip)，仅下载解包和读取签名，未加载驱动。未对其余候选的所有版本做二进制验证。

虚拟串口代理需要修改目标程序的端口配置；用户态 API Hook 只覆盖被插桩进程且需处理异步 I/O；这些是需求取舍，不是当前过滤驱动的等价替换。开发测试仍可使用测试签名，但不能把更改终端安全配置作为普通用户正式发行的完成标准。

## 5. OSSign 的明确要求与尚待确认事项

依据 [OSSign 资格条件、FAQ 和暂停公告](https://ossign.org/#apply)：

1. 项目主要采用 OSI 认可的开源许可证；最终发行所包含的代码须可公开审查。
2. 项目积极维护，具有可展示的用户群或社区；账号、组织及项目至少有六个月活动历史，派生项目另有参与历史要求。
3. 仓库具备公开构建流水线，整个构建过程可核验，源码与发布二进制有直接关联，流水线包含代码质量检查。
4. 主要支持志愿维护的免费软件；商业开源项目需另行沟通。其政策不接受明确具有政治或争议目的的项目，包括主要用于进攻性安全研究的工具。
5. 使用机构控制的证书签名，持续满足资格条件；不是向作者交付可自由使用的私钥。
6. 当前暂停新申请；联系表也不接受绕过暂停的签名申请，可以询问一般政策问题。

公开政策未明确保证中国大陆个人项目可获接收；也未公开适用于本项目的流水线接入协议、审批周期、认证路线或最终再分发条件。应在恢复申请后确认，不能凭空预设其 API 或自动签名权限。

固定工具链、依赖清单、产物哈希、签名校验、发布审批和目标机器验证，是本项目为满足可核验构建及可靠发行提出的工程措施。**它们不是 OSSign 官网逐条规定的额外硬性申请条件，也不意味着官网要求逐字节可复现构建。**

## 6. 仓库现状与差距

| 事项 | 已观察到的现状 | 需要补齐的证据或改造 |
| --- | --- | --- |
| 开源许可 | 根目录 [LICENSE](../../LICENSE) 为 MIT | 最终包中依赖、字体、资源、运行时等的来源、许可证及审查入口清单 |
| 项目历史 | 本地最早提交日期 2026-02-02，距研究日期超过六个月 | 公开仓库、维护者账号、持续活动及用户/社区证据；本地提交日期不能单独证明全部资格达标 |
| 公开构建定义 | [release.yml](../../.github/workflows/release.yml) 从版本 tag 构建，包含 C# 测试、Rust 测试及 Clippy，WDK 下载有哈希校验 | 验证日志实际公开可访问；补充日常变更检查及源码—构建—发行产物关联证据 |
| 工具链和依赖 | Rust 使用浮动 `stable`，.NET 使用 `10.0.x`；已有 Cargo.lock | 明确可审查的版本和依赖记录，避免只凭浮动环境解释产物来源 |
| 驱动包 | [Package-DebugDriver.ps1](../../driver/serial-monitor/scripts/Package-DebugDriver.ps1) 对 debug/release 都创建或复用 WDRLocalTestCert，并签 SYS/CAT | 分离测试包与交给合作方的候选包，禁止正式流程回退到测试签名 |
| 应用打包 | [FlexComDotnet.csproj](../../src/FlexComDotnet/FlexComDotnet.csproj) 的 Release 仍调用上述脚本；发布流水线检查并打包测试证书 | 引入已审核签名产物的消费流程，保证应用打包不重新编译或覆盖已签驱动 |
| 发行验证 | 流水线检查驱动文件存在及 INF 版本；MSIX 签名独立配置 | 分别验证应用签名和驱动内核签名；建立目标系统安装、运行、升级及卸载证据 |

这些是静态代码和配置检查结果，不代表本次运行了流水线、完成了驱动测试或核实了 GitHub 账户保护设置。

## 7. 计划与外部依赖

实施任务追加在 [ROADMAP 的计划中分区](../../ROADMAP.md)。顺序为：资格与许可证据 → 公开质量检查和构建关联 → 测试/候选包分离 → 申请材料 → 获批后的签名集成 → 正式包验证与用户说明。

等待项目历史、OSSign 恢复受理、合作方审核及微软认证的时间不计入开发人日。准备材料不等于提交申请，提交申请不等于获批。没有合作方明确接收前，不承诺签名完成日期，不把测试签名包标为正式签名包。
