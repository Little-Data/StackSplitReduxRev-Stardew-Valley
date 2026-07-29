using Microsoft.Xna.Framework;

namespace StackSplitRedux.MenuHandlers
    {
    public interface IShopAction
        {
        /// <summary>Gets the size of the stack the action is acting on.</summary>
        int StackAmount { get; }

        /// <summary>Verifies the conditions to perform the action.</summary>
        bool CanPerformAction();

        /// <summary>Does the action.</summary>
        /// <param name="amount">Number of items.</param>
        /// <param name="clickLocation">Where the player clicked.</param>
        void PerformAction(int amount, Point clickLocation);
        }
    }
