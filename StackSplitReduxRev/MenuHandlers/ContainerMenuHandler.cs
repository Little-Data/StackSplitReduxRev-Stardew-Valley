using System;
using StackSplitRedux.UI;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.MenuHandlers
    {
    /// <summary>Handles container menus that are not an <see cref="ItemGrabMenu"/>.</summary>
    /// <remarks>
    /// <para>Vanilla has three container menus: <see cref="ItemGrabMenu"/> (handled by
    /// <see cref="ItemGrabMenuHandler"/>), <see cref="QuestContainerMenu"/> (special order drop boxes such as Gus's
    /// fridge) and <see cref="StorageContainer"/> (the fair grange display). Mods can add their own menus with the same
    /// shape, so every member is looked up defensively instead of casting to a concrete type.</para>
    /// <para>The container pane is handled by asking the menu to move the items, never by writing its stacks directly:
    /// menus maintain their own bookkeeping (quest donation counts, grange display score, autosorting) that would
    /// silently go out of sync otherwise. When the menu offers no safe way to move a partial stack, the pane is left
    /// to vanilla behavior instead.</para>
    /// </remarks>
    public class ContainerMenuHandler : BaseMenuHandler<IClickableMenu>
        {
        /// <summary>What the currently open split input box is going to do.</summary>
        private enum ESplitTarget
            {
            /// <summary>Nothing selected.</summary>
            None,

            /// <summary>Take items out of the container pane and put them on the menu's cursor.</summary>
            ContainerToCursor,

            /// <summary>Move items from the player inventory into the menu's cursor (the vanilla held item contract).</summary>
            PlayerToCursor,

            /// <summary>Move items from the player inventory into the container through the menu's own API.</summary>
            PlayerToContainer
            }

        /// <summary>The description of the current menu, or <c>null</c>.</summary>
        private ContainerMenuAccess Access;

        /// <summary>What the split input box is currently operating on.</summary>
        private ESplitTarget Target = ESplitTarget.None;

        /// <summary>Container slot that was clicked, or -1.</summary>
        private int SelectedContainerIndex = -1;

        /// <summary>The item that was in the clicked container slot.</summary>
        private Item SelectedContainerItem;

        /// <summary>The stack size the clicked container item had.</summary>
        private int SelectedContainerAmount;

        /// <summary>Player inventory slot that was clicked, or -1.</summary>
        private int SelectedPlayerIndex = -1;

        /// <summary>The item that was in the clicked player inventory slot.</summary>
        private Item SelectedPlayerItem;

        /// <summary>
        /// True while the menu's cursor holds a stack that this handler put there, so clicks can put it back even in
        /// menus whose own click handling ignores the cursor (special order drop boxes).
        /// </summary>
        private bool HandHoldsSplit;

        /// <summary>Null constructor that currently only invokes the base null constructor</summary>
        public ContainerMenuHandler()
            : base() {
            }

        /// <summary>Checks the menu has a container-like shape we know how to drive.</summary>
        /// <param name="menu">The menu to check.</param>
        /// <returns>True if this handler can handle the menu.</returns>
        public override bool IsCorrectMenuType(IClickableMenu menu) {
            try {
                return ContainerMenuAccess.TryCreate(menu, out _);
                }
            catch {
                return false;
                }
            }

        /// <summary>Notifies the handler that its native menu has been opened.</summary>
        /// <param name="menu">The menu that was opened.</param>
        public override void Open(IClickableMenu menu) {
            this.Access = null;
            this.ResetSelection();
            this.Target = ESplitTarget.None;
            this.HandHoldsSplit = false;

            if (!ContainerMenuAccess.TryCreate(menu, out ContainerMenuAccess access)) {
                Log.WarnAlways(
                    $"[{nameof(ContainerMenuHandler)}] Ignored menu {menu?.GetType().FullName ?? "null"}: " +
                    "it has no container pane this mod can drive."
                    );
                this.NativeMenu = null;
                // Don't let a fault on this menu be blamed on the menu type we handled before.
                this.OpenedMenuType = null;
                return;
                }

            this.Access = access;
            this.HasInventory = access.SupportsPlayerInventory || access.SupportsPlayerPlacement;
            Log.TraceIfD(
                $"[{nameof(ContainerMenuHandler)}] Attached to {menu.GetType().FullName}: strategy={access.Strategy}, " +
                $"hasInventory={this.HasInventory}, heldItemTransfer={access.SupportsHeldItemTransfer}"
                );
            base.Open(menu);
            }

        /// <summary>Notifies the handler that its native menu was closed.</summary>
        public override void Close() {
            this.Access = null;
            this.ResetSelection();
            this.Target = ESplitTarget.None;
            this.HandHoldsSplit = false;
            base.Close();
            }

        /// <summary>Initializes the player inventory using the members found while probing the menu.</summary>
        protected override void InitInventory() {
            ContainerMenuAccess access = this.Access;
            if (access is null) return;

            if (!access.SupportsPlayerInventory && !access.SupportsPlayerPlacement) {
                Log.TraceIfD($"[{nameof(ContainerMenuHandler)}] No player pane to drive on {access.Menu?.GetType().Name}; inventory disabled");
                this.HasInventory = false;
                return;
                }

            this.InvHandler.Init(access.PlayerInventory, access.GetHoveredItem);

            // MenuWithInventory based menus park the split-off items in their own heldItem, not in the player's cursor slot.
            if (access.SupportsPlayerInventory)
                this.InvHandler.UseMenuHeldItem(access.GetHeldItem, access.SetHeldItem);
            }

        /// <summary>Additional checks before opening the split menu.</summary>
        /// <returns>True if it can be opened.</returns>
        protected override bool CanOpenSplitMenu() {
            // Finish placing the stack that is already in hand first, like the game's own container menus.
            if (this.HandHoldsSplit) return false;
            return this.Access is not null && base.CanOpenSplitMenu();
            }

        /// <summary>Splits a stack of either the player inventory or the container pane, whichever was clicked.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled InventoryClicked() {
            if (!this.HasInventory) return EInputHandled.NotHandled;

            try {
                ContainerMenuAccess access = this.Access;
                if (access is null) return EInputHandled.NotHandled;

                int mouseX = Game1.getMouseX(true);
                int mouseY = Game1.getMouseY(true);
                this.InvHandler.SelectItem(mouseX, mouseY);

                if (access.SupportsPlayerInventory) {
                    // The menu has its own drawn cursor: split into it.
                    if (!this.InvHandler.CanSplitSelectedItem()) {
                        Item hovered = this.InvHandler.GetHoveredItem();
                        if (ItemSplitRules.IsUnsplittable(hovered)) {
                            Log.TraceIfD(
                                $"[{nameof(ContainerMenuHandler)}] Cannot split {InventoryHandler.Describe(hovered)} " +
                                "from the player inventory; showing the hint"
                                );
                            CannotSplitHint.Show(hovered);
                            this.InvHandler.ClearSelection();
                            return EInputHandled.Consumed;
                            }
                        return EInputHandled.NotHandled;
                        }
                    this.ResetSelection();
                    this.Target = ESplitTarget.PlayerToCursor;
                    }
                else {
                    // The menu only lets the player hand items over through its own API (special order drop boxes).
                    Item item = this.InvHandler.GetHoveredItem();
                    InventoryMenu player = access.PlayerInventory;
                    if (item is null || player is null) return EInputHandled.NotHandled;
                    if (ItemSplitRules.IsUnsplittable(item)) {
                        Log.TraceIfD(
                            $"[{nameof(ContainerMenuHandler)}] Cannot split {InventoryHandler.Describe(item)} " +
                            "from the player inventory of a place-only menu; showing the hint"
                            );
                        CannotSplitHint.Show(item);
                        this.InvHandler.ClearSelection();
                        return EInputHandled.Consumed;
                        }

                    int index = player.getInventoryPositionOfClick(mouseX, mouseY);
                    if (index < 0 || index >= player.actualInventory.Count) return EInputHandled.NotHandled;
                    if (!ReferenceEquals(player.actualInventory[index], item)) return EInputHandled.NotHandled;

                    this.ResetSelection();
                    this.SelectedPlayerIndex = index;
                    this.SelectedPlayerItem = item;
                    this.Target = ESplitTarget.PlayerToContainer;
                    }

                this.SplitMenu = new StackSplitMenu(this.OnStackAmountReceived, this.InvHandler.GetDefaultSplitStackAmount());
                Log.TraceIfD($"[{nameof(ContainerMenuHandler)}] Split box opened for target {this.Target}, default amount {this.SplitMenu.HeldStackAmount}");
                return EInputHandled.Consumed;
                }
            catch (Exception e) {
                Log.ErrorAlways($"[{nameof(ContainerMenuHandler)}.{nameof(InventoryClicked)}] {e}");
                this.ResetSelection();
                this.Target = ESplitTarget.None;
                return EInputHandled.NotHandled;
                }
            }

        /// <summary>Splits a stack of the container pane.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled OpenSplitMenu() {
            ContainerMenuAccess access = this.Access;
            if (access is null || access.ContainerInventory is null)
                return EInputHandled.NotHandled;

            try {
                InventoryMenu container = access.ContainerInventory;
                int index = container.getInventoryPositionOfClick(Game1.getMouseX(true), Game1.getMouseY(true));
                if (index < 0 || index >= container.actualInventory.Count)
                    return EInputHandled.NotHandled;

                Item item = container.actualInventory[index];
                if (item is null)
                    return EInputHandled.NotHandled;

                // A container pane this mod cannot drive (the menu offers no safe way to move parts of a stack), or an
                // item the game never splits: say so instead of silently ignoring the click.
                if (access.Strategy == EContainerStrategy.PlayerOnly
                    || access.Strategy == EContainerStrategy.None
                    || ItemSplitRules.IsUnsplittable(item)) {
                    Log.TraceIfD(
                        $"[{nameof(ContainerMenuHandler)}] Cannot split container slot {index} " +
                        $"({InventoryHandler.Describe(item)}, strategy={access.Strategy}); showing the hint"
                        );
                    CannotSplitHint.Show(item);
                    return EInputHandled.Consumed;
                    }

                if (access.Strategy == EContainerStrategy.HeldItem
                    || (access.Strategy == EContainerStrategy.DirectTransfer && access.SupportsHeldItemTransfer)) {
                    Item held = access.GetHeldItem?.Invoke();
                    if (held is not null && (!item.canStackWith(held) || held.Stack >= held.maximumStackSize())) {
                        Log.TraceIfD(
                            $"[{nameof(ContainerMenuHandler)}] Cannot split container slot {index}: cursor holds " +
                            $"{InventoryHandler.Describe(held)}"
                            );
                        return EInputHandled.NotHandled;
                        }
                    }

                this.ResetSelection();
                this.SelectedContainerIndex = index;
                this.SelectedContainerItem = item;
                this.SelectedContainerAmount = item.Stack;
                this.Target = ESplitTarget.ContainerToCursor;
                this.SplitMenu = new StackSplitMenu(this.OnStackAmountReceived, (item.Stack + 1) / 2);
                Log.TraceIfD(
                    $"[{nameof(ContainerMenuHandler)}] Split box opened for container slot {index} " +
                    $"({InventoryHandler.Describe(item)}, strategy={access.Strategy})"
                    );
                return EInputHandled.Consumed;
                }
            catch (Exception e) {
                Log.ErrorAlways($"[{nameof(ContainerMenuHandler)}.{nameof(OpenSplitMenu)}] {e}");
                this.ResetSelection();
                this.Target = ESplitTarget.None;
                return EInputHandled.NotHandled;
                }
            }

        /// <summary>Callback given to the split menu that is invoked when a value is submitted.</summary>
        /// <param name="s">The user input.</param>
        protected override void OnStackAmountReceived(string s) {
            Log.TraceIfD($"[{nameof(ContainerMenuHandler)}] Received split amount \"{s}\" for target {this.Target}");
            try {
                if (int.TryParse(s, out int amount) && amount > 0) {
                    switch (this.Target) {
                        case ESplitTarget.ContainerToCursor:
                            this.TransferFromContainer(amount);
                            break;
                        case ESplitTarget.PlayerToCursor:
                            this.InvHandler.SplitSelectedItem(amount);
                            this.RefreshHandState(this.Access);
                            break;
                        case ESplitTarget.PlayerToContainer:
                            this.PlaceIntoContainer(amount);
                            break;
                        }
                    }
                }
            catch (Exception e) {
                // The move may be half applied; stop using this menu type instead of risking a second mutation.
                this.MarkFault(e, nameof(OnStackAmountReceived));
                }
            finally {
                this.ResetSelection();
                this.Target = ESplitTarget.None;
                }

            base.OnStackAmountReceived(s);
            }

        /// <summary>Moves the requested amount out of the clicked container slot.</summary>
        /// <param name="amount">The requested amount.</param>
        private void TransferFromContainer(int amount) {
            ContainerMenuAccess access = this.Access;
            Item selected = this.SelectedContainerItem;
            int index = this.SelectedContainerIndex;

            if (access is null || selected is null || index < 0) return;

            // The menu may have been closed (which drops the reference) while the player was typing.
            if (this.NativeMenu is null) return;

            InventoryMenu container = access.ContainerInventory;
            if (container is null || index >= container.actualInventory.Count) return;
            if (!ReferenceEquals(container.actualInventory[index], selected)) return;

            amount = Math.Min(amount, Math.Min(selected.Stack, this.SelectedContainerAmount));
            if (amount <= 0) return;

            Log.TraceIfD(
                $"[{nameof(ContainerMenuHandler)}] Taking {amount} of {InventoryHandler.Describe(selected)} from container slot {index}"
                );

            bool useHand = access.Strategy == EContainerStrategy.HeldItem
                || (access.Strategy == EContainerStrategy.DirectTransfer && access.SupportsHeldItemTransfer);

            if (!useHand) {
                // The menu moves the items itself (into the player inventory) and reports what is left in the slot.
                if (access.Strategy != EContainerStrategy.DirectTransfer || access.TryToGrabMethod is null) return;

                object leftover = access.TryToGrabMethod.Invoke(access.Menu, new object[] { selected, amount });
                container.actualInventory[index] = leftover as Item;
                Log.TraceIfD(
                    $"[{nameof(ContainerMenuHandler)}] TryToGrab done: slot {index} now " +
                    $"{InventoryHandler.Describe(leftover as Item)}"
                    );
                return;
                }

            Item held = access.GetHeldItem?.Invoke();
            if (held is not null && !selected.canStackWith(held)) return;

            int freeSpace = (held is null) ? selected.maximumStackSize() : held.maximumStackSize() - held.Stack;
            amount = Math.Min(amount, Math.Max(0, freeSpace));
            if (amount <= 0) {
                Log.TraceIfD($"[{nameof(ContainerMenuHandler)}] No room in the cursor for {InventoryHandler.Describe(selected)}");
                return;
                }

            Item take = null;
            if (held is null) {
                take = selected.getOne();
                if (take is null) return;
                take.Stack = amount;
                }

            // Reduce the stack first and leave it in the list while the menu's callback runs, exactly like the menu's
            // own TryToGrab does; only then drop an emptied slot.
            selected.Stack -= amount;
            container.actualInventory[index] = selected;

            if (held is null) {
                access.SetHeldItem?.Invoke(take);

                // The setter is reflective; if the menu refused it, put the items back instead of destroying them.
                if (access.GetHeldItem?.Invoke() is null) {
                    Log.WarnAlways(
                        $"[{nameof(ContainerMenuHandler)}] {access.Menu?.GetType().Name} refused the split stack; " +
                        $"putting {amount} of {InventoryHandler.Describe(selected)} back"
                        );
                    selected.Stack += amount;
                    container.actualInventory[index] = selected;
                    if (access.Strategy == EContainerStrategy.DirectTransfer)
                        access.NotifyItemChanged();
                    return;
                    }
                }
            else {
                held.Stack += amount;
                access.SetHeldItem?.Invoke(held);
                }

            // The list was changed without going through TryToGrab, so let the menu recalculate its own bookkeeping
            // (drop box donation counts) before the player continues.
            if (access.Strategy == EContainerStrategy.DirectTransfer)
                access.NotifyItemChanged();

            if (selected.Stack <= 0)
                container.actualInventory[index] = null;

            this.HandHoldsSplit = access.GetHeldItem?.Invoke() is not null;
            Log.TraceIfD(
                $"[{nameof(ContainerMenuHandler)}] Container slot {index} left with {InventoryHandler.Describe(selected)}, " +
                $"cursor={InventoryHandler.Describe(access.GetHeldItem?.Invoke())}"
                );
            }

        /// <summary>Moves the requested amount from the clicked player slot into the container through the menu's API.</summary>
        /// <param name="amount">The requested amount.</param>
        private void PlaceIntoContainer(int amount) {
            ContainerMenuAccess access = this.Access;
            Item selected = this.SelectedPlayerItem;
            int index = this.SelectedPlayerIndex;

            if (access is null || access.TryToPlaceMethod is null || selected is null || index < 0) return;

            // The menu may have been closed (which drops the reference) while the player was typing.
            if (this.NativeMenu is null) return;

            InventoryMenu player = access.PlayerInventory;
            if (player is null || index >= player.actualInventory.Count) return;
            if (!ReferenceEquals(player.actualInventory[index], selected)) return;

            amount = Math.Min(amount, selected.Stack);
            if (amount <= 0) return;

            Log.TraceIfD(
                $"[{nameof(ContainerMenuHandler)}] Placing {amount} of {InventoryHandler.Describe(selected)} from slot {index} into the container"
                );

            // The menu checks how much the container still accepts and moves the items itself, so its donation
            // counters stay correct.
            object leftover = access.TryToPlaceMethod.Invoke(access.Menu, new object[] { selected, amount });
            player.actualInventory[index] = leftover as Item;
            Log.TraceIfD(
                $"[{nameof(ContainerMenuHandler)}] TryToPlace done: player slot {index} now " +
                $"{InventoryHandler.Describe(leftover as Item)}"
                );
            }

        /// <summary>
        /// Puts a split-off cursor stack back the way the menu itself cannot: onto the player inventory, or into the
        /// container through <c>TryToPlace</c>. Only active for menus whose own click handling ignores the cursor.
        /// </summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled HandleLeftClick() {
            if (this.SplitMenu is not null) return EInputHandled.NotHandled;
            if (!this.TryGetManagedHand(out ContainerMenuAccess access, out Item held)) return EInputHandled.NotHandled;

            try {
                int mouseX = Game1.getMouseX(true);
                int mouseY = Game1.getMouseY(true);

                InventoryMenu player = access.PlayerInventory;
                if (player is not null && player.getInventoryPositionOfClick(mouseX, mouseY) >= 0) {
                    Log.TraceIfD($"[{nameof(ContainerMenuHandler)}] Left click puts {InventoryHandler.Describe(held)} back into the player inventory");
                    access.SetHeldItem?.Invoke(player.leftClick(mouseX, mouseY, held));
                    this.RefreshHandState(access);
                    return EInputHandled.Consumed;
                    }

                InventoryMenu container = access.ContainerInventory;
                if (container is not null && container.getInventoryPositionOfClick(mouseX, mouseY) >= 0) {
                    if (access.TryToPlaceMethod is null) return EInputHandled.NotHandled;

                    Log.TraceIfD($"[{nameof(ContainerMenuHandler)}] Left click places {InventoryHandler.Describe(held)} into the container");
                    object leftover = access.TryToPlaceMethod.Invoke(access.Menu, new object[] { held, held.Stack });
                    access.SetHeldItem?.Invoke(leftover as Item);
                    this.RefreshHandState(access);
                    return EInputHandled.Consumed;
                    }
                }
            catch (Exception e) {
                this.MarkFault(e, nameof(HandleLeftClick));
                }
            return EInputHandled.NotHandled;
            }

        /// <summary>Merges items from a clicked player slot into the split-off cursor stack.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled HandleRightClick() {
            if (this.SplitMenu is not null) return EInputHandled.NotHandled;
            if (!this.TryGetManagedHand(out ContainerMenuAccess access, out Item held)) return EInputHandled.NotHandled;

            try {
                int mouseX = Game1.getMouseX(true);
                int mouseY = Game1.getMouseY(true);

                InventoryMenu player = access.PlayerInventory;
                if (player is null || player.getInventoryPositionOfClick(mouseX, mouseY) < 0)
                    return EInputHandled.NotHandled;

                Log.TraceIfD($"[{nameof(ContainerMenuHandler)}] Right click merges into {InventoryHandler.Describe(held)}");
                access.SetHeldItem?.Invoke(player.rightClick(mouseX, mouseY, held));
                this.RefreshHandState(access);
                return EInputHandled.Consumed;
                }
            catch (Exception e) {
                this.MarkFault(e, nameof(HandleRightClick));
                return EInputHandled.NotHandled;
                }
            }

        /// <summary>Called when the split menu loses focus; the move is cancelled instead of running a default split.</summary>
        /// <returns>If the input was handled or consumed.</returns>
        protected override EInputHandled CancelMove() {
            CloseSplitMenu();
            this.ResetSelection();
            this.Target = ESplitTarget.None;
            return EInputHandled.Consumed;
            }

        private void ResetSelection() {
            this.SelectedContainerIndex = -1;
            this.SelectedContainerItem = null;
            this.SelectedContainerAmount = 0;
            this.SelectedPlayerIndex = -1;
            this.SelectedPlayerItem = null;
            }

        /// <summary>Checks whether the menu cursor currently holds a stack this handler put there.</summary>
        /// <param name="access">The menu description.</param>
        /// <param name="held">The item on the cursor.</param>
        /// <returns>True if clicks must be handled to give the stack back.</returns>
        private bool TryGetManagedHand(out ContainerMenuAccess access, out Item held) {
            access = this.Access;
            held = null;

            // Only menus whose own click handling ignores the cursor need help; all others use vanilla behavior.
            if (access is null || !access.SupportsHeldItemTransfer || !this.HandHoldsSplit) return false;
            if (access.GetHeldItem is null) return false;

            held = access.GetHeldItem();
            if (held is null) {
                this.HandHoldsSplit = false;
                return false;
                }
            return true;
            }

        /// <summary>Updates the owned-cursor flag after a click moved, merged or emptied the held stack.</summary>
        /// <param name="access">The menu description.</param>
        private void RefreshHandState(ContainerMenuAccess access) {
            bool previous = this.HandHoldsSplit;
            this.HandHoldsSplit = access is not null
                && access.SupportsHeldItemTransfer
                && access.GetHeldItem is not null
                && access.GetHeldItem() is not null;
            if (previous != this.HandHoldsSplit) {
                Log.TraceIfD(
                    $"[{nameof(ContainerMenuHandler)}] Cursor ownership changed: HandHoldsSplit={this.HandHoldsSplit}, " +
                    $"cursor={InventoryHandler.Describe(access?.GetHeldItem?.Invoke())}"
                    );
                }
            }
        }
    }
