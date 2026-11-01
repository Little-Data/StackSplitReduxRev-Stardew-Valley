using StackSplitRedux.UI;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.MenuHandlers
    {
    public class JunimoNoteMenuHandler : BaseMenuHandler<JunimoNoteMenu>
        {
        /// <summary>Null constructor that currently only invokes the base null constructor</summary>
        public JunimoNoteMenuHandler()
            : base() {
            }

        /// <summary>Initializes the player inventory, using the menu's own cursor item when it has one.</summary>
        protected override void InitInventory() {
            base.InitInventory();

            // The Junimo note menu draws its own heldItem; the player's cursor slot is not drawn while it is open.
            if (this.InvHandler.Initialized
                && ContainerMenuAccess.TryGetHeldItemAccessors(this.NativeMenu, out var heldGetter, out var heldSetter)) {
                this.InvHandler.UseMenuHeldItem(heldGetter, heldSetter);
                }
            }

        /// <summary>Alternative of OpenSplitMenu which is invoked when the generic inventory handler is clicked.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled InventoryClicked() {
            this.InvHandler.SelectItem(Game1.getMouseX(true), Game1.getMouseY(true));
            Log.TraceIfD($"[{nameof(JunimoNoteMenuHandler)}] Inventory click: {InventoryHandler.Describe(this.InvHandler.GetHoveredItem())}");
            if (this.InvHandler.CanSplitSelectedItem()) {
                int stackAmount = this.InvHandler.GetDefaultSplitStackAmount();
                this.SplitMenu = new StackSplitMenu(OnStackAmountReceived, stackAmount);
                Log.TraceIfD($"[{nameof(JunimoNoteMenuHandler)}] Split box opened, default {stackAmount}");
                return EInputHandled.Consumed;
                }
            Item hovered = this.InvHandler.GetHoveredItem();
            if (ItemSplitRules.IsUnsplittable(hovered)) {
                Log.TraceIfD($"[{nameof(JunimoNoteMenuHandler)}] Cannot split {InventoryHandler.Describe(hovered)}; showing the hint");
                CannotSplitHint.Show(hovered);
                this.InvHandler.ClearSelection();
                return EInputHandled.Consumed;
                }
            return EInputHandled.NotHandled;
            }

        /// <summary>Main event that derived handlers use to setup necessary hooks and other things needed to take over how the stack is split.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled OpenSplitMenu() {
            return EInputHandled.NotHandled;
            }

        /// <summary>Callback given to the split menu that is invoked when a value is submitted.</summary>
        /// <param name="s">The user input.</param>
        protected override void OnStackAmountReceived(string s) {
            try {
                if (int.TryParse(s, out int amount) && amount > 0)
                    this.InvHandler.SplitSelectedItem(amount);
                }
            catch (System.Exception e) {
                Log.ErrorAlways($"[{nameof(JunimoNoteMenuHandler)}.{nameof(OnStackAmountReceived)}] {e}");
                }
            base.OnStackAmountReceived(s);
            }
        }
    }
