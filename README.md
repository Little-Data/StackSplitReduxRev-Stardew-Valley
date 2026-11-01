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
| 兼容其它模组的菜单 | true | 自动为其它模组添加的物品菜单启用拆分；出现异常时可关闭 |
| 显示“无法拆分”提示 | true | 点击不可拆分的物品时显示提示 |
| “无法拆分”提示文字 | 无法拆分或堆叠 | 留空则使用内置文字 |
| 提示显示时间（秒） | 2 | 提示在屏幕上停留的时间，无淡出 |
| 提示位置偏移 (X) | 0 | 提示相对鼠标的水平偏移，正数=向右 |
| 提示位置偏移 (Y) | 0 | 提示相对鼠标的垂直偏移，正数=向下 |
| 提示文字大小 (%) | 100 | 以游戏小字号为基准的百分比 |
| 提示文字描边厚度 | 2 | 黑色描边像素厚度，0 = 无描边 |
| 提示文字阴影强度 (%) | 0 | 0 = 无阴影 |
| 提示文字颜色 | #FFFFFF | 十六进制颜色，如 `#FFFFFF`、`#FFF`、`#AARRGGBB` |
| 水平偏移 (X) | 0 | 输入框相对鼠标的水平偏移，正数=向右 |
| 垂直偏移 (Y) | -64 | 输入框相对鼠标的垂直偏移，正数=向下 |

按 Esc、E、取消按钮、鼠标点击其它地方和输入 0 均可取消操作，输入后按 Enter 或 OK 确认按钮即可确认。

### 手动配置

编辑模组文件夹中的 `config.json`：

