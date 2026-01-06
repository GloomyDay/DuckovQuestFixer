using System;
using System.Reflection;
using QuestFixer.Models;

namespace QuestFixer.Logics
{
    /// <summary>
    /// Actions for manipulating quests - complete, reset, force activate
    /// </summary>
    public static class QuestActions
    {
        // Completes all tasks in a quest by calling ForceFinish() on each task
        // Mimics DuckovMenu's approach: Task.ForceFinish() + Task.ReportStatusChanged()
        // Quest still needs to be turned in to NPC after this!
        // Returns: void
        public static void CompleteQuest(ModState state, QuestInfo quest)
        {
            if (quest?.RawQuest == null)
            {
                ModLogger.Log(state, "Quest not found");
                return;
            }

            ModLogger.Log(state, $"=== Completing quest: {quest.Name} (ID: {quest.Id}) ===");

            try
            {
                var qType = quest.QuestType;
                int completedTasks = 0;
                
                var tasksProp = qType.GetProperty("Tasks");
                if (tasksProp == null)
                {
                    ModLogger.Log(state, "Tasks property not found");
                    return;
                }
                
                var tasks = tasksProp.GetValue(quest.RawQuest) as System.Collections.IEnumerable;
                if (tasks == null)
                {
                    ModLogger.Log(state, "Tasks = null");
                    return;
                }
                
                foreach (var task in tasks)
                {
                    if (task == null) continue;
                    
                    var taskType = task.GetType();
                    
                    // Skip already finished tasks
                    var isFinishedMethod = taskType.GetMethod("IsFinished", BindingFlags.Public | BindingFlags.Instance);
                    if (isFinishedMethod != null)
                    {
                        var isFinished = isFinishedMethod.Invoke(task, null);
                        if (isFinished is bool b && b)
                        {
                            completedTasks++;
                            continue;
                        }
                    }
                    
                    // Call ForceFinish() - private method that marks task as done
                    var forceFinishMethod = taskType.GetMethod("ForceFinish", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (forceFinishMethod != null)
                    {
                        forceFinishMethod.Invoke(task, null);
                        completedTasks++;
                        ModLogger.Log(state, $"Task.ForceFinish() called");
                        
                        // Notify game of status change
                        var reportMethod = taskType.GetMethod("ReportStatusChanged", 
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        reportMethod?.Invoke(task, null);
                    }
                }
                
                ModLogger.Log(state, $"Completed {completedTasks} tasks. Quest ready to turn in to NPC!");
                QuestLoader.LoadQuests(state);
            }
            catch (Exception ex)
            {
                ModLogger.Log(state, $"Error: {ex.Message}");
            }
        }

        // Fully resets a quest to initial state - uncompletes it and all tasks
        // Resets: complete flag, rewards claimed status, all task progress
        // Then tries to re-activate the quest via QuestManager
        // Returns: void
        public static void ResetQuest(ModState state, QuestInfo quest)
        {
            if (quest?.RawQuest == null)
            {
                ModLogger.Log(state, "Quest not found");
                return;
            }

            ModLogger.Log(state, $"=== Resetting quest: {quest.Name} (ID: {quest.Id}) ===");

            try
            {
                var qType = quest.QuestType;
                int resetTasks = 0;
                
                // Reset quest completion flag
                var completeField = qType.GetField("complete", BindingFlags.NonPublic | BindingFlags.Instance);
                completeField?.SetValue(quest.RawQuest, false);
                
                var setCompleteMethod = qType.GetMethod("set_Complete", BindingFlags.NonPublic | BindingFlags.Instance);
                setCompleteMethod?.Invoke(quest.RawQuest, new object[] { false });
                
                // Reset all rewards to unclaimed
                var rewardsField = qType.GetField("rewards", BindingFlags.NonPublic | BindingFlags.Instance);
                if (rewardsField != null)
                {
                    var rewards = rewardsField.GetValue(quest.RawQuest) as System.Collections.IEnumerable;
                    if (rewards != null)
                    {
                        foreach (var reward in rewards)
                        {
                            if (reward == null) continue;
                            var claimedField = reward.GetType().GetField("claimed", 
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            claimedField?.SetValue(reward, false);
                        }
                    }
                }
                
                // Reset all task progress
                var tasksProp = qType.GetProperty("Tasks");
                var tasks = tasksProp?.GetValue(quest.RawQuest) as System.Collections.IEnumerable;
                
                if (tasks != null)
                {
                    foreach (var task in tasks)
                    {
                        if (task == null) continue;
                        
                        var taskType = task.GetType();
                        var currentType = taskType;
                        
                        // Find forceFinish field in type hierarchy
                        while (currentType != null && currentType != typeof(object))
                        {
                            var forceFinishField = currentType.GetField("forceFinish", 
                                BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (forceFinishField != null)
                            {
                                forceFinishField.SetValue(task, false);
                                resetTasks++;
                                break;
                            }
                            currentType = currentType.BaseType;
                        }
                        
                        // Reset task-specific fields
                        ReflectionHelper.SetFieldIfExists(task, taskType, "amount", 0);
                        ReflectionHelper.SetFieldIfExists(task, taskType, "submitted", false);
                        ReflectionHelper.SetFieldIfExists(task, taskType, "currentProgress", 0);
                        
                        var reportMethod = taskType.GetMethod("ReportStatusChanged", 
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        reportMethod?.Invoke(task, null);
                    }
                }
                
                // Re-initialize quest
                var initMethod = qType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
                if (initMethod?.GetParameters().Length == 0)
                {
                    initMethod.Invoke(quest.RawQuest, null);
                }
                
                // Try to activate via QuestManager
                TryActivateQuest(state, quest);
                
                ModLogger.Log(state, $"Quest '{quest.Name}' reset! ({resetTasks} tasks)");
                QuestLoader.LoadQuests(state);
            }
            catch (Exception ex)
            {
                ModLogger.Log(state, $"Error: {ex.Message}");
            }
        }

        // Attempts to activate a quest through QuestManager
        // Removes quest from HistoryQuests and ActiveQuests, then calls ActivateQuest
        // Also saves the game state after activation
        // Returns: void
        public static void TryActivateQuest(ModState state, QuestInfo quest)
        {
            if (state.QuestManager == null || state.QuestManagerType == null) return;
            
            try
            {
                // Remove from completed quests list
                var historyProp = state.QuestManagerType.GetProperty("HistoryQuests", BindingFlags.Public | BindingFlags.Instance);
                if (historyProp != null)
                {
                    var historyQuests = historyProp.GetValue(state.QuestManager);
                    var removeMethod = historyQuests?.GetType().GetMethod("Remove");
                    removeMethod?.Invoke(historyQuests, new[] { quest.RawQuest });
                }
                
                // Remove from active quests list
                var activeProp = state.QuestManagerType.GetProperty("ActiveQuests", BindingFlags.Public | BindingFlags.Instance);
                if (activeProp != null)
                {
                    var activeQuests = activeProp.GetValue(state.QuestManager);
                    var removeMethod = activeQuests?.GetType().GetMethod("Remove");
                    removeMethod?.Invoke(activeQuests, new[] { quest.RawQuest });
                }
                
                // Call ActivateQuest(id, null)
                if (int.TryParse(quest.Id, out int questId))
                {
                    var activateMethod = state.QuestManagerType.GetMethod("ActivateQuest", BindingFlags.Public | BindingFlags.Instance);
                    if (activateMethod != null)
                    {
                        var parameters = activateMethod.GetParameters();
                        if (parameters.Length == 2)
                            activateMethod.Invoke(state.QuestManager, new object[] { questId, null });
                        else if (parameters.Length == 1)
                            activateMethod.Invoke(state.QuestManager, new object[] {questId });
                        
                        ModLogger.Log(state, $"QuestManager.ActivateQuest({questId}) called!");
                    }
                }
                
                // Save game state
                var saveMethod = state.QuestManagerType.GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveMethod?.GetParameters().Length == 0)
                {
                    saveMethod.Invoke(state.QuestManager, null);
                    ModLogger.Log(state, "QuestManager.Save() called!");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log(state, $"Activation error: {ex.Message}");
            }
        }
        
        // Force activates a quest by ID, bypassing normal requirements
        // Used for bugged quests that require non-existent scenes or have impossible conditions
        // WARNING: May break quest progression if used incorrectly!
        // Returns: void
        public static void ForceActivateQuest(ModState state, int questId)
        {
            ModLogger.Log(state, $"=== FORCE ACTIVATING QUEST {questId} ===");
            
            try
            {
                if (state.QuestManager == null || state.QuestManagerType == null)
                {
                    ModLogger.Log(state, "QuestManager not found!");
                    return;
                }
                
                var activateMethod = state.QuestManagerType.GetMethod("ActivateQuest", BindingFlags.Public | BindingFlags.Instance);
                if (activateMethod != null)
                {
                    activateMethod.Invoke(state.QuestManager, new object[] { questId, null });
                    ModLogger.Log(state, $"ActivateQuest({questId}) called!");
                    
                    // Save immediately
                    var saveMethod = state.QuestManagerType.GetMethod("Save", 
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    saveMethod?.Invoke(state.QuestManager, null);
                    
                    // Refresh UI status
                    QuestTreeBuilder.LoadQuestStatuses(state);
                    ModLogger.Log(state, $"Quest {questId} force activated!");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log(state, $"Force activation error: {ex.Message}");
            }
        }
    }
}
