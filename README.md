# ReMakePlace 插件（国服维护版）

ReMakePlace 插件可在 FFXIV 中自动保存和加载房屋家具布局，并支持导入/导出原 MakePlace 程序、MakePlace 插件以及新版 [ReMakePlace](https://github.com/RemakePlace/app) 的布局文件。

本仓库是 `blackappleD` 维护的国服维护版，基于 [QiongHHHZZZ/plugin](https://github.com/QiongHHHZZZ/plugin)（上游为 [RemakePlace/plugin](https://github.com/RemakePlace/plugin)），适配国服 Dalamud 并汉化界面，通过 [blackappleD/DalamudPlugins](https://github.com/blackappleD/DalamudPlugins) 插件仓库发布。

## 目录

- [安装](#安装)
- [使用](#使用)
  - [从游戏中保存布局](#从游戏中保存布局)
  - [将布局加载到游戏中](#将布局加载到游戏中)
- [发布新版本](#发布新版本)
- [从源码构建](#从源码构建)
- [常见问题](#常见问题)
- [致谢](#致谢)

## 安装

- 你需要通过支持 Dalamud 的启动器运行游戏。
- 本仓库建议使用 DailyRoutines 维护的 Dalamud：<https://github.com/Dalamud-DailyRoutines/Dalamud>
- 在游戏内输入 `/xlsettings`，或按 `Esc` 打开 Dalamud 设置。
- 进入 “Experimental/实验性” 页面。
- 在 “Custom Plugin Repositories/自定义插件仓库” 中新增以下地址：

  ```text
  https://raw.githubusercontent.com/blackappleD/DalamudPlugins/main/repo.json
  ```

- 点击 `+` 添加，并确认该仓库已启用。
- 点击 “Save and Close/保存并关闭”。
- 输入 `/xlplugins`，或按 `Esc` 打开插件安装窗口。
- 搜索 “ReMakePlace” 并安装 “ReMakePlace（国服维护版）”。

> [!WARNING]
> 本版本的 InternalName 与原版相同（`ReMakePlacePlugin`），请勿与原版或其他仓库提供的 ReMakePlace 同时安装。

> [!NOTE]
> 原上游插件不适用于国服；本版本针对国服 Dalamud（API 15）构建。如无法加载，请先确认你使用的 Dalamud 与 API 版本是否匹配。

## 使用

输入 `/remakeplace`、`/makeplace` 或 `/rmp` 打开插件窗口。大多数功能需要在家具布局模式中使用：房屋菜单 -> 室内/室外家具 -> 点击“旋转”。

### 从游戏中保存布局

- 确认当前处于家具布局模式。
- 点击 `另存为`，选择文件名后保存。

### 将布局加载到游戏中

- 确认当前处于“旋转”家具模式。
- 确认需要处理的楼层已勾选。
- 确认布局需要的家具已经放置在房屋中：
  - 插件不会使用你背包或仓库中的家具。
  - 插件不会自动给家具染色，除非你使用染色相关功能。
  - 重新加载设计后，列表中匹配成功的家具应显示为白色，未匹配或不匹配的项目会变灰。
- 如果需要插件自动移动家具，请勾选 `☑ 应用布局`。
- 点击 `从文件加载`，选择 `.json` 布局文件。
- 如果应用布局时崩溃，请参考 [为什么应用布局时插件有时会崩溃？](#为什么应用布局时插件有时会崩溃)

### 界面说明

![Settings](screenshot.png?raw=true)

- **保存**：保存到当前文件。首次启动时若未选择文件，该按钮不可用。如果想把室内和室外保存到同一个文件，可在室内保存一次，再到室外保存到同一文件。
- **另存为**：保存到指定文件。
- **加载**：从当前文件加载布局。插件会检查当前已放置家具是否与布局匹配，也会检查染色是否匹配；未匹配项目会显示为灰色。
- **从文件加载**：与“加载”相同，但会先选择布局文件。
- **摆放间隔**：应用布局时两次家具摆放之间的间隔。过低（如 200ms）可能导致漏摆。
- **标注家具**：在屏幕上显示家具名称，并提供按钮将布局位置应用到对应家具。
- **物品列表**：点击物品图标或名称即可复制物品名称。
- **显示物品详情**：鼠标悬停在物品列表的家具、材质或装置上时，弹出游戏原生的物品详情。如果同时启用了 Daily Routines 的「更好的市场布告板」模块及其「在道具帮助界面显示市场数据」选项，详情中会附带本服与区域的市场价格。

## 发布新版本

版本号使用四段数字：前三段跟随上游，第 4 段从 `2001` 起递增；上游前三段升级后重新从 `2001` 起（例如上游 `7.5.1` → `7.5.1.2001`）。

1. 修改 `ReMakePlacePlugin/ReMakePlacePlugin.csproj` 中的 `PluginVersion`，并同步根目录 `ReMakePlacePlugin.json` 中的版本与下载链接。
2. 提交并推送 `master`，然后推送 `v<版本号>` 格式的 tag（如 `v7.5.1.2001`）。
3. GitHub Actions 会构建并创建 Release，上传 `ReMakePlacePlugin.zip`。
4. 在 [DalamudPlugins](https://github.com/blackappleD/DalamudPlugins) 中更新 `repo.json` 的 `AssemblyVersion` 与下载链接。

## 从源码构建

本项目使用 `Dalamud.NET.Sdk`，构建时需要本机存在 Dalamud dev 目录。

- 标准 XIVLauncher 默认路径：`%AppData%\XIVLauncher\addon\Hooks\dev\`
- XIVLauncherCN 常见路径：`%AppData%\XIVLauncherCN\addon\Hooks\dev\`

如果构建时报错：

```text
Dalamud.NET.Sdk: Dalamud installation not found at ...\XIVLauncher\addon\Hooks\dev\
```

可以通过设置 `DALAMUD_HOME` 指定实际 Dalamud dev 路径。

PowerShell 示例：

```powershell
$env:DALAMUD_HOME = "$env:AppData\XIVLauncherCN\addon\Hooks\dev"
dotnet build .\ReMakePlacePlugin.sln -c Release
```

cmd 示例：

```cmd
set DALAMUD_HOME=%AppData%\XIVLauncherCN\addon\Hooks\dev
dotnet build ReMakePlacePlugin.sln -c Release
```

## 常见问题

### 插件无法正常工作怎么办？

请先确认：

- Dalamud 可正常加载。
- 插件 API 等级与当前 Dalamud 环境匹配。
- 构建时使用的 `DALAMUD_HOME` 指向正确的 `Hooks\dev` 目录。

如果仍无法解决，可以联系 `blackappleD` 反馈；原版问题也可以到官方 Discord 频道反馈：<https://discord.gg/f2VAqXKWUw>

### 非英语客户端可以使用吗？

可以。保存、加载、导入和导出布局不依赖游戏语言。

### 可以用它复制别人房子的布局吗？

不可以。插件只能在你自己的房屋中、且处于家具布局模式时加载布局。

### 插件会使用背包或仓库中的家具吗？

不会。插件只会操作已经放置在房屋中的家具，不会移动背包或仓库里的物品。由于放置家具会绑定并使其不可交易，建议玩家自行完成家具放置。

### 使用这个插件安全吗？

任何第三方插件都不符合游戏服务条款，使用时始终存在风险。自动摆放家具可能因为操作间隔过短而被服务器检测到；你可以调高摆放间隔来降低风险。

### 如何更新插件？

当插件有更新时，打开 `/xlplugins` 插件窗口并点击“更新插件”。

### 为什么应用布局并退出装修菜单后，家具会贴回地面或墙面？

家具有最低浮空距离限制，低于该距离会被游戏自动贴回地面。壁挂家具也需要与墙或隔断保持最低距离，否则会被贴回墙面。这是游戏限制，需要手动调整设计。

如果使用 ReMakePlace 应用，可以在设置中启用 `Minimum Float Distance`，放置家具时会显示最低浮空距离提示。

### 为什么应用布局时插件有时会崩溃？

请确认以下事项：

- 鼠标指针没有悬停在任何家具上，即使该家具被插件 UI 遮住也不行。
- 角色没有挡在家具将要摆放的位置上。
- 单个家具上不要附着超过 10 个物品，例如桌上物、挂在隔断上的壁挂物等。

这些问题与游戏本身的处理方式有关，目前没有通用解决方案。

## 致谢

本插件是 MakePlace 的延续项目，由 Jawslouis 创建，并参考/致谢以下插件：

- [HousingPos](https://github.com/Bluefissure/HousingPos)
- [BDTH](https://github.com/LeonBlade/BDTHPlugin)
- [HouseMate](https://github.com/lmcintyre/Housemate)
