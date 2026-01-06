using System;

namespace QuestFixer.Models
{
    /// <summary>
    /// Data container for a single quest - holds extracted quest information
    /// Used throughout the mod for displaying and manipulating quests
    /// </summary>
    public class QuestInfo
    {
        // Quest unique identifier (e.g., "1504", "331")
        public string Id { get; set; }
        
        // Localized quest name for display
        public string Name { get; set; }
        
        // Human-readable status string ("Complete", "Active", "Inactive")
        public string Status { get; set; }
        
        // Whether quest is marked as completed in game data
        public bool IsCompleted { get; set; }
        
        // Whether quest is currently active (in player's quest log)
        public bool IsActive { get; set; }
        
        // Type of the quest object for reflection operations
        public Type QuestType { get; set; }
        
        // Reference to the actual game quest object for direct manipulation
        public object RawQuest { get; set; }
    }
}
