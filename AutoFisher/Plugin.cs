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
    private bool _waitingForBite;
    private bool _eventsSubscribed;
    private const float ReactionTime = 0.1f;

    private void Awake()
    {
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
        _waitingForBite = false;
        if (_eventsSubscribed && FishingController.Instance != null)
        {
            FishingController.GameStateChange -= HandleStateChange;
            FishingController.IndicateFishingCrit -= HandleFishingCrit;
        }
        _eventsSubscribed = false;
    }

    private void HandleStateChange(MinigameState state)
    {
        Logger.LogDebug($"Fishing state changed to: {state}");

        switch (state)
        {
            case MinigameState.Setup:
                Logger.LogDebug("Fishing minigame setup detected");
                break;
                
            case MinigameState.Pregame:
                if (_waitingForBite) break;
                Logger.LogDebug("Fishing pregame state - player needs to click when fish bites");
                _waitingForBite = true;
                StartCoroutine(MonitorForBite());
                break;
                
            case MinigameState.Active:
                Logger.LogDebug("Fishing minigame active - automation ready");
                _waitingForBite = false;
                break;
                
            case MinigameState.Win:
                Logger.LogInfo("Fishing minigame won!");
                break;
                
            case MinigameState.Lose:
                Logger.LogWarning("Fishing minigame lost - this should not happen with automation");
                break;
                
            case MinigameState.Canceled:
                Logger.LogDebug("Fishing minigame was canceled");
                break;
                
            case MinigameState.InActive:
                Logger.LogDebug("Fishing minigame ended");
                break;
        }
    }
    
    private IEnumerator MonitorForBite()
    {
        if (_waitingForBite == false) yield break;
        
        while (_waitingForBite && FishingController.GameState == MinigameState.Pregame)
        {
            var activeRod = FishingRod.ActiveRod;
            if (activeRod?.line?.state == FishingLineBehaviour.LineState.Biting)
            {
                Logger.LogInfo("Bite detected, starting minigame!");
                FishingController.Instance.hooked = true;
                _waitingForBite = false;
                yield break;
            }
        
            yield return new WaitForSeconds(0.05f); // Check frequently
        }
    }

    private void HandleFishingCrit(FishingPattern.FishingCritType critType)
    {
        if (!_isEnabled.Value || FishingController.GameState != MinigameState.Active)
            return;

        Logger.LogDebug($"Fishing crit indicated: {critType}");

        // Only respond to Good crits to maximize success rate
        if (critType == FishingPattern.FishingCritType.Good)
        {
            StartCoroutine(DelayedResponse());
        }
        else if (critType == FishingPattern.FishingCritType.Bad)
        {
            Logger.LogDebug("Bad crit detected - avoiding input");
        }
        else if (critType == FishingPattern.FishingCritType.Miss)
        {
            Logger.LogDebug("Miss crit detected - no action needed");
        }
    }

    private IEnumerator DelayedResponse()
    {
        // Add a small delay to simulate human reaction time
        yield return new WaitForSeconds(ReactionTime);
        
        // Verify we're still in active state and respond
        if (FishingController.GameState == MinigameState.Active)
        {
            Logger.LogInfo("Simulating fishing input");
            SimulatePlayerInput();
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