using QuestFixer.Models;
using UnityEngine;

namespace QuestFixer.UI
{
    /// <summary>
    /// Main window content - handles tab switching and status display
    /// </summary>
    public static class MainWindow
    {
        // Draws the main content area with status bar and tab content
        // Delegates to appropriate UI class based on selected tab
        // Returns: void
        public static void Draw(ModState state, int windowId)
        {
            GUILayout.BeginVertical();

            // Tab bar (status is already shown in ModBehaviour.DrawWindow)
            state.CurrentTab = GUILayout.Toolbar(state.CurrentTab, state.TabNames, GUILayout.Height(30));
            GUILayout.Space(5);

            // Render selected tab content
            switch (state.CurrentTab)
            {
                case 0: QuestListUI.Draw(state); break;    // Quest list with complete/reset
                case 1: QuestTreeUI.Draw(state); break;    // Visual quest tree
                case 2: ApiViewerUI.Draw(state); break;    // API inspector for debugging
                case 3: LogUI.Draw(state); break;          // In-game log viewer
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }
    }
}
