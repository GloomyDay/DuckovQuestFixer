using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QuestFixer.Logics;
using QuestFixer.Models;
using UnityEngine;

namespace QuestFixer.UI
{
    /// <summary>
    /// Quest tree visualization tab - shows quests as a dependency tree grouped by NPC
    /// </summary>
    public static class QuestTreeUI
    {
        // Main draw method for the tree tab
        // Contains toolbar, stats, and either mindmap or search view
        // Returns: void
        public static void Draw(ModState state)
        {
            DrawToolbar(state);
            DrawStats(state);
            
            state.TreeScrollPosition = GUILayout.BeginScrollView(state.TreeScrollPosition, GUILayout.Height(400));
            
            if (!state.TreeBuilt)
            {
                DrawCenteredMessage("Click 'Build' to analyze quests");
            }
            else if (state.TreeViewMode == 0)
            {
                DrawMindMapByGiver(state);
            }
            else
            {
                DrawSearchResults(state);
            }
            
            GUILayout.EndScrollView();
        }
        
        // Draws toolbar with build button, view mode selector, expand/collapse controls
        // Also shows search input when in search mode
        // Returns: void
        private static void DrawToolbar(ModState state)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[R] Build", GUILayout.Height(28), GUILayout.Width(100)))
            {
                QuestTreeBuilder.BuildQuestTree(state);
            }
            
            state.TreeViewMode = GUILayout.SelectionGrid(state.TreeViewMode, new[] { "By NPC", "Search" }, 2, GUILayout.Width(180));
            
            if (state.TreeViewMode == 0)
            {
                if (GUILayout.Button("Expand", GUILayout.Width(80)))
                {
                    // Expand all NPC groups
                    foreach (var g in state.QuestsByGiver.Keys) state.ExpandedGivers.Add(g);
                    // Expand all quests that have children
                    foreach (var kvp in state.QuestDependencies)
                    {
                        if (kvp.Value.Count > 0) state.ExpandedQuests.Add(kvp.Key);
                    }
                }
                if (GUILayout.Button("Collapse", GUILayout.Width(80)))
                {
                    state.ExpandedGivers.Clear();
                    state.ExpandedQuests.Clear();
                }
            }
            GUILayout.EndHorizontal();
            
