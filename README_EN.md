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
| Support other mods' menus | true | Automatically split inside inventory menus added by other mods; turn off if one misbehaves |
| Show "can't split" hint | true | Show a hint when the clicked item cannot be split |
| "Can't split" hint text | Cannot be split or stacked. | Leave empty to use the built-in text |
| Hint display time (seconds) | 2 | How long the hint stays on screen (no fade) |
| Hint position offset (X) | 0 | Horizontal offset from the cursor, positive = right |
| Hint position offset (Y) | 0 | Vertical offset from the cursor, positive = down |
| Hint text size (%) | 100 | Percentage of the game's small font |
| Hint outline thickness | 2 | Black outline in pixels, 0 = none |
| Hint shadow strength (%) | 0 | 0 = no shadow |
| Hint text colour | #FFFFFF | Hex colour such as `#FFFFFF`, `#FFF` or `#AARRGGBB` |
| Horizontal offset (X) | 0 | Input box offset from the cursor, positive = right |
| Vertical offset (Y) | -64 | Input box offset from the cursor, positive = down |

Cancel operations with: Esc, E, cancel button, clicking elsewhere, or entering 0. Confirm with Enter or OK button.

### Manual Configuration

Edit `config.json` in the mod folder:

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

Registers a **menu type** with the handler it resolves to. It returns `true` only when the type or one of its base classes already has a handler, for example:

- types derived from a supported menu: `ShopMenu`, `ItemGrabMenu`, `GameMenu`, `CraftingPage`, `JunimoNoteMenu`, `GeodeMenu`, `QuestContainerMenu`, `StorageContainer`;
- or types that were already registered by auto-detection.

A brand-new unrelated menu type returns `false` (the debug log says why). In that case rely on the structural auto-detection below, or give the menu the same members.

Registration is per type, no instance is needed; the type is checked again when the menu actually opens, so a mismatched handler is never attached.

Add this to your `ModEntry.cs` `Entry()` method:

```csharp
public override void Entry(IModHelper helper)
{
    var api = helper.ModRegistry.GetApi<StackSplitRedux.IStackSplitAPI>("littledata.StackSplitReduxRev");
    api?.TryRegisterMenu(typeof(MyCustomMenu));
}
```

### Auto-Detection (No Code Required)

As long as "Support other mods' menus" (`EnableGenericMenuSupport`, on by default) is enabled, the mod recognises unknown menus by their **members** (not by class name or namespace) and registers the matching handler:

| Detection | Handler | Behavior |
|-----------|---------|----------|
| The type is `ItemGrabMenu` or derives from it | `ItemGrabMenuHandler` | Split in both the container and the player inventory |
| Container-shaped menu: has `ItemsToGrabMenu` (`InventoryMenu`), or has `inventory` and derives from `MenuWithInventory` | `ContainerMenuHandler` | Player inventory can be split; the container pane depends on what the menu offers |
| Any other menu with the player's `inventory` and a readable/writable `heldItem` | `InventoryOnlyHandler` | Single-inventory split (e.g. Geode Menu) |
| None of the above | not handled | Left entirely to the vanilla game |

Whether the container pane can be split depends on what the menu itself offers, in this order:

1. `TryToGrab(Item, int)` / `TryToPlace(Item, int)` exist: items move through the menu's own methods, so its counters/scores stay in sync;
2. a readable/writable `heldItem` exists and there is no item-change callback: split items are put on the menu's own cursor;
3. the menu has an item-change callback (e.g. the fair grange display) or no safe way to move items: the container pane is left to vanilla, only the player inventory is handled.

Notes:

- Split items need a cursor the menu actually **draws** (`heldItem`, or the player cursor on the inventory page). Without one the mod refuses to split rather than park items where they cannot be seen.
- If a menu type fails at runtime, only that type is disabled (and logged); other menus keep working.
- Turning "Support other mods' menus" off disables all of the above auto-detection; only the built-in known menus (see the API section) are still handled.

## Project History

**Original Mod:** ["StackSplitX" mod](https://github.com/tstaples/StackSplitX)

**Stack Split Redux:** [Stack Split Redux](https://github.com/pepoluan/StackSplitRedux)

## License

MIT License
