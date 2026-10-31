# PCL2 - DevRemake
__TODO__

1. License合规要求 ✅（已在本轮完成）
合规性需要在开发初期保留，遵守《PCL 分发有限许可》，仓库需要按照AGENT.md里的要求同步到github上。
   - 保留未改动的 `LICENCE`；README 与关于页均注明本项目为第三方二次创作，非 PCL 官方版本。
   - 关于页首位保留龙腾猫跃署名及赞助链接；新增本分支说明卡片与许可证入口。
   - 移除与“赞助解锁”相关的土豆码入口、识别码入口和 Release 版赞助遮罩，保留赞助链接本身。

2. 修改软件名称 ✅（已在本轮完成）
将这个新的版本更名为PCL - DTA （全称PCL-Developer Tools Addons，这个全称需要写到“关于”界面里，放到“许可证与版权声明”栏的下面。）
   - 窗口标题、程序集 Title/Product/Description、关于页版本条目均改为 `PCL - DTA`。
   - “许可与版权声明”卡片下方新增 `PCL - DTA` 卡片，写明全称 `PCL-Developer Tools Addons`。

3. 新增插件功能 🚧（顶栏入口已完成）
这个模块很复杂，在上栏UI中新增“插件”选项，后期会更新本TODO.md。
   - 已在顶栏“设置”和“更多”之间新增“插件”入口，并提供占位页面 `Pages\PagePlugin\PagePlugin.xaml`。
   - 插件浏览、安装、启用和配置等具体功能等待后续 TODO 细化。

4. 新增关于Java的AI开发MCP功能 🚧（设置入口已完成）
这个模块也很复杂，将这个UI先添加到“设置”里。后续会更新TODO.md，以便让你知晓更多关于本模块的细节。
   - 已在“设置”左侧导航新增“AI 开发”页面 `Pages\PageSetup\PageSetupAI.xaml`。
   - 页面包含 MCP 服务说明、服务开关（开发中）、监听端口与启动按钮占位；具体功能等待后续 TODO。 

5. 制作要求
需要下载什么东西可以使用我的proxy：127.0.0.1:7897

6. 交付要求
按照AGENT.md里的做