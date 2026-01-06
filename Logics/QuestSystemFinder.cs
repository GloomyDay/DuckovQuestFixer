using System;
using System.Linq;
using System.Reflection;
using QuestFixer.Models;
using UnityEngine;

namespace QuestFixer.Logics
{
    /// <summary>
    /// Discovers and analyzes game's quest management systems via reflection
    /// </summary>
    public static class QuestSystemFinder
    {
        // Main entry point - finds QuestManager and QuestCollection singletons
        // Uses reflection to locate Duckov.Quests.* types in loaded assemblies
        // Also triggers initial quest loading if successful
        // Returns: void (populates state with found systems)
        public static void FindQuestSystems(ModState state, Func<Type, UnityEngine.Object> findObjectOfType)
        {
            try
            {
                // QuestManager - main runtime quest controller
                state.QuestManagerType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(t => t.FullName == "Duckov.Quests.QuestManager");
                
                if (state.QuestManagerType != null)
                {
                    ModLogger.Log(state, $"Found QuestManager: {state.QuestManagerType.FullName}");
                    state.QuestManager = state.QuestManagerType.GetProperty("Instance", 
                        BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                    
                    if (state.QuestManager != null)
                    {
                        ModLogger.Log(state, $"QuestManager.Instance found!");
                        AnalyzeQuestManagerMethods(state);
                    }
                    else
                    {
                        ModLogger.Log(state, "QuestManager.Instance = null");
                    }
                }
                
                // QuestCollection - ScriptableObject containing all quest definitions
                state.QuestCollectionType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(t => t.FullName == "Duckov.Quests.QuestCollection");
                
                if (state.QuestCollectionType != null)
                {
                    ModLogger.Log(state, $"Found QuestCollection: {state.QuestCollectionType.FullName}");
                    
                    var instanceProp = state.QuestCollectionType.GetProperty("Instance", 
                        BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        state.QuestCollection = instanceProp.GetValue(null);
                        if (state.QuestCollection != null)
                        {
                            ModLogger.Log(state, $"QuestCollection.Instance found!");
                            AnalyzeQuestCollectionMethods(state);
                        }
                    }
                    
                    // Fallback: try FindObjectOfType if Instance property didn't work
                    if (state.QuestCollection == null && typeof(UnityEngine.Object).IsAssignableFrom(state.QuestCollectionType))
                    {
                        var found = findObjectOfType(state.QuestCollectionType);
                        if (found != null)
                        {
                            state.QuestCollection = found;
                            ModLogger.Log(state, $"QuestCollection found via FindObjectOfType!");
                            AnalyzeQuestCollectionMethods(state);
                        }
                    }
                }
                
                if (state.QuestCollection != null)
                {
                    QuestLoader.LoadQuests(state);
                }
                else
                {
                    ModLogger.Log(state, "QuestCollection not found!");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log(state, $"Search error: {ex.Message}");
            }
        }
        
        // Extracts all methods from QuestManager type for API viewer tab
        // Filters out Unity base class methods (MonoBehaviour, etc.)
        // Returns: void (populates state.QuestManagerMethods)
        private static void AnalyzeQuestManagerMethods(ModState state)
        {
            if (state.QuestManagerType == null) return;
            
            state.QuestManagerMethods.Clear();
            
            var methods = state.QuestManagerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (IsUnityBaseType(method.DeclaringType)) continue;
                
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                var access = method.IsPublic ? "public" : "private";
                string methodStr = $"[{access}] {method.ReturnType.Name} {method.Name}({parameters})";
                state.QuestManagerMethods.Add(methodStr);
                
                // Log methods related to quest availability
                string nameLower = method.Name.ToLower();
                if (nameLower.Contains("available") || nameLower.Contains("can") || nameLower.Contains("meets") || 
                    nameLower.Contains("start") || nameLower.Contains("prerequisit") || nameLower.Contains("unlock"))
                {
                    ModLogger.Log(state, $"[QM] {methodStr}");
                }
            }
            
            // Also log properties
            var props = state.QuestManagerType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (IsUnityBaseType(prop.DeclaringType)) continue;
                string propLower = prop.Name.ToLower();
                if (propLower.Contains("available") || propLower.Contains("active") || propLower.Contains("history"))
                {
                    ModLogger.Log(state, $"[QM prop] {prop.PropertyType.Name} {prop.Name}");
                }
            }
        }

        // Extracts all methods from QuestCollection type
        // Stores MethodInfo objects for later invocation (e.g., GetAll, ToList)
        // Returns: void (populates state.QuestCollectionMethods)
        private static void AnalyzeQuestCollectionMethods(ModState state)
        {
            if (state.QuestCollectionType == null) return;
            
            state.QuestCollectionMethods.Clear();
            var methods = state.QuestCollectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var method in methods)
            {
                if (IsUnityBaseType(method.DeclaringType)) continue;
                state.QuestCollectionMethods.Add(method);
            }
        }

        // Analyzes a Quest type - extracts all methods, properties, and fields
        // Used to populate API viewer tab with quest class structure
        // Returns: void (populates state.QuestMethods, QuestProperties, QuestFields)
        public static void AnalyzeQuestType(ModState state, Type type)
        {
            state.QuestMethods.Clear();
            state.QuestProperties.Clear();
            state.QuestFields.Clear();
            
            ModLogger.Log(state, $"=== Analyzing Quest type: {type.FullName} ===");
            
            // Collect all methods
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (IsUnityBaseType(method.DeclaringType)) continue;
                
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                var access = method.IsPublic ? "public" : "private";
                string methodStr = $"[{access}] {method.ReturnType.Name} {method.Name}({parameters})";
                state.QuestMethods.Add(methodStr);
            }
            
            // Collect all properties
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (IsUnityBaseType(prop.DeclaringType)) continue;
                
                var canRead = prop.CanRead ? "get" : "";
                var canWrite = prop.CanWrite ? "set" : "";
                var access = canRead + (canRead != "" && canWrite != "" ? "/" : "") + canWrite;
                string propStr = $"{prop.PropertyType.Name} {prop.Name} {{ {access} }}";
                state.QuestProperties.Add(propStr);
            }
            
            // Collect all fields
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (IsUnityBaseType(field.DeclaringType)) continue;
                
                var access = field.IsPublic ? "public" : "private";
                state.QuestFields.Add($"[{access}] {field.FieldType.Name} {field.Name}");
            }
            
            // Log summary
            ModLogger.Log(state, $"Quest API: {state.QuestMethods.Count} methods, {state.QuestProperties.Count} properties, {state.QuestFields.Count} fields");
            
            // Log ALL methods to log
            ModLogger.Log(state, "=== ALL QUEST METHODS ===");
            foreach (var m in state.QuestMethods) ModLogger.Log(state, $"  {m}");
            
            // Log ALL properties to log
            ModLogger.Log(state, "=== ALL QUEST PROPERTIES ===");
            foreach (var p in state.QuestProperties) ModLogger.Log(state, $"  {p}");
        }
        
        // Checks if a type is a Unity base class that should be filtered out
        // Used to exclude inherited methods like MonoBehaviour.Update, Component.transform, etc.
        // Returns: true if type is object/MonoBehaviour/Component/etc.
        public static bool IsUnityBaseType(Type type)
        {
            return type == typeof(object) || 
                   type == typeof(MonoBehaviour) ||
                   type == typeof(Component) ||
                   type == typeof(Behaviour) ||
                   type == typeof(UnityEngine.Object) ||
                   type == typeof(ScriptableObject);
        }
    }
}
