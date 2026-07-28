using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace StackSplitRedux.UI
    {
    public class StackSplitMenu
        {
        public delegate void TextSubmittedDelegate(string input);

        public int HeldStackAmount { get; private set; }
        private StackSplitOverlay Overlay;

        public StackSplitMenu(TextSubmittedDelegate textSubmittedCallback, int heldStackAmount) {
            this.HeldStackAmount = heldStackAmount;
            this.Overlay = new StackSplitOverlay(
                Game1.activeClickableMenu,
                heldStackAmount,
                text => {
                    this.Overlay = null;
                    textSubmittedCallback?.Invoke(text);
                    });
            Game1.activeClickableMenu = this.Overlay;
            }

        public void Close() {
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
