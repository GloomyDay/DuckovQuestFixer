using System.Collections.Generic;

namespace QuestFixer.Models
{
    /// <summary>
    /// Cached quest details to prevent reflection on every frame
    /// Populated once when user clicks "?" button on a quest
    /// </summary>
    public class QuestDetailsCache
    {
        // Quest ID this cache is for
        public int QuestId;
        
        // Basic info
        public string QuestGiver = "";
        public string Description = "";
        public int RequireLevel = 0;
        public bool LockInDemo = false;
        public int RequiredItemId = 0;
        public int RequiredItemCount = 0;
        public string RequireSceneId = "";
        
        // Player state at cache time
        public int PlayerLevel = -1;
        public string CurrentScene = "";
        
        // Tasks
        public List<TaskInfo> Tasks = new List<TaskInfo>();
        
        // All properties for "show all" view
        public List<PropertyDisplay> AllProperties = new List<PropertyDisplay>();
        
        // Blockers list
        public List<string> Blockers = new List<string>();
    }
    
    /// <summary>
    /// Cached task information
    /// </summary>
    public class TaskInfo
    {
        public string Text = "";
        public string Progress = "";
        public bool IsFinished = false;
    }
    
    /// <summary>
    /// Property display for "show all" view
    /// </summary>
    public class PropertyDisplay
    {
        public string Name;
        public string Value;
        public string Type; // "property", "field", "method"
    }
}

