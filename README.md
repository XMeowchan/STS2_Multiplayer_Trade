# STS2 Multiplayer Trade

这是一个面向《Slay the Spire 2》的联机交易 Mod 工作区。

当前版本目标：

- 在联机 run 中提供玩家之间的金币、药水、遗物交易
- 尽量复用游戏原生多人界面与视觉风格
- 保留 Windows-first 的构建、部署、安装器、便携包、GitHub Release 与匿名 telemetry / Pages 曲线链路

## 当前目录

| 路径 | 作用 |
| --- | --- |
| `src/` | Mod 本体代码：交易协议、运行时、UI patch、自动更新、匿名 telemetry 客户端 |
| `pack_assets/` | 打进 `.pck` 的静态资源 |
| `scripts/` | 构建、部署、便携包、安装器、Release、Pages 生成脚本 |
| `installer/` | Inno Setup 安装器脚本 |
| `telemetry/` | Cloudflare Worker + D1 的匿名心跳统计服务 |
| `.github/workflows/` | Release / Pages 工作流 |

## 关键文件

- `Sts2MultiplayerTrade.json`
- `config.json`
- `src/ModEntry.cs`
- `src/TradeSessionManager.cs`
- `src/TradeProposalPopup.cs`
- `src/MultiplayerPlayerExpandedStatePatches.cs`

## 运行时文件布局

部署到游戏 `mods` 目录后，目标结构为：

```text
Slay the Spire 2\mods\Sts2MultiplayerTrade
  Sts2MultiplayerTrade.dll
  Sts2MultiplayerTrade.pck
  Sts2MultiplayerTrade.json
  config.cfg
```

## 常用命令

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-mod-artifacts.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\deploy.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-portable-package.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-pages-data.ps1
```

## 配置来源

- 仓库内源码默认配置文件：`config.json`
- 发布/部署后实际生效文件：`config.cfg`

当前主要配置项：

- `trade_enabled`
- `trade_invite_timeout_seconds`
- `trade_session_timeout_seconds`
- `trade_allow_gold`
- `trade_allow_potions`
- `trade_allow_relics`
- `mod_update_enabled`
- `mod_update_github_repo`
- `telemetry_enabled`
- `telemetry_endpoint`
- `telemetry_endpoints`

## 当前实现事实

- 交易入口挂在原生 `NMultiplayerPlayerExpandedState` 中，不劫持玩家状态条整行点击
- 交易消息使用游戏现有 `INetMessage` 总线
- 交易会话绑定 `RunLocation`，离开当前房间会取消
- 自动更新以 GitHub Release 里的 portable zip 为唯一更新源
- telemetry 与 Pages 只统计匿名安装/活跃趋势，不记录交易内容
