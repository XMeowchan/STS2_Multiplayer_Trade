# 项目共识

这是一个《Slay the Spire 2》联机交易 Mod 工作区，不再是纯本地模板。

## 当前目标

1. 在联机 run 中提供金币、药水、遗物交易
2. UI 尽量原生 Like，优先复用现有多人展开详情页与原生 modal 层
3. 保持 Windows-first 构建、部署、安装器、便携包、Release 流程
4. 提供 GitHub 自动更新、匿名 telemetry 与 Pages 曲线页

## 明确不做

- 不做卡牌交易
- 不做三方/多人撮合
- 不做交易记录云同步、账号系统、风控后端
- 不把 `collector/`、`syncer/`、卡牌统计数据链路迁回本项目

## 运行时结构

- manifest：`Sts2MultiplayerTrade.json`
- 发布配置：`config.cfg`
- 自动更新缓存：`_update_runtime`

## 开发优先级

1. 先保证 `dll/pck/json/cfg` 布局与脚本链路稳定
2. 再保证交易协议与 Host 裁决正确
3. 再做 UI 原生化与视觉细节

## 验收最低线

1. `build-mod-artifacts.ps1` 可生成 `dll + pck`
2. `build-portable-package.ps1` 可生成 portable zip
3. `build-installer.ps1` 可生成 Setup.exe
4. `build-pages-data.ps1` 可生成公开曲线页静态文件
5. 交易入口不破坏原生 `NMultiplayerPlayerExpandedState` 打开行为
