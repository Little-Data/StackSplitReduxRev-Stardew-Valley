using System;
using StackSplitRedux.UI;
using StardewValley.Menus;

namespace StackSplitRedux.MenuHandlers
    {
    public class ShopMenuHandler : BaseMenuHandler<ShopMenu>
        {
        public const float RIGHT_CLICK_POLLING_INTVL = 300f;

        /// <summary>The shop action for the current operation.</summary>
        private IShopAction CurrentShopAction = null;

        /// <summary>Null constructor that currently only invokes the base null constructor</summary>
        public ShopMenuHandler()
            : base() {
            }

        protected override bool CanOpenSplitMenu() {
            if (this.NativeMenu.heldItem != null) return false;
            return base.CanOpenSplitMenu();
            }

        /// <summary>Alternative of OpenSplitMenu which is invoked when the generic inventory handler is clicked.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled InventoryClicked() {
            this.CurrentShopAction = SellAction.Create(this.NativeMenu, this.ClickItemLocation);
            Log.TraceIfD(
                $"[{nameof(ShopMenuHandler)}] Inventory click: action={(this.CurrentShopAction is null ? "none" : this.CurrentShopAction.GetType().Name)}, " +
                $"item={InventoryHandler.Describe(this.CurrentShopAction?.TargetItem)}"
                );
            return TryOpenSplitMenu(this.CurrentShopAction);
            }

        /// <summary>Main event that derived handlers use to setup necessary hooks and other things needed to take over how the stack is split.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled OpenSplitMenu() {
            Log.TraceIfD($"[{nameof(ShopMenuHandler)}.{nameof(OpenSplitMenu)}] Entered");
            try {
                this.CurrentShopAction = BuyAction.Create(this.NativeMenu, this.ClickItemLocation);
                Log.TraceIfD(
                    $"[{nameof(ShopMenuHandler)}] Shop click: action={(this.CurrentShopAction is null ? "none" : this.CurrentShopAction.GetType().Name)}, " +
                    $"item={InventoryHandler.Describe(this.CurrentShopAction?.TargetItem)}"
                    );
                }
            catch (Exception e) {
                Log.Error($"[ShopMenuHandler.OpenSplitMenu] BuyAction.Create failed: {e}");
                return EInputHandled.NotHandled;
                }
            return TryOpenSplitMenu(this.CurrentShopAction);
            }

        /// <summary>Callback given to the split menu that is invoked when a value is submitted.</summary>
        /// <param name="s">The user input.</param>
        protected override void OnStackAmountReceived(string s) {
            if (int.TryParse(s, out int amount)) {
                if (amount > 0) // Canceled if 0
                    this.CurrentShopAction.PerformAction(amount, this.ClickItemLocation);
                }
            base.OnStackAmountReceived(s);
            }

        /// <summary>How long the right click has to be held for before the receiveRIghtClick gets called rapidly (See Game1.Update)</summary>
        /// <returns>The polling interval.</returns>
        protected override float GetRightClickPollingInterval() {
            return RIGHT_CLICK_POLLING_INTVL; // From ShopMenu.receiveRightClick
            }

        /// <summary>Called when the current handler loses focus when the split menu is open, allowing it to cancel the operation or run the default behaviour.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled CancelMove() {
            // TODO: add mod config to set whether or not default behavior should be used when canceling
            // StackAmount will be default for the appropriate sale type, so just fake the input being submitted.
            OnStackAmountReceived(this.CurrentShopAction?.StackAmount.ToString());
            return EInputHandled.NotHandled;
            }

        /// <summary>Checks if the action can be performed and creates the split menu if it can.</summary>
        /// <param name="action">The action to perform.</param>
        private EInputHandled TryOpenSplitMenu(IShopAction action) {
            if (action is null) return EInputHandled.NotHandled;

            // Non-stackable goods (wallpaper, flooring, furniture, ...) cannot be bought or sold in batches. Say so
            // instead of opening an amount box that would only ever take one item.
            bool unsplittable = action is BuyAction
                ? ItemSplitRules.IsTypeUnsplittable(action.TargetItem)
                : action is SellAction && ItemSplitRules.IsUnsplittable(action.TargetItem);
            if (unsplittable) {
                Log.TraceIfD(
                    $"[{nameof(ShopMenuHandler)}] {action.GetType().Name} cannot split " +
                    $"{InventoryHandler.Describe(action.TargetItem)}; showing the hint"
                    );
                CannotSplitHint.Show(action.TargetItem);
                return EInputHandled.Consumed;
                }

            if (action.CanPerformAction()) {
                Log.TraceIfD($"[{nameof(ShopMenuHandler)}.{nameof(TryOpenSplitMenu)}] Creating Split Menu");
                this.SplitMenu = new StackSplitMenu(OnStackAmountReceived, this.CurrentShopAction.StackAmount);
                return EInputHandled.Consumed;
                }

            Log.TraceIfD(
                $"[{nameof(ShopMenuHandler)}] {action.GetType().Name} cannot run for " +
                $"{InventoryHandler.Describe(action.TargetItem)}; leaving the click to the shop"
                );
            return EInputHandled.NotHandled;
            }
        }
    }
