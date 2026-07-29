using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace StackSplitRedux
    {
    public class ModConfig
        {
        public int DefaultCraftingAmount { get; set; } = 1;
        public int DefaultShopAmount { get; set; } = 5;
        public bool DebuggingMode { get; set; } = false;
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