```json
{
  "DefaultCraftingAmount": 1,
  "DefaultShopAmount": 5,
  "DebuggingMode": false,
  "EnableGenericMenuSupport": true,
  "ShowCannotSplitHint": true,
  "CannotSplitMessage": "",
  "HintDurationSeconds": 2,
  "HintOffsetX": 0,
  "HintOffsetY": 0,
  "HintTextScalePercent": 100,
  "HintOutlineThickness": 2,
  "HintShadowStrength": 0,
  "HintTextColor": "#FFFFFF",
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

唯一 ID：`littledata.StackSplitReduxRev`

接口：`StackSplitRedux.IStackSplitAPI`

方法：`bool TryRegisterMenu(Type menuType)`

把指定**菜单类型**注册到它能匹配到的处理器上。只有该类型本身或它的基类已经有处理器时才返回 `true`，例如：

- 继承自已支持的菜单：`ShopMenu`、`ItemGrabMenu`、`GameMenu`、`CraftingPage`、`JunimoNoteMenu`、`GeodeMenu`、`QuestContainerMenu`、`StorageContainer`；
- 或该类型已经被自动检测注册过。

全新且与上述无关的菜单类型会返回 `false`，并在调试日志中说明原因；这种情况建议改用下面的结构自动检测，或让菜单具备相同的成员结构。

注册只针对类型，不需要传实例；菜单真正打开时还会再校验一次类型，不匹配的处理器不会接手。

在 `ModEntry.cs` 的 `Entry()` 中：

```csharp
public override void Entry(IModHelper helper)
{
    var api = helper.ModRegistry.GetApi<StackSplitRedux.IStackSplitAPI>("littledata.StackSplitReduxRev");
    api?.TryRegisterMenu(typeof(MyCustomMenu));
}
```

### 自动检测（无需代码）

只要开启“兼容其它模组的菜单”（`EnableGenericMenuSupport`，默认开启），本模组就会按**成员结构**（而不是类名或命名空间）识别未知菜单并注册处理器：

| 检测条件 | 使用的处理器 | 效果 |
|---------|------------|------|
| 类型是 `ItemGrabMenu` 或其子类 | `ItemGrabMenuHandler` | 容器栏 + 玩家背包栏均可拆分 |
| 结构像容器菜单：有 `ItemsToGrabMenu`（`InventoryMenu`），或有 `inventory` 且继承 `MenuWithInventory` | `ContainerMenuHandler` | 玩家背包可拆；容器格按菜单自身提供的能力决定 |
| 其它拥有玩家背包 `inventory` 且暴露可读写 `heldItem` 的菜单 | `InventoryOnlyHandler` | 单栏库存拆分（如砸晶石界面） |
| 以上都不满足 | 不接管 | 完全交给原版处理 |

容器格能否拆分取决于菜单自身提供的能力，按以下优先级选择：

1. 有 `TryToGrab(Item, int)` / `TryToPlace(Item, int)`：通过菜单自己的方法移动物品，菜单的计数、评分等状态保持同步；
2. 有可读写的 `heldItem` 且没有物品变更回调：拆分出的物品放进菜单自己的光标；
3. 带物品变更回调（如集市农庄展示）或没有安全的移动方式：容器格保持原版行为，只处理玩家背包。

补充说明：

- 拆分出的物品必须有菜单真正**绘制**的光标（`heldItem`，或背包页的玩家 cursor）；找不到可绘制光标时不会强行拆分，避免物品“看不见”。
- 某个菜单类型在运行中出错时，只会禁用该类型并记录日志，其它菜单不受影响。
- 关闭“兼容其它模组的菜单”后，以上自动检测全部停用，只有内置的已知菜单（见 API 一节）仍会被处理。

## 项目历史

**原始Mod：** ["StackSplitX" mod](https://github.com/tstaples/StackSplitX)

**Stack Split Redux** [Stack Split Redux](https://github.com/pepoluan/StackSplitRedux)

## 开源许可

MIT License

# 更新内容

## 0.14.6

### 修复

- 修复打开格斯的冰箱（特别订单投放箱 `QuestContainerMenu`）、集市农庄展示（`StorageContainer`）等非 `ItemGrabMenu` 容器菜单时游戏直接崩溃的问题：`Debug.Assert` 失败在 .NET 6 下会终止进程，现已移除所有断言并改为失败安全的分支。
- 修复拆分出的物品不可见、看似丢失的问题：物品改为放进菜单自己的 `heldItem`（游戏真正绘制的光标），而不是玩家的隐藏光标槽。
- 修复拆分数量可能超过上限的问题：实际取走量同时受物品堆叠数与光标剩余空间限制。
- 修复菜单高亮规则拒绝该物品时仍强行取走、绕过菜单规则的问题：原生点击没有取走任何东西时直接放弃。
- 修复清空背包格子按“值相等”匹配、可能清空另一个外观相同格子的问题：改为按对象引用匹配。
- 修复商店同种商品重复上架、库存耗尽时可能删错货架项的问题：改为按点击的索引移除。
- 修复容器拒绝写入光标时拆出的一叠物品凭空消失的问题：写入后回读校验，失败则把数量加回原堆并重算菜单状态。
- 修复从容器取出物品后未触发菜单自身 `behaviorOnItemGrab` 回调的问题（例如冒险者公会奖励会被领走却不记账）。
- 修复取消输入框被当作“默认拆分”执行的问题，现在取消不做任何操作。
- 修复切换页面/空页面导致的空引用，以及数量为 0 时误开输入框的问题。
- 修复输入框获得焦点时方向键、数字键影响底层菜单的问题。

### 容器与第三方菜单

- 新增按菜单结构（而非菜单名）的探测：`ItemsToGrabMenu`、`TryToGrab`/`TryToPlace`、`heldItem`、变更回调、`MenuWithInventory`、退出时手持物处理方式，自动选择最安全的移动方式。
- 支持特别订单投放箱、集市农庄展示（玩家背包侧可拆，展示格保持原版行为以避免评分/回调失步），以及结构相同的第三方模组容器。
- 新增“兼容其它模组的菜单”开关（`EnableGenericMenuSupport`，默认开启），可整体关闭对未知菜单的自动接管。
- 只会把菜单交给真正兼容的处理器，不再出现“把菜单交给不支持它的处理器”的旧崩溃路径。

### 故障隔离

- 某个菜单类型一旦出错，只禁用该类型并记录日志；其它菜单照常工作，也不会错误地禁用当前恰好打开的无关菜单。
- 被处理的菜单被别的模组菜单替换时，旧处理器会主动解除绑定。
- 拆分输入框会被强制关闭、不残留；缩放窗口后能正确重开处理器。

### “无法拆分”提示

- 对不可拆分/不可堆叠的物品（壁纸、家具、工具、只剩 1 个、模组不支持的容器格）显示提示，而不是毫无反应；提示出现时该次点击会被吞掉，游戏不会再执行购买/拾取等操作。
- 提示使用游戏自带的 UI 边框，尺寸随文字自适应；位置跟随鼠标（默认在光标上方），并自动避开光标旁的物品 tooltip，既不遮挡它也不会被它遮挡。
- 可配置：开关、提示文字、显示时长（默认 2 秒，无淡出，文字与边框同时出现/消失）、位置偏移 X/Y、文字大小（百分比）、描边厚度、阴影强度（默认 0）、文字颜色（十六进制）。
- 提示绘制于菜单之后（`RenderedActiveMenu`），在任意缩放倍率下都位于菜单之上。

### 数量输入框

- 支持长按数字键连续输入、长按 Backspace/Delete 连续删除。
- 闪烁的“|”光标可用 ←/→ 移动、Home/End 跳到首尾，可在数字中间插入/删除；输入仍限制 4 位。

### 调试与日志

- 调试模式下的日志覆盖全部功能：加载的配置、菜单映射与兜底判断、处理器开关、每一次拆分/转移的决策与物品 ID/数量、商店购买、提示布局与避让、故障与禁用列表。
- 日志写入以及新增的日志语句本身都做了防护，不会因为记录日志导致游戏出错。

## 0.14.5

- 修复部分界面（如砸晶石）无法拆分的问题

## 0.14.4

- 支持星露谷物语 1.6，并包含大量改进与修复。

## 0.14.3

- 正确处理菜单的 `emergencyShutdown`。

## 0.14.2

- 启用拆分动画。

## 0.14.1

- 购买动作改用 `MaxPurchasable` 判断，而不是简单的 `sellPrice`。

## 0.14.0

- 自动把数量限制在可购买上限内。

## 0.13.2

- 修复手持物品数量判断错误。

## 0.13.0

- 支持 Generic Mod Config Menu，大量内部优化，修复 `_materialContainers` 为空时的异常。

## 0.12.1

- 优化合成页处理器，合成/烹饪只播放一次音效。

## 0.12.0

- 支持工作台与烹饪。

## 0.11.0

- 内部改进。

## 0.10.2

- 修复更新检查键。

## 0.10.1

- 修复合成页面问题。

## 0.10.0

- 大量细节改进。

## 0.9.0

- 初始版本。