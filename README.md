# Stack Split Redux Rev

简体中文 | [English](/README_EN.md)

自定义堆叠拆分模组，最初基于 StackSplitX。该版本基于 Stack Split Redux

## 功能

按住修饰键（默认 LeftShift）并右键点击物品，弹出输入框指定拆分数量。

### 支持界面

- 背包
- 商店（购买/出售）
- 箱子等容器
- 制作/烹饪
- 通用兜底：自动检测包含库存的第三方模组菜单

## 安装

1. 安装 SMAPI - 从 [smapi.io](https://smapi.io) 下载
2. 从 [GitHub release](https://github.com/Little-Data/StackSplitReduxRev-Stardew-Valley/releases) 获取最新版本
3. 将模组解压到 `Stardew Valley/Mods/StackSplitReduxRev`
4. 启动游戏

## 配置

通过 [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) 配置：

| 设置 | 默认值 | 说明 |
|------|--------|------|
| 默认烹饪/背包中选择数量 | 1 | 在烹饪/背包中按下 修饰键 + 右键 时的默认选择数量 |
| 默认商店数量 | 5 | 商店菜单中 修饰键+右键 时的默认数量 |
| 修饰键 | LeftShift | 按住此键并右键点击以打开拆分输入 |
| 启用调试模式 | false | 启用详细日志以协助问题排查 |
| 水平偏移 (X) | 0 | 正数=向右，负数=向左 |
| 垂直偏移 (Y) | -64 | 正数=向下，负数=向上 |

按 Esc、E、取消按钮、鼠标点击其它地方和输入 0 均可取消操作，输入后按 Enter 或 OK 确认按钮即可确认。

### 手动配置

编辑模组文件夹中的 `config.json`：

```json
{
  "DefaultCraftingAmount": 1,
  "DefaultShopAmount": 5,
  "DebuggingMode": false,
  "ModifierKey": "LeftShift",
  "InputBoxOffsetX": 0,
  "InputBoxOffsetY": -64
}
```

## 技术细节

### 需求

- **[SMAPI 4.0.0+](https://smapi.io/)** - 星露谷物语模组框架
- **[星露谷物语 1.6+](https://store.steampowered.com/app/413150/Stardew_Valley/)** - 基础游戏
- **[.NET 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)**（随游戏附带，编译 dll 所需）

### 可选依赖

- **[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)** - 用于游戏内配置

## 开发

### 从源码构建

构建前确保你已经有了星露谷游戏本体

在包含 `StackSplitReduxRev.csproj` 的目录中使用：

```bash
dotnet build
```

手动指定游戏安装目录：

```bash
dotnet build -c Release /p:GamePath="[GamePath]"
```

### 本地化翻译

参考 `i18n` 文件夹中已有的翻译，新建一个 json 语言文件。

## 第三方模组集成

其他模组可在其 `ModEntry.cs`（或主入口文件）的 `Entry()` 方法中调用 API 注册自定义菜单，获得堆叠拆分功能：

```csharp
public override void Entry(IModHelper helper)
{
    var stackSplit = helper.ModRegistry.GetApi<StackSplitRedux.IStackSplitAPI>("littledata.StackSplitReduxRev");
    if (stackSplit != null)
    {
        stackSplit.TryRegisterMenu(typeof(MyCustomMenu));
    }
}
```

注册的菜单需要包含名为 `inventory` 的 `InventoryMenu` 公共字段。

### 自动检测

本模组会自动检测同时包含以下字段的菜单，自动注册 `ItemGrabMenuHandler`，无需第三方模组编写任何集成代码：
- `inventory` 字段（`InventoryMenu`）
- `ItemsToGrabMenu` 字段（`InventoryMenu`）

如果你的模组菜单满足此条件，开箱即用。

## 项目历史

**原始Mod：** ["StackSplitX" mod](https://github.com/tstaples/StackSplitX)

**Stack Split Redux** [Stack Split Redux](https://github.com/pepoluan/StackSplitRedux)

## 开源许可

MIT License