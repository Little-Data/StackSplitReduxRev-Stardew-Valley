using StardewModdingAPI;
using System.Linq;

namespace StackSplitRedux
    {
    public static class Log {
        private static bool ShouldLog() => Mod.Config is not null && Mod.Config.DebuggingMode;
        public static void Alert(string msg) { if (ShouldLog()) Mod.Instance.Monitor.Log(msg, LogLevel.Alert); }
        public static void Error(string msg) { if (ShouldLog()) Mod.Instance.Monitor.Log(msg, LogLevel.Error); }
        public static void Warn(string msg) { if (ShouldLog()) Mod.Instance.Monitor.Log(msg, LogLevel.Warn); }
        public static void Info(string msg) { if (ShouldLog()) Mod.Instance.Monitor.Log(msg, LogLevel.Info); }
        public static void Debug(string msg) { if (ShouldLog()) Mod.Instance.Monitor.Log(msg, LogLevel.Debug); }
        public static void Trace(string msg) { if (ShouldLog()) Mod.Instance.Monitor.Log(msg, LogLevel.Trace); }
        public static void TraceIfD(string msg) { if (ShouldLog()) Trace(msg); }
        }
    public static class Seq {
        public static int Min(params int[] args) => args.Min();
        }
    }
