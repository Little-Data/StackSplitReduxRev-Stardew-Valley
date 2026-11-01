using System;
using System.Collections.Generic;
using StardewValley.Menus;
using StardewValley;
using StackSplitRedux.UI;

namespace StackSplitRedux.MenuHandlers
    {
    /// <summary>Handles <see cref="ItemGrabMenu"/>, i.e. chests, fridges, dressers and similar containers.</summary>
    /// <remarks>
    /// <para>The player inventory pane is split through <see cref="InventoryHandler"/> and parks the split-off stack in
    /// the menu's own <c>heldItem</c>, exactly like the game's native right-click does.</para>
    /// <para>The container pane is handled here and only when the menu really shows a separate container: menus that
    /// hide their receiving pane (shipping bin, event item pickers) are left to vanilla behavior.</para>
    /// </remarks>
    public class ItemGrabMenuHandler : BaseMenuHandler<ItemGrabMenu>
        {
        private Item HoverItem = null;
        private int TotalItems = 0;

        /// <summary>True when the split input box was opened by clicking the player inventory.</summary>
        private bool SplittingPlayerInventory = false;

        public ItemGrabMenuHandler()
            : base() {
            this.HasInventory = true;
            }

        protected override bool CanOpenSplitMenu() {
            if (this.NativeMenu?.heldItem != null) return false;

            // A menu that discards the held item on exit would eat a split-off stack the player did not place again.
            if (this.NativeMenu?.HeldItemExitBehavior == ItemExitBehavior.Discard) return false;

            return this.NativeMenu is not null && this.NativeMenu.allowRightClick && base.CanOpenSplitMenu();
            }

        protected override void InitInventory() {
            try {
                base.InitInventory();

                // ItemGrabMenu derives from MenuWithInventory and draws its own heldItem; the player's cursor slot is
                // never drawn while it is open.
                if (this.InvHandler.Initialized
                    && ContainerMenuAccess.TryGetHeldItemAccessors(this.NativeMenu, out var heldGetter, out var heldSetter)) {
                    this.InvHandler.UseMenuHeldItem(heldGetter, heldSetter);
                    Log.TraceIfD($"[{nameof(ItemGrabMenuHandler)}] Player inventory will use the menu's heldItem (showReceivingMenu={this.NativeMenu.showReceivingMenu})");
                    }
                }
            catch (Exception e) {
                Log.ErrorAlways($"[{nameof(ItemGrabMenuHandler)}.{nameof(InitInventory)}] {e}");
                }
            }

        private static int IndexOfReference(IList<Item> list, Item item) {
            if (list is null || item is null) return -1;
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], item)) return i;
            return -1;
            }

        /// <summary>Split a stack of the player inventory into the menu's held item.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled InventoryClicked() {
            try {
                this.InvHandler.SelectItem(Game1.getMouseX(true), Game1.getMouseY(true));
                if (!this.InvHandler.CanSplitSelectedItem()) {
                    // Explain why the click did nothing, but only when the item itself is the reason. The click is
                    // consumed so the game does not also pick the item up.
                    Item hovered = this.InvHandler.GetHoveredItem();
                    if (ItemSplitRules.IsUnsplittable(hovered)) {
                        Log.TraceIfD(
                            $"[{nameof(ItemGrabMenuHandler)}] Cannot split {InventoryHandler.Describe(hovered)} " +
                            "from the player inventory; showing the hint"
                            );
                        CannotSplitHint.Show(hovered);
                        this.InvHandler.ClearSelection();
                        return EInputHandled.Consumed;
                        }
                    return EInputHandled.NotHandled;
                    }

                this.HoverItem = null;
                this.SplittingPlayerInventory = true;
                this.SplitMenu = new StackSplitMenu(this.OnStackAmountReceived, this.InvHandler.GetDefaultSplitStackAmount());
                Log.TraceIfD(
                    $"[{nameof(ItemGrabMenuHandler)}] Split box opened for player inventory: " +
                    $"{InventoryHandler.Describe(this.InvHandler.GetHoveredItem())}, default {this.SplitMenu.HeldStackAmount}"
                    );
                return EInputHandled.Consumed;
                }
            catch (Exception e) {
                Log.ErrorAlways($"[{nameof(ItemGrabMenuHandler)}.{nameof(InventoryClicked)}] {e}");
                this.SplittingPlayerInventory = false;
                return EInputHandled.NotHandled;
                }
            }

        /// <summary>Split a stack of the container pane.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled OpenSplitMenu() {
            try {
                if (!this.HasContainerPane(out InventoryMenu pane))
                    return EInputHandled.NotHandled;

                // Use the item under the mouse instead of a cached hover so a stale tooltip can never pick the wrong one.
                int mouseX = Game1.getMouseX(true);
                int mouseY = Game1.getMouseY(true);
                if (!pane.isWithinBounds(mouseX, mouseY))
                    return EInputHandled.NotHandled;

                Item item = pane.getItemAt(mouseX, mouseY);
                if (item is null)
                    return EInputHandled.NotHandled;
                if (ItemSplitRules.IsUnsplittable(item)) {
                    Log.TraceIfD($"[{nameof(ItemGrabMenuHandler)}] Cannot split container item {InventoryHandler.Describe(item)}; showing the hint");
                    CannotSplitHint.Show(item);
                    this.HoverItem = null;
                    return EInputHandled.Consumed;
                    }

                this.HoverItem = item;
                this.TotalItems = item.Stack;
                this.SplittingPlayerInventory = false;
                this.SplitMenu = new StackSplitMenu(this.OnStackAmountReceived, (this.TotalItems + 1) / 2);
                Log.TraceIfD(
                    $"[{nameof(ItemGrabMenuHandler)}] Split box opened for container item " +
                    $"{InventoryHandler.Describe(item)} at ({mouseX},{mouseY})"
                    );
                return EInputHandled.Consumed;
                }
            catch (Exception e) {
                Log.ErrorAlways($"[{nameof(ItemGrabMenuHandler)}.{nameof(OpenSplitMenu)}] {e}");
                this.HoverItem = null;
                return EInputHandled.NotHandled;
                }
            }

        /// <summary>Callback given to the split menu that is invoked when a value is submitted.</summary>
        /// <param name="s">The user input.</param>
        protected override void OnStackAmountReceived(string s) {
            try {
                if (int.TryParse(s, out int amount) && amount > 0) {
                    if (this.SplittingPlayerInventory)
                        this.InvHandler.SplitSelectedItem(amount);
                    else
                        this.TakeFromContainer(amount);
                    }
                }
            catch (Exception e) {
                // The move may be half applied; stop using this menu type instead of risking a second mutation.
                this.MarkFault(e, nameof(OnStackAmountReceived));
                }
            finally {
                this.SplittingPlayerInventory = false;
                this.HoverItem = null;
                }

            base.OnStackAmountReceived(s);
            }

        /// <summary>Checks the menu really shows a container pane separate from the player inventory.</summary>
        /// <param name="pane">The container pane.</param>
        /// <returns>True if the pane is a real container.</returns>
        private bool HasContainerPane(out InventoryMenu pane) {
            pane = null;
            ItemGrabMenu menu = this.NativeMenu;
            if (menu is null || !menu.showReceivingMenu) {
                Log.TraceIfD($"[{nameof(ItemGrabMenuHandler)}] No container pane: showReceivingMenu={menu?.showReceivingMenu}");
                return false;
                }

            pane = menu.ItemsToGrabMenu;
            if (pane?.actualInventory is null) return false;

            // Menus created without an inventory (shipping bin, event item pickers) point the pane at the player's own
            // item list; those are not containers and are left alone.
            return !ReferenceEquals(pane.actualInventory, Game1.player?.Items);
            }

        /// <summary>Moves the requested amount out of the clicked container slot.</summary>
        /// <param name="amount">The requested amount.</param>
        private void TakeFromContainer(int amount) {
            Item hovered = this.HoverItem;
            if (hovered is null) return;

            ItemGrabMenu menu = this.NativeMenu;

            // The menu may have been closed, or something may have been picked up, while the player was typing.
            if (menu is null || menu.heldItem is not null) return;
            if (!this.HasContainerPane(out InventoryMenu pane)) return;

            int index = IndexOfReference(pane.actualInventory, hovered);
            if (index < 0) return;

            amount = Math.Min(amount, Math.Min(hovered.Stack, this.TotalItems));
            if (amount <= 0) return;

            Log.TraceIfD(
                $"[{nameof(ItemGrabMenuHandler)}] Taking {amount} of {InventoryHandler.Describe(hovered)} " +
                $"from container slot {index} into the menu's held item"
                );

            Item take = hovered.getOne();
            if (take is null) return;
            take.Stack = amount;

            int remaining = hovered.Stack - amount;
            hovered.Stack = remaining;
            if (remaining <= 0)
                pane.actualInventory[index] = null;

            menu.heldItem = take;

            // Vanilla calls this when an item is taken out of the receiving pane (the Adventurer's Guild reward menu
            // tracks collected rewards there). Splitting bypasses the menu's own click handling, so do it ourselves.
            try {
                menu.behaviorOnItemGrab?.Invoke(take, Game1.player);
                Log.TraceIfD($"[{nameof(ItemGrabMenuHandler)}] Container slot {index} left with {InventoryHandler.Describe(hovered)}, held {InventoryHandler.Describe(take)}");
                }
            catch (Exception e) {
                Log.ErrorAlways($"[{nameof(ItemGrabMenuHandler)}.{nameof(TakeFromContainer)}] A grab callback failed: {e}");
                }
            }

        protected override EInputHandled CancelMove() {
            CloseSplitMenu();
            this.SplittingPlayerInventory = false;
            this.HoverItem = null;
            return EInputHandled.Consumed;
            }
        }
    }
