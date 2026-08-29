# How-to Guides — Runtime / Orchestration / Backtest (EN)

> **中文版本见 [README-ch.md](README-ch.md)** · [Back to docs root](../Manual.md)

## What this folder is

This folder is the **task-oriented deep reference** for the layers *above* the core library — the
**Runtime / Orchestration / Backtest** stack (`Quant.Infra.Net.Runtime`, `Quant.Infra.Net.Orchestration`,
`Quant.Infra.Net.Backtest`). One topic per file, one question per page: "how do I configure…", "how do I
write my own strategy…", "how do I plug in my own data source or broker…". Every code snippet in these pages
is verified against the current source in this repository (class names, method signatures, parameter names).

It is **not** a fourth kind of tutorial. The documentation in this repository splits into four distinct
kinds, and this folder is exactly one of them:

| Kind | Where | What it answers |
|------|-------|-----------------|
| **Core library API reference** | [../Manual.md](../Manual.md) | How to call the core `Quant.Infra.Net` modules: data sources, broker services, analysis, notifications, portfolio |
| **Quick-start tutorials (5-minute walkthroughs)** | [../UnifiedRuntimeQuickStart-en.md](../UnifiedRuntimeQuickStart-en.md), [../OrchestrationQuickStart-en.md](../OrchestrationQuickStart-en.md), [../BacktestQuickStart-en.md](../BacktestQuickStart-en.md) | How to run the bundled demo host and swap a single setting, step by step |
| **Complete walkthrough (one example end-to-end)** | [../CompleteWalkthrough-en.md](../CompleteWalkthrough-en.md) | One real strategy (QQQM reverse-MA200 DCA) taken from idea → custom stage → backtest → paper, with the exact run output |
| **Task-oriented deep guides (this folder)** | [README-en.md](README-en.md) | Everything you need when you go beyond the demo: full configuration reference, writing your own strategy, custom risk/data source/broker, testing & deployment, FAQ |
| **Architecture & design rationale** | [../Architect.md](../Architect.md), [../OrchestrationLayerDesign.md](../OrchestrationLayerDesign.md), [../TradingRuntimeDesign.md](../TradingRuntimeDesign.md) | Why the layers are shaped the way they are, contracts, D1/D2 mechanisms |

## The guides



| Guide | English | 中文 | Status |
|-------|---------|------|--------|
| **Configuration reference** — every field of `RuntimeOptions` / `OrchestrationOptions` / `BacktestOptions`, all enums | [configuration-reference-en.md](configuration-reference-en.md) | [configuration-reference-ch.md](configuration-reference-ch.md) | ✅ ready |
| **Writing a strategy from scratch** — `Strategy` base, descriptors, `customStages`, built-in examples | [writing-a-strategy-en.md](writing-a-strategy-en.md) | [writing-a-strategy-ch.md](writing-a-strategy-ch.md) | ✅ ready |
| **Risk management** — the three default rules, `IRiskManager`, kill-switch, custom risk | [risk-management-en.md](risk-management-en.md) | [risk-management-ch.md](risk-management-ch.md) | ✅ ready |
| **Custom data source** — `ITraditionalFinanceSourceDataService`, `DataSourceKind.Custom`, `Custom` vs new enum values | [custom-data-source-en.md](custom-data-source-en.md) | [custom-data-source-ch.md](custom-data-source-ch.md) | ✅ ready |
| **Custom broker execution** — `IExecutionBroker`, adapters, the `customBroker` entry point | [custom-broker-en.md](custom-broker-en.md) | [custom-broker-ch.md](custom-broker-ch.md) | ✅ ready |
| **Testing & deployment** — unit-testing your strategy, Backtest/Paper parity, long-running Paper/Live, crash recovery | [testing-and-deployment-en.md](testing-and-deployment-en.md) | [testing-and-deployment-ch.md](testing-and-deployment-ch.md) | ✅ ready |
| **FAQ** — common failures and their intended behavior | [faq-en.md](faq-en.md) | [faq-ch.md](faq-ch.md) | ✅ ready |

## Reading order suggestion

1. [Configuration reference](configuration-reference-en.md) — know every knob before touching anything.
2. [Writing a strategy](writing-a-strategy-en.md) — the most common real task.
3. Then, only if you need to go beyond defaults: [Risk management](risk-management-en.md),
   [Custom data source](custom-data-source-en.md), [Custom broker](custom-broker-en.md).
4. Before you deploy anything: [Testing & deployment](testing-and-deployment-en.md) and
   [FAQ](faq-en.md).

