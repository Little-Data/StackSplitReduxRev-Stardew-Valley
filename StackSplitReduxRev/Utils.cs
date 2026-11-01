using StardewModdingAPI;
using StardewValley;
using System.Linq;

namespace StackSplitRedux
    {
    public static class Log {
        private static bool ShouldLog() => Mod.Instance is not null && Mod.Config is not null && Mod.Config.DebuggingMode;

        /// <summary>Whether debug logging is enabled. Use to guard logs built from expensive work.</summary>
        public static bool IsEnabled => ShouldLog();

        /// <summary>Writes to the SMAPI log. Never throws: the mod logs from finalizers and from exception handlers.</summary>
        private static void Write(string msg, LogLevel level) {
            try {
                Mod.Instance?.Monitor.Log(msg, level);
                }
            catch {
                // Logging must never be the reason the game goes down.
                }
            }

        public static void Alert(string msg) { if (ShouldLog()) Write(msg, LogLevel.Alert); }
        public static void Error(string msg) { if (ShouldLog()) Write(msg, LogLevel.Error); }
        public static void Warn(string msg) { if (ShouldLog()) Write(msg, LogLevel.Warn); }
        public static void Info(string msg) { if (ShouldLog()) Write(msg, LogLevel.Info); }
        public static void Debug(string msg) { if (ShouldLog()) Write(msg, LogLevel.Debug); }
        public static void Trace(string msg) { if (ShouldLog()) Write(msg, LogLevel.Trace); }
        public static void TraceIfD(string msg) { if (ShouldLog()) Trace(msg); }

        /// <summary>
        /// Log a warning even when <see cref="ModConfig.DebuggingMode"/> is disabled.
        /// </summary>
        /// <remarks>Used by fail-safe guards, where silently ignoring a problem would make it impossible to diagnose.</remarks>
        public static void WarnAlways(string msg) { Write(msg, LogLevel.Warn); }

        /// <summary>
        /// Log an error even when <see cref="ModConfig.DebuggingMode"/> is disabled.
        /// </summary>
        /// <remarks>Used for exceptions caught in handlers, so a broken assumption is visible without killing the game.</remarks>
        public static void ErrorAlways(string msg) { Write(msg, LogLevel.Error); }
        }
    public static class Seq {
        public static int Min(params int[] args) => args.Min();
        }

    /// <summary>Rules deciding whether the game allows an item to be split at all.</summary>
    /// <remarks>
    /// The game itself treats these items as unsplittable: tools, weapons, rings, boots, hats, clothing, furniture,
    /// wallpaper and flooring report a maximum stack of 1, and a stack with a single item has nothing to split off.
    /// </remarks>
    public static class ItemSplitRules {
        /// <summary>Checks whether the item type never stacks, so no amount of it can ever be split.</summary>
        /// <param name="item">The item to check.</param>
        public static bool IsTypeUnsplittable(Item item) {
            try {
                if (item is null) return false;

                int maxStack = item.maximumStackSize();
                // The game also uses -1 for items that cannot be picked up at all.
                return maxStack < 0 || maxStack <= 1;
                }
            catch {
                // Unknown item data: leave it to the normal checks instead of claiming it cannot be split.
                return false;
                }
            }

        /// <summary>Checks whether the item cannot be split right now: it never stacks, or only one is left.</summary>
        /// <param name="item">The item to check.</param>
        public static bool IsUnsplittable(Item item) {
            try {
                if (item is null) return false;
                return IsTypeUnsplittable(item) || item.Stack <= 1;
                }
            catch {
                return false;
                }
            }
        }
    }
