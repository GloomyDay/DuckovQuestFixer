using QuestFixer.Logics;
using QuestFixer.Models;
using QuestFixer.UI;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuestFixer
{
    /// <summary>
    /// Quest Fixer - Main entry point
    /// Unity MonoBehaviour that initializes and controls the mod
    /// Inherits from Duckov.Modding.ModBehaviour for game integration
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        // Hotkey to toggle mod window visibility
        private readonly KeyCode _toggleKey = KeyCode.F8;
        
        // Prevents key repeat when holding F8
        private bool _keyWasPressed = false;
        
        // Central state container shared across all mod components
        private readonly ModState _state = new ModState();
        
        // Game input components to disable when UI is open
        private MonoBehaviour _charInput;
        private PlayerInput _playerInput;
        private MonoBehaviour _cursorManager;
        
        // Track if we're in gameplay (not main menu)
        private bool _isInGame = false;

        // Called once when mod is loaded
        // Logs startup message to both Unity console and in-game log
        // Returns: void
        private void Start()
        {
            ModLogger.Log(_state, "=== Quest Fixer loaded ===");
            ModLogger.Log(_state, "Press F8 to open/close menu (in-game only)");
        }

        // Called every frame
        // Handles F8 key press to toggle window visibility
        // On first window open, searches for game quest systems
        // Only allows UI when in gameplay (CharacterMainControl.Main exists)
        // Returns: void
        private void Update()
        {
            // Check if we're in gameplay (like DuckovMenu does)
            bool inGame = IsInGame();
            
            // If we left gameplay while window was open, close it
            if (!inGame && _isInGame && _state.ShowWindow)
            {
                ModLogger.Log(_state, "Returning to main menu - closing UI");
                CloseUI();
            }
            _isInGame = inGame;
            
            // Toggle window on F8 press (with debounce) - only if in game
            if (Input.GetKeyDown(_toggleKey) && !_keyWasPressed)
            {
                _keyWasPressed = true;
                
                if (!inGame)
                {
                    // Don't open in main menu
                    return;
                }
                
                _state.ShowWindow = !_state.ShowWindow;
                
                if (_state.ShowWindow)
                {
                    OpenUI();
                }
                else
                {
                    CloseUI();
                }
                
                ModLogger.Log(_state, $"Window: {(_state.ShowWindow ? "opened" : "closed")}");
            }
            
            // Reset key state when released
            if (Input.GetKeyUp(_toggleKey))
            {
                _keyWasPressed = false;
            }
            
            // Keep cursor unlocked while window is open
            if (_state.ShowWindow)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        
        /// <summary>
        /// Checks if player is in gameplay (not main menu)
        /// Uses CharacterMainControl.Main like DuckovMenu
        /// </summary>
        private bool IsInGame()
        {
            try
            {
                // Find CharacterMainControl type in all loaded assemblies
                Type charMainType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    charMainType = assembly.GetType("Duckov.Character.CharacterMainControl");
                    if (charMainType != null) break;
                }
                
                if (charMainType != null)
                {
                    var mainProp = charMainType.GetProperty("Main", BindingFlags.Static | BindingFlags.Public);
                    if (mainProp != null)
                    {
                        var main = mainProp.GetValue(null) as UnityEngine.Object;
                        return main != null;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            
            // Fallback: check if LevelManager exists (alternative way to detect gameplay)
            var levelManager = GameObject.Find("LevelConfig/LevelManager(Clone)");
            return levelManager != null;
        }
        
        // Opens UI and disables game input (like DuckovMenu)
        private void OpenUI()
        {
            // Unlock cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            
            // Disable CursorManager (game's cursor controller)
            var cursorManagerType = FindType("CursorManager");
            if (cursorManagerType != null)
            {
                var instanceProp = cursorManagerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp != null)
                {
                    _cursorManager = instanceProp.GetValue(null) as MonoBehaviour;
                    if (_cursorManager != null)
                    {
                        _cursorManager.enabled = false;
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    }
                }
            }
            
            // Disable CharacterInputControl (movement/shooting)
            var charInputType = FindType("CharacterInputControl");
            if (charInputType != null)
            {
                _charInput = FindObjectOfType(charInputType) as MonoBehaviour;
                if (_charInput != null)
                {
                    _charInput.enabled = false;
                    ModLogger.Log(_state, "CharacterInputControl disabled");
                }
            }
            
            // Disable PlayerInput (new input system)
            _playerInput = FindObjectOfType<PlayerInput>();
            if (_playerInput != null)
            {
                _playerInput.DeactivateInput();
                ModLogger.Log(_state, "PlayerInput disabled");
            }
            
            // Find quest systems on first open
            if (!_state.QuestManagerSearched)
            {
                _state.QuestManagerSearched = true;
                QuestSystemFinder.FindQuestSystems(_state, FindObjectOfType);
            }
        }
        
        // Closes UI and restores game input
        private void CloseUI()
        {
            // Re-enable CharacterInputControl
            if (_charInput != null)
            {
                _charInput.enabled = true;
                _charInput = null;
                ModLogger.Log(_state, "CharacterInputControl restored");
            }
            
            // Re-enable PlayerInput
            if (_playerInput != null)
            {
                _playerInput.ActivateInput();
                _playerInput = null;
                ModLogger.Log(_state, "PlayerInput restored");
            }
            
            // Re-enable CursorManager
            if (_cursorManager != null)
            {
                _cursorManager.enabled = true;
                _cursorManager = null;
            }
        }
        
        // Finds a type by name across all loaded assemblies
        private System.Type FindType(string typeName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == typeName) return type;
                    }
                }
                catch { }
            }
            return null;
        }

        // Called for IMGUI rendering
        // Draws main mod window if visible (centered, dark style like DuckovMenu)
        // Returns: void
        private void OnGUI()
        {
            if (!_state.ShowWindow) return;
            
            // Apply dark skin
            ApplyDarkSkin();
            
            // Centered window (similar size to DuckovMenu)
            float windowWidth = 900;
            float windowHeight = 600;
            float x = (Screen.width - windowWidth) / 2;
            float y = (Screen.height - windowHeight) / 2;
            _state.WindowRect = new Rect(x, y, windowWidth, windowHeight);
            
            _state.WindowRect = GUI.Window(GetInstanceID(), _state.WindowRect, DrawWindow, "Quest Fixer");
        }
        
        // Applies dark visual style similar to DuckovMenu
        private void ApplyDarkSkin()
        {
            // Dark window background
            GUI.backgroundColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            GUI.contentColor = Color.white;
            
            // Modify default skin for dark theme
            var skin = GUI.skin;
            
            // Window style
            skin.window.normal.background = MakeTex(2, 2, new Color(0.12f, 0.12f, 0.15f, 1f));
            skin.window.normal.textColor = Color.white;
            skin.window.onNormal.background = skin.window.normal.background;
            
            // Button style  
            skin.button.normal.background = MakeTex(2, 2, new Color(0.25f, 0.25f, 0.3f, 1f));
            skin.button.normal.textColor = Color.white;
            skin.button.hover.background = MakeTex(2, 2, new Color(0.35f, 0.35f, 0.4f, 1f));
            skin.button.active.background = MakeTex(2, 2, new Color(0.2f, 0.4f, 0.6f, 1f));
            
            // Label style
            skin.label.normal.textColor = Color.white;
            
            // Box style
            skin.box.normal.background = MakeTex(2, 2, new Color(0.18f, 0.18f, 0.22f, 1f));
            skin.box.normal.textColor = Color.white;
            
            // TextField style
            skin.textField.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.12f, 1f));
            skin.textField.normal.textColor = Color.white;
            skin.textField.focused.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.2f, 1f));
            skin.textField.focused.textColor = Color.white;
            
            // ScrollView style
            skin.scrollView.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.12f, 1f));
        }
        
        // Creates a solid color texture for UI backgrounds
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        // Draws the window content
        // Contains status bar, control buttons, and delegates to MainWindow for tabs
        // windowId: Unity window ID for IMGUI
        // Returns: void
        private void DrawWindow(int windowId)
        {
            var oldColor = GUI.color;
            
            GUILayout.BeginVertical();

            // Status indicator
            GUI.color = _state.QuestCollection != null ? Color.green : Color.red;
            GUILayout.Label(_state.QuestCollection != null ? 
                $"QuestCollection: OK | Quests: {_state.CachedQuests.Count}" : 
                "QuestCollection: NOT FOUND", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUI.color = oldColor;

            // Control buttons row
            GUILayout.BeginHorizontal();
            
            GUILayout.FlexibleSpace();
            
            // Service menu toggle button (next to Close button)
            string serviceIcon = _state.ShowServiceMenu ? "v" : ">";
            if (GUILayout.Button($"{serviceIcon} Service Functions", GUILayout.Height(25), GUILayout.Width(150)))
            {
                _state.ShowServiceMenu = !_state.ShowServiceMenu;
            }
            
            if (GUILayout.Button("Close [F8]", GUILayout.Height(25), GUILayout.Width(120)))
            {
                _state.ShowWindow = false;
                CloseUI();
            }
            GUILayout.EndHorizontal();
            
            // Service menu panel (expandable)
            if (_state.ShowServiceMenu)
            {
                GUI.backgroundColor = new Color(0.2f, 0.2f, 0.25f, 1f);
                GUILayout.BeginHorizontal(GUI.skin.box);
                
                if (GUILayout.Button("Find System", GUILayout.Height(22)))
                {
                    _state.QuestManagerSearched = false;
                    _state.QuestManager = null;
                    QuestSystemFinder.FindQuestSystems(_state, FindObjectOfType);
                }
                if (GUILayout.Button("Refresh Quests", GUILayout.Height(22)))
                {
                    QuestLoader.LoadQuests(_state);
                }
                if (GUILayout.Button("Refresh Statuses", GUILayout.Height(22)))
                {
                    QuestTreeBuilder.LoadQuestStatuses(_state);
                }
                
                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }

            // Tab content (delegated to MainWindow)
            MainWindow.Draw(_state, windowId);

            GUILayout.EndVertical();
            
            // Make window draggable by title bar
            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }
    }
}
