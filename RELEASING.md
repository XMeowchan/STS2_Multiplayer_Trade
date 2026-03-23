# Releasing

本项目固定发布两类产物：

- `Sts2MultiplayerTrade-Setup-x.y.z.exe`
- `Sts2MultiplayerTrade-portable-x.y.z.zip`

玩家端自动更新只使用 `portable zip`，不使用安装器。

## 本地发布

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

脚本会：

- 构建 `dll + pck`
- 生成 portable zip
- 生成 installer exe
- 在配置证书时对 DLL / Setup.exe 做代码签名
- 校验 `.pck` header 仍保持 Godot 4.5 兼容
- 生成 release notes

## 直接上传 GitHub Release

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Upload -Repo owner/repo
```

当前简化上传流程依赖 `gh`。

## Pages

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-pages-data.ps1
```

该命令会生成：

- `dist/pages/index.html`
- `dist/pages/usage-stats.json`
- `dist/pages/users-history.svg`

## 运行时远端能力

- 自动更新：读取 GitHub latest release，并下载 portable zip
- 匿名统计：每日最多一次 heartbeat
- 公开曲线页：从 telemetry stats 接口生成安装/活跃趋势图
