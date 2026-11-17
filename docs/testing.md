# 回归测试

从仓库根目录运行 `python3 scripts/verify.py`（Windows 可用 `python`）。需 Python 3.9+、.NET SDK **10.0.400** 及首次 NuGet 下载网络。SDK 由 global.json 精确固定；依赖图由两个 packages.lock.json 固定。正式门禁使用 locked-mode；有意更新依赖时运行 `dotnet restore RumDice.sln --force-evaluate` 并审阅锁文件差异。

入口依次检查门禁自身的负向样例、还原、Release 全量构建、xUnit 断言测试，并解析本次 TRX。测试为零、有跳过、有失败、计数与结果不一致都返回非零退出码。每次运行先重建本入口独占的 artifacts/verification/，避免旧成功报告混入；请勿在该目录存放手工文件。SDK、原始构建告警、测试数量和 TRX 均保存在此目录。GitHub Actions 在 Ubuntu、Windows、macOS 执行相同命令；项目须在托管平台启用该 workflow，并在需要合并保护时将三个 Verify 检查设为必需。

测试宿主为 net10.0，引用原 net6.0 产品，不运行 Program/Host 或真实平台服务，不需账号与 token。生产目标框架及依赖没有升级；此测试结果不能证明 .NET 6 制品能在目标环境启动。目标框架 NETSDK1138、既有可空引用等告警保持可见，不 blanket suppress、不把本次告警清理扩入范围。首次本地基线与测试数见 Topic T2 的验证记录；每轮精确结果见 summary.json 与 build.log。

真实覆盖包括 EventManager 匹配/作用域/优先级及事件回复、DataCenter JSON 往返/缺失读取/写失败、MsgTool 模板与回复目标。严格接口替身拒绝意外调用；RecordingSender 可记录发送或返回失败，FailingStorage 可复现文件异常，ManualClock 可由测试推进时间。时间和失败替身是后续测试工具，不能据此声称现有 RumTimer、平台重试或原子写入已经修复。

遗留静态单例以 StaticScope 保存并恢复，测试禁用并行。模板夹具只在测试代码内反射替换静态依赖，不调用 CoreData 有文件副作用的构造器。文件测试使用唯一临时沙盒并自动释放；为兼容现有反斜杠路径，legacy root 放在沙盒子路径，确保 Unix 产生的反斜杠文件也留在沙盒内。路径兼容和持久化失败一致性分别由 T3/T4 改进；改变这些契约时应更新相应回归。

新增测试放 tests/RumDice.Tests/，测试真实生产类；不要复制业务实现，也不要使用随机消息、真实网络、固定 sleep 或共享用户数据。Test/Tester.cs 仍是旧人工驱动，不纳入自动化通过依据。

工具接入依据：[xUnit v2 官方入门](https://xunit.net/docs/getting-started/v2/getting-started)、[.NET SDK 选择规则](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)。
