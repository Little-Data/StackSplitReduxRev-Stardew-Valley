using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.MenuHandlers
    {
    class BuyAction : ShopAction
        {
        private readonly Guid GUID = Guid.NewGuid();

        /// <summary>Index of the clicked entry in the shop's for-sale list, or -1 if unknown.</summary>
        private readonly int ClickedIndex;

        private bool? _CanPerformAction = null;
        private int? _MaxPurchasable = null;

        /// <summary>Constructs an instance.</summary>
        /// <param name="menu">The native shop menu.</param>
        /// <param name="item">The item to buy.</param>
        /// <param name="clickedIndex">Index of the item in <see cref="ShopMenu.forSale"/>, or -1 if unknown.</param>
        public BuyAction(ShopMenu menu, ISalable item, int clickedIndex = -1)
            : base(menu, item) {
            this.ClickedIndex = clickedIndex;
            // Default
            if (CanPerformAction())
                this.Amount = Math.Min(Mod.Config.DefaultShopAmount, GetMaxPurchasable());
            Log.TraceIfD($"[{nameof(BuyAction)}] Instantiated for shop {menu} item {item}, Amount = {this.Amount}, GUID = {GUID}");
            }

        ~BuyAction() {
            Log.TraceIfD($"[{nameof(BuyAction)}] Finalized for GUID = {GUID}");
            }

        /// <summary>Verifies the conditions to perform the action.</summary>
        public override bool CanPerformAction() {
            if (this._CanPerformAction is null) {
                Item held;
                try {
                    held = Mod.Reflection.GetField<Item>(this.NativeShopMenu, "heldItem").GetValue();
                    }
                catch {
                    held = null;
                    }

                // Same conditions as before, but split up so the debug log can show which one failed.
                bool stackable = this.ClickedItem is not null && this.ClickedItem.canStackWith(this.ClickedItem);
                bool heldOk = held is null
                    || (this.ClickedItem is not null
                        && this.ClickedItem.canStackWith(held)
                        && held.Stack < held.maximumStackSize());
                int maxPurchasable = stackable && heldOk ? GetMaxPurchasable() : 0;

                this._CanPerformAction = stackable && heldOk && maxPurchasable > 0;

                Log.TraceIfD(
                    $"[{nameof(BuyAction)}] CanPerformAction={this._CanPerformAction} for " +
                    $"{InventoryHandler.Describe(this.ClickedItem)}: stackable={stackable}, heldOk={heldOk} " +
                    $"(held {InventoryHandler.Describe(held)}), maxPurchasable={maxPurchasable}"
                    );
                }
            return this._CanPerformAction.Value;
            }

        /// <summary>Does the action.</summary>
        /// <param name="amount">Number of items.</param>
        /// <param name="clickLocation">Where the player clicked.</param>
        public override void PerformAction(int amount, Point clickLocation) {
            var pfx = $"[{nameof(BuyAction)}.{nameof(PerformAction)}]";
            var chosen = this.ClickedItem;
            var chosen_max = chosen.maximumStackSize();
            var nativeMenu = this.NativeShopMenu;
            var heldItem = Mod.Reflection.GetField<Item>(nativeMenu, "heldItem").GetValue();

            Log.Trace(
                $"{pfx} chosen = {chosen}, nativeMenu = {nativeMenu}, ShopCurrencyType = {this.ShopCurrencyType} ({this.ShopCurrencyName})"
                );

            // Using Linq here is slower by A LOT but ultimately MUCH more readable
            amount = Seq.Min(amount, GetMaxPurchasable(), chosen_max);

            // If we couldn't grab all that we wanted then only subtract the amount we were able to grab
            int numHeld = heldItem?.Stack ?? 0;
            if ((numHeld + amount) > chosen_max) amount = chosen_max - numHeld;

            if (amount <= 0) {
                Log.Trace($"{pfx} purchasable amount <= 0, purchase aborted");
                return;
                }

            Log.Trace(
                $"{pfx} Purchasing {amount} of {InventoryHandler.Describe(chosen)} " +
                $"(requested stock cap {GetMaxPurchasable()}, max stack {chosen_max}, held {numHeld}), " +
                $"price index {this.ClickedIndex}"
                );

            // Try to purchase the item - method returns true if it should be removed from the shop since there's no more.
            var purchaseMethod = Mod.Reflection.GetMethod(nativeMenu, "tryToPurchaseItem");
            if (purchaseMethod.Invoke<bool>(chosen, heldItem, amount, clickLocation.X, clickLocation.Y)) {
                Log.TraceIfD($"{pfx} Item is limited, reducing stock");
                // remove the purchased item from the stock etc.
                nativeMenu.itemPriceAndStock.Remove(chosen);

                // Remove the exact entry that was clicked (vanilla behavior). Removing by value could remove a
                // different entry when a modded shop lists the same item more than once.
                if (this.ClickedIndex >= 0
                    && this.ClickedIndex < nativeMenu.forSale.Count
                    && ReferenceEquals(nativeMenu.forSale[this.ClickedIndex], chosen)) {
                    nativeMenu.forSale.RemoveAt(this.ClickedIndex);
                    Log.TraceIfD($"{pfx} Removed for-sale entry {this.ClickedIndex}; {nativeMenu.forSale.Count} entries left");
                    }
                else {
                    nativeMenu.forSale.Remove(chosen);
                    Log.TraceIfD($"{pfx} Removed the for-sale entry by value; {nativeMenu.forSale.Count} entries left");
                    }
                }
            else {
                Log.TraceIfD($"{pfx} The shop kept the item in stock (unlimited stock or purchase refused)");
                }
            }

        /// <summary>
        /// Determine how many of an item player can purchase based on player's current monies/inventories and shop's current stock
        /// </summary>
        /// <returns>Maximum amount purchasable, cached</returns>
        public int GetMaxPurchasable() {
            if (this._MaxPurchasable is null) {
                var pfx = $"[{nameof(BuyAction)}.{nameof(GetMaxPurchasable)}]";

                // Guards instead of Debug.Assert; see the note in BaseMenuHandler.Open.
                if (this.ClickedItem is not Item chosen) {
                    Log.Error($"{pfx} No item was clicked!");
                    this._MaxPurchasable = 0;
                    return 0;
                    }
                var priceAndStockMap = this.NativeShopMenu.itemPriceAndStock;

                // Calculate the number to purchase
                if (!priceAndStockMap.TryGetValue(chosen, out var stockInfo)) {
                    Log.Error($"{pfx} chosen item {chosen?.Name} not found in itemPriceAndStock!");
                    this._MaxPurchasable = 0;
                    return 0;
                    }
                Log.Trace($"{pfx} chosen stockInfo = price={stockInfo.Price}, stock={stockInfo.Stock}");
                int numInStock = stockInfo.Stock;
                int itemPrice = stockInfo.Price;
                int currentMonies;
                if (stockInfo.TradeItem is null) {  // using money
                    currentMonies = ShopMenu.getPlayerCurrencyAmount(Game1.player, this.ShopCurrencyType);
                    Log.TraceIfD($"{pfx} player has {currentMonies} of currency {this.ShopCurrencyType} ({this.ShopCurrencyName})");
                    }
                else {  // barter system
                    itemPrice = stockInfo.TradeItemCount ?? 0;
                    currentMonies = stockInfo.TradeItem is not null
                        ? Game1.player.Items.CountId(stockInfo.TradeItem)
                        : 0;
                    Log.TraceIfD($"{pfx} Barter system: player has {currentMonies} of item {stockInfo.TradeItem ?? "null"}");
                    }
                Log.Trace($"{pfx} chosen item price is {itemPrice}");
                if (itemPrice <= 0) {
                    // Avoid a division by zero (and the Debug.Assert this replaced).
                    Log.Error($"{pfx} chosen item {chosen.Name} has an invalid price of {itemPrice}!");
                    this._MaxPurchasable = 0;
                    return 0;
                    }

                this._MaxPurchasable = Math.Min(currentMonies / itemPrice, numInStock);
                }
            return this._MaxPurchasable.Value;
            }

        /// <summary>Helper method getting which item in the shop was clicked.</summary>
        /// <param name="shopMenu">Native shop menu.</param>
        /// <param name="p">Mouse location.</param>
        /// <returns>The clicked item or null if none was clicked.</returns>
        public static ISalable GetClickedShopItem(ShopMenu shopMenu, Point p) {
            var itemsForSale = shopMenu.forSale;
            int index = GetClickedItemIndex(shopMenu, p);
            // Guard instead of Debug.Assert: an out-of-range index would throw (and the assert used to kill the game).
            return index >= 0 && index < itemsForSale.Count ? itemsForSale[index] : null;
            }

        /// <summary>Gets the index of the clicked shop item. This index corresponds to the list of buttons and list of items.</summary>
        /// <param name="shopMenu">Native shop menu.</param>
        /// <param name="p">Mouse location.</param>
        /// <returns>The clicked item or null if none was clicked.</returns>
        public static int GetClickedItemIndex(ShopMenu shopMenu, Point p) {
            int currentItemIndex = shopMenu.currentItemIndex;
            int saleButtonIndex = shopMenu.forSaleButtons.FindIndex(button => button.containsPoint(p.X, p.Y));
            return saleButtonIndex > -1 ? currentItemIndex + saleButtonIndex : -1;
            }

        /// <summary>Creates an instance of the action.</summary>
        /// <param name="shopMenu">Native shop menu.</param>
        /// <param name="mouse">Mouse position.</param>
        /// <returns>The instance or null if no valid item was selected.</returns>
        public static ShopAction Create(ShopMenu shopMenu, Point mouse) {
            int index = BuyAction.GetClickedItemIndex(shopMenu, mouse);
            if (index < 0 || index >= shopMenu.forSale.Count) return null;

            var item = shopMenu.forSale[index];
            return item != null ? new BuyAction(shopMenu, item, index) : null;
            }
        }
    }
