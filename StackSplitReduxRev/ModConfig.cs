using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace StackSplitRedux
    {
    public class ModConfig
        {
        public int DefaultCraftingAmount { get; set; } = 1;
        public int DefaultShopAmount { get; set; } = 5;
        public bool DebuggingMode { get; set; } = false;
        public bool EnableGenericMenuSupport { get; set; } = true;
        public bool ShowCannotSplitHint { get; set; } = true;
        /// <summary>Text shown when the clicked item cannot be split. Blank = use the localized default.</summary>
        public string CannotSplitMessage { get; set; } = "";
        /// <summary>How long the hint stays on screen, in seconds.</summary>
        public int HintDurationSeconds { get; set; } = 2;

        // Appearance of the "cannot split" hint.
        /// <summary>Horizontal offset of the hint relative to the mouse cursor, in pixels.</summary>
        public int HintOffsetX { get; set; } = 0;
        /// <summary>Vertical offset of the hint relative to the mouse cursor, in pixels (negative = above).</summary>
        public int HintOffsetY { get; set; } = 0;
        /// <summary>Hint font size as a percentage of the game's small font.</summary>
        public int HintTextScalePercent { get; set; } = 100;
        /// <summary>Thickness of the black outline around the hint text, in pixels (0 = no outline).</summary>
        public int HintOutlineThickness { get; set; } = 2;
        /// <summary>Strength of the drop shadow behind the hint text, in percent (0 = no shadow).</summary>
        public int HintShadowStrength { get; set; } = 0;
        /// <summary>Hint text colour as a hex value (e.g. <c>#FFFFFF</c>). Blank = white.</summary>
        public string HintTextColor { get; set; } = "#FFFFFF";
        public KeybindList ModifierKey { get; set; } = new(SButton.LeftShift);
        public int InputBoxOffsetX { get; set; } = 0;
        public int InputBoxOffsetY { get; set; } = -64;
        }

    /// <summary>
    /// This class containe "tunables" that should not be user-editable
    /// </summary>
    /// <remarks>Gathered here so we can tune the mod from one place, instead of hunting down config knobs everywhere</remarks>
    internal static class StaticConfig
        {

        /// <summary>Delay between new menu appearing & our handler beginning</summary>
        /// <remarks>To allow time for other mods to manipulate inventories</remarks>
        internal readonly static int SplitMenuOpenDelayTicks = 2;

        /// <summary>Mods that we are conflicting with. If found, abort this mod</summary>
        internal readonly static string[] ConflictingMods = new [] { "tstaples.StackSplitX" };

        }
    }
