using QuestFixer.Models;
using UnityEngine;

namespace QuestFixer.UI
{
    /// <summary>
    /// API viewer tab - shows discovered game API for debugging
    /// Displays QuestManager and Quest type methods, properties, fields
    /// </summary>
    public static class ApiViewerUI
    {
        // Draws the API inspection tab with QuestManager and Quest class info
        // Useful for modders to understand available game APIs
        // Returns: void
        public static void Draw(ModState state)
        {
            state.ScrollPosition2 = GUILayout.BeginScrollView(state.ScrollPosition2, GUILayout.Height(420));
            
            // QuestManager section
            DrawQuestManagerSection(state);
            
            GUILayout.Space(10);
            
            // Quest type section
            DrawQuestTypeSection(state);

            GUILayout.EndScrollView();
        }
        
        // Draws QuestManager API information
        // Shows all discovered methods from QuestManager class
        // Returns: void
        private static void DrawQuestManagerSection(ModState state)
        {
            GUI.color = Color.green;
            GUILayout.Label($"=== QuestManager ({(state.QuestManager != null ? "found" : "NOT FOUND")}) ===", 
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 });
            GUI.color = Color.white;
            
            foreach (var method in state.QuestManagerMethods)
            {
                GUILayout.Label(method, new GUIStyle(GUI.skin.label) { fontSize = 10 });
            }
        }
        
        // Draws Quest type API information
        // Shows methods, properties and fields discovered via reflection
        // Returns: void
        private static void DrawQuestTypeSection(ModState state)
        {
            GUI.color = Color.cyan;
            GUILayout.Label($"=== Quest API ({state.QuestType?.Name ?? "not loaded"}) ===", 
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 });
            GUI.color = Color.white;

            // Methods section
            GUILayout.Label($"--- METHODS ({state.QuestMethods.Count}) ---", 
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 });
            foreach (var method in state.QuestMethods)
            {
                GUILayout.Label(method, new GUIStyle(GUI.skin.label) { fontSize = 10 });
            }

            GUILayout.Space(5);

            // Properties section
            GUI.color = Color.yellow;
            GUILayout.Label($"--- PROPERTIES ({state.QuestProperties.Count}) ---", 
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 });
            GUI.color = Color.white;
            foreach (var prop in state.QuestProperties)
            {
                GUILayout.Label(prop, new GUIStyle(GUI.skin.label) { fontSize = 10 });
            }

            GUILayout.Space(5);

            // Fields section
            GUI.color = Color.magenta;
            GUILayout.Label($"--- FIELDS ({state.QuestFields.Count}) ---", 
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 });
            GUI.color = Color.white;
            foreach (var field in state.QuestFields)
            {
                GUILayout.Label(field, new GUIStyle(GUI.skin.label) { fontSize = 10 });
            }
        }
    }
}
