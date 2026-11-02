using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.MenuHandlers
    {
    public class InventoryHandler
        {
        /// <summary>If the handler has been initialized yet by calling Init.</summary>
        public bool Initialized => this.NativeInventoryMenu != null;

        /// <summary>Convenience for grabbing native inventory buttons.</summary>
        private List<ClickableComponent> Inventory => this.NativeInventoryMenu.inventory;

        /// <summary>Convenience for grabbing native inventory items.</summary>
        private IList<Item> InventoryItems => this.NativeInventoryMenu.actualInventory;

        /// <summary>Native inventory.</summary>
        private InventoryMenu NativeInventoryMenu;

        /// <summary>Inventory interface bounds.</summary>
        private Rectangle Bounds;

        /// <summary>Where the user clicked so moving </summary>
        private int SelectedItemPosition_X;
        private int SelectedItemPosition_Y;

        /// <summary>The getter for the hovered item owned by the parent menu that contains the inventory.</summary>
        private Func<Item> HoveredItemGetter;

        /// <summary>Currently hovered item in the inventory.</summary>
        private Item HoveredItem;
        public Item GetHoveredItem() => this.HoveredItem;

        /// <summary>Forgets the selected item without disabling the handler (used when the click was refused).</summary>
        public void ClearSelection() => this.HoveredItem = null;

        /// <summary>Short description of an item for debug logs.</summary>
        internal static string Describe(Item item) {
            if (item is null) return "none";
            try {
                return $"{item.DisplayName} ({item.QualifiedItemId}) x{item.Stack}";
                }
            catch {
                return item.GetType().Name;
                }
            }

        /// <summary>
        /// Where the split-off items are put while the player is moving them.
        /// </summary>
        /// <remarks>
        /// Menus that derive from <see cref="MenuWithInventory"/> (chests, geode menu, quest drop boxes, ...) keep the
        /// item in their own <c>heldItem</c>, while the inventory tab of the game menu uses
        /// <see cref="Farmer.CursorSlotItem"/>. Using the wrong one makes the split items invisible (and apparently
        /// lost) until the menu is closed, so the owning handler selects the right accessors.
        /// </remarks>
        private Func<Item> CursorItemGetter = static () => Game1.player?.CursorSlotItem;
        private Action<Item> CursorItemSetter = static item => { if (Game1.player is not null) Game1.player.CursorSlotItem = item; };

        /// <summary>Null constructor that currently only invokes the base null constructor</summary>
        public InventoryHandler() {
            }

        /// <summary>Split items into the menu's own <c>heldItem</c> instead of the player's cursor slot.</summary>
        /// <param name="getter">Reads the menu's held item.</param>
        /// <param name="setter">Writes the menu's held item.</param>
        public void UseMenuHeldItem(Func<Item> getter, Action<Item> setter) {
            if (getter is null || setter is null) return;
            this.CursorItemGetter = getter;
            this.CursorItemSetter = setter;
            Log.TraceIfD($"[{nameof(InventoryHandler)}] Split items will be parked in the menu's own held item");
            }

        /// <summary>Split items into <see cref="Farmer.CursorSlotItem"/> (the game menu inventory page behavior).</summary>
        public void UseDefaultCursorItem() {
            this.CursorItemGetter = static () => Game1.player?.CursorSlotItem;
            this.CursorItemSetter = static item => { if (Game1.player is not null) Game1.player.CursorSlotItem = item; };
            }

        /// <summary>This must be called everytime the inventory is opened/resized.</summary>
        /// <param name="inventoryMenu">Native inventory.</param>
        /// <param name="hoveredItemGetter">Reads the native menu's hovered item.</param>
        public void Init(InventoryMenu inventoryMenu, Func<Item> hoveredItemGetter) {
            // Handlers are singletons reused by every menu of their type, so start from the default cursor target.
            this.UseDefaultCursorItem();
            this.HoveredItem = null;

            // Guard instead of Debug.Assert: a failed assert calls Environment.FailFast in .NET 6.
            if (inventoryMenu is null || inventoryMenu.inventory is null || inventoryMenu.inventory.Count == 0) {
                Log.WarnAlways($"[{nameof(InventoryHandler)}.{nameof(Init)}] The menu has no usable inventory; the inventory handler stays disabled.");
                this.NativeInventoryMenu = null;
                this.HoveredItemGetter = null;
                return;
                }

            this.NativeInventoryMenu = inventoryMenu;
            this.HoveredItemGetter = hoveredItemGetter;

            // Create the bounds around the inventory
            var first = this.Inventory[0].bounds;
            var last = this.Inventory.Last().bounds;
            this.Bounds = new Rectangle(
                first.X,
                first.Y,
                last.X + last.Width - first.X,
                last.Y + last.Height - first.Y);

            Log.TraceIfD(
                $"[{nameof(InventoryHandler)}] Inventory bound: {this.Inventory.Count} slots, " +
                $"items={this.InventoryItems.Count}, area={this.Bounds}"
                );
            }

        /// <summary>Forgets the current inventory.</summary>
        /// <remarks>Handlers are singletons, so they must not keep pointing at a menu that has been closed.</remarks>
        public void Reset() {
            if (this.NativeInventoryMenu is not null)
                Log.TraceIfD($"[{nameof(InventoryHandler)}] Reset (was bound to {this.InventoryItems.Count} slots)");
            this.NativeInventoryMenu = null;
            this.HoveredItem = null;
            this.HoveredItemGetter = null;
            this.UseDefaultCursorItem();
            }

        /// <summary>Broad phase check to see if the inventory interface was clicked.</summary>
        /// <param name="mousePos">Mouse position.</param>
        public bool WasClicked(Point mousePos) {
            if (!this.Initialized) return false;
            return this.Bounds.Contains(mousePos);
            }

        /// <summary>Broad phase check to see if the inventory interface was clicked.</summary>
        /// <param name="mouseX">Mouse X position.</param>
        /// <param name="mouseY">Mouse Y position.</param>
        public bool WasClicked(int mouseX, int mouseY) {
            if (!this.Initialized) return false;
            return this.Bounds.Contains(mouseX, mouseY);
            }

        /// <summary>Stores the data needed to be able to split an item stack. This must be called before CanSplitSelectedItem and SplitSelectedItem.</summary>
        /// <param name="mouseX">Mouse x position.</param>
        /// <param name="mouseY">Mouse y position.</param>
        public void SelectItem(int mouseX, int mouseY) {
            if (!this.Initialized || this.HoveredItemGetter is null) return;

            this.SelectedItemPosition_X = mouseX;
            this.SelectedItemPosition_Y = mouseY;
            this.HoveredItem = this.HoveredItemGetter();
            Log.TraceIfD(
                $"[{nameof(InventoryHandler)}] Selected item at ({mouseX},{mouseY}): {Describe(this.HoveredItem)}, " +
                $"cursor={Describe(this.CursorItemGetter?.Invoke())}"
                );
            }

        /// <summary>Checks if the selected item can be split. SelectItem must be called first.</summary>
        /// <returns>True if it can be split.</returns>
        public bool CanSplitSelectedItem() {
            if (!this.Initialized) {
                Log.TraceIfD($"[{nameof(InventoryHandler)}] CanSplit=false: the inventory handler is not initialized");
                return false;
                }

            var hoveredItem = this.HoveredItem;
            Item heldItem;
            try {
                heldItem = this.CursorItemGetter?.Invoke();
                }
            catch {
                heldItem = null;
                }

            if (hoveredItem is null) {
                Log.TraceIfD($"[{nameof(InventoryHandler)}] CanSplit=false: no item under the cursor");
                return false;
                }
            // Item types the game never splits (including recipe items) must not reach the amount box even when a mod
            // left a stack larger than one in the inventory. The callers show the "cannot split" hint afterwards.
            if (ItemSplitRules.IsTypeUnsplittable(hoveredItem)) {
                Log.TraceIfD($"[{nameof(InventoryHandler)}] CanSplit=false: {Describe(hoveredItem)} is a type that never stacks");
                return false;
                }
            if (hoveredItem.Stack <= 1) {
                Log.TraceIfD($"[{nameof(InventoryHandler)}] CanSplit=false: {Describe(hoveredItem)} has only one item");
                return false;
                }
            if (heldItem is null) return true;

            bool canStack = hoveredItem.canStackWith(heldItem);
            bool hasRoom = heldItem.Stack < heldItem.maximumStackSize();
            Log.TraceIfD(
                $"[{nameof(InventoryHandler)}] CanSplit with cursor: {Describe(hoveredItem)} on {Describe(heldItem)}, " +
                $"canStack={canStack}, hasRoom={hasRoom}"
                );
            return canStack && hasRoom;
            }

        /// <summary>Updates the stack values of the hovered and held item.</summary>
        /// <param name="stackAmount">The amount to be added to the held amount.</param>
        public void SplitSelectedItem(int stackAmount) {
            if (stackAmount <= 0) {
                Log.TraceIfD($"[{nameof(InventoryHandler)}] Split skipped: requested amount {stackAmount} is not positive");
                return;
                }
            if (!this.Initialized) return;

            try {
                var hoveredItem = this.HoveredItem;
                if (hoveredItem is null) return;

                // The menu may have changed the slot between the click and this callback; acting on a stale item would
                // remove a stack that was never picked up.
                if (!ReferenceEquals(
                        this.NativeInventoryMenu.getItemAt(this.SelectedItemPosition_X, this.SelectedItemPosition_Y),
                        hoveredItem)) {
                    Log.TraceIfD(
                        $"[{nameof(InventoryHandler)}] Split aborted: the slot no longer holds {Describe(hoveredItem)} " +
                        "(it now holds " + Describe(this.NativeInventoryMenu.getItemAt(this.SelectedItemPosition_X, this.SelectedItemPosition_Y)) + ")"
                        );
                    return;
                    }

                int originalHoveredCount = hoveredItem.Stack;  // Grab & hold the value
                Item previousHeldItem = this.CursorItemGetter?.Invoke();
                int heldItemCount = previousHeldItem?.Stack ?? 0;  // Grab & hold the value
                int maxStack = (previousHeldItem ?? hoveredItem).maximumStackSize();

                // Guard against a cursor stack that changed type while the amount was being entered; merging it would
                // convert the hovered items into another item.
                if (previousHeldItem is not null && !hoveredItem.canStackWith(previousHeldItem)) {
                    Log.TraceIfD(
                        $"[{nameof(InventoryHandler)}] Split aborted: {Describe(hoveredItem)} cannot merge into " +
                        Describe(previousHeldItem)
                        );
                    return;
                    }

                // How many items may actually be moved: limited by the stack and by the free space on the cursor.
                int take = Math.Min(stackAmount, originalHoveredCount);
                take = Math.Min(take, Math.Max(0, maxStack - heldItemCount));
                if (take <= 0) {
                    Log.TraceIfD(
                        $"[{nameof(InventoryHandler)}] Split aborted: no room on the cursor for {Describe(hoveredItem)} " +
                        $"(held {heldItemCount}/{maxStack})"
                        );
                    return;
                    }

                Log.TraceIfD(
                    $"[{nameof(InventoryHandler)}] Splitting {take} of {Describe(hoveredItem)} " +
                    $"(requested {stackAmount}, stack {originalHoveredCount}, cursor {heldItemCount}/{maxStack})"
                    );

                // Run native click code to get the selected item; it performs the game's own side effects (sounds,
                // tool attachment, ...). Its stack math is overwritten below.
                Item heldItem = this.NativeInventoryMenu.rightClick(
                    this.SelectedItemPosition_X, this.SelectedItemPosition_Y, previousHeldItem);
                if (heldItem is null) {
                    Log.TraceIfD($"[{nameof(InventoryHandler)}] Split aborted: the game's click handler returned nothing");
                    return;
                    }

                if (previousHeldItem is not null) {
                    // Something unexpected happened (e.g. a tool attachment); leave the native result untouched.
                    if (!ReferenceEquals(heldItem, previousHeldItem)) {
                        Log.TraceIfD($"[{nameof(InventoryHandler)}] Split aborted: the game replaced the cursor item ({Describe(heldItem)})");
                        return;
                        }

                    // The native click refused to take anything (the menu's highlight method rejected the item, or the
                    // stacks cannot merge). Taking the items anyway would bypass that menu rule, so do nothing.
                    if (hoveredItem.Stack == originalHoveredCount) {
                        Log.TraceIfD($"[{nameof(InventoryHandler)}] Split aborted: the menu refused to hand over {Describe(hoveredItem)}");
                        return;
                        }
                    }

                int remaining = originalHoveredCount - take;
                if (remaining <= 0)
                    // Remove the item from the inventory if it's now all being held.
                    RemoveItemFromInventory(hoveredItem);
                else
                    // Commit the manipulated hovered value, overwriting changes by rightClick() above
                    hoveredItem.Stack = remaining;

                // Commit the new held amount, overwriting changes by rightClick() above
                heldItem.Stack = heldItemCount + take;
                this.CursorItemSetter?.Invoke(heldItem);

                Log.TraceIfD(
                    $"[{nameof(InventoryHandler)}] Split done: inventory {remaining} left, cursor now {Describe(heldItem)}"
                    );
                }
            catch (Exception e) {
                Log.ErrorAlways($"[{nameof(InventoryHandler)}.{nameof(SplitSelectedItem)}] {e}");
                }
            finally {
                // Null it out now that we're done with this operation
                this.HoveredItem = null;
                }
            }

        /// <summary>Runs the default shift+right-click behavior on the selected item.</summary>
        public void CancelSplit() {
            if (this.Initialized && this.HoveredItem != null) {
                Log.TraceIfD($"[{nameof(InventoryHandler)}] Cancelling: running the game's default split for {Describe(this.HoveredItem)}");
                // Split with the default amount to simulate the default behaviour
                SplitSelectedItem(GetDefaultSplitStackAmount());

                // Null it out now that we're done with this operation
                this.HoveredItem = null;
                }
            }

        /// <summary>Gets the stack amount you would usually have when shift+right-clicking.</summary>
        public int GetDefaultSplitStackAmount() {
            if (this.HoveredItem is null) return 1;
            return Math.Max(1, Math.Min(Mod.Config?.DefaultCraftingAmount ?? 1, this.HoveredItem.Stack));
            }

        /// <summary>Removes an item from the native inventory</summary>
        /// <param name="item">The item to remove.</param>
        public void RemoveItemFromInventory(Item item) {
            // Match by reference: two slots may hold equal-looking items, and only the clicked one may be emptied.
            for (int i = 0; i < this.InventoryItems.Count; i++) {
                if (ReferenceEquals(this.InventoryItems[i], item)) {
                    Log.TraceIfD($"[{nameof(InventoryHandler)}] Clearing inventory slot {i} ({Describe(item)})");
                    this.InventoryItems[i] = null;
                    return;
                    }
                }
            Log.TraceIfD($"[{nameof(InventoryHandler)}] Could not find {Describe(item)} in the inventory to clear it");
            }
        }
    }
