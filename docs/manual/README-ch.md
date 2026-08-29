# 任务导向深度指南 — Runtime / Orchestration / Backtest（中文）

> **English version: [README-en.md](README-en.md)** · [返回文档首页](../Manual.md)

## 这个文件夹是什么

本文件夹是**核心库之上**那几层——**Runtime / Orchestration / Backtest** 栈
（`Quant.Infra.Net.Runtime`、`Quant.Infra.Net.Orchestration`、`Quant.Infra.Net.Backtest`）的
**任务导向深度参考**。一个问题一个文件：「怎么配……」「怎么写自己的策略……」「怎么接自己的
数据源/券商……」。所有代码示例都对照本仓库当前源码逐字核对过（类名、方法签名、参数名）。

它**不是**又一类教程。本仓库文档分四类，本文件夹只是其中一类：

| 类别 | 位置 | 回答的问题 |
|------|------|-----------|
| **核心库 API 参考** | [../Manual.md](../Manual.md) | 核心库 `Quant.Infra.Net` 各模块（数据源、券商、分析、通知、组合）怎么调用 |
| **快速上手教程（5 分钟走一遍）** | [../UnifiedRuntimeQuickStart-ch.md](../UnifiedRuntimeQuickStart-ch.md)、[../OrchestrationQuickStart-ch.md](../OrchestrationQuickStart-ch.md)、[../BacktestQuickStart-ch.md](../BacktestQuickStart-ch.md) | 手把手跑通自带 Demo 宿主，改一个配置 |
| **完整图文教程（一个范例走到底）** | [../CompleteWalkthrough-ch.md](../CompleteWalkthrough-ch.md) | 一个真实策略（QQQM 逆向 MA200 定投）从想法 → 自定义阶段 → 回测 → 纸面盘，附真实运行输出 |
| **任务导向深度指南（本文件夹）** | [README-ch.md](README-ch.md) | 超出 Demo 之后你需要的一切：全字段配置参考、写自己的策略、自定义风控/数据源/券商、测试与部署、FAQ |
| **架构与设计说明** | [../Architect.md](../Architect.md)、[../OrchestrationLayerDesign.md](../OrchestrationLayerDesign.md)、[../TradingRuntimeDesign.md](../TradingRuntimeDesign.md) | 为什么这样分层、契约、D1/D2 机制 |

## 指南清单


| 指南 | 英文 | 中文 | 状态 |
|------|------|------|------|
| **配置参考** — `RuntimeOptions` / `OrchestrationOptions` / `BacktestOptions` 全字段、全部枚举 | [configuration-reference-en.md](configuration-reference-en.md) | [configuration-reference-ch.md](configuration-reference-ch.md) | ✅ 就绪 |
| **从零写一个策略** — `Strategy` 基类、描述符、`customStages`、内置策略示例 | [writing-a-strategy-en.md](writing-a-strategy-en.md) | [writing-a-strategy-ch.md](writing-a-strategy-ch.md) | ✅ 就绪 |
| **风控** — 三条默认规则、`IRiskManager`、kill-switch、自定义风控 | [risk-management-en.md](risk-management-en.md) | [risk-management-ch.md](risk-management-ch.md) | ✅ 就绪 |
| **自定义数据源** — `ITraditionalFinanceSourceDataService`、`DataSourceKind.Custom`、何时该新增枚举值 | [custom-data-source-en.md](custom-data-source-en.md) | [custom-data-source-ch.md](custom-data-source-ch.md) | ✅ 已就绪 |
| **自定义券商执行** — `IExecutionBroker`、适配器、`customBroker` 入口 | [custom-broker-en.md](custom-broker-en.md) | [custom-broker-ch.md](custom-broker-ch.md) | ✅ 就绪 |
| **测试与部署** — 给策略写单测、Backtest/Paper 一致性、Paper/Live 长跑、崩溃恢复 | [testing-and-deployment-en.md](testing-and-deployment-en.md) | [testing-and-deployment-ch.md](testing-and-deployment-ch.md) | ✅ 就绪 |
| **FAQ** — 常见报错与其"预期行为" | [faq-en.md](faq-en.md) | [faq-ch.md](faq-ch.md) | ✅ 就绪 |
| **MCP Server（AI Agent 接入）** — 4 个工具、Claude Desktop 配置、SOLID 数据源、Agent Prompt 模式 | [mcp-server-en.md](mcp-server-en.md) | [mcp-server-ch.md](mcp-server-ch.md) | ✅ 新增 |

## 建议阅读顺序

1. [配置参考](configuration-reference-ch.md) — 动手之前先把所有旋钮认全。
2. [写策略](writing-a-strategy-ch.md) — 最常见的真实任务。
3. 只有需要超出默认配置时，再看：[风控](risk-management-ch.md)、[自定义数据源](custom-data-source-ch.md)、[自定义券商](custom-broker-ch.md)。
4. 部署之前：[测试与部署](testing-and-deployment-ch.md) 与 [FAQ](faq-ch.md)。


