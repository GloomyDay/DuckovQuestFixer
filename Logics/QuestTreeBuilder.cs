using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QuestFixer.Models;

namespace QuestFixer.Logics
{
    /// <summary>
    /// Builds quest dependency tree and tracks quest statuses
    /// </summary>
    public static class QuestTreeBuilder
    {
        // Builds the complete quest dependency tree for visualization
        // Groups quests by NPC, finds prerequisites from QuestRelationGraph
        // Also identifies isolated (deprecated) quests with no connections
        // Returns: void (populates state tree data structures)
        public static void BuildQuestTree(ModState state)
        {
            ModLogger.Log(state, "=== Building quest tree ===");
            
            state.ClearTreeData();
            LoadQuestStatuses(state);
            
            if (state.CachedQuests.Count == 0)
            {
                ModLogger.Log(state, "Quests not loaded!");
                return;
            }
            
            // Index all quests by ID and group by quest giver NPC
            foreach (var quest in state.CachedQuests)
            {
                if (int.TryParse(quest.Id, out int id))
                {
                    state.QuestById[id] = quest;
                    state.QuestDependencies[id] = new List<int>();
                    state.QuestPrerequisites[id] = new List<int>();
                    
                    string giverName = GetQuestGiver(quest);
                    if (!state.QuestsByGiver.ContainsKey(giverName))
                        state.QuestsByGiver[giverName] = new List<int>();
                    state.QuestsByGiver[giverName].Add(id);
                }
            }
            
            // Load quest relations from game's QuestRelationGraph
            int totalRelations = LoadRelationsFromGraph(state);
            
            // Find root quests (no prerequisites)
            foreach (var kvp in state.QuestPrerequisites)
            {
                if (kvp.Value.Count == 0)
                    state.RootQuests.Add(kvp.Key);
            }
            state.RootQuests.Sort();
            
            // Find isolated quests (no prerequisites AND no dependents = likely deprecated)
            foreach (var questId in state.QuestById.Keys)
            {
                var hasPrereqs = state.QuestPrerequisites.TryGetValue(questId, out var prereqs) && prereqs.Count > 0;
                var hasDeps = state.QuestDependencies.TryGetValue(questId, out var deps) && deps.Count > 0;
                
                if (!hasPrereqs && !hasDeps)
                    state.IsolatedQuestIds.Add(questId);
            }
            
            state.TreeBuilt = true;
            ModLogger.Log(state, $"Tree built! Quests: {state.QuestById.Count}, NPC: {state.QuestsByGiver.Count}, Relations: {totalRelations}");
        }
        
        // Loads quest dependencies from QuestManager.QuestRelation.GetRequiredIDs()
        // Populates both QuestPrerequisites (what this quest needs) and QuestDependencies (what needs this quest)
        // Returns: Total number of relations found
        private static int LoadRelationsFromGraph(ModState state)
        {
            int totalRelations = 0;
            
            if (state.QuestManager == null || state.QuestManagerType == null)
                return 0;
            
            try
            {
                // Get QuestRelation property (QuestRelationGraph object)
                var relationProp = state.QuestManagerType.GetProperty("QuestRelation", 
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                
                if (relationProp == null) return 0;
                
                var relationGraph = relationProp.GetValue(state.QuestManager);
                if (relationGraph == null) return 0;
                
                // Get the GetRequiredIDs method
                var getRequiredMethod = relationGraph.GetType().GetMethod("GetRequiredIDs", 
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (getRequiredMethod == null) return 0;
                
                // For each quest, get its required quest IDs
                foreach (var questId in state.QuestById.Keys)
                {
                    try
                    {
                        var requiredIds = getRequiredMethod.Invoke(relationGraph, new object[] { questId });
                        
                        if (requiredIds is System.Collections.IList list)
                        {
                            foreach (var reqId in list)
                            {
                                if (reqId is int requiredQuestId && requiredQuestId > 0)
                                {
                                    // This quest requires requiredQuestId
                                    if (!state.QuestPrerequisites[questId].Contains(requiredQuestId))
                                        state.QuestPrerequisites[questId].Add(requiredQuestId);
                                    
                                    // requiredQuestId unlocks this quest
                                    if (state.QuestDependencies.ContainsKey(requiredQuestId) && 
                                        !state.QuestDependencies[requiredQuestId].Contains(questId))
                                        state.QuestDependencies[requiredQuestId].Add(questId);
                                    
                                    totalRelations++;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log(state, $"QuestRelationGraph error: {ex.Message}");
            }
            
            return totalRelations;
        }
        
        // Loads current quest statuses from QuestManager runtime state
        // ActiveQuests = currently in progress, HistoryQuests = completed
        // This reflects actual game state, not prefab data!
        // Returns: void (populates state.ActiveQuestIds and state.CompletedQuestIds)
        public static void LoadQuestStatuses(ModState state)
        {
            if (state.QuestManager == null || state.QuestManagerType == null) return;
            
            state.ActiveQuestIds.Clear();
            state.CompletedQuestIds.Clear();
            
            try
            {
                // Get active quests (in progress)
                var activeProp = state.QuestManagerType.GetProperty("ActiveQuests", BindingFlags.Public | BindingFlags.Instance);
                if (activeProp != null)
                {
                    var activeList = activeProp.GetValue(state.QuestManager) as System.Collections.IList;
                    if (activeList != null)
                    {
                        foreach (var quest in activeList)
                        {
                            var id = quest?.GetType().GetProperty("ID")?.GetValue(quest);
                            if (id is int questId && questId > 0)
                                state.ActiveQuestIds.Add(questId);
                        }
                    }
                }
                
                // Get completed quests (history)
                var historyProp = state.QuestManagerType.GetProperty("HistoryQuests", BindingFlags.Public | BindingFlags.Instance);
                if (historyProp != null)
                {
                    var historyList = historyProp.GetValue(state.QuestManager) as System.Collections.IList;
                    if (historyList != null)
                    {
                        foreach (var quest in historyList)
                        {
                            var id = quest?.GetType().GetProperty("ID")?.GetValue(quest);
                            if (id is int questId && questId > 0)
                                state.CompletedQuestIds.Add(questId);
                        }
                    }
                }
                
                ModLogger.Log(state, $"Statuses: {state.ActiveQuestIds.Count} active, {state.CompletedQuestIds.Count} completed");
            }
            catch (Exception ex)
            {
                ModLogger.Log(state, $"Status load error: {ex.Message}");
            }
        }
        
        // Gets the quest giver NPC name from QuestGiverID property
        // Used to group quests by NPC in the tree view
        // Returns: NPC name string, or "Unknown NPC" if not found
        public static string GetQuestGiver(QuestInfo quest)
        {
            try
            {
                var giverProp = quest.QuestType.GetProperty("QuestGiverID", BindingFlags.Public | BindingFlags.Instance);
                return giverProp?.GetValue(quest.RawQuest)?.ToString() ?? "Unknown NPC";
            }
            catch
            {
                return "Unknown NPC";
            }
        }
    }
}
