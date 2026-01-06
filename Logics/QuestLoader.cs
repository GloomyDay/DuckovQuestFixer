using System;
using System.Reflection;
using QuestFixer.Models;

namespace QuestFixer.Logics
{
    /// <summary>
    /// Loads quest data from QuestCollection into cached list
    /// </summary>
    public static class QuestLoader
    {
        // Loads all quests from QuestCollection into state.CachedQuests
        // Tries multiple approaches: field "list", property "list", methods like GetAll/ToList
        // Also triggers quest type analysis on first quest found
        // Returns: void (populates state.CachedQuests)
        public static void LoadQuests(ModState state)
        {
            state.CachedQuests.Clear();
            
            if (state.QuestCollection == null) return;

            try
            {
                object questList = null;
                
                // Try field "list" first
                var listField = state.QuestCollectionType.GetField("list", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (listField != null)
                {
                    questList = listField.GetValue(state.QuestCollection);
                }
                
                // Try property "list"
                if (questList == null)
                {
                    var listProp = state.QuestCollectionType.GetProperty("list", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (listProp != null)
                    {
                        questList = listProp.GetValue(state.QuestCollection);
                    }
                }
                
                // Try methods like GetAll, ToList
                if (questList == null)
                {
                    foreach (var method in state.QuestCollectionMethods)
                    {
                        if ((method.Name.Contains("Get") || method.Name.Contains("All") || method.Name == "ToList") &&
                            method.GetParameters().Length == 0 &&
                            typeof(System.Collections.IEnumerable).IsAssignableFrom(method.ReturnType))
                        {
                            try
                            {
                                questList = method.Invoke(state.QuestCollection, null);
                                if (questList != null) break;
                            }
                            catch { }
                        }
                    }
                }
                
                // Fallback: collection itself might be IEnumerable
                if (questList == null && state.QuestCollection is System.Collections.IEnumerable)
                {
                    questList = state.QuestCollection;
                }
                
                // Extract quest info from each item
                if (questList is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item == null) continue;
                        var info = ExtractQuestInfo(item);
                        if (info != null)
                        {
                            state.CachedQuests.Add(info);
                            
                            // Analyze quest type on first quest
                            if (state.QuestType == null)
                            {
                                state.QuestType = item.GetType();
                                QuestSystemFinder.AnalyzeQuestType(state, state.QuestType);
                            }
                        }
                    }
                    ModLogger.Log(state, $"Loaded {state.CachedQuests.Count} quests");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log(state, $"Load error: {ex.Message}");
            }
        }

        // Extracts quest information from a raw quest object using reflection
        // Gets ID, Name, Complete status, Active status from various properties/fields
        // Returns: QuestInfo with extracted data, or null if extraction failed
        private static QuestInfo ExtractQuestInfo(object quest)
        {
            if (quest == null) return null;

            try
            {
                var type = quest.GetType();
                var info = new QuestInfo { RawQuest = quest, QuestType = type };

                // ID - try property first, then fallback names
                var idProp = type.GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
                info.Id = idProp?.GetValue(quest)?.ToString() ?? 
                          ReflectionHelper.GetValue<string>(quest, type, "Id", "ID", "TypeID") ?? "?";

                // Name - try multiple common property names
                info.Name = ReflectionHelper.GetValue<string>(quest, type, "DisplayName", "Name", "Title", "name") ?? type.Name;

                // Complete status - property or private field
                var completeProp = type.GetProperty("Complete", BindingFlags.Public | BindingFlags.Instance);
                if (completeProp != null)
                {
                    var completeVal = completeProp.GetValue(quest);
                    info.IsCompleted = completeVal is bool b && b;
                    info.Status = info.IsCompleted ? "Complete" : "Active";
                }
                else
                {
                    var completeField = type.GetField("complete", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (completeField != null)
                    {
                        var val = completeField.GetValue(quest);
                        info.IsCompleted = val is bool b && b;
                        info.Status = info.IsCompleted ? "Complete" : "Active";
                    }
                }

                // Active status
                var activeProp = type.GetProperty("Active", BindingFlags.Public | BindingFlags.Instance);
                if (activeProp != null)
                {
                    var activeVal = activeProp.GetValue(quest);
                    info.IsActive = activeVal is bool b && b;
                    if (!info.IsCompleted && info.IsActive)
                        info.Status = "Active";
                    else if (!info.IsCompleted && !info.IsActive)
                        info.Status = "Inactive";
                }

                return info;
            }
            catch
            {
                return null;
            }
        }
    }
}
