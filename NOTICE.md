# NOTICE（修改声明 / Modification Notice）

本仓库 **ClassislandLite-For-ybsz** 是对开源项目
[ClassIsland](https://github.com/ClassIsland/ClassIsland) 的修改版本（fork）。

依据 **GNU General Public License v3.0** 第 5 条 a 款，特此声明：

| 项目 | 内容 |
| --- | --- |
| 上游项目 | ClassIsland（<https://github.com/ClassIsland/ClassIsland>） |
| 上游作者 | HelloWRC 及 ClassIsland 全体贡献者 |
| 本仓库性质 | 二次开发版本，**非**上游官方发行版 |
| 修改时间 | 2026 年 11 月起 |
| 维护者 | 高 2026 级某位同学（四川省宜宾市第四中学校） |
| 联系方式 | tty180819@gmail.com |
| 许可证 | GNU General Public License v3.0（见 `LICENSE.txt`） |

## 修改范围概述

- **移除/禁用**：自动化（Automation）相关设置入口、插件市场/插件中心、教程中心、
  娱乐模式、全屏强调提醒等模块的界面与使用路径
- **新增**：食谱预览、远程提醒（含 Cloudflare Worker 服务端与阿里云 FC 反代方案）、
  分贝检测器、课程分组 ×N 合并、次日课表与次日天气、版本回滚、
  单双周合并选择器、CSES 导入保留时间表名、配置导出导入、Lite 品牌化
- **修复**：天气数据源恢复、组件隐藏整行收起的显示问题、次日课表与次日天气判定统一
- **品牌**：应用名称、图标、安装器、快捷方式统一为 ClassIsland Lite

详细改动清单见 `README.md` 的「相对上游的改动」一节。

## 许可证继承

本仓库的全部修改同样以 **GNU GPL v3.0** 发布。任何对本仓库的再分发，
必须：

1. 保留本 NOTICE 与原项目的版权声明；
2. 继续以 GPL-3.0 发布，并提供完整的对应源代码；
3. 不得移除或修改原作者署名。

## 侵权联系

若本仓库中的任何代码、资源或文案侵犯了你的权益，请通过 **tty180819@gmail.com** 联系，
核实后将立即删除相关内容。本项目仅用于本校教学场景，不作商业用途。
