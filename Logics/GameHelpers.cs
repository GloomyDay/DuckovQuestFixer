using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QuestFixer.Models;

namespace QuestFixer.Logics
{
    /// <summary>
    /// Helper functions for interacting with game systems (scenes, player level, etc.)
    /// </summary>
    public static class GameHelpers
    {
        // Gets all scene names from Unity's build settings using reflection
        // Results are cached in state.CachedSceneNames to avoid repeated calls
        // Returns: List of scene names (e.g., "Level_Farm_01", "Level_City_Main")
        public static List<string> GetAllSceneNames(ModState state)
        {
            if (state.CachedSceneNames != null) return state.CachedSceneNames;
            
            state.CachedSceneNames = new List<string>();
            try
            {
                var sceneManagerType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine.CoreModule") ??
                                       Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                
                if (sceneManagerType != null)
                {
                    var countProp = sceneManagerType.GetProperty("sceneCountInBuildSettings", BindingFlags.Static | BindingFlags.Public);
                    if (countProp != null)
                    {
                        int count = (int)countProp.GetValue(null);
                        
                        var sceneUtilType = Type.GetType("UnityEngine.SceneManagement.SceneUtility, UnityEngine.CoreModule") ??
                                            Type.GetType("UnityEngine.SceneManagement.SceneUtility, UnityEngine");
                        
                        if (sceneUtilType != null)
                        {
                            var getPathMethod = sceneUtilType.GetMethod("GetScenePathByBuildIndex", BindingFlags.Static | BindingFlags.Public);
                            if (getPathMethod != null)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    string path = getPathMethod.Invoke(null, new object[] { i })?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(path))
                                    {
                                        state.CachedSceneNames.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            
            return state.CachedSceneNames;
        }
        
        // Checks if a scene with given name exists in the game's build settings
        // Used to detect bugged quests that require non-existent scenes (e.g., renamed levels)
        // Returns: true if scene exists or if unable to verify, false if definitely doesn't exist
        public static bool SceneExistsInGame(ModState state, string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return true;
            
            var allScenes = GetAllSceneNames(state);
            if (allScenes.Count == 0) return true;
            
            return allScenes.Any(s => 
                s.Equals(sceneName, StringComparison.OrdinalIgnoreCase) ||
                s.Contains(sceneName) || 
                sceneName.Contains(s));
        }
        
        // Gets the name of the currently active scene in Unity
        // Used to check if player is on the required scene for a quest
        // Returns: Scene name string, or "?" if unable to determine
        public static string GetCurrentSceneName()
        {
            try
            {
                var sceneType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine.CoreModule") ??
                               Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                
                if (sceneType != null)
                {
                    var getActiveScene = sceneType.GetMethod("GetActiveScene", BindingFlags.Static | BindingFlags.Public);
                    if (getActiveScene != null)
                    {
                        var scene = getActiveScene.Invoke(null, null);
                        return scene?.GetType().GetProperty("name")?.GetValue(scene)?.ToString() ?? "?";
                    }
                }
            }
            catch { }
            return "?";
        }
        
        // Gets the player's current experience level from EXPManager
        // Used to check if player meets level requirements for quests
        // Returns: Player level as int, or -1 if unable to determine
        public static int GetPlayerLevel()
        {
            try
            {
                Type expType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(t => t.Name == "EXPManager");
                
                if (expType != null)
                {
                    var levelProp = expType.GetProperty("Level", BindingFlags.Static | BindingFlags.Public);
                    if (levelProp != null)
                        return (int)levelProp.GetValue(null);
                }
            }
            catch { }
            return -1;
        }

        // Searches for a quest by ID or name (partial match supported)
        // Input can be quest ID (e.g., "1504") or part of quest name
        // Returns: QuestInfo if found, null otherwise
        public static QuestInfo FindQuestByInput(ModState state, string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            
            return state.CachedQuests.FirstOrDefault(q => 
                q.Id.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                q.Id.Contains(input) ||
                (q.Name?.IndexOf(input, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
        }
    }
}
