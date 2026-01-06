using System;
using QuestFixer.Models;
using UnityEngine;

namespace QuestFixer.Logics
{
    /// <summary>
    /// Handles logging for the mod - both to Unity console and in-game log tab
    /// </summary>
    public static class ModLogger
    {
        // Logs a message to Unity Debug.Log and stores it in ModState for UI display
        // Also adds timestamp and limits log history to 200 entries
        // Returns: void
        public static void Log(ModState state, string message)
        {
            Debug.Log($"[QuestFixer] {message}");
            state.LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (state.LogMessages.Count > 200) 
                state.LogMessages.RemoveAt(0);
        }
    }
}
