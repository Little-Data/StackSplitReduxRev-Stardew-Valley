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

        /// <summary>How long a key must be held before it starts repeating, in milliseconds.</summary>
        private const float REPEAT_START_DELAY_MS = 400f;

        /// <summary>Delay between repeats while a key is held, in milliseconds.</summary>
        private const float REPEAT_INTERVAL_MS = 60f;

        private readonly IClickableMenu PreviousMenu;
        private string _text = "";
        private readonly Action<string> OnSubmit;
        private readonly ClickableTextureComponent OKButton;
        private readonly ClickableTextureComponent CancelButton;
        private readonly Rectangle TextBoxBounds;
        private int _caretTimer;
        private bool _finished;

        /// <summary>The repeatable key currently held, or <see cref="Keys.None"/>.</summary>
        private Keys HeldKey = Keys.None;

        /// <summary>Time left before the held key produces its next character.</summary>
        private float RepeatTimer;

        /// <summary>Index in <see cref="_text"/> where the next character is inserted (the "|" caret).</summary>
        private int CaretIndex;

        private readonly Rectangle MouseRect = Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 0, Game1.smallestTileSize, Game1.smallestTileSize);
        private readonly Color MouseTransparency = Color.White * Game1.mouseCursorTransparency;
        private readonly float MouseScale = Game1.pixelZoom + (Game1.dialogueButtonScale / 150f);

        public StackSplitOverlay(IClickableMenu previousMenu, int defaultAmount, Action<string> onSubmit) {
            this.PreviousMenu = previousMenu;
            this._text = defaultAmount.ToString();
            this.CaretIndex = this._text.Length;
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

            Log.TraceIfD(
                $"[{nameof(StackSplitOverlay)}] Amount box at {this.TextBoxBounds} over " +
                $"{previousMenu?.GetType().Name ?? "none"}, default {defaultAmount}, " +
                $"offsets=({Mod.Config?.InputBoxOffsetX},{Mod.Config?.InputBoxOffsetY})"
                );
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
                RemoveCharacterBeforeCaret();
                BeginRepeat(key);
                return;
                }
            if (key == Keys.Delete) {
                RemoveCharacterAtCaret();
                BeginRepeat(key);
                return;
                }
            if (key == Keys.Left) {
                MoveCaret(-1);
                BeginRepeat(key);
                return;
                }
            if (key == Keys.Right) {
                MoveCaret(1);
                BeginRepeat(key);
                return;
                }
            if (key == Keys.Home) {
                this.CaretIndex = 0;
                return;
                }
            if (key == Keys.End) {
                this.CaretIndex = this._text.Length;
                return;
                }

            if (TryGetDigit(key, out char digit)) {
                AppendDigit(digit);
                BeginRepeat(key);
                }
            }

        /// <summary>Called every frame; repeats a held digit/Backspace/arrow key.</summary>
        public override void update(GameTime time) {
            base.update(time);
            this._caretTimer = (this._caretTimer + 1) % 60;
            this.UpdateKeyRepeat(time);
            }

        /// <summary>Repeats the held key after a short delay, so long presses are usable.</summary>
        private void UpdateKeyRepeat(GameTime time) {
            if (this._finished) return;

            Keys held = Keys.None;
            try {
                held = GetHeldRepeatKey();
                }
            catch {
                // No keyboard state (e.g. during a transition): nothing to repeat.
                return;
                }

            if (held == Keys.None) {
                this.HeldKey = Keys.None;
                return;
                }

            if (held != this.HeldKey) {
                // The first press is delivered by receiveKeyPress; wait before repeating it.
                this.HeldKey = held;
                this.RepeatTimer = REPEAT_START_DELAY_MS;
                return;
                }

            this.RepeatTimer -= (float)time.ElapsedGameTime.TotalMilliseconds;
            if (this.RepeatTimer > 0f) return;

            this.RepeatTimer = REPEAT_INTERVAL_MS;

            switch (held) {
                case Keys.Back:
                    RemoveCharacterBeforeCaret();
                    break;
                case Keys.Delete:
                    RemoveCharacterAtCaret();
                    break;
                case Keys.Left:
                    MoveCaret(-1);
                    break;
                case Keys.Right:
                    MoveCaret(1);
                    break;
                default:
                    if (TryGetDigit(held, out char digit))
                        AppendDigit(digit);
                    break;
                }
            }

        /// <summary>Starts the repeat delay for a key that was just pressed.</summary>
        private void BeginRepeat(Keys key) {
            this.HeldKey = key;
            this.RepeatTimer = REPEAT_START_DELAY_MS;
            Log.TraceIfD(
                $"[{nameof(StackSplitOverlay)}] Edit key {key}: text=\"{this._text}\", caret={this.CaretIndex}"
                );
            }

        /// <summary>Finds the digit, Backspace or arrow key that is currently held, if any.</summary>
        private static Keys GetHeldRepeatKey() {
            KeyboardState state = Game1.input.GetKeyboardState();

            // Editing keys first so they stay responsive even if another key is also down.
            if (state.IsKeyDown(Keys.Back)) return Keys.Back;
            if (state.IsKeyDown(Keys.Delete)) return Keys.Delete;
            if (state.IsKeyDown(Keys.Left)) return Keys.Left;
            if (state.IsKeyDown(Keys.Right)) return Keys.Right;

            foreach (Keys key in RepeatableDigitKeys) {
                if (state.IsKeyDown(key)) return key;
                }
            return Keys.None;
            }

        private static readonly Keys[] RepeatableDigitKeys = {
            Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9,
            Keys.NumPad0, Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4,
            Keys.NumPad5, Keys.NumPad6, Keys.NumPad7, Keys.NumPad8, Keys.NumPad9,
            };

        /// <summary>Inserts a digit at the caret, respecting the character limit.</summary>
        private void AppendDigit(char digit) {
            if (this._text.Length >= CHAR_LIMIT) return;
            this._text = this._text.Insert(this.CaretIndex, digit.ToString());
            this.CaretIndex++;
            }

        /// <summary>Removes the character before the caret.</summary>
        private void RemoveCharacterBeforeCaret() {
            if (this.CaretIndex <= 0) return;
            this._text = this._text.Remove(this.CaretIndex - 1, 1);
            this.CaretIndex--;
            }

        /// <summary>Removes the character under the caret.</summary>
        private void RemoveCharacterAtCaret() {
            if (this.CaretIndex >= this._text.Length) return;
            this._text = this._text.Remove(this.CaretIndex, 1);
            }

        /// <summary>Moves the caret, clamped to the text.</summary>
        private void MoveCaret(int delta) {
            this.CaretIndex = Math.Clamp(this.CaretIndex + delta, 0, this._text.Length);
            }

        /// <summary>Converts a top-row or numpad digit key to its character.</summary>
        private static bool TryGetDigit(Keys key, out char digit) {
            digit = '\0';
            if (key >= Keys.D0 && key <= Keys.D9) {
                digit = (char)('0' + (key - Keys.D0));
                return true;
                }
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) {
                digit = (char)('0' + (key - Keys.NumPad0));
                return true;
                }
            return false;
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
                // The caret sits at the edit position, not always at the end of the text.
                Vector2 caretOffset = font.MeasureString(this._text.Substring(0, this.CaretIndex));
                float lineHeight = font.MeasureString("0").Y;
                b.Draw(Game1.staminaRect,
                    new Rectangle(
                        (int)(this.TextBoxBounds.X + Game1.tileSize / 4 + caretOffset.X + Game1.pixelZoom),
                        this.TextBoxBounds.Y + Game1.tileSize / 3 - Game1.pixelZoom,
                        Game1.pixelZoom, (int)lineHeight),
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
                Log.TraceIfD($"[{nameof(StackSplitOverlay)}] Force-closed while showing \"{this._text}\"");
                this._finished = true;
                if (Game1.activeClickableMenu == this)
                    Game1.activeClickableMenu = this.PreviousMenu;
                }
            }

        private void Submit() {
            if (this._finished) return;
            this._finished = true;
            Log.TraceIfD($"[{nameof(StackSplitOverlay)}] Submitted \"{this._text}\"");
            try {
                this.OnSubmit?.Invoke(this._text);
                }
            catch (Exception ex) {
                // The split callback runs inside the game's input loop; an exception here would crash the game.
                Log.ErrorAlways($"[{nameof(StackSplitOverlay)}.{nameof(Submit)}] {ex}");
                }
            Game1.activeClickableMenu = this.PreviousMenu;
            }

        private void Cancel() {
            if (this._finished) return;
            this._finished = true;
            Log.TraceIfD($"[{nameof(StackSplitOverlay)}] Cancelled (was \"{this._text}\")");
            try {
                this.OnSubmit?.Invoke("0");
                }
            catch (Exception ex) {
                Log.ErrorAlways($"[{nameof(StackSplitOverlay)}.{nameof(Cancel)}] {ex}");
                }
            Game1.activeClickableMenu = this.PreviousMenu;
            }
        }
    }
