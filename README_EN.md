# Stack Split Redux Rev

Custom stack splitting mod, originally based on StackSplitX. This fork is based on Stack Split Redux.

## Features

Hold the modifier key (default LeftShift) and RightClick on an item to open an input box for specifying the split amount.

### Supported Interfaces

- Player inventory
- Shops (buy/sell)
- Containers (chests, etc.)
- Crafting / Cooking
- Generic fallback: auto-detects third-party mod menus with inventory support

## Installation

1. Install SMAPI - download from [smapi.io](https://smapi.io)
2. Get the latest release from [GitHub releases](https://github.com/Little-Data/StackSplitReduxRev-Stardew-Valley/releases)
3. Extract the mod to `Stardew Valley/Mods/StackSplitReduxRev`
4. Launch the game

## Configuration

Configure via [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098):

| Setting | Default | Description |
|---------|---------|-------------|
| Default cooking/inventory amount | 1 | Default amount when pressing Modifier + RightClick in Cooking / Inventory |
| Default shop amount | 5 | Default amount when pressing Modifier + RightClick in a Shop menu |
| Modifier key | LeftShift | Hold this key and RightClick to open the split input |
| Enable debug mode | false | Enable detailed logging for troubleshooting |
| Horizontal offset (X) | 0 | Positive = right, negative = left |
| Vertical offset (Y) | -64 | Positive = down, negative = up |

Cancel operations with: Esc, E, cancel button, clicking elsewhere, or entering 0. Confirm with Enter or OK button.

### Manual Configuration

Edit `config.json` in the mod folder:

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

## Technical Details

### Requirements

- **[SMAPI 4.0.0+](https://smapi.io/)** - Stardew Valley modding framework
- **[Stardew Valley 1.6+](https://store.steampowered.com/app/413150/Stardew_Valley/)** - Base game
- **[.NET 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)** (included with the game, needed for compiling the DLL)

### Optional Dependencies

- **[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)** - In-game configuration UI

## Development

### Building from Source

Make sure you have Stardew Valley installed before building.

Build from the directory containing `StackSplitReduxRev.csproj`:

```bash
dotnet build
```

Manually specify the game path:

```bash
dotnet build -c Release /p:GamePath="[GamePath]"
```

### Localization

Refer to existing translations in the `i18n` folder and create a new JSON language file.

## Third-Party Mod Integration

### API

Unique ID: `littledata.StackSplitReduxRev`

Interface: `StackSplitRedux.IStackSplitAPI`

Method: `bool TryRegisterMenu(Type menuType)`

Registers a handler for the given menu type. Returns `true` if a matching handler was found and registered (e.g., the menu inherits from `ShopMenu`, `ItemGrabMenu`, etc.), `false` otherwise.

Add this to your `ModEntry.cs` `Entry()` method:

```csharp
public override void Entry(IModHelper helper)
{
    var api = helper.ModRegistry.GetApi<StackSplitRedux.IStackSplitAPI>("littledata.StackSplitReduxRev");
    api?.TryRegisterMenu(typeof(MyCustomMenu));
}
```

### Auto-Detection (No Code Required)

The mod automatically detects menus with the following field patterns and registers the appropriate handler, no integration code needed:

| Detection | Registered Handler | Behavior |
|-----------|-------------------|----------|
| Both `inventory` + `ItemsToGrabMenu` fields (`InventoryMenu`) | `ItemGrabMenuHandler` | Split/transfer between two inventories |
| `inventory` field (`InventoryMenu`) without `ItemsToGrabMenu` | `InventoryOnlyHandler` | Single-inventory split (e.g., Geode Menu) |

Custom menus matching these criteria work out of the box.

## Project History

**Original Mod:** ["StackSplitX" mod](https://github.com/tstaples/StackSplitX)

**Stack Split Redux:** [Stack Split Redux](https://github.com/pepoluan/StackSplitRedux)

## License

MIT License
