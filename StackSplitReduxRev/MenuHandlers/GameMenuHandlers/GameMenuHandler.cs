using StackSplitRedux.UI;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System.Collections.Generic;

namespace StackSplitRedux.MenuHandlers
    {
    public class GameMenuHandler : BaseMenuHandler<GameMenu>
        {
        /// <summary>Represents an invalid tab index.</summary>
        protected const int INVALID_TAB = -1;

        /// <summary>Tab index mapped to its handler. Using a dict because not all indices are handled.</summary>
        private readonly Dictionary<int, IGameMenuPageHandler> PageHandlers = new() {
                { GameMenu.inventoryTab, new InventoryPageHandler() },
                { GameMenu.craftingTab, new CraftingPageHandler() }
            };

        /// <summary>The handler for the current tab,</summary>
        private IGameMenuPageHandler CurrentPageHandler = null;

        /// <summary>The last tab that was open.</summary>
        private int PreviousTab = INVALID_TAB;

        /// <summary>The current tab that is open.</summary>
        private int CurrentTab => this.NativeMenu.currentTab;

        /// <summary>The native list of clickable tabs, used for checking if they were clicked.</summary>
        private List<ClickableComponent> Tabs;

        /// <summary>Null constructor that currently only invokes the base null constructor</summary>
        public GameMenuHandler()
            : base() {
            }

        /// <summary>Notifies the handler that its native menu has been opened.</summary>
        /// <param name="menu">The menu that was opened.</param>
        public override void Open(IClickableMenu menu) {
            base.Open(menu);

            // The base handler rejected an unsupported menu, so there is nothing to prepare here.
            if (this.NativeMenu is null) return;

            this.Tabs = this.NativeMenu.tabs;
            Log.TraceIfD(
                $"[{nameof(GameMenuHandler)}] Opened game menu: currentTab={this.CurrentTab}, tabs={this.Tabs?.Count ?? 0}, " +
                $"pages={this.NativeMenu.pages?.Count ?? 0}"
                );

            if (!ChangeTabs(this.CurrentTab)) {
                Log.Trace($"[{nameof(GameMenuHandler)}.{nameof(Open)}] Could not change to tab {this.CurrentTab}");
                }
            }

        /// <summary>Notifies the handler that its native menu was closed.</summary>
        public override void Close() {
            base.Close();
            CloseCurrentHandler();
            this.Tabs = null;
            }

        /// <summary>Initializes the inventory using the most common variable names.</summary>
        protected override void InitInventory() {
            // Do nothing; let the PageHandler init the inventory in Open.
            }

        /// <summary>Called when the current handler loses focus when the split menu is open, allowing it to cancel the operation or run the default behaviour.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled CancelMove() {
            base.CancelMove();
            return this.CurrentPageHandler != null
                ? this.CurrentPageHandler.CancelMove()
                : EInputHandled.NotHandled;
            }

        /// <summary>Additional checks before opening the split menu.</summary>
        /// <returns>True if it can be opened.</returns>
        protected override bool CanOpenSplitMenu() {
            return base.CanOpenSplitMenu() && this.PageHandlers.ContainsKey(this.CurrentTab);
            }

        /// <summary>Alternative of OpenSplitMenu which is invoked when the generic inventory handler is clicked.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled InventoryClicked() {
            // The tab may have changed without a page handler being available; do nothing rather than throw.
            IGameMenuPageHandler pageHandler = this.CurrentPageHandler;
            if (pageHandler is null) return EInputHandled.NotHandled;

            var handled = pageHandler.InventoryClicked(out int stackAmount);
            // The page may consume the click without opening the box (it showed the "cannot split" hint); only create
            // the input box when an amount was actually provided.
            if (handled != EInputHandled.NotHandled && stackAmount > 0) {
                this.SplitMenu = new StackSplitMenu(OnStackAmountReceived, stackAmount);
                Log.TraceIfD($"[{nameof(GameMenuHandler)}] Split box opened on tab {this.CurrentTab}: default {stackAmount}");
                }
            else {
                Log.TraceIfD(
                    $"[{nameof(GameMenuHandler)}] Tab {this.CurrentTab} handled the inventory click without a split box " +
                    $"(result={handled}, amount={stackAmount})"
                    );
                }
            return handled;
            }

        /// <summary>Main event that derived handlers use to setup necessary hooks and other things needed to take over how the stack is split.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled OpenSplitMenu() {
            IGameMenuPageHandler pageHandler = this.CurrentPageHandler;
            if (pageHandler is null) return EInputHandled.NotHandled;

            var handled = pageHandler.OpenSplitMenu(out int stackAmount);
            if (handled != EInputHandled.NotHandled && stackAmount > 0) {
                this.SplitMenu = new StackSplitMenu(OnStackAmountReceived, stackAmount);
                Log.TraceIfD($"[{nameof(GameMenuHandler)}] Split box opened from the page content: default {stackAmount}");
                }
            return handled;
            }

        /// <summary>Passes the input to the page handler.</summary>
        /// <param name="s">Stack amount inputted by user.</param>
        protected override void OnStackAmountReceived(string s) {
            if (this.CurrentPageHandler is not null && int.TryParse(s, out int amount)) {
                this.CurrentPageHandler.OnStackAmountEntered(amount);
                }
            base.OnStackAmountReceived(s);
            }

        /// <summary>Checks if one of the tabs was selected and changes the current tab accordingly.</summary>
        protected override EInputHandled HandleLeftClick() {
            // Check which tab was click and switch to the corresponding handler.
            var mX = Game1.getMouseX(true);
            var mY = Game1.getMouseY(true);
            Log.TraceIfD($"[{nameof(GameMenuHandler)}.{nameof(HandleLeftClick)}] Mouse clicked on ({mX}, {mY})");
            int tabIndex = this.Tabs is not null ? this.Tabs.FindIndex(tab => tab.containsPoint(mX, mY)) : INVALID_TAB;
            if (tabIndex > INVALID_TAB) {
                Log.TraceIfD($"Changed tab to {tabIndex}");
                ChangeTabs(tabIndex);
                }
            return EInputHandled.NotHandled;
            }

        /// <summary>Switches the current page handler to the one for the new tab.</summary>
        /// <param name="newTab">The index of the new tab.</param>
        /// <returns>True if it successfully changed tabs or is already on that tab.</returns>
        private bool ChangeTabs(int newTab) {
            if (this.PreviousTab == newTab)
                return true;

            CloseCurrentHandler();

            if (!PageHandlers.TryGetValue(newTab, out IGameMenuPageHandler pageHandler)) {
                // Please note that this is NOT AN ERROR
                Log.TraceIfD($"[{nameof(GameMenuHandler)}.{nameof(ChangeTabs)}] No handler for tab {newTab}");
                return false;
                }

            Log.TraceIfD($"[{nameof(GameMenuHandler)}.{nameof(ChangeTabs)}] Found a handler for tab {newTab} : {pageHandler}");

            this.PreviousTab = newTab;
            this.CurrentPageHandler = pageHandler;

            var pages = this.NativeMenu.pages;
            Log.TraceIfD(
                $"[{nameof(GameMenuHandler)}] Switching to tab {newTab}, page " +
                $"{(pages is not null && newTab >= 0 && newTab < pages.Count ? pages[newTab]?.GetType().Name : "out of range")}"
                );
            pageHandler.Open(this.NativeMenu, pages[newTab], this.InvHandler);

            return true;
            }

        /// <summary>Closes the current handler and sets the previous tab to invalid.</summary>
        private void CloseCurrentHandler() {
            this.CurrentPageHandler?.Close();
            this.CurrentPageHandler = null;
            this.PreviousTab = INVALID_TAB;
            }
        }
    }
