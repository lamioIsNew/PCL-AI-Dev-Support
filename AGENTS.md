# AGENTS.md

本仓库是 `Meloong-Git/PCL` 的个人分支（PCL2 Minecraft 启动器），Windows 专用。当前 `main` 与上游一致；本分支的 AI 相关改动尚未落在主代码里，不要假设已有 AI 功能。

## 环境与构建

- 构建需要完整 Visual Studio（含 .NET 桌面开发工作负载）提供的 x64 MSBuild，以及 .NET Framework 4.8 Developer Pack。`dotnet msbuild` 不能正确生成 VB/WPF 主工程；缺少 .NET SDK 组件的 Build Tools 会报 `MSB4236: 找不到 Microsoft.NET.Sdk`，此时应补装工作负载，不要改项目文件。
- `msbuild` 通常不在 PATH。可用 `vswhere.exe -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe` 定位。
- 若 MSBuild 报缺少 `Microsoft.NET.Sdk`，先在当前 shell 设置 `DOTNET_ROOT` 和 `DOTNET_HOST_PATH`（例如 `C:\Program Files\dotnet` 与 `C:\Program Files\dotnet\dotnet.exe`）再重试；不要改项目文件。
- 首次克隆后必须初始化子模块：`git submodule update --init --recursive`。`MeloongCore` 是解决方案必需项目，缺失时主工程无法加载/编译。修改子模块前先读 `MeloongCore/AGENTS.md`。
- 本地构建（PowerShell；路径含空格，必须加引号）：

  ```powershell
  git submodule update --init --recursive
  dotnet restore PCLCS\PCLCS.csproj
  msbuild "Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" -p:Configuration=Debug
  ```

  - 配置为 `Debug` / `Release` / `Snapshot`，产物在 `Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe`。
  - 主工程通过 `ProjectReference` 构建 `PCLCS` 和 `MeloongCore`，并把 `PCLCS\bin\*.dll` 作为嵌入资源打包；不要只单独编译 `PCLCS` 后手工复制 DLL。
  - 本地构建不需要 CI 的 `Help.zip` 更新步骤（`Plain Craft Launcher 2\Resources\Help.zip` 已入库）。CI 还会替换 `ModSecret.vb` 中的密钥和 `ModBase.CommitHash`；本地保持空值即可。
- 主仓库没有测试工程；测试只在 `MeloongCore/Tests`（TUnit，net48/Windows）。
- 本地编译产物和测试统一放在仓库根目录的 `dist\`；该目录已加入 `.gitignore`，不要把构建或测试运行产物提交到仓库。每次编译后新建 `dist\Release<VersionBaseName>\`（例如 `dist\Release2.99.0.1\`），把 `Plain Craft Launcher 2\bin\` 的内容复制进去，并删除旧版本目录；PCL 会在 exe 旁创建 `PCL\` 数据目录。
- `.github/workflows/build.yml` 只监听 `Plain Craft Launcher 2/**` 和 `.github/workflows/**`；只改 `PCLCS` 或 `MeloongCore` 不会触发构建检查。

## 代码结构

- 入口是 `Plain Craft Launcher 2\Application.xaml.vb`：初始化 MeloongCore、日志和单例，然后加载 `FormMain`。Debug 构建还会调用 `Modules\ModDevelop.vb` 的 `Start()`（目前为空，适合放调试实验）。
- `Modules\Mod*.vb` 是全局 `Module` 和业务逻辑（无 DI）；`Modules\Base\ModLoader.vb` 是任务/加载器框架，`Modules\Base\ModNet.vb` 是网络层。
- `Pages\PageX\PageX.xaml` + `.xaml.vb` 是页面；页面切换和导航集中在 `FormMain.xaml.vb`（约 1300–1500 行），左侧栏是各 `PageXLeft`。
- `PCLCS\` 是新的 C# 辅助库（net48、Nullable、C# 13、PolySharp）；`MeloongCore\` 是共享基础库子模块。
- 主工程是旧式 vbproj：新增页面必须同时添加 `<Compile Include="...xaml.vb">` 和 `<Page Include="...xaml">`，否则不会编译。

## 约定与陷阱

- VB 工程使用 `Option Strict Off`，代码以 `Module` 和共享状态为主。新代码沿用 `Mod*.vb` 风格，不要引入大型框架或 DI 重构。
- 注释和 UI 文案使用中文，源文件编码为 UTF-8（无 BOM）。PowerShell 5.1 控制台可能把中文显示成乱码，不要据此判断文件编码。
- 所有设置项必须先在 `Pages\PageSetup\Settings.vb` 的 `Settings.Entries` 注册；`Settings.Get/Set` 对未注册键会抛 `KeyNotFoundException`。加密项禁止使用 `GetSafe`。
- UI 线程使用 `RunInUi` / `RunInUiWait`，后台使用 `RunInThread` / `RunInNewThread`；日志统一用 `Logger`，不要用 `Debug.WriteLine` 或 `Console`。
- 密钥只从环境变量读取：`PCL_MS_CLIENT_ID`、`PCL_CURSEFORGE_API_KEY`（见 `Modules\ModSecret.vb`）。不要提交真实密钥；文件中的空白值是有意为之。
- `patches\` 是历史发行补丁归档，不要手改；源码改动不要混入这些文件。
- 编译完成后将产物放在/dist中，以便用户查看软件进度。