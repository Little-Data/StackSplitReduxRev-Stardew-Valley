using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using StardewModdingAPI;
using System;

namespace StackSplitRedux.MenuHandlers
    {
    /// <summary>Represents states of input being handled.</summary>
    public enum EInputHandled
        {
        /// <summary>Input should be forwarded and the split menu should be closed.</summary>
        NotHandled,
        /// <summary>Input should be forwarded but the split menu should remain open.</summary>
        Handled,
        /// <summary>Input consumed by the handler, don't propogate it.</summary>
        Consumed
        }

    public interface IMenuHandler
        {
        /// <summary>Checks the menu is the correct type.</summary>
        bool IsCorrectMenuType(IClickableMenu menu);

        /// <summary>Whether the handler threw while driving the last menu it was given.</summary>
        /// <remarks>
        /// A handler that fails on a menu keeps failing on it, so the caller drops that menu type for the rest of the
        /// session instead of writing an error to the log on every single click.
        /// </remarks>
        bool Faulted { get; }

        /// <summary>The menu type that caused <see cref="Faulted"/>, or <c>null</c> if unknown.</summary>
        Type FaultedMenuType { get; }

        /// <summary>Clears <see cref="Faulted"/> so the handler can be used for other menus again.</summary>
        void ClearFault();

        /// <summary>Notifies the handler that its native menu has been opened.</summary>
        /// <param name="menu">The menu that was opened.</param>
        void Open(IClickableMenu menu);

        /// <summary>Notifies the handler that its native menu was closed.</summary>
        void Close();

        /// <summary>Runs on tick for handling things like highlighting text.</summary>
        void Update();

        /// <summary>Draws the split menu.</summary>
        void Draw(SpriteBatch spriteBatch);

        /// <summary>Tells the handler to close the split menu.</summary>
        void CloseSplitMenu();

        /// <summary>Handle user input.</summary>
        /// <param name="button">The pressed button.</param>
        EInputHandled HandleInput(SButton button);
        }
    }
