<!--markdownlint-disable MD033 MD041-->

<div align="center">

# ClassislandLite-For-ybsz

<img src="ClassIsland/Assets/AppLogo.png" width="150" alt="ClassislandLite Logo" />

> **ClassislandLite** —— 基于 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 修改的校园课表工具
> 为 **四川省宜宾市第四中学校** 定制开发，遵循 **GNU General Public License v3.0** 开源

![License](https://img.shields.io/badge/License-GPL--3.0-blue?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-8-512bd4?style=flat-square)
![Avalonia](https://img.shields.io/badge/Avalonia-11-8a2be2?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078d4?style=flat-square)
![Version](https://img.shields.io/badge/Version-2.1.0.1-success?style=flat-square)

</div>

---

## 1. 项目概览

**ClassislandLite** 是面向班级多媒体大屏的课表显示工具，基于开源项目
[ClassIsland](https://github.com/ClassIsland/ClassIsland) 修改而来。

本仓库是**针对四川省宜宾市第四中学校的实际情况定制**的版本，并非上游官方发行版。
修改方向有两个：**一是轻量化**（移除本校用不到的重型模块，减小体积、降低维护成本），
**二是贴合本校场景**（食谱预览、远程提醒等专用功能）。

- 运行平台：Windows 10 / 11（x64）
- 技术栈：.NET 8 + Avalonia 11
- 当前版本：`2.1.0.1 (Codename Lite)`
- 安装包大小：约 97 MB（单文件安装包，内嵌运行时）

## 2. 与上游的关系与开源协议

| 项目 | 说明 |
| --- | --- |
| 上游项目 | [ClassIsland/ClassIsland](https://github.com/ClassIsland/ClassIsland) |
| 原作者 | HelloWRC 及 ClassIsland 全体贡献者 |
| 本项目性质 | 二次开发（fork），非官方发行版 |
| 开源协议 | **GNU General Public License v3.0**（见 `LICENSE.txt`） |
| 本仓库修改 | 同样以 GPL-3.0 发布，修改声明见 `NOTICE.md` |

> 依照 GPL-3.0，本仓库完整开放修改后的源代码；任何人可以自由使用、修改、再分发，
> 但再分发时必须保留同样的许可证与原作者署名。

## 3. 相对上游的改动

### 3.1 精简：为轻量化移除的模块

本校只需要"课表 + 提醒 + 天气"，因此把上游较重的模块做了裁剪：

- **自动化（Automation）**：不再注册自动化相关的设置入口与触发器/动作界面
- **插件市场 / 插件中心**：移除插件安装与市场入口，避免额外依赖与安全面
- **教程中心**：移除教程引导模块，减少资源体积
- **娱乐模式、全屏强调提醒**：移除这些与教学场景无关的展示功能
- **设置页裁剪**：仅保留 通用 / 主界面 / 天气 / 提醒 / 实验性功能 / 分贝检测器 / 主题 / 关于
- **资源与依赖精简**：安装包控制在 100 MB 以内，冷启动更快

### 3.2 新增：为本校定制的功能

- **食谱预览**：读取学校食堂的 Excel 菜单（午餐 / 晚餐、加餐分类），
  在主界面窗口内以气泡形式滚动展示，支持独立缩放与字号调整
- **远程提醒**：老师可从网页向所有教室电脑推送全屏大字提醒，
  每 5 秒播放一次提示音；服务端可部署在 Cloudflare Worker（含 D1 存储），
  客户端支持**多服务器地址自动切换**、DoH 兜底解析与边缘 IP 竞速，
  在国内网络环境下也能稳定收消息；可选用阿里云函数计算（FC）做国内反代
- **分贝检测器**：调用麦克风实时显示班级音量，可按课程时段自动显隐
- **课程分组 ×N 合并**：同一科目连续多节时，可在「档案 → 科目」里按科目开启
  「重复出现时合并显示」，显示为 `数学×2`；未开启的科目逐节显示
- **次日课表 + 次日天气**：最后一节课 / 放学后自动切到次日课表，
  并在第二行显示次日天气（含天气图标与雨雪提示），当日天气自动让位
- **版本回滚**：对配置与档案做快照，出问题可一键恢复
- **单双周合并**：时间表选择器里自动把单周 / 双周的同名课程合并为一组
- **CSES 课表导入**：导入时保留原有时间表名称，不再被统一重命名
- **配置导出 / 导入**：便于在机房多台电脑间同步配置
- **Lite 品牌化**：应用名、图标、安装器与快捷方式统一为 ClassIsland Lite

### 3.3 修复与优化

- 天气数据恢复为上游的小米天气接口
- 修复组件自隐藏后主界面残留空块的问题（组件隐藏会同步整行收起）
- 统一「次日课表」与「次日天气」的显示判定，避免课表切了、天气没切
- 远程提醒在无 VPN 环境下可用：多地址 + DNS over HTTPS + 边缘 IP 自动优选
- 安装器支持覆盖升级，保留用户配置与档案

## 4. 定制说明（四川省宜宾市第四中学校）

- 本版本的功能取舍、默认配置、界面文案（含关于页）均按本校使用习惯调整
- 食谱预览读取的菜单文件由本校自行维护，仓库不包含任何校内数据
- 远程提醒的服务器地址、通知标题等属于各校部署配置，仓库不内置任何密钥
- 若你需要用于其他学校，请自行调整配置与文案；本项目不提供"通用版"支持

## 5. 编译与打包

环境要求：**.NET 9 SDK**（用于构建）与 **.NET 8 运行时**、Windows 10/11。

```bash
# 1. 编译主程序（Release）
dotnet build src/ClassIsland/ClassIsland.csproj -c Release -p:ClassIsland_PlatformTarget=x64

# 2. 发布为自包含目录
dotnet publish src/ClassIsland.Desktop/ClassIsland.Desktop.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishBuilding=true -p:PublishPlatform=windows \
  -p:ClassIsland_PlatformTarget=x64 -p:Platforms_Windows=true \
  -p:PublishSingleFile=false -p:PublishTrimmed=false \
  -o dist/ClassislandLite-by-Yu/app-2.1.0.1-0

# 3. 生成安装包（Inno Setup 6）
tools/InnoSetup6/ISCC.exe tools/ClassislandLite-setup.iss
```

> 说明：本仓库未提供 CI 流水线，构建产物不入库。

## 6. 反馈与联系

- 维护者：高 2026 级某位同学（四川省宜宾市第四中学校）
- 联系方式：**tty180819@gmail.com**
- 问题反馈：欢迎在 Issues 中说明版本号、复现步骤与截图
- 上游项目：<https://github.com/ClassIsland/ClassIsland>

## 7. 免责声明

- 本项目是**个人二次开发**的定制版本，与 ClassIsland 官方无隶属关系，
  官方不对本版本的功能与稳定性负责
- 本项目按 **GPL-3.0** 以"现状"提供，不附带任何明示或暗示的担保；
  使用本软件造成的任何直接或间接损失，作者不承担责任
- 若本项目中的任何修改、资源或文案**侵犯了你的权益**，
  请通过 **tty180819@gmail.com** 联系，核实后将立即删除相关内容
- 本项目仅用于本校教学场景，不得用于商业用途

---

<div align="center">

**感谢 ClassIsland 全体开发同学**，以及所有开源依赖的作者。

</div>