            // Search mode input
            if (state.TreeViewMode == 1)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("ID:", GUILayout.Width(25));
                state.SelectedQuestForTree = GUILayout.TextField(state.SelectedQuestForTree, GUILayout.Width(80));
                if (GUILayout.Button("Chain →", GUILayout.Width(80)))
                {
                    ShowQuestChain(state, state.SelectedQuestForTree);
                }
                GUILayout.EndHorizontal();
            }
        }
        
        // Draws statistics bar with quest/NPC/relation counts
        // Also has toggle for hiding deprecated quests
        // Returns: void
        private static void DrawStats(ModState state)
        {
            GUILayout.BeginHorizontal();
            var statStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
            GUI.color = new Color(0.5f, 1f, 0.8f);
            GUILayout.Label($"Quests: {state.QuestById.Count} | NPC: {state.QuestsByGiver.Count} | Relations: {state.QuestDependencies.Values.Sum(l => l.Count)} | Hidden: {state.IsolatedQuestIds.Count}", statStyle);
            GUI.color = Color.white;
            
            bool newHide = GUILayout.Toggle(state.HideIsolatedQuests, "Hide deprecated", GUILayout.Width(120));
            if (newHide != state.HideIsolatedQuests)
            {
                state.HideIsolatedQuests = newHide;
            }
            GUILayout.EndHorizontal();
        }
        
        // Draws a centered message (used when tree not yet built)
        // Returns: void
        private static void DrawCenteredMessage(string msg)
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(msg, new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Italic });
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }
        
        // Draws the main mindmap view grouped by quest giver NPC
        // Each NPC is a collapsible section with their quest chains
        // Returns: void
        private static void DrawMindMapByGiver(ModState state)
        {
            foreach (var kvp in state.QuestsByGiver.OrderBy(x => x.Key))
            {
                string giverName = kvp.Key;
                var questIds = kvp.Value;
                
                // Find which quests are children of others from same NPC
                var childrenOfThisGiver = new HashSet<int>();
                foreach (var qId in questIds)
                {
                    if (state.QuestDependencies.TryGetValue(qId, out var deps))
                    {
                        foreach (var childId in deps)
                        {
                            if (questIds.Contains(childId))
                            {
                                childrenOfThisGiver.Add(childId);
                            }
                        }
                    }
                }
                
                // Root quests = not children of another quest from same NPC
                var rootQuestsForGiver = questIds
                    .Where(id => !childrenOfThisGiver.Contains(id))
                    .Where(id => !state.HideIsolatedQuests || !state.IsolatedQuestIds.Contains(id))
                    .OrderBy(id => id)
                    .ToList();
                
                bool isExpanded = state.ExpandedGivers.Contains(giverName);
                
                // NPC header button
                GUI.backgroundColor = new Color(0.15f, 0.4f, 0.7f);
                string arrow = isExpanded ? "v" : ">";
                if (GUILayout.Button($"{arrow} {giverName}  ({rootQuestsForGiver.Count} root / {questIds.Count} total)", 
                    GUILayout.Height(34)))
                {
                    if (isExpanded) state.ExpandedGivers.Remove(giverName);
                    else state.ExpandedGivers.Add(giverName);
                }
                GUI.backgroundColor = Color.white;
                
                // Draw quest tree if expanded
                if (isExpanded)
                {
                    state.CurrentDrawingGiver = giverName;
                    foreach (var questId in rootQuestsForGiver)
                    {
                        DrawQuestNode(state, questId, 0);
                    }
                    state.CurrentDrawingGiver = "";
                }
            }
        }
        
        // Recursively draws a quest node and its children
        // Shows status icon, name, child count, and optional details panel
        // depth: indentation level (max 8 to prevent infinite loops)
        // Returns: void
        private static void DrawQuestNode(ModState state, int questId, int depth)
        {
            if (!state.QuestById.TryGetValue(questId, out var quest)) return;
            if (depth > 40) return; // Safety limit to prevent infinite recursion
            
            // Count only children that exist AND belong to the same NPC (CurrentDrawingGiver)
            var existingChildren = state.QuestDependencies.ContainsKey(questId) 
                ? state.QuestDependencies[questId]
                    .Where(id => state.QuestById.ContainsKey(id))
                    .Where(id => string.IsNullOrEmpty(state.CurrentDrawingGiver) || 
                                 string.Equals(QuestTreeBuilder.GetQuestGiver(state.QuestById[id]), 
                                              state.CurrentDrawingGiver, 
                                              StringComparison.OrdinalIgnoreCase))
                    .ToList()
                : new List<int>();
            bool hasChildren = existingChildren.Count > 0;
            bool isExpanded = state.ExpandedQuests.Contains(questId);
            
            bool isCompleted = state.CompletedQuestIds.Contains(questId);
            bool isActive = state.ActiveQuestIds.Contains(questId);
            
            string treeChar = depth > 0 ? "└─ " : "";
            
            // Color based on status: green=done, blue=active, gray=not started
            Color btnColor;
            if (isCompleted)
                btnColor = new Color(0.2f, 0.65f, 0.3f);
            else if (isActive)
                btnColor = new Color(0.25f, 0.5f, 0.85f);
            else
                btnColor = new Color(0.35f, 0.35f, 0.4f);
            
            GUI.backgroundColor = btnColor;
            
            string statusIcon = isCompleted ? "[v]" : (isActive ? "[*]" : "[ ]");
            string expandIcon = hasChildren ? (isExpanded ? "v" : ">") : "  ";
            string childInfo = hasChildren && !isExpanded ? $" [+{existingChildren.Count}]" : "";
            
            GUILayout.BeginHorizontal();
            
            // Indentation
            if (depth > 0)
            {
                GUILayout.Label("", GUILayout.Width(depth * 25));
            }
            
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true,
                padding = new RectOffset(8, 8, 4, 4)
            };
            
            // Check if quest belongs to different NPC (cross-NPC dependency)
            string realGiver = QuestTreeBuilder.GetQuestGiver(quest);
            string otherNpcMark = "";
            if (depth > 0 && !string.IsNullOrEmpty(state.CurrentDrawingGiver) && realGiver != state.CurrentDrawingGiver)
            {
                otherNpcMark = $" <color=#ff9900>[→{realGiver}]</color>";
            }
            
            // Quest button - click to expand/collapse (expands to fill available width)
            if (GUILayout.Button($"{treeChar}{expandIcon} {statusIcon} [{questId}] {quest.Name}{childInfo}{otherNpcMark}", 
                style, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
            {
                if (hasChildren)
                {
                    if (isExpanded) state.ExpandedQuests.Remove(questId);
                    else state.ExpandedQuests.Add(questId);
                }
            }
            
            // Info button - toggle details panel
            GUI.backgroundColor = new Color(0.5f, 0.5f, 0.6f);
            if (GUILayout.Button("?", GUILayout.Width(28), GUILayout.Height(28)))
            {
                if (state.SelectedQuestForDetails == questId)
                {
                    // Close details
                    state.SelectedQuestForDetails = null;
                    state.QuestDetailsCache = null;
                    state.ShowAllQuestProperties = false;
                }
                else
                {
                    // Open details - cache data NOW (once)
                    state.SelectedQuestForDetails = questId;
                    state.ShowAllQuestProperties = false;
                    CacheQuestDetails(state, questId, quest);
                }
            }
            
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            
            // Details panel if selected
            if (state.SelectedQuestForDetails == questId)
            {
                DrawQuestRequirements(state, questId, quest, depth);
            }
            
            // Recursively draw children (only existing ones)
            if (isExpanded && hasChildren)
            {
                foreach (var childId in existingChildren.OrderBy(id => id))
                {
                    DrawQuestNode(state, childId, depth + 1);
                }
            }
        }
        
        // Caches all quest details via reflection (called ONCE when "?" is clicked)
        // This prevents lag from doing reflection every frame
        // Returns: void
        private static void CacheQuestDetails(ModState state, int questId, QuestInfo quest)
        {
            var cache = new QuestDetailsCache { QuestId = questId };
            
            try
            {
                var t = quest.QuestType;
                var raw = quest.RawQuest;
                
                // Basic properties
                cache.QuestGiver = t.GetProperty("QuestGiverID")?.GetValue(raw)?.ToString() ?? "";
                cache.Description = t.GetProperty("Description")?.GetValue(raw)?.ToString() ?? "";
                
                var reqLevel = t.GetProperty("RequireLevel")?.GetValue(raw);
                int.TryParse(reqLevel?.ToString() ?? "0", out cache.RequireLevel);
                
                var lockDemo = t.GetProperty("LockInDemo")?.GetValue(raw);
                cache.LockInDemo = lockDemo is bool b && b;
                
                var reqItemId = t.GetProperty("RequiredItemID")?.GetValue(raw);
                cache.RequiredItemId = reqItemId is int ii ? ii : 0;
                
                var reqItemCount = t.GetProperty("RequiredItemCount")?.GetValue(raw);
                cache.RequiredItemCount = reqItemCount is int cc ? cc : 0;
                
                var reqSceneId = t.GetField("requireSceneID", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(raw);
                cache.RequireSceneId = reqSceneId?.ToString() ?? "";
                
                // Player state (snapshot)
                cache.PlayerLevel = GameHelpers.GetPlayerLevel();
                cache.CurrentScene = GameHelpers.GetCurrentSceneName();
                
                // Tasks
                var tasksProp = t.GetProperty("Tasks");
                if (tasksProp != null)
                {
                    var tasksList = tasksProp.GetValue(raw) as System.Collections.IList;
                    if (tasksList != null)
                    {
                        foreach (var task in tasksList)
                        {
                            if (task == null) continue;
                            var taskType = task.GetType();
                            
                            var taskDesc = taskType.GetProperty("Description")?.GetValue(task)?.ToString();
                            var taskName = taskType.GetProperty("DisplayName")?.GetValue(task)?.ToString();
                            var taskGoal = taskType.GetProperty("Goal")?.GetValue(task);
                            var taskProgress = taskType.GetProperty("Progress")?.GetValue(task);
                            var isFinished = taskType.GetProperty("IsFinished")?.GetValue(task);
                            
                            string taskText = !string.IsNullOrEmpty(taskDesc) ? taskDesc : 
                                              (!string.IsNullOrEmpty(taskName) ? taskName : taskType.Name);
                            
                            string progressStr = (taskGoal != null && taskProgress != null) 
                                ? $"[{taskProgress}/{taskGoal}]" : "";
                            
                            cache.Tasks.Add(new TaskInfo
                            {
                                Text = taskText,
                                Progress = progressStr,
                                IsFinished = isFinished is bool fin && fin
                            });
                        }
                    }
                }
                
                // All properties for "show all" view
                foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    try
                    {
                        var val = prop.GetValue(raw);
                        string valStr = val?.ToString() ?? "null";
                        if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                        cache.AllProperties.Add(new PropertyDisplay { Name = prop.Name, Value = valStr, Type = "prop" });
                    }
                    catch { }
                }
                
                foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var val = field.GetValue(raw);
                        string valStr = val?.ToString() ?? "null";
                        if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                        cache.AllProperties.Add(new PropertyDisplay { Name = field.Name, Value = valStr, Type = "field" });
                    }
                    catch { }
                }
                
                // Calculate blockers
                if (cache.RequireLevel >= 100)
                    cache.Blockers.Add($"[BLOCKED] (level {cache.RequireLevel} -- developer placeholder!)");
                else if (cache.RequireLevel > 0 && cache.PlayerLevel >= 0 && cache.PlayerLevel < cache.RequireLevel)
                    cache.Blockers.Add($"Need level {cache.RequireLevel} (you have {cache.PlayerLevel})");
                
                if (cache.LockInDemo)
                    cache.Blockers.Add("Locked in demo!");
                
                if (cache.RequiredItemId > 0)
                    cache.Blockers.Add($"Need item ID {cache.RequiredItemId} x{cache.RequiredItemCount}");
                
                // Prerequisites not done
                if (state.QuestPrerequisites.TryGetValue(questId, out var prereqs))
                {
                    foreach (var prereqId in prereqs)
                    {
                        if (!state.CompletedQuestIds.Contains(prereqId))
                        {
                            string prereqName = state.QuestById.TryGetValue(prereqId, out var pq) ? pq.Name : "?";
                            cache.Blockers.Add($"Not completed [{prereqId}] {prereqName}");
                        }
                    }
                }
                
                if (state.IsolatedQuestIds.Contains(questId))
                    cache.Blockers.Add("ISOLATED — possibly deprecated!");
            }
            catch (Exception ex)
            {
                ModLogger.Log(state, $"Error caching quest {questId}: {ex.Message}");
            }
            
            state.QuestDetailsCache = cache;
        }
        
        // Draws detailed quest requirements panel using CACHED data (no reflection per frame)
        // Shows: description, tasks, level/scene/item requirements, relations, blockers
        // Also has "Force Activate" and "Show All Properties" buttons
        // Returns: void
        private static void DrawQuestRequirements(ModState state, int questId, QuestInfo quest, int depth)
        {
            var cache = state.QuestDetailsCache;
            if (cache == null || cache.QuestId != questId)
            {
                GUILayout.Label("Loading...");
                return;
            }
            
            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(depth * 25 + 10, 10, 2, 6)
            };
            
            GUI.backgroundColor = new Color(0.2f, 0.25f, 0.35f);
            GUILayout.BeginVertical(boxStyle);
            
            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, richText = true };
            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
            
            GUILayout.Label($"QUEST INFORMATION [{questId}]", headerStyle);
            
            // Quest giver
            GUILayout.Label($"Quest Giver: <color=cyan>{cache.QuestGiver}</color>", labelStyle);
            
            // Description
            if (!string.IsNullOrEmpty(cache.Description))
            {
                GUILayout.Label($"<color=white>{cache.Description}</color>", labelStyle);
                GUILayout.Space(4);
            }
            
            // Tasks (from cache)
            if (cache.Tasks.Count > 0)
            {
                GUILayout.Label($"Tasks ({cache.Tasks.Count}):", labelStyle);
                foreach (var task in cache.Tasks)
                {
                    string finishIcon = task.IsFinished ? "[v]" : "o";
                    string color = task.IsFinished ? "green" : "white";
                    string progressStr = !string.IsNullOrEmpty(task.Progress) ? $" {task.Progress}" : "";
                    GUILayout.Label($"   {finishIcon} <color={color}>{task.Text}{progressStr}</color>", labelStyle);
                }
                GUILayout.Space(4);
            }
            
            GUILayout.Label("── START REQUIREMENTS ──", labelStyle);
            
            // Level requirement (from cache)
            bool isLevelBlocker = cache.RequireLevel >= 100;
            bool levelNotMet = cache.RequireLevel > 0 && cache.PlayerLevel >= 0 && cache.PlayerLevel < cache.RequireLevel;
            string levelColor = cache.RequireLevel == 0 ? "gray" : (isLevelBlocker ? "red" : (levelNotMet ? "orange" : "green"));
            string levelNote = "";
            if (isLevelBlocker)
                levelNote = $" [BLOCKED!] (yours: {cache.PlayerLevel})";
            else if (cache.RequireLevel > 0 && cache.PlayerLevel >= 0)
                levelNote = cache.PlayerLevel >= cache.RequireLevel ? $" [ok] (yours: {cache.PlayerLevel})" : $" [X] (yours: {cache.PlayerLevel})";
            GUILayout.Label($"Required Level: <color={levelColor}>{cache.RequireLevel}{levelNote}</color>", labelStyle);
            
            // Scene requirement (from cache)
            if (!string.IsNullOrEmpty(cache.RequireSceneId))
            {
                string sceneDisplay = cache.RequireSceneId.Replace(".SceneReference", "").Trim();
                GUILayout.Label($"Location for Completion: <color=cyan>{sceneDisplay}</color>", labelStyle);
                GUILayout.Label($"   <color=#888>Your location: {cache.CurrentScene}</color>", labelStyle);
            }
            else
            {
                GUILayout.Label($"Location for Completion: <color=gray>Any</color>", labelStyle);
            }
            
            // Demo lock (from cache)
            GUILayout.Label($"Locked in Demo: <color={(cache.LockInDemo ? "red" : "gray")}>{(cache.LockInDemo ? "YES" : "No")}</color>", labelStyle);
            
            // Item requirement (from cache)
            bool hasItem = cache.RequiredItemId > 0;
            GUILayout.Label($"Required Item: <color={(hasItem ? "yellow" : "gray")}>{(hasItem ? $"ID {cache.RequiredItemId} x{cache.RequiredItemCount}" : "None")}</color>", labelStyle);
            
            // Relations (these don't need caching - simple dict lookups)
            DrawRelations(state, questId, labelStyle, new List<string>()); // blockers already in cache
            
            // Isolated warning
            if (state.IsolatedQuestIds.Contains(questId))
            {
                GUILayout.Label($"<color=orange>[!] ISOLATED -- possibly deprecated!</color>", labelStyle);
            }
            
            // Status and force activate button (use cached blockers)
            DrawStatusCached(state, questId, labelStyle, cache.Blockers);
            
            // Show All Properties button
            GUILayout.Space(8);
            if (GUILayout.Button(state.ShowAllQuestProperties ? "v Hide All Properties" : "^ Show All Quest Properties", GUILayout.Height(24)))
            {
                state.ShowAllQuestProperties = !state.ShowAllQuestProperties;
            }
            
            if (state.ShowAllQuestProperties && cache.AllProperties.Count > 0)
            {
                GUILayout.Label("── ALL PROPERTIES ──", labelStyle);
                foreach (var prop in cache.AllProperties)
                {
                    string typeIcon = prop.Type == "prop" ? "[P]" : "[F]";
                    GUILayout.Label($"{typeIcon} <color=yellow>{prop.Name}</color>: <color=white>{prop.Value}</color>", labelStyle);
                }
            }
            
            GUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }
        
        // Draws quest relation info (prerequisites and unlocks)
        // Shows which quests must be done first and what this quest unlocks
        // Returns: void
        private static void DrawRelations(ModState state, int questId, GUIStyle labelStyle, List<string> blockers)
        {
            GUILayout.Space(4);
            GUILayout.Label("Graph Relations:", labelStyle);
            
            // Prerequisites (quests that must be done first)
            if (state.QuestPrerequisites.TryGetValue(questId, out var prereqs) && prereqs.Count > 0)
            {
                GUILayout.Label($"   Requires ({prereqs.Count}):", labelStyle);
                foreach (var prereqId in prereqs.Take(5))
                {
                    string prereqName = state.QuestById.TryGetValue(prereqId, out var pq) ? pq.Name : "?";
                    string prereqGiver = state.QuestById.TryGetValue(prereqId, out var pg) ? QuestTreeBuilder.GetQuestGiver(pg) : "";
                    bool prereqDone = state.CompletedQuestIds.Contains(prereqId);
                    string color = prereqDone ? "green" : "red";
                    GUILayout.Label($"      <color={color}>• [{prereqId}] {prereqName} ({prereqGiver})</color>", labelStyle);
                    
                    if (!prereqDone)
                        blockers.Add($"Not completed [{prereqId}] {prereqName}");
                }
            }
            else
            {
                GUILayout.Label($"   <color=gray>No prerequisite quests</color>", labelStyle);
            }
            
            // Dependents (quests that this unlocks)
            if (state.QuestDependencies.TryGetValue(questId, out var deps) && deps.Count > 0)
            {
                GUILayout.Label($"   Unlocks ({deps.Count}):", labelStyle);
                foreach (var depId in deps.Take(5))
                {
                    string depName = state.QuestById.TryGetValue(depId, out var dq) ? dq.Name : "?";
                    GUILayout.Label($"      <color=cyan>→ [{depId}] {depName}</color>", labelStyle);
                }
            }
        }
        
        // Draws quest status and force activate button (uses cached blockers)
        // Shows why quest is blocked and provides bypass option for bugged quests
        // Returns: void
        private static void DrawStatusCached(ModState state, int questId, GUIStyle labelStyle, List<string> blockers)
        {
            bool isCompleted = state.CompletedQuestIds.Contains(questId);
            bool isActive = state.ActiveQuestIds.Contains(questId);
            
            GUILayout.Space(6);
            if (isCompleted)
            {
                GUILayout.Label("═══ STATUS ═══", labelStyle);
                GUILayout.Label("[v] <color=green>QUEST COMPLETED</color>", labelStyle);
            }
            else if (isActive)
            {
                GUILayout.Label("═══ STATUS ═══", labelStyle);
                GUILayout.Label("[*] <color=cyan>QUEST ACTIVE</color>", labelStyle);
            }
            else if (blockers.Count > 0)
            {
                bool isDevBlocked = blockers.Any(bl => bl.Contains("[BLOCKED]"));
                GUILayout.Label(isDevBlocked ? "=== [X] HIDDEN CONTENT ===" : "=== [X] WHY UNAVAILABLE ===", labelStyle);
                
                foreach (var blocker in blockers.Take(8))
                {
                    GUILayout.Label($"   <color=red>• {blocker}</color>", labelStyle);
                }
            }
            else
            {
                GUILayout.Label("═══ STATUS ═══", labelStyle);
                GUILayout.Label("[?] <color=yellow>Quest not started -- NPC dialog or hidden trigger needed?</color>", labelStyle);
            }
            
            // Force activate button - always shown
            GUILayout.Space(8);
            GUI.backgroundColor = new Color(0.8f, 0.4f, 0.1f);
            if (GUILayout.Button("FORCE ACTIVATE QUEST", GUILayout.Height(28)))
            {
                QuestActions.ForceActivateQuest(state, questId);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Label("<color=#888>Warning: may break progression!</color>", labelStyle);
        }
        
        // Draws search results view showing quest chain (prerequisites and dependents)
        // Used when tree view mode = 1 (search)
        // Returns: void
        private static void DrawSearchResults(ModState state)
        {
            if (string.IsNullOrEmpty(state.SelectedQuestForTree))
            {
                GUILayout.Label("Enter quest ID to search chain");
                return;
            }
            
            if (!int.TryParse(state.SelectedQuestForTree, out int questId) || !state.QuestById.TryGetValue(questId, out var quest))
            {
                GUI.color = Color.red;
                GUILayout.Label($"Quest with ID {state.SelectedQuestForTree} not found");
                GUI.color = Color.white;
                return;
            }
            
            GUI.color = Color.yellow;
            GUILayout.Label($"Chain for: [{questId}] {quest.Name}", 
                new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
            
            GUILayout.Space(10);
            
            // Prerequisites section
            GUI.color = new Color(1f, 0.6f, 0.6f);
            GUILayout.Label("^ REQUIRED FIRST:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
            
            if (state.QuestPrerequisites.TryGetValue(questId, out var prereqs) && prereqs.Count > 0)
            {
                foreach (var prereqId in prereqs)
                {
                    DrawMiniQuestNode(state, prereqId, "  ^ ");
                }
            }
            else
            {
                GUILayout.Label("  (root quest)");
            }
            
            GUILayout.Space(5);
            
            // Current quest highlight
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUI.backgroundColor = Color.yellow;
            GUILayout.Box($"* [{questId}] {quest.Name}", GUILayout.Height(30), GUILayout.Width(400));
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Dependents section
            GUI.color = new Color(0.6f, 1f, 0.6f);
            GUILayout.Label("v WILL UNLOCK AFTER:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
            
            if (state.QuestDependencies.TryGetValue(questId, out var deps) && deps.Count > 0)
            {
                foreach (var depId in deps)
                {
                    DrawMiniQuestNode(state, depId, "  v ");
                }
            }
            else
            {
                GUILayout.Label("  (final quest)");
            }
        }
        
        // Draws a compact quest node for search results
        // Shows status icon and quest name
        // Returns: void
        private static void DrawMiniQuestNode(ModState state, int questId, string prefix)
        {
            if (!state.QuestById.TryGetValue(questId, out var quest)) return;
            
            string icon = state.CompletedQuestIds.Contains(questId) ? "[v]" : "[ ]";
            GUI.color = state.CompletedQuestIds.Contains(questId) ? Color.green : Color.gray;
            GUILayout.Label($"{prefix}{icon} [{questId}] {quest.Name}", GUILayout.Width(450));
            GUI.color = Color.white;
        }
        
        // Logs quest chain to the in-game log (for search mode button)
        // Shows prerequisites, current quest, and what it unlocks
        // Returns: void
        private static void ShowQuestChain(ModState state, string input)
        {
            if (!int.TryParse(input, out int questId) || !state.QuestById.TryGetValue(questId, out var quest))
            {
                ModLogger.Log(state, $"Quest {input} not found");
                return;
            }
            
            ModLogger.Log(state, $"== CHAIN [{questId}] {quest.Name} ==");
            
            if (state.QuestPrerequisites.TryGetValue(questId, out var prereqs) && prereqs.Count > 0)
            {
                ModLogger.Log(state, "| ^ PREREQUISITES:");
                foreach (var pId in prereqs)
                {
                    var p = state.QuestById.GetValueOrDefault(pId);
                    ModLogger.Log(state, $"|   {(state.CompletedQuestIds.Contains(pId) ? "[v]" : "[ ]")} [{pId}] {p?.Name ?? "?"}");
                }
            }
            
            ModLogger.Log(state, $"| * [{questId}] {quest.Name}");
            
            if (state.QuestDependencies.TryGetValue(questId, out var deps) && deps.Count > 0)
            {
                ModLogger.Log(state, "| v WILL UNLOCK:");
                foreach (var dId in deps)
                {
                    var d = state.QuestById.GetValueOrDefault(dId);
                    ModLogger.Log(state, $"|   -> [{dId}] {d?.Name ?? "?"}");
                }
            }
            
            ModLogger.Log(state, "=========================================");
        }
    }
}
