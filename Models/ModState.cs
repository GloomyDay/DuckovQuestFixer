using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace QuestFixer.Models
{
    /// <summary>
    /// Central state container for the entire mod
    /// All UI components and logic modules share this state
    /// Inspired by Go-style state management (single struct passed around)
    /// </summary>
    public class ModState
    {
        #region UI State
        // Whether the main mod window is visible
        public bool ShowWindow = false;
        
        // Whether service menu is expanded
        public bool ShowServiceMenu = false;
        
        // Window position and size for IMGUI
        public Rect WindowRect = new Rect(20, 20, 700, 600);
        
        // Currently selected tab index (0-3)
        public int CurrentTab = 0;
        
        // Tab names for toolbar display
        public string[] TabNames = { "Quests", "Tree", "API Quest", "Log" };
        #endregion
        
        #region Scroll Positions
        // Quest list scroll position
        public Vector2 ScrollPosition;
        
        // API viewer scroll position
        public Vector2 ScrollPosition2;
        
        // Log tab scroll position
        public Vector2 LogScrollPosition;
        
        // Tree view scroll position
        public Vector2 TreeScrollPosition;
        #endregion
        
        #region User Input
        // Filter text for quest list search
        public string SearchFilter = "";
        
        // Quest ID input for complete/reset actions
        public string QuestIdInput = "";
        
        // Quest ID for chain search in tree view
        public string SelectedQuestForTree = "";
        #endregion
        
        #region Tree UI State
        // 0 = By NPC view, 1 = Search view
        public int TreeViewMode = 0;
        
        // Set of expanded NPC groups in tree
        public HashSet<string> ExpandedGivers = new HashSet<string>();
        
        // Set of expanded quest nodes in tree
        public HashSet<int> ExpandedQuests = new HashSet<int>();
        
        // Whether tree has been built at least once
        public bool TreeBuilt = false;
        
        // Whether to hide isolated/deprecated quests
        public bool HideIsolatedQuests = true;
        
        // Quest ID whose details panel is expanded (null = none)
        public int? SelectedQuestForDetails = null;
        
        // Current NPC being drawn (for cross-NPC dependency detection)
        public string CurrentDrawingGiver = "";
        #endregion
        
        #region Quest Data
        // All loaded quests as QuestInfo objects
        public List<QuestInfo> CachedQuests = new List<QuestInfo>();
        
        // Quick lookup: quest ID -> QuestInfo
        public Dictionary<int, QuestInfo> QuestById = new Dictionary<int, QuestInfo>();
        
        // Grouping: NPC name -> list of quest IDs
        public Dictionary<string, List<int>> QuestsByGiver = new Dictionary<string, List<int>>();
        
        // Quest children: quest ID -> list of quests that this unlocks
        public Dictionary<int, List<int>> QuestDependencies = new Dictionary<int, List<int>>();
        
        // Quest parents: quest ID -> list of quests required first
        public Dictionary<int, List<int>> QuestPrerequisites = new Dictionary<int, List<int>>();
        
        // Quests with no prerequisites (tree roots)
        public List<int> RootQuests = new List<int>();
        #endregion
        
        #region Runtime Quest Status
        // Currently active quest IDs (from QuestManager.ActiveQuests)
        public HashSet<int> ActiveQuestIds = new HashSet<int>();
        
        // Completed quest IDs (from QuestManager.HistoryQuests)
        public HashSet<int> CompletedQuestIds = new HashSet<int>();
        
        // Quests with no relations (likely deprecated/unused)
        public HashSet<int> IsolatedQuestIds = new HashSet<int>();
        #endregion
        
        #region Game System References
        // QuestCollection instance (ScriptableObject with all quest definitions)
        public object QuestCollection;
        public Type QuestCollectionType;
        public List<MethodInfo> QuestCollectionMethods = new List<MethodInfo>();
        
        // QuestManager instance (runtime quest controller singleton)
        public object QuestManager;
        public Type QuestManagerType;
        public List<string> QuestManagerMethods = new List<string>();
        public bool QuestManagerSearched = false;
        
        // Quest type info for reflection
        public Type QuestType;
        public List<string> QuestMethods = new List<string>();
        public List<string> QuestProperties = new List<string>();
        public List<string> QuestFields = new List<string>();
        #endregion
        
        #region Cache
        // Cached list of all scene names in build (for bugged scene detection)
        public List<string> CachedSceneNames = null;
        
        // Cached quest details for selected quest (prevents reflection every frame)
        public QuestDetailsCache QuestDetailsCache = null;
        
        // Whether to show all properties for selected quest
        public bool ShowAllQuestProperties = false;
        #endregion
        
        #region Log
        // Log messages for in-game display (limited to 200 entries)
        public List<string> LogMessages = new List<string>();
        #endregion
        
        #region Helper Methods
        // Clears all tree-related data for rebuilding
        // Called when "Build Tree" button is pressed
        // Returns: void
        public void ClearTreeData()
        {
            QuestDependencies.Clear();
            QuestPrerequisites.Clear();
            QuestById.Clear();
            QuestsByGiver.Clear();
            RootQuests.Clear();
            ExpandedGivers.Clear();
            ExpandedQuests.Clear();
            ActiveQuestIds.Clear();
            CompletedQuestIds.Clear();
            IsolatedQuestIds.Clear();
        }
        #endregion
    }
}
