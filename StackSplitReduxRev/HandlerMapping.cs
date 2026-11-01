using System;
using System.Collections.Generic;
using StackSplitRedux.MenuHandlers;
using StardewModdingAPI;

namespace StackSplitRedux
    {
    internal static class HandlerMapping
        {
        private static readonly Dictionary<Type, IMenuHandler> HandlerByType = new();
        private static readonly Dictionary<string, IMenuHandler> HandlerByName = new();
        private static readonly Dictionary<Type, IMenuHandler> HandlerSingletons = new();

        private static IMenuHandler GetSingleton(Type handlerType) {
            if (!typeof(IMenuHandler).IsAssignableFrom(handlerType))
                throw new Exception($"{handlerType} does not implement IMenuHandler!");
            if (!HandlerSingletons.TryGetValue(handlerType, out IMenuHandler handler)) {
                handler = (IMenuHandler)Activator.CreateInstance(handlerType);
                HandlerSingletons[handlerType] = handler;
                }
            return handler;
            }

        internal static void Add(Type menuType, Type handlerType) {
            if (HandlerByType.ContainsKey(menuType))
                Log.Warn($"Redefining handler for {menuType}");
            Log.TraceIfD($"[{nameof(HandlerMapping)}] Mapping menu type {menuType.FullName} -> {handlerType.Name}");
            Add(menuType, GetSingleton(handlerType));
            }

        internal static void Add(string menuClass, Type handlerType) {
            if (HandlerByName.ContainsKey(menuClass))
                Log.Warn($"Redefining handler for {menuClass}");
            Log.TraceIfD($"[{nameof(HandlerMapping)}] Mapping menu name \"{menuClass}\" -> {handlerType.Name}");
            HandlerByName[menuClass] = GetSingleton(handlerType);
            }

        internal static void Add(Type menuType, IMenuHandler handler) {
            if (HandlerByType.ContainsKey(menuType))
                Log.Warn($"Redefining handler for {menuType}");
            HandlerByType[menuType] = handler;
            }

        internal static bool TryGetHandler(Type menuType, out IMenuHandler handler) {
            if (HandlerByType.TryGetValue(menuType, out handler)) {
                Log.TraceIfD($"[{nameof(HandlerMapping)}] Exact mapping {menuType.FullName} -> {handler.GetType().Name}");
                return true;
                }
            foreach (var kvp in HandlerByType) {
                if (menuType.IsSubclassOf(kvp.Key)) {
                    handler = kvp.Value;
                    Log.TraceIfD($"[{nameof(HandlerMapping)}] Base mapping {menuType.FullName} -> {handler.GetType().Name} (registered for {kvp.Key.FullName})");
                    return true;
                    }
                }
            Log.TraceIfD($"[{nameof(HandlerMapping)}] No mapping for {menuType.FullName}");
            return false;
            }

        /// <summary>Forgets the handler registered for exactly this menu type.</summary>
        /// <param name="menuType">The menu type to forget.</param>
        /// <returns>True if a mapping was removed.</returns>
        internal static bool Remove(Type menuType) {
            bool removed = menuType is not null && HandlerByType.Remove(menuType);
            Log.TraceIfD($"[{nameof(HandlerMapping)}] Remove mapping for {menuType?.FullName}: {(removed ? "removed" : "none")}");
            return removed;
            }

        internal static bool TryGetHandler(string menuClass, out IMenuHandler handler) => HandlerByName.TryGetValue(menuClass, out handler);
        }

    }
