# STS2 Mod Template Agent Guide

## 这是什么

这是一个已经清理过的通用 STS2 Mod 模板工作区，不再包含卡牌数据、小黑盒、遥测、远程数据同步或自动更新逻辑。

后续 Agent 接手时，默认把它当成一个“本地功能型 Mod 模板”，而不是带外部服务依赖的数据项目。

## 当前目标

- 用最小骨架支撑新的 STS2 Mod 开发
- 保留稳定的 Windows-first 构建、部署、安装和发布流程
- 避免把项目重新长成带采集器、同步器、遥测服务的复杂系统，除非用户明确要求

## 目录职责

| 路径 | 作用 |
| --- | --- |
| `src/` | Mod 本体代码 |
| `pack_assets/` | 打进 `.pck` 的静态资源 |
| `scripts/` | 构建、部署、安装、打包、发版脚本 |
| `installer/` | Inno Setup 安装器脚本 |
| `.github/workflows/` | 发布工作流 |

## Agent 工作规则

### 先看哪些文件

- 初始化和配置：`src/ModEntry.cs`, `src/ModConfig.cs`
- 现有示例补丁：`src/MultiplayerModListPatches.cs`
- 构建与部署：`scripts/build-mod-artifacts.ps1`, `scripts/deploy.ps1`
- 打包与发版：`scripts/build-portable-package.ps1`, `scripts/build-installer.ps1`, `scripts/publish-release.ps1`
- 元信息：`mod_manifest.json`, `config.json`

### 默认不要做的事

- 不要重新加回卡牌数据采集、同步、远程拉数逻辑
- 不要默认加遥测、自动更新、联网依赖
- 不要把需要外部登录态、浏览器环境、第三方接口的链路塞回模板

### 默认要做的事

- 优先做最小改动
- 优先沿用现有 PowerShell 脚本链路
- 如果增加新资源，确认 `.pck` 打包仍然可用
- 如果改安装/打包逻辑，同时检查 `deploy / portable / installer / release`

## 验收清单

提交前至少确认：

1. `mod_manifest.json` 和实际构建产物名称是否一致
2. `deploy.ps1` 是否还能把 Mod 正常放进游戏 `mods` 目录
3. `build-portable-package.ps1` 和 `build-installer.ps1` 是否仍然能产出安装包
4. 是否无意中重新引入了模板明确去掉的卡牌数据逻辑或外部服务依赖
5. `.pck` header 是否仍保持 Godot 4.5.x 兼容补丁流程

## 文档优先级

1. 代码
2. 脚本
3. `README.md`
4. 其他说明文档
