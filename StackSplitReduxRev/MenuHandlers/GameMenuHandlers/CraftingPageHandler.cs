using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

using StardewValley;
using StardewValley.Menus;

using StackSplitRedux.UI;

using SChest = StardewValley.Objects.Chest;

namespace StackSplitRedux.MenuHandlers
    {
    public class CraftingPageHandler : GameMenuPageHandler<CraftingPage>
        {
        /// <summary>Store the click location so it's the same after the user submits the split menu even if they moved their mouse.</summary>
        private Point ClickItemLocation;

        /// <summary>Used to track if the inventory or crafting menu recieved input last.</summary>
        private bool WasInventoryClicked = false;

        /// <summary>Maximum possible amount for crafting/cooking item selected</summary>
        private int MaxAmount;

        /// <summary>Null constructor that currently only invokes the base null constructor</summary>
        public CraftingPageHandler()
            : base() {
            }

        /// <summary>Initializes the inventory using the most common variable names.</summary>
        public override void InitInventory() {
            try {
                // We need to do this explicitly because the crafting page uses a different variable name for hover item.
                var inventoryMenu = this.MenuPage.inventory;
                var hoveredItemField = Mod.Reflection.GetField<Item>(this.MenuPage, "hoverItem");

                this.InventoryHandler.Init(inventoryMenu, () => hoveredItemField.GetValue());

                // The crafting page draws its own heldItem while a stack is being moved; the player's cursor slot is not
                // drawn here, so putting the split-off items there would make them invisible.
                if (ContainerMenuAccess.TryGetHeldItemAccessors(this.MenuPage, out var heldGetter, out var heldSetter))
                    this.InventoryHandler.UseMenuHeldItem(heldGetter, heldSetter);
                }
            catch (System.Exception e) {
                Log.ErrorAlways($"[{nameof(CraftingPageHandler)}.{nameof(InitInventory)}] {e}");
                }
            }

        /// <summary>Tells the handler that the inventory was shift-clicked.</summary>
        /// <param name="stackAmount">The default stack amount to display in the split menu.</param>
        /// <returns>If the input was handled or consumed. Generally returns not handled if an invalid item was selected.</returns>
        public override EInputHandled InventoryClicked(out int stackAmount) {
            this.WasInventoryClicked = true;
            return base.InventoryClicked(out stackAmount);
            }

        /// <summary>Tells the handler that the interface recieved the hotkey input.</summary>
        /// <param name="stackAmount">The default stack amount to display in the split menu.</param>
        /// <returns>If the input was handled or consumed. Generally returns not handled if an invalid item was selected.</returns>
        public override EInputHandled OpenSplitMenu(out int stackAmount) {
            stackAmount = Mod.Config.DefaultCraftingAmount;
            this.WasInventoryClicked = false;

            var hoverRecipe = Mod.Reflection.GetField<CraftingRecipe>(this.MenuPage, "hoverRecipe").GetValue();
            var hoveredItem = hoverRecipe?.createItem();
            var heldItem = Mod.Reflection.GetField<Item>(this.MenuPage, "heldItem").GetValue();

            Log.TraceIfD(
                $"[{nameof(CraftingPageHandler)}] Recipe click: recipe={hoverRecipe?.name ?? "none"}, " +
                $"output={InventoryHandler.Describe(hoveredItem)}, held={InventoryHandler.Describe(heldItem)}"
                );

            // If we're holding an item already then it must stack with the item we want to craft.
            if (hoveredItem == null)
                return EInputHandled.NotHandled;

            // A recipe whose output never stacks (tools, furniture, ...) can only be crafted one at a time.
            if (ItemSplitRules.IsTypeUnsplittable(hoveredItem)) {
                Log.TraceIfD($"[{nameof(CraftingPageHandler)}] Recipe output {InventoryHandler.Describe(hoveredItem)} cannot stack; showing the hint");
                CannotSplitHint.Show(hoveredItem);
                stackAmount = 0;
                return EInputHandled.Consumed;
                }

            if (heldItem != null && heldItem.Name != hoveredItem.Name) {
                Log.TraceIfD($"[{nameof(CraftingPageHandler)}] Held item {InventoryHandler.Describe(heldItem)} does not match the recipe output");
                return EInputHandled.NotHandled;
                }

            // We might need to put in some mutex-wrangling here if there' a problem with MultiPlayer desyncs...

            // Grab ingredients in additional places (if any)
            List<Item> extraItems = null;
            if (this.MenuPage._materialContainers is not null) {
                extraItems = new();
                // Not using .SelectMany() but iterating manually so we can trigger TraceIfD (optionally if Config = Release)
                foreach (SChest chest in this.MenuPage._materialContainers) {
                    Log.TraceIfD($"Engrabbening {chest} @ {chest.TileLocation} (fridge = {chest.fridge.Value})");
                    extraItems.AddRange(chest.Items);
                    }
                }

            // Only allow items that can actually stack
            if (!hoveredItem.canStackWith(hoveredItem)) {
                Log.TraceIfD($"[{nameof(CraftingPageHandler)}] Recipe output {InventoryHandler.Describe(hoveredItem)} cannot be stacked; showing the hint");
                CannotSplitHint.Show(hoveredItem);
                stackAmount = 0;
                return EInputHandled.Consumed;
                }
            if (!hoverRecipe.doesFarmerHaveIngredientsInInventory(extraItems)) {
                Log.TraceIfD($"[{nameof(CraftingPageHandler)}] Not enough ingredients for recipe {hoverRecipe.name}");
                return EInputHandled.NotHandled;
                }

            this.MaxAmount = hoverRecipe.getCraftableCount(extraItems);
            this.ClickItemLocation = new Point(Game1.getOldMouseX(true), Game1.getOldMouseY(true));
            Log.TraceIfD($"[{nameof(CraftingPageHandler)}] Craftable amount for {hoverRecipe.name}: {this.MaxAmount}");
            return EInputHandled.Consumed;
            }

        /// <summary>Lets the handler run the logic for doing the split after the amount has been input.</summary>
        /// <param name="amount">The stack size the user requested.</param>
        public override void OnStackAmountEntered(int amount) {
            // Run regular inventory logic if that's what was clicked.
            if (this.WasInventoryClicked) {
                base.OnStackAmountEntered(amount);
                return;
                }

            int count = Math.Min(amount, this.MaxAmount);
            Log.TraceIfD($"[{nameof(CraftingPageHandler)}] Crafting {count} of the hovered recipe (requested {amount}, max {this.MaxAmount})");
            for (int i = 0; i < count; ++i) {
                this.MenuPage.receiveRightClick(this.ClickItemLocation.X, this.ClickItemLocation.Y, playSound: i == 0);
                }
            }
        }
    }
