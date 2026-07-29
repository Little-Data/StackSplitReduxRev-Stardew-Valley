using System;
using System.Collections.Generic;
using StardewValley.Menus;
using StardewValley;
using StackSplitRedux.UI;

namespace StackSplitRedux.MenuHandlers
    {
    public class ItemGrabMenuHandler : BaseMenuHandler<ItemGrabMenu>
        {
        private Item HoverItem = null;
        private int StackAmount = 0;
        private int TotalItems = 0;

        public ItemGrabMenuHandler()
            : base() {
            this.HasInventory = false;
            }

        protected override bool CanOpenSplitMenu() {
            if (this.NativeMenu.heldItem != null) return false;
            return this.NativeMenu.allowRightClick && base.CanOpenSplitMenu();
            }

        private static int IndexOfReference(IList<Item> list, Item item) {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], item)) return i;
            return -1;
            }

        protected override EInputHandled OpenSplitMenu() {
            try {
                this.HoverItem = this.NativeMenu.hoveredItem;
                }
            catch {
                return EInputHandled.NotHandled;
                }

            if (this.HoverItem == null || this.HoverItem.Stack <= 1)
                return EInputHandled.NotHandled;

            this.TotalItems = this.HoverItem.Stack;
            this.StackAmount = (this.TotalItems + 1) / 2;
            this.SplitMenu = new StackSplitMenu(OnStackAmountReceived, this.StackAmount);
            return EInputHandled.Consumed;
            }

        protected override void OnStackAmountReceived(string s) {
            if (int.TryParse(s, out this.StackAmount) && this.StackAmount > 0) {
                int wantToTake = Math.Min(this.StackAmount, this.TotalItems);
                int remaining = this.TotalItems - wantToTake;

                var playerInv = this.NativeMenu.inventory.actualInventory;
                var chestInv = this.NativeMenu.ItemsToGrabMenu.actualInventory;

                bool inPlayer = IndexOfReference(playerInv, this.HoverItem) >= 0;
                int hoverIdx = inPlayer
                    ? IndexOfReference(playerInv, this.HoverItem)
                    : IndexOfReference(chestInv, this.HoverItem);

                this.HoverItem.Stack = remaining;
                if (remaining <= 0) {
                    if (inPlayer && hoverIdx >= 0 && hoverIdx < playerInv.Count)
                        playerInv[hoverIdx] = null;
                    else if (!inPlayer && hoverIdx >= 0 && hoverIdx < chestInv.Count)
                        chestInv[hoverIdx] = null;
                    }

                var one = this.HoverItem.getOne();
                if (one != null) {
                    one.Stack = wantToTake;
                    this.NativeMenu.heldItem = one;
                    }
                }
            base.OnStackAmountReceived(s);
            }

        protected override EInputHandled CancelMove() {
            CloseSplitMenu();
            return EInputHandled.Consumed;
            }
        }
    }
