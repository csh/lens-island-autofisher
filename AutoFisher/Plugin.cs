using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using Minigame;
using Minigame.Fishing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoFisher;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private ConfigEntry<bool> _isEnabled;
    private bool _inGameScene;
    private bool _eventsSubscribed;
    private const float ReactionTime = 0.1f;

    private void Awake()
    {
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} is loaded!");

        _isEnabled = Config.Bind("AutoFisher", "Enabled", true, "Enable/disable fishing automation");

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        _inGameScene = arg0.name is not ("MainMenu" or "LoadingScreen" or "Boot" or "Intro");
        
        CleanupAutomation(); // Always cleanup first
        
        if (_inGameScene)
        {
            SetupAutomation();
        }
    }

    private void SetupAutomation()
    {
        if (!_isEnabled.Value || _eventsSubscribed) return;

        // Wait for FishingController to be available
        StartCoroutine(WaitForFishingController());
    }

    private IEnumerator WaitForFishingController()
    {
        yield return new WaitUntil(() => FishingController.Instance);

        if (_eventsSubscribed) yield break; // Prevent double subscription

        Logger.LogInfo("FishingController instance found, subscribing to events");
        
        // Subscribe to state change events
        FishingController.GameStateChange += HandleStateChange;
        FishingController.IndicateFishingCrit += HandleFishingCrit;
        _eventsSubscribed = true;
    }

    private void CleanupAutomation()
    {
        if (_eventsSubscribed && FishingController.Instance != null)
        {
            FishingController.GameStateChange -= HandleStateChange;
            FishingController.IndicateFishingCrit -= HandleFishingCrit;
        }
        _eventsSubscribed = false;
    }

    private void HandleStateChange(MinigameState state)
    {
        Logger.LogInfo($"Fishing state changed to: {state}");

        switch (state)
        {
            case MinigameState.Setup:
                Logger.LogInfo("Fishing minigame setup detected");
                break;
                
            case MinigameState.Pregame:
                Logger.LogInfo("Fishing pregame state - player needs to click when fish bites");
                break;
                
            case MinigameState.Active:
                Logger.LogInfo("Fishing minigame active - automation ready");
                break;
                
            case MinigameState.Win:
                Logger.LogInfo("Fishing minigame won!");
                break;
                
            case MinigameState.Lose:
                Logger.LogWarning("Fishing minigame lost - this should not happen with automation");
                break;
                
            case MinigameState.Canceled:
                Logger.LogInfo("Fishing minigame was canceled");
                break;
                
            case MinigameState.InActive:
                Logger.LogInfo("Fishing minigame ended");
                break;
        }
    }

    private void HandleFishingCrit(FishingPattern.FishingCritType critType)
    {
        if (!_isEnabled.Value || FishingController.GameState != MinigameState.Active)
            return;

        Logger.LogInfo($"Fishing crit indicated: {critType}");

        // Only respond to Good crits to maximize success rate
        if (critType == FishingPattern.FishingCritType.Good)
        {
            StartCoroutine(DelayedResponse());
        }
        else if (critType == FishingPattern.FishingCritType.Bad)
        {
            Logger.LogInfo("Bad crit detected - avoiding input");
        }
        else if (critType == FishingPattern.FishingCritType.Miss)
        {
            Logger.LogInfo("Miss crit detected - no action needed");
        }
    }

    private IEnumerator DelayedResponse()
    {
        // Add a small delay to simulate human reaction time
        yield return new WaitForSeconds(ReactionTime);
        
        // Verify we're still in active state and respond
        if (FishingController.GameState == MinigameState.Active)
        {
            SimulatePlayerInput();
            Logger.LogInfo("Responded to Good crit");
        }
    }

    private void SimulatePlayerInput()
    {
        try
        {
            // Find the fishing state and call the Reel method
            var player = Singleton<Player>.Instance;
            if (player?.currentState is Lenstate_Fishing fishingState)
            {
                fishingState.Reel();
            }
            else
            {
                Logger.LogWarning("Player is not in fishing state when trying to reel");
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"Failed to simulate player input: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CleanupAutomation();
    }
}