using System;
using System.Collections.Generic;
using StackSplitRedux.MenuHandlers;
using StackSplitRedux.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux
    {
    public class StackSplit {
        private static readonly int TICKS_DELAY_OPEN = StaticConfig.SplitMenuOpenDelayTicks;

        /// <summary>Are we subscribed to the events listened to while a handler is active.</summary>
        private bool IsSubscribed = false;

        /// <summary>The handler for the current menu.</summary>
        private IMenuHandler CurrentMenuHandler;

        /// <summary>Used to avoid resize events sent to menu changed.</summary>
        private bool WasResizeEvent = false;

        /// <summary>An index incremented on every tick and reset every 60th tick (0–59).</summary>
        private int CurrentUpdateTick = 0;

        /// <summary>Tracks what tick a resize event occurs on so we can resize the current handler next frame. -1 means no resize event.</summary>
        private int TickResizedOn = -1;

        private IClickableMenu MenuToHandle;
        private int WaitOpenTicks = 0;

        /// <summary>Menu types that failed once and are left to vanilla behavior for the rest of the session.</summary>
        private static readonly HashSet<Type> DisabledMenuTypes = new();

        private ModConfigMenu ConfigMenu;

        public StackSplit() {
            PrepareMapping();
            RegisterEvents();
            }

        ~StackSplit() {
            Log.Error($"[{nameof(StackSplit)}] We got finalized! How come??");
            }

        public void PrepareModConfigMenu() {
            this.ConfigMenu = new ModConfigMenu();
            }

        public void PrepareMapping() {
            HandlerMapping.Add(typeof(GameMenu), typeof(GameMenuHandler));
            HandlerMapping.Add(typeof(ShopMenu), typeof(ShopMenuHandler));
            HandlerMapping.Add(typeof(ItemGrabMenu), typeof(ItemGrabMenuHandler));
            HandlerMapping.Add(typeof(CraftingPage), typeof(CraftingMenuHandler));
            HandlerMapping.Add(typeof(JunimoNoteMenu), typeof(JunimoNoteMenuHandler));
            HandlerMapping.Add(typeof(GeodeMenu), typeof(InventoryOnlyHandler));

            // Container menus which are not an ItemGrabMenu: special order drop boxes (Gus's fridge) and the fair
            // grange display. Mapping them explicitly keeps them off the reflection-based fallback.
            HandlerMapping.Add(typeof(QuestContainerMenu), typeof(ContainerMenuHandler));
            HandlerMapping.Add(typeof(StorageContainer), typeof(ContainerMenuHandler));
            }

        public void RegisterEvents() {
            Mod.Events.Display.MenuChanged += OnMenuChanged;
            Mod.Events.Display.WindowResized += OnWindowResized;
            Mod.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            Mod.Events.GameLoop.GameLaunched += OnGameLaunched;
            }

        /// <summary>Raised after a game menu is opened, closed, or replaced.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnMenuChanged(object sender, MenuChangedEventArgs e) {
            try {
                OnMenuChangedImpl(e);
                }
            catch (Exception ex) {
                // Nothing we do with menus is worth taking the game down for.
                Log.ErrorAlways($"[{nameof(StackSplit)}.{nameof(OnMenuChanged)}] {ex}");
                }
            }

        private void OnMenuChangedImpl(MenuChangedEventArgs e) {
            // Ignore menu changes caused by StackSplitOverlay opening/closing; the menu underneath is restored by the
            // overlay and the handler must stay attached to it.
            if (e.OldMenu is StackSplitOverlay) {
                Log.TraceIfD($"[{nameof(StackSplit)}] MenuChanged: overlay closed, new menu = {e.NewMenu?.GetType().FullName ?? "none"}");
                // If the box disappeared without submitting, whatever replaced the menu means the handled menu is gone;
                // detach instead of letting a stale handler react to clicks in the new menu.
                if (e.NewMenu is null) {
                    DequeueMenuHandlerOpener();
                    try { this.CurrentMenuHandler?.Close(); }
                    catch { }
                    this.CurrentMenuHandler = null;
                    UnsubscribeHandlerEvents();
                    }
                return;
                }

            // menu closed
            if (e.NewMenu == null) {
                Log.TraceIfD($"[{nameof(StackSplit)}] MenuChanged: closed {e.OldMenu?.GetType().FullName ?? "none"}, handler = {this.CurrentMenuHandler?.GetType().Name ?? "none"}");
                // close the current handler and unsubscribe from the events
                if (this.CurrentMenuHandler != null) {
                    //Log.Trace("[OnMenuClosed] Closing current menu handler");
                    this.CurrentMenuHandler.Close();
                    this.CurrentMenuHandler = null;

                    UnsubscribeHandlerEvents();
                    }
                return;
                }

            // ignore resize event
            if (e.OldMenu?.GetType() == e.NewMenu?.GetType() && this.WasResizeEvent) {
                Log.TraceIfD($"[{nameof(StackSplit)}] MenuChanged: ignored resize of {e.NewMenu.GetType().FullName}");
                this.WasResizeEvent = false;
                return;
                }
            this.WasResizeEvent = false; // Reset

            // switch the currently handler to the one for the new menu type
            var nuMenu = e.NewMenu;
            Log.TraceIfD($"[{nameof(StackSplit)}] MenuChanged: {e.OldMenu?.GetType().FullName ?? "none"} -> {nuMenu.GetType().FullName} ({nuMenu})");

            // A menu type that already failed is left to vanilla behavior; don't leave a handler attached to the
            // menu that was just replaced, and don't subscribe the input/draw events again.
            if (DisabledMenuTypes.Contains(nuMenu.GetType())) {
                Log.TraceIfD($"[{nameof(StackSplit)}] {nuMenu.GetType().FullName} is in the disabled list; leaving it to vanilla");
                if (this.CurrentMenuHandler != null) {
                    DequeueMenuHandlerOpener();
                    this.CurrentMenuHandler.Close();
                    this.CurrentMenuHandler = null;
                    }
                UnsubscribeHandlerEvents();
                return;
                }

            if (
                HandlerMapping.TryGetHandler(nuMenu.GetType(), out IMenuHandler handler)
                || HandlerMapping.TryGetHandler(nuMenu.ToString(), out handler)
                ) {
                // A handler that already failed on this menu type is not tried again.
                if (handler.Faulted) {
                    Log.TraceIfD($"[{nameof(StackSplit)}] {handler.GetType().Name} already faulted; disabling its menu type");
                    DisableFaultedHandler(handler, nuMenu.GetType());

                    // The previous handler belonged to the menu that was just replaced; it must not keep listening.
                    if (this.CurrentMenuHandler is not null && !ReferenceEquals(this.CurrentMenuHandler, handler)) {
                        DequeueMenuHandlerOpener();
                        try { this.CurrentMenuHandler.Close(); }
                        catch { }
                        this.CurrentMenuHandler = null;
                        UnsubscribeHandlerEvents();
                        }
                    return;
                    }

                Log.TraceIfD($"{nuMenu} intercepted by {handler.GetType().Name}");
                // Close the current one if still open, it's likely invalid
                if (this.CurrentMenuHandler != null) {
                    DequeueMenuHandlerOpener();
                    this.CurrentMenuHandler.Close();
                    }
                EnqueueMenuHandlerOpener(nuMenu, handler);
                }
            else {
                Log.TraceIfD($"[{nameof(StackSplit)}] No explicit handler for {nuMenu.GetType().FullName}; trying the generic fallback");
                // The handled menu was replaced by one we do not drive. Let it go: a stale handler would otherwise act
                // on a menu the player is no longer in.
                if (nuMenu is not StackSplitOverlay) {
                    if (this.CurrentMenuHandler is not null) {
                        DequeueMenuHandlerOpener();
                        try { this.CurrentMenuHandler.Close(); }
                        catch { }
                        this.CurrentMenuHandler = null;
                        UnsubscribeHandlerEvents();
                        }
                    }

                // Generic fallback: detect inventory-capable menus from any mod
                HandleUnknownMenu(nuMenu);
                }
            }

        /// <summary>Raised after the game window is resized.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnWindowResized(object sender, WindowResizedEventArgs e) {
            try {
                Log.TraceIfD(
                    $"[{nameof(StackSplit)}] Window resized: viewport={Game1.viewport.Width}x{Game1.viewport.Height}, " +
                    $"uiViewport={Game1.uiViewport.Width}x{Game1.uiViewport.Height}, " +
                    $"zoom={Game1.options?.zoomLevel}, uiScale={Game1.options?.uiScale}"
                    );
                }
            catch {
                // Logging must never interfere with a resize.
                }
            // set flags to notify handler to resize next tick as the menu isn't always recreated
            this.WasResizeEvent = true;
            this.TickResizedOn = this.CurrentUpdateTick;
            }

        /// <summary>Raised after the game state is updated (≈60 times per second).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e) {
            if (++this.CurrentUpdateTick >= 60)
                this.CurrentUpdateTick = 0;

            // If TickResizedOn isn't -1 then there was a resize event, so do the resize next tick.
            // We need to do it this way rather than where we ignore resize in menu changed since not all menus are recreated on resize,
            // and during the actual resize event the new menu will not have been created yet so we need to wait.
            if (this.TickResizedOn > -1 && this.TickResizedOn != this.CurrentUpdateTick) {
                this.TickResizedOn = -1;
                Log.TraceIfD(
                    $"[{nameof(StackSplit)}] Reopening {this.CurrentMenuHandler?.GetType().Name ?? "no handler"} after resize; " +
                    $"active menu = {Game1.activeClickableMenu?.GetType().FullName ?? "none"}"
                    );
                this.CurrentMenuHandler?.Close();
                // Checking the menu type since actions like returning to title will cause a resize event (idk why the window is maximized)
                // and the activeClickableMenu will not be what it was before.
                if (this.CurrentMenuHandler?.IsCorrectMenuType(Game1.activeClickableMenu) == true) {
                    this.CurrentMenuHandler?.Open(Game1.activeClickableMenu);
                    }
                else {
                    Log.TraceIfD($"[{nameof(StackSplit)}] Dropping the handler after resize: the menu is no longer supported");
                    this.CurrentMenuHandler = null;
                    }
                }

            this.CurrentMenuHandler?.Update();
            DisableFaultedHandler(this.CurrentMenuHandler);
            }

        /// <summary>
        /// Prepare to open MenuHandler by attaching to UpateTicked event. This is done to allow other mods to finish manipulating
        /// the inventory + extended inventory
        /// </summary>
        /// <param name="newMenu">The new menu being opened</param>
        /// <param name="handler">The handler for the new menu</param>
        private void EnqueueMenuHandlerOpener(IClickableMenu newMenu, IMenuHandler handler) {
            // Restart the delay for the newest menu instead of silently ignoring it, and make sure the event is
            // never subscribed twice (it would then run twice per tick and open the handler prematurely).
            DequeueMenuHandlerOpener();
            Log.TraceIfD($"[{nameof(StackSplit)}] Queueing {handler.GetType().Name} for {newMenu.GetType().FullName} (opens in {TICKS_DELAY_OPEN} ticks)");
            this.MenuToHandle = newMenu;
            this.CurrentMenuHandler = handler;
            Mod.Events.GameLoop.UpdateTicked += MenuHandlerOpener;
            }

        /// <summary>
        /// <para>Detach MenuHandlerOpener from UpdateTicked and reset the timer.</para>
        /// <para>Called to cancel a not-yet triggered opening of MenuHandler</para>
        /// </summary>
        private void DequeueMenuHandlerOpener() {
            Mod.Events.GameLoop.UpdateTicked -= MenuHandlerOpener;
            this.WaitOpenTicks = 0;
            }

        /// <summary>
        /// Opens a MenuHandler after several ticks have passed. This is to allow other mods to finish manipulating the
        /// inventory + extended inventory
        /// </summary>
        /// <param name="sender">Event's sender -- not used</param>
        /// <param name="e">Event's args -- not used</param>
        private void MenuHandlerOpener(object sender, UpdateTickedEventArgs e) {
            if (this.WaitOpenTicks++ >= TICKS_DELAY_OPEN) {
                DequeueMenuHandlerOpener();
                // Guards
                if (this.CurrentMenuHandler is null) return;
                if (this.MenuToHandle is null) return;
                // Final check; it's possible game has invoked emergencyShutdown and we're left with a dangling ref
                if (Game1.activeClickableMenu is null) {
                    Log.Trace($"Menu {this.MenuToHandle} ran away while we're prepping!");
                    return;
                    }
                // Never hand a menu to a handler that doesn't support it. Besides doing nothing useful, the
                // handler used to fail its type assertion, which terminates the game in .NET 6.
                if (!this.CurrentMenuHandler.IsCorrectMenuType(this.MenuToHandle)) {
                    Log.WarnAlways(
                        $"[{nameof(StackSplit)}] Skipped {this.CurrentMenuHandler.GetType().Name}: " +
                        $"it doesn't support menus of type {this.MenuToHandle.GetType().FullName}."
                        );
                    // Drop any state the handler may still hold from an earlier menu.
                    this.CurrentMenuHandler.Close();
                    this.CurrentMenuHandler = null;
                    return;
                    }
                try {
                    Log.TraceIfD($"[{nameof(StackSplit)}] Opening {this.CurrentMenuHandler.GetType().Name} for {this.MenuToHandle.GetType().FullName}");
                    this.CurrentMenuHandler.Open(this.MenuToHandle);
                    }
                catch (Exception ex) {
                    Log.ErrorAlways(
                        $"[{nameof(StackSplit)}.{nameof(MenuHandlerOpener)}] {this.CurrentMenuHandler.GetType().Name} " +
                        $"failed to open menu {this.MenuToHandle.GetType().FullName}; leaving it to vanilla behavior.\n{ex}"
                        );
                    // A half-opened handler must not keep running on every tick.
                    try { this.CurrentMenuHandler.Close(); }
                    catch { }
                    this.CurrentMenuHandler = null;
                    // Don't retry a menu type that already proved unworkable.
                    DisableMenuType(this.MenuToHandle.GetType());
                    return;
                    }
                SubscribeHandlerEvents();
                }
            }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e) {
            Log.TraceIfD($"[{nameof(StackSplit)}] Game launched; preparing the config menu");
            PrepareModConfigMenu();
            }

        /// <summary>Subscribes to the events we care about when a handler is active.</summary>
        private void SubscribeHandlerEvents() {
            if (this.IsSubscribed) return;
            Mod.Events.Input.ButtonPressed += OnButtonPressed;
            Mod.Events.Display.Rendered += OnRendered;
            // The hint is drawn from here: this event runs while the menu sprite batch is still open, so the text ends
            // up on the UI layer above the menu. Display.Rendered can end up in the world buffer (which is covered by
            // the UI layer) when the player uses a non-default zoom level.
            Mod.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
            this.IsSubscribed = true;
            }

        /// <summary>Unsubscribes from events when the handler is no longer active.</summary>
        private void UnsubscribeHandlerEvents() {
            if (!this.IsSubscribed) return;
            Mod.Events.Input.ButtonPressed -= OnButtonPressed;
            Mod.Events.Display.Rendered -= OnRendered;
            Mod.Events.Display.RenderedActiveMenu -= OnRenderedActiveMenu;
            this.IsSubscribed = false;
            }

        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e) {
            // When overlay is active, let the game's menu system handle all input
            if (Game1.activeClickableMenu is StackSplitOverlay)
                return;

            if (Log.IsEnabled) {
                Log.Trace(
                    $"[{nameof(StackSplit)}] Input {e.Button}: handler={this.CurrentMenuHandler?.GetType().Name ?? "none"}, " +
                    $"menu={Game1.activeClickableMenu?.GetType().Name ?? "none"}, modifier={IsModifier()} " +
                    $"(cursor=({Game1.getMouseX(true)},{Game1.getMouseY(true)}))"
                    );
                }

            // Forward input to the handler and consumes it while the tooltip is active.
            // Intercept keyboard input while the tooltip is active so numbers don't change the actively equipped item etc.
            switch (this.CurrentMenuHandler?.HandleInput(e.Button)) {
                case EInputHandled.Handled:
                    Log.TraceIfD($"[{nameof(StackSplit)}] {e.Button} handled by the split box");
                    // Obey unless we're hitting 'cancel' keys.
                    if (e.Button != SButton.Escape)
                        Mod.Input.Suppress(e.Button);
                    else
                        this.CurrentMenuHandler.CloseSplitMenu();
                    break;

                case EInputHandled.Consumed:
                    Log.TraceIfD($"[{nameof(StackSplit)}] {e.Button} consumed by {this.CurrentMenuHandler?.GetType().Name}; game will not see it");
                    Mod.Input.Suppress(e.Button);
                    break;

                case EInputHandled.NotHandled:
                    // NotHandled and no handler both land here; keep the log meaningful.
                    if (Log.IsEnabled)
                        Log.Trace($"[{nameof(StackSplit)}] {e.Button} left to the game");
                    if (e.Button == SButton.MouseLeft || e.Button == SButton.MouseRight)
                        this.CurrentMenuHandler.CloseSplitMenu(); // click wasn't handled meaning the split menu no longer has focus and should be closed.
                    break;
                }

            DisableFaultedHandler(this.CurrentMenuHandler);
            }

        /// <summary>Whether the configured split modifier is held right now, for logging.</summary>
        private bool IsModifier() {
            try {
                return Mod.Config?.ModifierKey?.IsDown() == true;
                }
            catch {
                return false;
                }
            }

        /// <summary><para>Raised after the game draws to the sprite patch in a draw tick, just before the final sprite batch
        /// is rendered to the screen.</para>
        /// <para>Since the game may open/close the sprite batch multiple times in a draw tick, 
        /// the sprite batch may not contain everything being drawn and some things may already be rendered to the screen. 
        /// Content drawn to the sprite batch at this point will be drawn over all vanilla content (including menus, HUD, 
        /// and cursor).</para></summary>
        private void OnRendered(object sender, RenderedEventArgs e) {
            // tell the current handler to draw the split menu if it's active
            this.CurrentMenuHandler?.Draw(e.SpriteBatch);

            // Fallback for the (rare) case where no menu is drawn this tick; normally OnRenderedActiveMenu draws it.
            CannotSplitHint.Draw(e.SpriteBatch);

            DisableFaultedHandler(this.CurrentMenuHandler);
            }

        /// <summary>Raised after the active menu is drawn, while its sprite batch is still open.</summary>
        private void OnRenderedActiveMenu(object sender, RenderedActiveMenuEventArgs e) {
            CannotSplitHint.Draw(e.SpriteBatch);
            }

        /// <summary>
        /// Fallback for menus we have no explicit mapping for, including menus added by other mods.
        /// </summary>
        /// <remarks>
        /// <para>The handler is chosen from the menu's shape, never from its name: a menu is only registered when its
        /// members really are the ones the handler will read, so a handler can never be paired with a menu it would
        /// fail to drive. That pairing used to fail a type assertion, which terminates the game in .NET 6.</para>
        /// <para>This method never throws and never registers anything when it is not sure.</para>
        /// </remarks>
        /// <param name="menu">The menu that was opened.</param>
        private void HandleUnknownMenu(IClickableMenu menu) {
            if (menu is null) return;
            // Our own amount input box is not a menu to handle (and the handler underneath must stay attached).
            if (menu is StackSplitOverlay) return;

            // Opt-out for players whose other mods keep adding menus that this fallback misreads.
            if (Mod.Config?.EnableGenericMenuSupport != true) return;

            try {
                Type type = menu.GetType();
                Log.TraceIfD($"[{nameof(StackSplit)}.{nameof(HandleUnknownMenu)}] Inspecting unmapped menu {type.FullName}");

                // A menu that really is (or extends) an ItemGrabMenu behaves like a chest.
                if (typeof(ItemGrabMenu).IsAssignableFrom(type)) {
                    Log.TraceIfD($"[{nameof(StackSplit)}] {type.FullName} extends ItemGrabMenu -> ItemGrabMenuHandler");
                    RegisterForHandler(menu, type, typeof(ItemGrabMenuHandler));
                    return;
                    }

                // A menu with a container pane (ItemsToGrabMenu), e.g. a modded container menu.
                if (ContainerMenuAccess.TryCreate(menu, out ContainerMenuAccess access)) {
                    Log.TraceIfD($"[{nameof(StackSplit)}] {type.FullName} looks like a container menu (strategy {access.Strategy}) -> ContainerMenuHandler");
                    RegisterForHandler(menu, type, typeof(ContainerMenuHandler));
                    return;
                    }

                // A menu that only shows the player's real inventory. It must also expose a cursor the menu actually
                // draws; without one the split-off stack would be parked somewhere invisible, so leave it vanilla.
                bool hasPlayerInventory = ContainerMenuAccess.HasPlayerInventory(menu);
                bool hasHeldItem = ContainerMenuAccess.TryGetHeldItemAccessors(menu, out _, out _);
                if (hasPlayerInventory && hasHeldItem) {
                    Log.TraceIfD($"[{nameof(StackSplit)}] {type.FullName} has the player inventory and a held item -> InventoryOnlyHandler");
                    RegisterForHandler(menu, type, typeof(InventoryOnlyHandler));
                    }
                else {
                    Log.TraceIfD(
                        $"[{nameof(StackSplit)}] {type.FullName} is left to vanilla: playerInventory={hasPlayerInventory}, " +
                        $"heldItemAccessors={hasHeldItem}"
                        );
                    }
                }
            catch (Exception e) {
                Log.ErrorAlways(
                    $"[{nameof(StackSplit)}.{nameof(HandleUnknownMenu)}] Failed to inspect menu " +
                    $"{menu.GetType().FullName}; it is left to vanilla behavior. Exception:\n{e}"
                    );
                }
            }

        private void RegisterForHandler(IClickableMenu menu, Type menuType, Type handlerType) {
            if (DisabledMenuTypes.Contains(menuType)) {
                Log.TraceIfD($"[{nameof(StackSplit)}] Not registering {menuType.FullName}: it is disabled for this session");
                return;
                }
            if (HandlerMapping.TryGetHandler(menuType, out IMenuHandler existing)) {
                Log.TraceIfD($"[{nameof(StackSplit)}] Not registering {menuType.FullName}: already mapped to {existing.GetType().Name}");
                return;
                }

            Log.TraceIfD($"[{nameof(StackSplit)}] Registering {menuType.FullName} -> {handlerType.Name}");
            HandlerMapping.Add(menuType, handlerType);

            // Never hand a menu to a handler which refuses it; that is the old crash path.
            if (!HandlerMapping.TryGetHandler(menuType, out IMenuHandler handler)) return;
            if (!handler.IsCorrectMenuType(menu)) {
                Log.WarnAlways(
                    $"[{nameof(StackSplit)}] {handlerType.Name} cannot drive menus of type {menuType.FullName}; " +
                    "leaving that menu to vanilla behavior."
                    );
                return;
                }

            if (this.CurrentMenuHandler != null) {
                DequeueMenuHandlerOpener();
                this.CurrentMenuHandler.Close();
                }
            EnqueueMenuHandlerOpener(menu, handler);
            }

        /// <summary>Stops using the handler if it reported a failure on its current menu.</summary>
        /// <param name="handler">The handler that just ran, or <c>null</c>.</param>
        /// <param name="menuType">Menu type to blame when the handler could not report one.</param>
        private void DisableFaultedHandler(IMenuHandler handler, Type menuType = null) {
            if (handler is null || !handler.Faulted) return;

            // Only the handler knows which menu it failed on; never blame whatever menu happens to be active now.
            Type failed = handler.FaultedMenuType ?? menuType;
            Log.TraceIfD(
                $"[{nameof(StackSplit)}] {handler.GetType().Name} reported a fault on {failed?.FullName ?? "an unknown menu"}; " +
                "that menu type is left to vanilla from now on"
                );
            handler.ClearFault();

            if (failed is not null && failed != typeof(StackSplitOverlay))
                DisableMenuType(failed);

            // A handler that just failed must not keep running on every tick for a menu type that is now vanilla.
            if (!ReferenceEquals(handler, this.CurrentMenuHandler)) return;
            DequeueMenuHandlerOpener();
            try { handler.Close(); }
            catch { }
            this.CurrentMenuHandler = null;
            UnsubscribeHandlerEvents();
            }

        /// <summary>Leaves a menu type to vanilla behavior for the rest of the session.</summary>
        /// <param name="menuType">The menu type to disable.</param>
        private static void DisableMenuType(Type menuType) {
            if (menuType is null) return;
            if (!DisabledMenuTypes.Add(menuType)) return;

            HandlerMapping.Remove(menuType);
            Log.WarnAlways(
                $"[{nameof(StackSplit)}] Stack splitting was disabled for menus of type {menuType.FullName} " +
                "after an error; they are left to vanilla behavior until the game is restarted."
                );
            }
        }
    }
