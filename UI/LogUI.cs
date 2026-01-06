using System.Linq;
using QuestFixer.Logics;
using QuestFixer.Models;
using UnityEngine;

namespace QuestFixer.UI
{
    /// <summary>
    /// Log viewer tab - displays mod activity log in reverse chronological order
    /// </summary>
    public static class LogUI
    {
        // Draws the log tab with scrollable message list and clear button
        // Shows newest messages at top (reversed order)
        // Returns: void
        public static void Draw(ModState state)
        {
            GUILayout.Label($"=== Log ({state.LogMessages.Count} entries) ===");
            
            // Scrollable log area
            state.LogScrollPosition = GUILayout.BeginScrollView(state.LogScrollPosition, GUILayout.Height(420));
            
            // Show messages newest first
            foreach (var msg in state.LogMessages.AsEnumerable().Reverse())
            {
                GUILayout.Label(msg, new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true });
            }
            
            GUILayout.EndScrollView();

            // Clear log button
            if (GUILayout.Button("Clear Log", GUILayout.Height(25)))
            {
                state.LogMessages.Clear();
                ModLogger.Log(state, "Log cleared");
            }
        }
    }
}
