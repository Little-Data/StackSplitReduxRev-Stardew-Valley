using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;

using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.MenuHandlers
    {
    class BuyAction : ShopAction
        {
        private readonly Guid GUID = Guid.NewGuid();

        private bool? _CanPerformAction = null;
        private int? _MaxPurchasable = null;

        /// <summary>Constructs an instance.</summary>
        /// <param name="menu">The native shop menu.</param>
        /// <param name="item">The item to buy.</param>
        public BuyAction(ShopMenu menu, ISalable item)
            : base(menu, item) {
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
                var held = Mod.Reflection.GetField<Item>(this.NativeShopMenu, "heldItem").GetValue();

                this._CanPerformAction =
                    this.ClickedItem is Item chosen    // not null
                    && chosen.canStackWith(chosen)     // Item type is stackable
                    && (
                        held == null                   // not holding anything, or...
                        || (chosen.canStackWith(held) && held.Stack < held.maximumStackSize())  // item held can stack with chosen and at max
                        )
                    && GetMaxPurchasable() > 0         // Can afford
                    ;
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

            Log.Trace($"{pfx} Purchasing {amount} of {chosen.Name}");

            // Try to purchase the item - method returns true if it should be removed from the shop since there's no more.
            var purchaseMethod = Mod.Reflection.GetMethod(nativeMenu, "tryToPurchaseItem");
            if (purchaseMethod.Invoke<bool>(chosen, heldItem, amount, clickLocation.X, clickLocation.Y)) {
                Log.TraceIfD($"{pfx} Item is limited, reducing stock");
                // remove the purchased item from the stock etc.
                nativeMenu.itemPriceAndStock.Remove(chosen);
                nativeMenu.forSale.Remove(chosen);
                }
            }

        /// <summary>
        /// Determine how many of an item player can purchase based on player's current monies/inventories and shop's current stock
        /// </summary>
        /// <returns>Maximum amount purchasable, cached</returns>
        public int GetMaxPurchasable() {
            if (this._MaxPurchasable is null) {
                var pfx = $"[{nameof(BuyAction)}.{nameof(GetMaxPurchasable)}]";

                Debug.Assert(this.ClickedItem is not null);
                Item chosen = this.ClickedItem;
                var priceAndStockMap = this.NativeShopMenu.itemPriceAndStock;
                Debug.Assert(priceAndStockMap.ContainsKey(chosen));

                // Calculate the number to purchase
                ItemStockInformation stockInfo = priceAndStockMap[chosen];
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
                Debug.Assert(itemPrice > 0);

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
            Debug.Assert(index < itemsForSale.Count);
            return index >= 0 ? itemsForSale[index] : null;
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
            var item = BuyAction.GetClickedShopItem(shopMenu, mouse);
            return item != null ? new BuyAction(shopMenu, item) : null;
            }
        }
    }
