using System;
using StackSplitRedux.UI;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.MenuHandlers
    {
    /// <summary>Handles menus that only expose the player inventory (no container pane we can drive).</summary>
    public class InventoryOnlyHandler : BaseMenuHandler<IClickableMenu>
        {
        public InventoryOnlyHandler()
            : base() {
            }

        /// <summary>Initializes the player inventory, using the menu's own cursor item when it has one.</summary>
        protected override void InitInventory() {
            try {
                base.InitInventory();
                if (!this.InvHandler.Initialized) return;

                // Menus deriving from MenuWithInventory (e.g. GeodeMenu) keep the held item in their own field; using
                // the player's cursor slot instead would make the split items invisible until the menu is closed.
                if (ContainerMenuAccess.TryGetHeldItemAccessors(this.NativeMenu, out Func<Item> getHeld, out Action<Item> setHeld)) {
                    this.InvHandler.UseMenuHeldItem(getHeld, setHeld);
                    Log.TraceIfD($"[{nameof(InventoryOnlyHandler)}] Using the menu's own held item on {this.NativeMenu?.GetType().Name}");
                    }
                }
            catch (Exception e) {
                Log.ErrorAlways($"[{nameof(InventoryOnlyHandler)}.{nameof(InitInventory)}] {e}");
                }
            }

        protected override EInputHandled InventoryClicked() {
            this.InvHandler.SelectItem(Game1.getMouseX(true), Game1.getMouseY(true));
            Log.TraceIfD($"[{nameof(InventoryOnlyHandler)}] Inventory click: {InventoryHandler.Describe(this.InvHandler.GetHoveredItem())}");
            if (this.InvHandler.CanSplitSelectedItem()) {
                int stackAmount = this.InvHandler.GetDefaultSplitStackAmount();
                this.SplitMenu = new StackSplitMenu(OnStackAmountReceived, stackAmount);
                Log.TraceIfD($"[{nameof(InventoryOnlyHandler)}] Split box opened, default {stackAmount}");
                return EInputHandled.Consumed;
                }
            Item hovered = this.InvHandler.GetHoveredItem();
            if (ItemSplitRules.IsUnsplittable(hovered)) {
                Log.TraceIfD($"[{nameof(InventoryOnlyHandler)}] Cannot split {InventoryHandler.Describe(hovered)}; showing the hint");
                CannotSplitHint.Show(hovered);
                this.InvHandler.ClearSelection();
                return EInputHandled.Consumed;
                }
            return EInputHandled.NotHandled;
            }

        protected override EInputHandled OpenSplitMenu() {
            return EInputHandled.NotHandled;
            }

        protected override void OnStackAmountReceived(string s) {
            try {
                if (int.TryParse(s, out int amount) && amount > 0)
                    this.InvHandler.SplitSelectedItem(amount);
                }
            catch (Exception e) {
                Log.ErrorAlways($"[{nameof(InventoryOnlyHandler)}.{nameof(OnStackAmountReceived)}] {e}");
                }
            base.OnStackAmountReceived(s);
            }

        protected override EInputHandled CancelMove() {
            CloseSplitMenu();
            return EInputHandled.Consumed;
            }
        }
    }
