using System;
using System.Linq;
using System.Reflection;
using QuestFixer.Logics;
using QuestFixer.Models;
using UnityEngine;

namespace QuestFixer.UI
{
    /// <summary>
    /// Quest list tab - shows all quests with search, complete and reset buttons
    /// </summary>
    public static class QuestListUI
    {
        // Draws the quest list tab with search box and action buttons
        // Supports filtering by name/ID and showing first 100 results
        // Buttons: "Complete" = complete quest tasks, "Activate" = activate/reset quest
        // Automatically loads runtime quest statuses from QuestManager
        // Returns: void
        public static void Draw(ModState state)
        {
            // Load runtime quest statuses if QuestManager is available
            if (state.QuestManager != null && (state.ActiveQuestIds.Count == 0 && state.CompletedQuestIds.Count == 0))
            {
                QuestTreeBuilder.LoadQuestStatuses(state);
            }
            
            // Action by quest ID section
            GUILayout.Label("Action by Quest ID:");
            GUILayout.BeginHorizontal();
            state.QuestIdInput = GUILayout.TextField(state.QuestIdInput, GUILayout.Width(250));
            
            // Complete button - finishes all tasks
            if (GUILayout.Button("Complete", GUILayout.Width(100)))
            {
                var quest = GameHelpers.FindQuestByInput(state, state.QuestIdInput);
                if (quest != null) QuestActions.CompleteQuest(state, quest);
                else ModLogger.Log(state, $"Quest '{state.QuestIdInput}' not found");
            }
            
            // Reset/Activate button - reverts quest to initial state or activates it
            if (GUILayout.Button("Activate", GUILayout.Width(100)))
            {
                var quest = GameHelpers.FindQuestByInput(state, state.QuestIdInput);
                if (quest != null) QuestActions.ResetQuest(state, quest);
                else ModLogger.Log(state, $"Quest '{state.QuestIdInput}' not found");
            }
            GUILayout.EndHorizontal();

            // Search filter
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(50));
            state.SearchFilter = GUILayout.TextField(state.SearchFilter);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Apply filter to quest list
            var filtered = string.IsNullOrWhiteSpace(state.SearchFilter) 
                ? state.CachedQuests 
                : state.CachedQuests.Where(q => 
                    (q.Name?.IndexOf(state.SearchFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (q.Id?.IndexOf(state.SearchFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0).ToList();

            GUILayout.Label($"Quests ({filtered.Count} of {state.CachedQuests.Count}):");

            // Scrollable quest list - shows ALL filtered quests
            // Height 380px fits within 600px window accounting for header, tabs, and controls
            state.ScrollPosition = GUILayout.BeginScrollView(state.ScrollPosition, 
                GUILayout.Height(380), GUILayout.ExpandWidth(true));
            
            // Draw all filtered quests (no limit)
            foreach (var quest in filtered)
            {
                DrawQuestRow(state, quest);
            }
            
            // Bottom padding
            GUILayout.Space(20);
            
            GUILayout.EndScrollView();
        }
        
        // Draws a single quest row with status icon and action buttons
        // Shows: status icon, quest name, ID, runtime status, complete/activate buttons
        // Uses runtime quest status from QuestManager (ActiveQuests/HistoryQuests)
        // Returns: void
        private static void DrawQuestRow(ModState state, QuestInfo quest)
        {
            // Parse quest ID
            if (!int.TryParse(quest.Id, out int questId))
            {
                // Fallback: try to get ID from raw quest object
                try
                {
                    var idProp = quest.QuestType.GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
                    questId = idProp != null && idProp.GetValue(quest.RawQuest) is int id ? id : -1;
                }
                catch
                {
                    questId = -1;
                }
            }
            
            // Get runtime status (from QuestManager, not template)
            bool isCompleted = questId > 0 && state.CompletedQuestIds.Contains(questId);
            bool isActive = questId > 0 && state.ActiveQuestIds.Contains(questId);
            
            string statusText;
            Color statusColor;
            string statusIcon;
            
            if (isCompleted)
            {
                statusText = "Completed";
                statusColor = Color.green;
                statusIcon = "[v]";
            }
            else if (isActive)
            {
                statusText = "In Progress";
                statusColor = new Color(0.25f, 0.5f, 0.85f); // Blue
                statusIcon = "[*]";
            }
            else
            {
                statusText = "Not Started";
                statusColor = new Color(0.5f, 0.5f, 0.5f); // Gray
                statusIcon = "[ ]";
            }
            
            GUILayout.BeginHorizontal(GUI.skin.box);
            
            // Status icon
            GUI.color = statusColor;
            GUILayout.Label(statusIcon, GUILayout.Width(30));
            GUI.color = Color.white;
            
            // Quest info
            GUILayout.BeginVertical();
            GUILayout.Label($"{quest.Name}", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.Label($"ID: {quest.Id} | {statusText}", new GUIStyle(GUI.skin.label) { fontSize = 11 });
            GUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            
            // Action buttons with descriptive names
            if (GUILayout.Button("Complete", GUILayout.Width(90), GUILayout.Height(40)))
            {
                QuestActions.CompleteQuest(state, quest);
            }
            if (GUILayout.Button("Activate", GUILayout.Width(100), GUILayout.Height(40)))
            {
                QuestActions.ResetQuest(state, quest);
            }
            
            GUILayout.EndHorizontal();
        }
    }
}
