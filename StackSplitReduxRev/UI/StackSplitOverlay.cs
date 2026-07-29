using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.UI
    {
    internal class StackSplitOverlay : IClickableMenu
        {
        private const int CHAR_LIMIT = 4;

        private readonly IClickableMenu PreviousMenu;
        private string _text = "";
        private readonly Action<string> OnSubmit;
        private readonly ClickableTextureComponent OKButton;
        private readonly ClickableTextureComponent CancelButton;
        private readonly Rectangle TextBoxBounds;
        private int _caretTimer;
        private bool _finished;

        private readonly Rectangle MouseRect = Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 0, Game1.smallestTileSize, Game1.smallestTileSize);
        private readonly Color MouseTransparency = Color.White * Game1.mouseCursorTransparency;
        private readonly float MouseScale = Game1.pixelZoom + (Game1.dialogueButtonScale / 150f);

        public StackSplitOverlay(IClickableMenu previousMenu, int defaultAmount, Action<string> onSubmit) {
            this.PreviousMenu = previousMenu;
            this._text = defaultAmount.ToString();
            this.OnSubmit = onSubmit;

            int width = Game1.tileSize * 2;
            int height = Game1.tileSize;
            int totalWidth = width + Game1.pixelZoom + Game1.tileSize + Game1.pixelZoom * 2 + Game1.tileSize;
            int x = Math.Clamp(Game1.getMouseX() + Mod.Config.InputBoxOffsetX, 0, Game1.uiViewport.Width - totalWidth);
            int y = Math.Clamp(Game1.getMouseY() + Mod.Config.InputBoxOffsetY, 0, Game1.uiViewport.Height - height);

            this.TextBoxBounds = new Rectangle(x, y, width, height);

            this.OKButton = new ClickableTextureComponent(
                new Rectangle(x + width + Game1.pixelZoom, y, Game1.tileSize, Game1.tileSize),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46, -1, -1),
                1f, false);

            this.CancelButton = new ClickableTextureComponent(
                new Rectangle(x + width + Game1.tileSize + Game1.pixelZoom * 2, y, Game1.tileSize, Game1.tileSize),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 47, -1, -1),
                1f, false);
            }

        public override void receiveKeyPress(Keys key) {
            if (key == Keys.Enter) {
                Submit();
                return;
                }
            if (key == Keys.Escape || key == Keys.E) {
                Cancel();
                return;
                }
            if (key == Keys.Back) {
                if (this._text.Length > 0)
                    this._text = this._text.Substring(0, this._text.Length - 1);
                return;
                }
            if (this._text.Length >= CHAR_LIMIT) return;

            char? digit = null;
            if (key >= Keys.D0 && key <= Keys.D9)
                digit = (char)('0' + (key - Keys.D0));
            else if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                digit = (char)('0' + (key - Keys.NumPad0));

            if (digit.HasValue)
                this._text += digit.Value;
            }

        public override void receiveLeftClick(int x, int y, bool playSound = true) {
            if (this.OKButton.containsPoint(x, y)) {
                Game1.playSound("smallSelect");
                Submit();
                return;
                }
            if (this.CancelButton.containsPoint(x, y)) {
                Game1.playSound("smallSelect");
                Cancel();
                return;
                }
            Cancel();
            }

        public override void performHoverAction(int x, int y) {
            this.OKButton.tryHover(x, y);
            this.CancelButton.tryHover(x, y);
            }

        public override void update(GameTime time) {
            base.update(time);
            this._caretTimer = (this._caretTimer + 1) % 60;
            }

        public override void draw(SpriteBatch b) {
            this.PreviousMenu?.draw(b);

            b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height),
                new Color(0, 0, 0, 0.15f));

            SpriteFont font = Game1.smallFont;
            IClickableMenu.drawTextureBox(b, this.TextBoxBounds.X, this.TextBoxBounds.Y,
                this.TextBoxBounds.Width, this.TextBoxBounds.Height, Color.White);

            Vector2 textPos = new Vector2(
                this.TextBoxBounds.X + Game1.tileSize / 4,
                this.TextBoxBounds.Y + Game1.tileSize / 3);
            b.DrawString(font, this._text, textPos, Game1.textColor);

            if (this._caretTimer < 30) {
                Vector2 textSize = font.MeasureString(this._text.Length > 0 ? this._text : " ");
                float letterWidth = textSize.X / (this._text.Length > 0 ? this._text.Length : 1);
                float caretX = letterWidth * this._text.Length;
                b.Draw(Game1.staminaRect,
                    new Rectangle(
                        (int)(this.TextBoxBounds.X + Game1.tileSize / 4 + caretX + Game1.pixelZoom),
                        this.TextBoxBounds.Y + Game1.tileSize / 3 - Game1.pixelZoom,
                        Game1.pixelZoom, (int)textSize.Y),
                    Game1.textColor);
                }

            this.OKButton.draw(b);
            this.CancelButton.draw(b);

            if (!Game1.options.hardwareCursor) {
                b.Draw(Game1.mouseCursors, new Vector2(Game1.getMouseX(), Game1.getMouseY()),
                    this.MouseRect, this.MouseTransparency, 0f, Vector2.Zero, this.MouseScale, SpriteEffects.None, 1f);
                }
            }

        public void ForceClose() {
            if (!this._finished) {
                this._finished = true;
                if (Game1.activeClickableMenu == this)
                    Game1.activeClickableMenu = this.PreviousMenu;
                }
            }

        private void Submit() {
            if (this._finished) return;
            this._finished = true;
            this.OnSubmit?.Invoke(this._text);
            Game1.activeClickableMenu = this.PreviousMenu;
            }

        private void Cancel() {
            if (this._finished) return;
            this._finished = true;
            this.OnSubmit?.Invoke("0");
            Game1.activeClickableMenu = this.PreviousMenu;
            }
        }
    }
