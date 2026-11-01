using System;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StackSplitRedux.MenuHandlers;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.UI
    {
    /// <summary>Hint shown when the player tries to split an item that the game does not allow to split.</summary>
    /// <remarks>
    /// <para>Drawn with the game's own tooltip frame (<see cref="Game1.menuTexture"/>, the same 9-slice box the
    /// inventory tooltips use), sized to fit the text.</para>
    /// <para>It is drawn from <c>Display.RenderedActiveMenu</c>, which runs while the menu sprite batch is still open,
    /// so it is drawn after (above) the menu and its item tooltip, regardless of the player's zoom level.</para>
    /// <para>Because the hint is above the tooltip, it must not cover it: the game always anchors the item tooltip at
    /// the cursor and clamps it on screen, so its bounds can be worked out here. The hint then picks the first free
    /// spot around the cursor instead of overlapping it.</para>
    /// </remarks>
    internal static class CannotSplitHint
        {
        /// <summary>Minimum delay between two hints, in game ticks.</summary>
        private const int REPEAT_TICKS = 20;

        /// <summary>Distance kept between the hint and the cursor.</summary>
        private const int CURSOR_GAP = 24;

        /// <summary>Space kept between the hint and another tooltip.</summary>
        private const int TOOLTIP_MARGIN = 8;

        /// <summary>Used when both the config and the translation file have no usable text.</summary>
        private const string FALLBACK = "无法拆分或堆叠";

        private static string Message;
        private static int ExpireTick;
        private static int DurationTicks = 180;
        private static int LastShowTick;
        private static int LastDrawTick = int.MinValue;
        private static int LastChoice = int.MinValue;
        private static string LastLoggedMessage;

        /// <summary>Size of the tooltip the game shows for the clicked item, or (0,0) when unknown.</summary>
        private static Point TooltipSize;

        /// <summary>Shows the hint for the clicked item.</summary>
        /// <param name="clickedItem">The item the player tried to split, used to stay clear of its tooltip.</param>
        public static void Show(Item clickedItem = null) {
            try {
                if (Mod.Config?.ShowCannotSplitHint == false) return;

                int now = Game1.ticks;
                // Throttle so a held right click does not queue a wall of messages. The comparison also survives a
                // tick counter that was reset (new save / return to title).
                if (Message is not null && now >= LastShowTick && now - LastShowTick < REPEAT_TICKS) return;

                string text = GetMessage();
                if (string.IsNullOrWhiteSpace(text)) return;

                int seconds = Math.Clamp(Mod.Config?.HintDurationSeconds ?? 2, 1, 30);
                DurationTicks = seconds * 60;

                Message = text;
                TooltipSize = EstimateTooltipSize(clickedItem);
                ExpireTick = now + DurationTicks;
                LastShowTick = now;
                LastChoice = int.MinValue;
                LastLoggedMessage = null;
                Log.TraceIfD(
                    $"[{nameof(CannotSplitHint)}] Showing \"{text}\" for {seconds}s " +
                    $"(item={InventoryHandler.Describe(clickedItem)}, estimated tooltip={TooltipSize.X}x{TooltipSize.Y})"
                    );
                }
            catch (Exception e) {
                // A hint is never worth breaking the menu for.
                Log.Trace($"[{nameof(CannotSplitHint)}.{nameof(Show)}] {e}");
                }
            }

        /// <summary>Draws the hint if it is still visible. Must be called inside a sprite batch, after the menu.</summary>
        /// <param name="b">The sprite batch to draw with.</param>
        public static void Draw(SpriteBatch b) {
            if (b is null || Message is null) return;

            try {
                // The hint is drawn from two events (menu and end-of-frame); draw it once per tick.
                int now = Game1.ticks;
                if (now == LastDrawTick) return;
                LastDrawTick = now;

                int ticksLeft = ExpireTick - now;
                // Expired, or the tick counter was reset and the stored expiry is meaningless.
                if (ticksLeft <= 0 || ticksLeft > DurationTicks + 60) {
                    Clear();
                    return;
                    }

                ModConfig config = Mod.Config;
                SpriteFont font = Game1.smallFont;

                float scale = Math.Clamp((config?.HintTextScalePercent ?? 100) / 100f, 0.5f, 4f);
                int outline = Math.Clamp(config?.HintOutlineThickness ?? 2, 0, 8);
                float shadow = Math.Clamp((config?.HintShadowStrength ?? 0) / 100f, 0f, 1f);
                Color textColor = ParseColor(config?.HintTextColor, Color.White);

                // Wrap long messages instead of letting the box grow past the screen.
                int maxTextWidth = Math.Max(160, (int)((Game1.uiViewport.Width - 160) / scale));
                string text = WrapText(font, Message, maxTextWidth);
                Vector2 textSize = font.MeasureString(text) * scale;

                // The game's tooltip frame keeps ~20px of border on each side; leave a bit more so the text never
                // touches the frame. Both dimensions grow with the text, so the box fits any message.
                int width = (int)Math.Ceiling(textSize.X) + 40 + outline * 2;
                int height = (int)Math.Ceiling(textSize.Y) + 32 + outline * 2;
                // Keep the 9-slice frame from squashing its corners together.
                width = Math.Max(width, 96);
                height = Math.Max(height, 60);

                int mouseX = Game1.getMouseX(true);
                int mouseY = Game1.getMouseY(true);
                int offsetX = config?.HintOffsetX ?? 0;
                int offsetY = config?.HintOffsetY ?? 0;

                // The item tooltip occupies this area; move the hint so it is neither covered by it nor covering it.
                Rectangle avoid = TooltipSize.X > 0
                    ? AnchorTooltip(TooltipSize.X, TooltipSize.Y, mouseX, mouseY)
                    : Rectangle.Empty;

                (int x, int y) = ChoosePosition(width, height, mouseX, mouseY, offsetX, offsetY, avoid, out int choice);

                if (choice != LastChoice || !string.Equals(LastLoggedMessage, Message, StringComparison.Ordinal)) {
                    Log.TraceIfD(
                        $"[{nameof(CannotSplitHint)}] Hint layout {(choice == 0 ? "at the configured spot" : $"moved (choice {choice})")}: " +
                        $"size={width}x{height}, pos=({x},{y}), cursor=({mouseX},{mouseY}), itemTooltip={avoid}"
                        );
                    LastChoice = choice;
                    LastLoggedMessage = Message;
                    }

                // Same texture and source rect the game uses for inventory tooltips.
                IClickableMenu.drawTextureBox(
                    b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    x, y, width, height, Color.White, 1f, true);

                Vector2 position = new(
                    x + (width - textSize.X) / 2f,
                    y + (height - textSize.Y) / 2f);

                DrawText(b, font, text, position, scale, outline, shadow, textColor);
                }
            catch (Exception e) {
                Clear();
                Log.Trace($"[{nameof(CannotSplitHint)}.{nameof(Draw)}] {e}");
                }
            }

        /// <summary>Stops showing the hint.</summary>
        private static void Clear() {
            if (Message is not null)
                Log.TraceIfD($"[{nameof(CannotSplitHint)}] Hint cleared");
            Message = null;
            TooltipSize = Point.Zero;
            LastChoice = int.MinValue;
            LastLoggedMessage = null;
            }

        /// <summary>Builds the message from the configured text, falling back to the localized default.</summary>
        private static string GetMessage() {
            string text = Mod.Config?.CannotSplitMessage;
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();

            text = null;
            try {
                text = Mod.I18n.Get("hint.cannot-split")?.ToString();
                }
            catch {
                text = null;
                }
            if (string.IsNullOrWhiteSpace(text) || text == "hint.cannot-split")
                text = FALLBACK;
            return text;
            }

        /// <summary>Picks the first spot around the cursor that is on screen and clear of the item tooltip.</summary>
        private static (int x, int y) ChoosePosition(
            int width, int height, int mouseX, int mouseY, int offsetX, int offsetY, Rectangle avoid, out int choice) {
            int viewportWidth = Game1.uiViewport.Width;
            int viewportHeight = Game1.uiViewport.Height;
            int gap = CURSOR_GAP;

            // Above the cursor first (the configured default), then the other sides.
            (int x, int y)[] candidates = {
                (mouseX - width / 2 + offsetX, mouseY - height - gap + offsetY),
                (mouseX - width - gap + offsetX, mouseY - height - gap + offsetY),
                (mouseX + gap + offsetX, mouseY - height - gap + offsetY),
                (mouseX - width - gap + offsetX, mouseY - height / 2 + offsetY),
                (mouseX + gap + offsetX, mouseY - height / 2 + offsetY),
                (mouseX - width / 2 + offsetX, mouseY + gap + offsetY),
                (mouseX - width - gap + offsetX, mouseY + gap + offsetY),
                (mouseX + gap + offsetX, mouseY + gap + offsetY),
                };

            Rectangle avoidZone = avoid.IsEmpty
                ? Rectangle.Empty
                : new Rectangle(avoid.X - TOOLTIP_MARGIN, avoid.Y - TOOLTIP_MARGIN,
                    avoid.Width + TOOLTIP_MARGIN * 2, avoid.Height + TOOLTIP_MARGIN * 2);

            (int x, int y) firstFree = (int.MinValue, int.MinValue);
            int firstFreeIndex = -1;
            (int x, int y) firstOnScreen = (int.MinValue, int.MinValue);

            for (int i = 0; i < candidates.Length; i++) {
                (int x, int y) = candidates[i];
                bool onScreen = x >= 8 && y >= 8
                    && x + width <= viewportWidth - 8 && y + height <= viewportHeight - 8;

                if (onScreen && !Intersects(new Rectangle(x, y, width, height), avoidZone)) {
                    firstFree = (x, y);
                    firstFreeIndex = i;
                    break;
                    }
                if (onScreen && firstOnScreen.x == int.MinValue)
                    firstOnScreen = (x, y);
                }

            if (firstFreeIndex >= 0) {
                choice = firstFreeIndex;
                return firstFree;
                }
            if (firstOnScreen.x != int.MinValue) {
                // Fits on screen but overlaps the tooltip; only used when nothing better exists.
                choice = -1;
                return firstOnScreen;
                }

            // Nothing fits cleanly: keep the configured spot, clamped on screen.
            choice = -2;
            return (
                Math.Clamp(candidates[0].x, 8, Math.Max(8, viewportWidth - width - 8)),
                Math.Clamp(candidates[0].y, 8, Math.Max(8, viewportHeight - height - 8)));
            }

        private static bool Intersects(Rectangle a, Rectangle b) {
            return !b.IsEmpty && a.Intersects(b);
            }

        /// <summary>Measures the item tooltip the game would show, using its own layout rules.</summary>
        /// <returns>The tooltip size, or (0,0) if it cannot be determined.</returns>
        private static Point EstimateTooltipSize(Item item) {
            if (item is null) return Point.Zero;

            try {
                SpriteFont small = Game1.smallFont;
                string description = item.getDescription() ?? "";
                Vector2 descriptionSize = small.MeasureString(description);

                string title = item.DisplayName ?? "";
                Vector2 titleSize = title.Length > 0 ? Game1.dialogueFont.MeasureString(title) : Vector2.Zero;

                // The game uses: width = max(text width, title width) + 32, then +4 for the border;
                // height = text height + 32, plus the title block when there is one.
                int width = (int)Math.Max(descriptionSize.X, titleSize.X) + 36;
                int height = Math.Max(
                    60,
                    (int)descriptionSize.Y + 32 + (title.Length > 0 ? (int)titleSize.Y + 16 : 0));

                return new Point(width, height);
                }
            catch (Exception e) {
                Log.Trace($"[{nameof(CannotSplitHint)}.{nameof(EstimateTooltipSize)}] {e}");
                return Point.Zero;
                }
            }

        /// <summary>Places a tooltip of the given size where the game would, next to the cursor.</summary>
        private static Rectangle AnchorTooltip(int width, int height, int mouseX, int mouseY) {
            Rectangle safe = GetSafeArea();

            int x = mouseX + 32;
            int y = mouseY + 32;
            if (x + width > safe.Right) {
                x = safe.Right - width;
                y += 16;
                }
            if (y + height > safe.Bottom) {
                x += 16;
                if (x + width > safe.Right)
                    x = safe.Right - width;
                y = safe.Bottom - height;
                }

            return new Rectangle(x, y, width, height);
            }

        /// <summary>The title-safe area in UI coordinates (same space the menu draws in).</summary>
        /// <remarks>Approximates the game's 5% title-safe margin; only used to decide where a tooltip is clamped.</remarks>
        private static Rectangle GetSafeArea() {
            try {
                int width = Game1.uiViewport.Width;
                int height = Game1.uiViewport.Height;
                int marginX = width / 20;
                int marginY = height / 20;
                return new Rectangle(marginX, marginY, width - marginX * 2, height - marginY * 2);
                }
            catch {
                return new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
                }
            }

        /// <summary>Draws the text with an optional shadow and outline.</summary>
        private static void DrawText(
            SpriteBatch b, SpriteFont font, string text, Vector2 position, float scale,
            int outline, float shadow, Color color) {
            // Shadow first: an offset copy the text sits on top of.
            if (shadow > 0f) {
                Vector2 shadowOffset = new(2f * scale, 3f * scale);
                b.DrawString(font, text, position + shadowOffset, Color.Black * shadow,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
                }

            if (outline > 0) {
                // Eight offset copies around the glyphs give a solid outline at any font size.
                float offset = outline * Math.Max(1f, scale);
                for (int dx = -1; dx <= 1; dx++) {
                    for (int dy = -1; dy <= 1; dy++) {
                        if (dx == 0 && dy == 0) continue;
                        Vector2 shift = new(dx * offset, dy * offset);
                        b.DrawString(font, text, position + shift, Color.Black,
                            0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
                        }
                    }
                }

            b.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
            }

        /// <summary>Wraps the text so the box never grows past the screen, including CJK text without spaces.</summary>
        private static string WrapText(SpriteFont font, string text, int maxWidth) {
            StringBuilder result = new();

            foreach (string rawLine in text.Split('\n')) {
                string line = rawLine.Replace("\r", "").TrimEnd();
                if (line.Length == 0) {
                    result.AppendLine();
                    continue;
                    }

                StringBuilder current = new();
                foreach (char c in line) {
                    if (current.Length > 0 && font.MeasureString(current.ToString() + c).X > maxWidth) {
                        string currentText = current.ToString();

                        // Prefer breaking on a space so Latin words stay readable; otherwise break per character.
                        int breakAt = currentText.LastIndexOf(' ');
                        if (breakAt > 0) {
                            result.AppendLine(currentText.Substring(0, breakAt));
                            current.Clear();
                            current.Append(currentText.Substring(breakAt + 1));
                            if (c != ' ') current.Append(c);
                            }
                        else {
                            result.AppendLine(currentText);
                            current.Clear();
                            if (c != ' ') current.Append(c);
                            }
                        }
                    else {
                        current.Append(c);
                        }
                    }
                result.AppendLine(current.ToString());
                }

            return result.ToString().TrimEnd('\r', '\n');
            }

        /// <summary>Parses a hex colour like <c>#FFFFFF</c>, <c>#FFF</c> or <c>#AARRGGBB</c>.</summary>
        /// <param name="value">The configured value.</param>
        /// <param name="fallback">Colour to use when the value is blank or invalid.</param>
        private static Color ParseColor(string value, Color fallback) {
            try {
                if (string.IsNullOrWhiteSpace(value)) return fallback;

                string hex = value.Trim().TrimStart('#');
                if (hex.Length == 3)
                    hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);

                if (hex.Length == 6
                    && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) {
                    return new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                    }
                if (hex.Length == 8
                    && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb)) {
                    return new Color(
                        (int)((argb >> 16) & 0xFF),
                        (int)((argb >> 8) & 0xFF),
                        (int)(argb & 0xFF),
                        (int)((argb >> 24) & 0xFF));
                    }
                }
            catch {
                // Fall through to the default colour.
                }
            return fallback;
            }
        }
    }
