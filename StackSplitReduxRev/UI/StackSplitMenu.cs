using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.UI
    {
    public class StackSplitMenu
        {
        public delegate void TextSubmittedDelegate(string input);

        public int HeldStackAmount { get; private set; }
        private StackSplitOverlay Overlay;

        public StackSplitMenu(TextSubmittedDelegate textSubmittedCallback, int heldStackAmount) {
            this.HeldStackAmount = heldStackAmount;
            IClickableMenu previousMenu = Game1.activeClickableMenu;
            Log.TraceIfD(
                $"[{nameof(StackSplitMenu)}] Opening the amount box over " +
                $"{previousMenu?.GetType().FullName ?? "none"}, default amount {heldStackAmount}"
                );
            this.Overlay = new StackSplitOverlay(
                previousMenu,
                heldStackAmount,
                text => {
                    this.Overlay = null;
                    textSubmittedCallback?.Invoke(text);
                    });
            Game1.activeClickableMenu = this.Overlay;
            }

        public void Close() {
            Log.TraceIfD($"[{nameof(StackSplitMenu)}] Closing the amount box");
            if (this.Overlay != null) {
                this.Overlay.ForceClose();
                this.Overlay = null;
                }
            }

        public void Draw(SpriteBatch b) { }
        public void Update() { }
        public void ReceiveLeftClick(int x, int y) { }
        public bool ContainsPoint(int x, int y) => false;
        public void HandleKeyPress(SButton button) { }
        }
    }
