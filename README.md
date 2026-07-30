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

### API

唯一ID：`littledata.StackSplitReduxRev`

接口：`StackSplitRedux.IStackSplitAPI`

方法：`bool TryRegisterMenu(Type menuType)`

注册指定菜单类型的处理器。如果该类型已有对应的处理器（如继承 `ShopMenu`、`ItemGrabMenu` 等），则注册成功并返回 `true`，否则返回 `false`。

在 `ModEntry.cs` 的 `Entry()` 中：

```csharp
public override void Entry(IModHelper helper)
{
    var api = helper.ModRegistry.GetApi<StackSplitRedux.IStackSplitAPI>("littledata.StackSplitReduxRev");
    api?.TryRegisterMenu(typeof(MyCustomMenu));
}
```

### 自动检测（无需代码）

本模组会自动检测以下特征的菜单并注册对应的处理器，无需第三方模组编写任何代码：

| 检测条件 | 注册的处理器 | 效果 |
|---------|------------|------|
| 同时有 `inventory` + `ItemsToGrabMenu` 字段 (`InventoryMenu`) | `ItemGrabMenuHandler` | 支持两栏库存间拆分转移 |
| 有 `inventory` 字段 (`InventoryMenu`) 但不含 `ItemsToGrabMenu` | `InventoryOnlyHandler` | 支持单栏库存拆分（如铁匠砸晶石界面） |

满足以上条件的自定义菜单开箱即用。

## 项目历史

**原始Mod：** ["StackSplitX" mod](https://github.com/tstaples/StackSplitX)

**Stack Split Redux** [Stack Split Redux](https://github.com/pepoluan/StackSplitRedux)

## 开源许可

MIT License