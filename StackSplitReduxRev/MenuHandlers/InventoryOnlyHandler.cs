using System;
using StackSplitRedux.UI;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.MenuHandlers
    {
    public class InventoryOnlyHandler : BaseMenuHandler<IClickableMenu>
        {
        public InventoryOnlyHandler()
            : base() {
            }

        protected override EInputHandled InventoryClicked() {
            this.InvHandler.SelectItem(Game1.getMouseX(true), Game1.getMouseY(true));
            if (this.InvHandler.CanSplitSelectedItem()) {
                int stackAmount = this.InvHandler.GetDefaultSplitStackAmount();
                this.SplitMenu = new StackSplitMenu(OnStackAmountReceived, stackAmount);
                return EInputHandled.Consumed;
                }
            return EInputHandled.NotHandled;
            }

        protected override EInputHandled OpenSplitMenu() {
            return EInputHandled.NotHandled;
            }

        protected override void OnStackAmountReceived(string s) {
            if (int.TryParse(s, out int amount) && amount > 0) {
                for (int i = 0; i < amount; ++i)
                    this.NativeMenu.receiveRightClick(this.ClickItemLocation.X, this.ClickItemLocation.Y, playSound: i == 0);
                }
            base.OnStackAmountReceived(s);
            }

        protected override EInputHandled CancelMove() {
            CloseSplitMenu();
            return EInputHandled.Consumed;
            }
        }
    }
