# STS2 Multiplayer Trade Telemetry

Cloudflare Worker + D1 服务，用于记录匿名安装/活跃统计。

当前接口：

- `POST /v1/heartbeat`
- `GET /v1/stats.json`

设计约束：

- 不记录交易内容
- 不记录双方资产
- 不记录 run / 房间 / floor 等业务细节
- 只记录匿名安装活跃趋势
