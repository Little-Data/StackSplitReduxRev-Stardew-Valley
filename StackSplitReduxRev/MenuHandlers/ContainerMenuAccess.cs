using System;
using System.Reflection;
using StardewValley;
using StardewValley.Menus;

namespace StackSplitRedux.MenuHandlers
    {
    /// <summary>How a container-like menu moves items out of its container pane.</summary>
    internal enum EContainerStrategy
        {
        /// <summary>The menu shape was not recognised.</summary>
        None,

        /// <summary>Only the player inventory pane can be handled.</summary>
        PlayerOnly,

        /// <summary>The menu exposes <c>TryToGrab(Item, int)</c> and moves items straight into the player inventory.</summary>
        DirectTransfer,

        /// <summary>The menu moves items through its <c>heldItem</c> (the vanilla chest contract).</summary>
        HeldItem
        }

    /// <summary>
    /// Reflection based description of a menu that owns an item container pane.
    /// </summary>
    /// <remarks>
    /// <para>Vanilla has three container menus: <see cref="ItemGrabMenu"/> (handled by
    /// <see cref="ItemGrabMenuHandler"/>), <see cref="QuestContainerMenu"/> (special order drop boxes such as Gus's
    /// fridge) and <see cref="StorageContainer"/> (the fair grange display). Mods can add their own menus with the same
    /// shape, so every member is looked up defensively instead of casting to a concrete type.</para>
    /// <para>Nothing in here throws: an unexpected menu simply yields <see cref="EContainerStrategy.None"/> so the
    /// caller can fall back to a simpler handler or to vanilla behavior.</para>
    /// </remarks>
    internal sealed class ContainerMenuAccess
        {
        private const BindingFlags MEMBER_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Names of fields that hold an item-change callback installed by the menu owner.</summary>
        private static readonly string[] ItemChangedCallbackNames = { "itemChangeBehavior", "onItemChanged", "onItemChange", "itemChanged", "onChange" };

        /// <summary>The menu this description was built for.</summary>
        public IClickableMenu Menu { get; private set; }

        /// <summary>The container pane (the <c>ItemsToGrabMenu</c> member), or <c>null</c>.</summary>
        public InventoryMenu ContainerInventory { get; private set; }

        /// <summary>The player inventory pane (the <c>inventory</c> member), or <c>null</c>.</summary>
        public InventoryMenu PlayerInventory { get; private set; }

        /// <summary>Reads the menu's hovered item, or <c>null</c> if the menu does not expose one.</summary>
        public Func<Item> GetHoveredItem { get; private set; }

        /// <summary>Reads the menu's held item, or <c>null</c> if the menu does not expose one.</summary>
        public Func<Item> GetHeldItem { get; private set; }

        /// <summary>Writes the menu's held item, or <c>null</c> if the menu does not allow it.</summary>
        public Action<Item> SetHeldItem { get; private set; }

        /// <summary>The menu's own <c>TryToGrab(Item, int)</c> method, or <c>null</c>.</summary>
        public MethodInfo TryToGrabMethod { get; private set; }

        /// <summary>The menu's own <c>TryToPlace(Item, int)</c> method, or <c>null</c>.</summary>
        public MethodInfo TryToPlaceMethod { get; private set; }

        /// <summary>How items can be moved out of the container pane.</summary>
        public EContainerStrategy Strategy { get; private set; } = EContainerStrategy.None;

        /// <summary>Whether the player inventory pane of this menu can be split safely.</summary>
        public bool SupportsPlayerInventory { get; private set; }

        /// <summary>
        /// Whether stacks of the player inventory pane can be put into the container through the menu's own
        /// <c>TryToPlace</c> method (special order drop boxes).
        /// </summary>
        public bool SupportsPlayerPlacement { get; private set; }

        /// <summary>
        /// Whether items of the container pane can be split into the menu's own <c>heldItem</c> (the visible cursor)
        /// instead of being transferred straight into the player inventory.
        /// </summary>
        public bool SupportsHeldItemTransfer { get; private set; }

        /// <summary>Whether the menu is based on the game's own <see cref="MenuWithInventory"/>.</summary>
        public bool IsMenuWithInventory { get; private set; }

        /// <summary>The menu's own "something changed" callback, or <c>null</c>.</summary>
        private Delegate ItemChangedCallback;

        /// <summary>Inspects a menu without side effects.</summary>
        /// <param name="menu">The menu to inspect.</param>
        /// <param name="access">The resulting description when the menu is recognised.</param>
        /// <returns>True if the menu has a container-like shape.</returns>
        public static bool TryCreate(IClickableMenu menu, out ContainerMenuAccess access) {
            access = null;
            if (menu is null) return false;

            try {
                Type type = menu.GetType();
                InventoryMenu container = GetInventoryMenu(menu, type, "ItemsToGrabMenu");
                InventoryMenu player = GetInventoryMenu(menu, type, "inventory");
                if (container is null && player is null) return false;

                var result = new ContainerMenuAccess {
                    Menu = menu,
                    ContainerInventory = container,
                    PlayerInventory = player,
                    GetHoveredItem = GetItemGetter(menu, type, "hoveredItem"),
                    IsMenuWithInventory = typeof(MenuWithInventory).IsAssignableFrom(type),
                    };

                if (TryGetHeldItemAccessors(menu, out Func<Item> heldGetter, out Action<Item> heldSetter)) {
                    result.GetHeldItem = heldGetter;
                    result.SetHeldItem = heldSetter;
                    }

                if (container is null) {
                    // No container pane at all: at most the player inventory can be split. Only trust menus built on the
                    // game's own MenuWithInventory, because those are guaranteed to use the heldItem cursor contract;
                    // guessing on an arbitrary third party menu could park items somewhere it never draws them.
                    if (!result.IsMenuWithInventory) {
                        Log.TraceIfD(
                            $"[{nameof(ContainerMenuAccess)}] {type.FullName} has no container pane and is not a " +
                            "MenuWithInventory; not handling it"
                            );
                        access = null;
                        return false;
                        }
                    result.Strategy = EContainerStrategy.PlayerOnly;
                    }
                else if (FindItemMethod(type, "TryToGrab", typeof(Item), typeof(int)) is MethodInfo tryToGrab) {
                    // QuestContainerMenu and look-alikes move items with their own API.
                    result.TryToGrabMethod = tryToGrab;
                    result.TryToPlaceMethod = FindItemMethod(type, "TryToPlace", typeof(Item), typeof(int));
                    result.ItemChangedCallback = FindItemChangedCallback(menu, type);
                    result.Strategy = EContainerStrategy.DirectTransfer;
                    }
                else if (result.GetHeldItem is not null && result.SetHeldItem is not null && !HasItemChangedCallback(menu, type)) {
                    result.Strategy = EContainerStrategy.HeldItem;
                    }
                else {
                    // Either the held-item contract is missing, or the menu tracks changes in a callback that we would
                    // bypass (the grange display does). Playing it safe is better than corrupting the menu's state.
                    result.Strategy = EContainerStrategy.PlayerOnly;
                    }

                // A DirectTransfer menu that is also a MenuWithInventory (QuestContainerMenu is) draws a cursor and can
                // hand items back through its own TryToPlace. In that case split stacks go to the cursor like they do
                // in a chest, instead of being dropped straight into the backpack. The menu's change callback is
                // invoked afterwards so its own bookkeeping (donation counts) stays correct.
                result.SupportsHeldItemTransfer =
                    result.Strategy == EContainerStrategy.DirectTransfer
                    && result.IsMenuWithInventory
                    && result.TryToPlaceMethod is not null
                    && result.GetHeldItem is not null
                    && result.SetHeldItem is not null
                    && result.GetHoveredItem is not null
                    && IsHeldItemSafeToPark(result);

                // Splitting the player pane is only meaningful when it really is the player's own inventory, and when
                // the split-off items can be parked on a cursor the menu actually draws.
                result.SupportsPlayerInventory =
                    (result.Strategy == EContainerStrategy.PlayerOnly
                        || result.Strategy == EContainerStrategy.HeldItem
                        || result.SupportsHeldItemTransfer)
                    && player is not null
                    && result.GetHeldItem is not null
                    && result.SetHeldItem is not null
                    && result.GetHoveredItem is not null
                    && IsPlayerOwnedInventory(player)
                    && IsHeldItemSafeToPark(result);

                // Menus without a usable cursor can still take a partial stack from the player inventory through
                // their own API; the menu itself keeps its bookkeeping in sync.
                result.SupportsPlayerPlacement =
                    result.Strategy == EContainerStrategy.DirectTransfer
                    && !result.SupportsHeldItemTransfer
                    && result.TryToPlaceMethod is not null
                    && player is not null
                    && result.GetHoveredItem is not null
                    && IsPlayerOwnedInventory(player);

                // Nothing safe to drive: let the caller try a simpler handler or vanilla behavior.
                if (result.Strategy != EContainerStrategy.DirectTransfer
                    && result.Strategy != EContainerStrategy.HeldItem
                    && !result.SupportsPlayerInventory
                    && !result.SupportsPlayerPlacement) {
                    Log.TraceIfD(
                        $"[{nameof(ContainerMenuAccess)}] {type.FullName}: nothing safe to drive " +
                        $"(strategy={result.Strategy}, playerInventory={result.SupportsPlayerInventory}, " +
                        $"placement={result.SupportsPlayerPlacement}); leaving it to vanilla"
                        );
                    access = null;
                    return false;
                    }

                Log.TraceIfD(
                    $"[{nameof(ContainerMenuAccess)}] {type.FullName}: " +
                    $"container={DescribeInventory(container)}, player={DescribeInventory(player)}, " +
                    $"playerOwned={IsPlayerOwnedInventory(player)}, " +
                    $"heldItem={result.GetHeldItem is not null && result.SetHeldItem is not null}, " +
                    $"hoveredItem={result.GetHoveredItem is not null}, " +
                    $"TryToGrab={result.TryToGrabMethod is not null}, TryToPlace={result.TryToPlaceMethod is not null}, " +
                    $"changeCallback={result.ItemChangedCallback is not null}, menuWithInventory={result.IsMenuWithInventory} " +
                    $"=> strategy={result.Strategy}, supportsPlayerInventory={result.SupportsPlayerInventory}, " +
                    $"supportsPlacement={result.SupportsPlayerPlacement}, supportsHeldItem={result.SupportsHeldItemTransfer}"
                    );

                access = result;
                return true;
                }
            catch (Exception e) {
                Log.WarnAlways($"[{nameof(ContainerMenuAccess)}] Could not inspect menu {menu.GetType().FullName}: {e.Message}");
                access = null;
                return false;
                }
            }

        /// <summary>Notifies the menu that its container contents changed.</summary>
        /// <remarks>The callback is the menu's own; invoking it after a manual move keeps its bookkeeping in sync.</remarks>
        public void NotifyItemChanged() {
            if (this.ItemChangedCallback is null) return;

            try {
                Log.TraceIfD($"[{nameof(ContainerMenuAccess)}] Invoking the menu's change callback on {this.Menu?.GetType().Name}");
                this.ItemChangedCallback.DynamicInvoke();
                }
            catch (Exception e) {
                Log.WarnAlways(
                    $"[{nameof(ContainerMenuAccess)}] The change callback of {this.Menu?.GetType().FullName ?? "a menu"} " +
                    $"failed: {e.Message}"
                    );
                }
            }

        /// <summary>Checks whether a menu shows the player's real inventory.</summary>
        /// <param name="menu">The menu to inspect.</param>
        /// <returns>True if the menu has an <c>inventory</c> member pointing at the player's item list.</returns>
        public static bool HasPlayerInventory(IClickableMenu menu) {
            try {
                if (menu is null) return false;
                InventoryMenu inventory = GetInventoryMenu(menu, menu.GetType(), "inventory");
                return inventory is not null && IsPlayerOwnedInventory(inventory);
                }
            catch {
                return false;
                }
            }

        /// <summary>Finds the <c>heldItem</c> accessors of a menu, if it has them.</summary>
        /// <param name="menu">The menu to inspect.</param>
        /// <param name="getter">Reads the held item.</param>
        /// <param name="setter">Writes the held item.</param>
        /// <returns>True if both accessors were found.</returns>
        public static bool TryGetHeldItemAccessors(IClickableMenu menu, out Func<Item> getter, out Action<Item> setter) {
            getter = null;
            setter = null;
            if (menu is null) return false;

            try {
                MemberInfo member = FindMember(menu.GetType(), "heldItem");
                if (member is PropertyInfo property) {
                    if (property.PropertyType != typeof(Item) || !property.CanRead || !property.CanWrite
                        || property.GetIndexParameters().Length != 0)
                        return false;

                    getter = () => ReadItem(() => property.GetValue(menu));
                    setter = value => WriteItem(() => property.SetValue(menu, value));
                    return true;
                    }

                if (member is FieldInfo field) {
                    if (field.FieldType != typeof(Item) || field.IsInitOnly || field.IsLiteral)
                        return false;

                    getter = () => ReadItem(() => field.GetValue(menu));
                    setter = value => WriteItem(() => field.SetValue(menu, value));
                    return true;
                    }
                }
            catch {
                // Unreadable member: treat the menu as having no held item.
                }

            getter = null;
            setter = null;
            return false;
            }

        /// <summary>Short description of an inventory pane for debug logs.</summary>
        private static string DescribeInventory(InventoryMenu inventory) {
            if (inventory is null) return "none";
            try {
                return $"{inventory.actualInventory?.Count ?? 0} slots";
                }
            catch {
                return "unknown";
                }
            }

        private static bool IsPlayerOwnedInventory(InventoryMenu inventory) {
            try {
                return inventory is not null
                    && Game1.player is not null
                    && ReferenceEquals(inventory.actualInventory, Game1.player.Items);
                }
            catch {
                return false;
                }
            }

        /// <summary>
        /// Checks that the menu will give a held stack back when it is closed.
        /// </summary>
        /// <param name="access">The menu description.</param>
        /// <returns>False for menus that deliberately discard their held item on exit.</returns>
        private static bool IsHeldItemSafeToPark(ContainerMenuAccess access) {
            try {
                if (access?.Menu is null) return false;

                // Non-MenuWithInventory menus have no exit contract we can inspect; their own cleanup is trusted (the
                // same assumption the vanilla chest contract relies on).
                if (!access.IsMenuWithInventory) return true;

                object value = GetMemberValue(access.Menu, access.Menu.GetType(), "HeldItemExitBehavior");
                return value is not ItemExitBehavior behavior || behavior != ItemExitBehavior.Discard;
                }
            catch {
                // Unknown member: fall back to the game default, which returns held items to the player.
                return true;
                }
            }

        private static Item ReadItem(Func<object> read) {
            try {
                return read() as Item;
                }
            catch {
                return null;
                }
            }

        private static void WriteItem(Action write) {
            try {
                write();
                }
            catch {
                // A menu that refuses the assignment keeps its previous state; the caller verifies the result.
                }
            }

        private static MemberInfo FindMember(Type type, string name) {
            for (Type current = type; current is not null && current != typeof(object); current = current.BaseType) {
                foreach (MemberInfo member in current.GetMember(name, MEMBER_FLAGS)) {
                    if (member.MemberType is MemberTypes.Field or MemberTypes.Property)
                        return member;
                    }
                }
            return null;
            }

        private static InventoryMenu GetInventoryMenu(IClickableMenu menu, Type type, string name) {
            try {
                MemberInfo member = FindMember(type, name);
                if (member is FieldInfo field) {
                    return typeof(InventoryMenu).IsAssignableFrom(field.FieldType)
                        ? field.GetValue(menu) as InventoryMenu
                        : null;
                    }

                if (member is PropertyInfo property
                    && typeof(InventoryMenu).IsAssignableFrom(property.PropertyType)
                    && property.CanRead
                    && property.GetIndexParameters().Length == 0) {
                    return property.GetValue(menu) as InventoryMenu;
                    }
                }
            catch {
                // Unreadable member: treat the menu as not having that pane.
                }

            return null;
            }

        private static Func<Item> GetItemGetter(IClickableMenu menu, Type type, string name) {
            try {
                MemberInfo member = FindMember(type, name);
                if (member is FieldInfo field && field.FieldType == typeof(Item))
                    return () => ReadItem(() => field.GetValue(menu));

                if (member is PropertyInfo property
                    && property.PropertyType == typeof(Item)
                    && property.CanRead
                    && property.GetIndexParameters().Length == 0) {
                    return () => ReadItem(() => property.GetValue(menu));
                    }
                }
            catch {
                // Unreadable member: treat the menu as not exposing a hovered item.
                }

            return null;
            }

        private static MethodInfo FindItemMethod(Type type, string name, params Type[] parameterTypes) {
            for (Type current = type; current is not null && current != typeof(object); current = current.BaseType) {
                MethodInfo method = current.GetMethod(name, MEMBER_FLAGS, null, parameterTypes, null);
                if (method is not null && !method.IsStatic && method.ReturnType == typeof(Item))
                    return method;
                }
            return null;
            }

        private static bool HasItemChangedCallback(IClickableMenu menu, Type type) {
            return FindItemChangedCallback(menu, type) is not null;
            }

        private static Delegate FindItemChangedCallback(IClickableMenu menu, Type type) {
            foreach (string name in ItemChangedCallbackNames) {
                if (GetMemberValue(menu, type, name) is Delegate callback)
                    return callback;
                }
            return null;
            }

        private static object GetMemberValue(IClickableMenu menu, Type type, string name) {
            try {
                MemberInfo member = FindMember(type, name);
                if (member is FieldInfo field)
                    return field.GetValue(menu);
                if (member is PropertyInfo property && property.CanRead && property.GetIndexParameters().Length == 0)
                    return property.GetValue(menu);
                }
            catch {
                // Ignore: a member that cannot be read cannot tell us anything.
                }

            return null;
            }
        }
    }
