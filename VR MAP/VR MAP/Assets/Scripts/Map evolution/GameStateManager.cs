using UnityEngine;
using System;
using System.Collections;
using UnityEngine.SceneManagement;


public class GameStateManager : MonoBehaviour
{
    public enum GameState
    {
        Menu,
        Game,
        GameOver
    }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Menu;


    [Header("Canvas References")]
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private GameObject gameCanvas;
    [SerializeField] private GameObject powerUpCanvas;
    [SerializeField] private GameObject gameOverWinCanvas;
    [SerializeField] private GameObject gameOverLoseCanvas;

    [Header("Game Objects - Ordre d'activation")]
    [SerializeField] private GameObject sceneManager;
    [SerializeField] private GameObject powerUpManager;
    [SerializeField] private GameObject player;

    [Header("Camera References")]
    [SerializeField] private Camera menuCamera;
    [SerializeField] private Camera playerCamera;

    [Header("References")]
    [SerializeField] private LevelData levelData;

    [Header("Player Spawn")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(0f, 1f, 0f);

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Game Result")]
    private bool hasWon = false;

    public event Action<GameState, GameState> OnStateChanged;

    private void Awake()
    {
        LogDebug("[GameStateManager] === AWAKE START ===");

        if (levelData == null)
            levelData = FindObjectOfType<LevelData>();

        ConfigureCameras();

        if (sceneManager != null)
        {
            sceneManager.SetActive(false);
            LogDebug($"[GameStateManager] Scene Manager désactivé au démarrage: {sceneManager.name}");
        }

        if (powerUpManager != null)
        {
            powerUpManager.SetActive(false);
            LogDebug($"[GameStateManager] Power Up Manager désactivé au démarrage: {powerUpManager.name}");
        }

        if (player != null)
        {
            player.SetActive(false);
            LogDebug($"[GameStateManager] Joueur désactivé au démarrage: {player.name}");
        }

        LogDebug("[GameStateManager] === AWAKE END ===");
    }


    private void ConfigureCameras()
    {
        if (menuCamera != null)
        {
            menuCamera.targetDisplay = 0;
            menuCamera.depth = 0;
            menuCamera.gameObject.SetActive(true);
            menuCamera.enabled = true;
            
            AudioListener menuListener = menuCamera.GetComponent<AudioListener>();
            if (menuListener != null)
                menuListener.enabled = true;
            
            LogDebug($"[GameStateManager] Menu Camera configurée: {menuCamera.name}");
        }
        else
        {
            LogDebug("[GameStateManager] Menu Camera est NULL");
        }

        if (playerCamera != null)
        {
            playerCamera.targetDisplay = 0;
            playerCamera.depth = 0;
            playerCamera.enabled = false;
            
            AudioListener playerListener = playerCamera.GetComponent<AudioListener>();
            if (playerListener != null)
                playerListener.enabled = false;
            
            LogDebug($"[GameStateManager] Player Camera configurée: {playerCamera.name}");
        }
        else
        {
            LogDebug("[GameStateManager] Player Camera est NULL");
        }
    }

    private void Start()
    {
        SetState(GameState.Menu);
    }



    private void SetState(GameState newState)
    {
        GameState previousState = currentState;
        currentState = newState;

        LogDebug($"[GameStateManager] ===== CHANGEMENT D'ÉTAT: {previousState} → {newState} =====");

        UpdateTimeScale(newState);

        OnStateChanged?.Invoke(previousState, newState);

        UpdateCanvasVisibility();

        StartCoroutine(ActivateGameObjectsSequentially());

        UpdateCameraState();

        UpdateCursorState();

        LogDebug($"[GameStateManager] ===== FIN CHANGEMENT D'ÉTAT =====");
    }

    private void UpdateTimeScale(GameState newState)
    {
        switch (newState)
        {
            case GameState.Menu:
                Time.timeScale = 1f; 
                LogDebug("[GameStateManager] Time.timeScale = 1 (Menu)");
                break;

            case GameState.Game:
                Time.timeScale = 1f; 
                LogDebug("[GameStateManager] Time.timeScale = 1 (Game)");
                break;

            case GameState.GameOver:
                Time.timeScale = 0f;
                LogDebug("[GameStateManager] Time.timeScale = 0 (GameOver - PAUSE)");
                break;
        }
    }

    public void TriggerGameOver(bool playerWon)
    {
        LogDebug($"[GameStateManager] TriggerGameOver({playerWon}) appelé");
        
        Time.timeScale = 0f;
        LogDebug("[GameStateManager] TEMPS ARRÊTÉ (timeScale = 0)");
        
        hasWon = playerWon;
        SetState(GameState.GameOver);
    }

 
    public void ReturnToMenu()
    {
        LogDebug("[GameStateManager] Retour au menu - Nettoyage avant reload");
        
        Time.timeScale = 1f;
        LogDebug("[GameStateManager] Temps restauré (timeScale = 1)");
        
        CleanupBeforeSceneReload();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


    public void RestartGame()
    {
        LogDebug("[GameStateManager] Restart game - Nettoyage avant reload");
        
        Time.timeScale = 1f;
        LogDebug("[GameStateManager] Temps restauré (timeScale = 1)");
        
        CleanupBeforeSceneReload();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


    private void CleanupBeforeSceneReload()
    {
        LogDebug("[GameStateManager] Nettoyage avant reload de scène...");

        if (player != null)
        {
            player.SetActive(false);
            LogDebug("[GameStateManager] Joueur désactivé");
        }

        if (powerUpManager != null)
        {
            powerUpManager.SetActive(false);
            LogDebug("[GameStateManager] Power Up Manager désactivé");
        }

        if (sceneManager != null)
        {
            sceneManager.SetActive(false);
            LogDebug("[GameStateManager] Scene Manager désactivé");
        }

        SetCanvasActive(menuCanvas, false);
        SetCanvasActive(gameCanvas, false);
        SetCanvasActive(powerUpCanvas, false);
        SetCanvasActive(gameOverWinCanvas, false);
        SetCanvasActive(gameOverLoseCanvas, false);

        LogDebug("[GameStateManager] Nettoyage terminé");
    }

    public void QuitGame()
    {
        LogDebug("[GameStateManager] QuitGame() appelé");


        Application.Quit();
        LogDebug("[GameStateManager] Application fermée");
    }

    private void CleanupEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in enemies)
        {
            if (enemy != null)
                Destroy(enemy);
        }

        LogDebug($"[GameStateManager] {enemies.Length} ennemis détruits");
    }

    private void ResetSpawners()
    {
        if (sceneManager == null)
        {
            LogDebug("[GameStateManager] Scene Manager NULL, impossible de reset les spawners");
            return;
        }


        SeasonalSpawnManager seasonalSpawnManager = sceneManager.GetComponentInChildren<SeasonalSpawnManager>();
        if (seasonalSpawnManager == null)
        {
            seasonalSpawnManager = sceneManager.GetComponent<SeasonalSpawnManager>();
        }

        if (seasonalSpawnManager != null)
        {
            seasonalSpawnManager.ResetSpawns();
            LogDebug("[GameStateManager] Tous les spawners réinitialisés (saisonniers + miniboss)");
        }
        else
        {
            LogDebug("[GameStateManager] SeasonalSpawnManager introuvable");
        }
    }



    private void UpdateCanvasVisibility()
    {
        LogDebug($"[GameStateManager] UpdateCanvasVisibility() pour état: {currentState}");

        switch (currentState)
        {
            case GameState.Menu:
                LogDebug("[GameStateManager] → Activation UNIQUEMENT du Menu Canvas");
                SetCanvasActive(menuCanvas, true);
                SetCanvasActive(gameCanvas, false);
                SetCanvasActive(powerUpCanvas, false);
                SetCanvasActive(gameOverWinCanvas, false);
                SetCanvasActive(gameOverLoseCanvas, false);
                break;

            case GameState.Game:
                LogDebug("[GameStateManager] → Activation du Game Canvas + Power Up Canvas");
                SetCanvasActive(menuCanvas, false);
                SetCanvasActive(gameCanvas, true);
                SetCanvasActive(powerUpCanvas, true);
                SetCanvasActive(gameOverWinCanvas, false);
                SetCanvasActive(gameOverLoseCanvas, false);
                break;

            case GameState.GameOver:
                SetCanvasActive(menuCanvas, false);
                SetCanvasActive(gameCanvas, false);
                SetCanvasActive(powerUpCanvas, false);

                PlayerStats ps = GameObject.FindObjectOfType<PlayerStats>(true);
                float score= ps != null ? ps.GetScore() : 0f;
                if (hasWon)
                {
                    gameOverWinCanvas.GetComponent<GameOverScore>()?.SetScore(score);
                    SetCanvasActive(gameOverWinCanvas, true);
                    SetCanvasActive(gameOverLoseCanvas, false);
                }
                else
                {
                    gameOverLoseCanvas.GetComponent<GameOverScore>()?.SetScore(score);

                    LogDebug("[GameStateManager] → Activation UNIQUEMENT du GameOver Lose Canvas");
                    SetCanvasActive(gameOverWinCanvas, false);
                    SetCanvasActive(gameOverLoseCanvas, true);
                }
                break;
        }

        // Log final
        LogDebug($"[GameStateManager] État final des Canvas:");
        LogCanvasState(menuCanvas, "Menu");
        LogCanvasState(gameCanvas, "Game");
        LogCanvasState(powerUpCanvas, "PowerUp");
        LogCanvasState(gameOverWinCanvas, "GameOverWin");
        LogCanvasState(gameOverLoseCanvas, "GameOverLose");
    }


    private void LogCanvasState(GameObject canvasObj, string name)
    {
        if (canvasObj == null)
        {
            LogDebug($"  [{name}] NULL");
            return;
        }

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        bool canvasEnabled = canvas != null ? canvas.enabled : false;
        LogDebug($"  [{name}] GameObject:{canvasObj.activeSelf}, Canvas.enabled:{canvasEnabled}");
    }


    private void SetCanvasActive(GameObject canvasObject, bool active)
    {
        if (canvasObject == null)
        {
            LogDebug($"  - Canvas NULL, ignoré");
            return;
        }

        canvasObject.SetActive(active);

        if (active)
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            if (canvas != null)
                canvas.enabled = true;

            UnityEngine.UI.GraphicRaycaster raycaster = canvasObject.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = true;
        }

        LogDebug($"  - Canvas '{canvasObject.name}' → GameObject.SetActive({active})");
    }


    private void UpdateCameraState()
    {
        LogDebug($"[GameStateManager] UpdateCameraState() pour état: {currentState}");

        switch (currentState)
        {
            case GameState.Menu:
                if (menuCamera != null)
                {
                    menuCamera.enabled = true;
                    LogDebug($"  - Menu Camera ACTIVÉE");
                }
                if (playerCamera != null)
                {
                    playerCamera.enabled = false;
                    LogDebug($"  - Player Camera DÉSACTIVÉE");
                }
                break;

            case GameState.Game:
                if (menuCamera != null)
                {
                    menuCamera.enabled = false;
                    LogDebug($"  - Menu Camera DÉSACTIVÉE");
                }
                if (playerCamera != null)
                {
                    playerCamera.enabled = true;
                    LogDebug($"  - Player Camera ACTIVÉE");
                }
                break;

            case GameState.GameOver:
                if (menuCamera != null)
                {
                    menuCamera.enabled = true;
                    LogDebug($"  - Menu Camera ACTIVÉE (GameOver)");
                }
                
                if (playerCamera != null)
                {
                    playerCamera.enabled = false;
                    LogDebug($"  - Player Camera DÉSACTIVÉE (GameOver)");
                }
                
                LogDebug($"  - Caméras GameOver configurées (Menu Camera active)");
                break;
        }

        VerifyAudioListeners();
    }


    private void VerifyAudioListeners()
    {
        AudioListener menuListener = menuCamera?.GetComponent<AudioListener>();
        AudioListener playerListener = playerCamera?.GetComponent<AudioListener>();

        bool menuActive = (currentState == GameState.Menu || currentState == GameState.GameOver);
        bool gameActive = (currentState == GameState.Game);

        if (menuListener != null)
        {
            menuListener.enabled = menuActive;
            LogDebug($"  - Menu AudioListener → {(menuActive ? "ACTIVÉ" : "DÉSACTIVÉ")}");
        }

        if (playerListener != null)
        {
            playerListener.enabled = gameActive;
            LogDebug($"  - Player AudioListener → {(gameActive ? "ACTIVÉ" : "DÉSACTIVÉ")}");
        }
    }


    private void UpdateCursorState()
    {
        switch (currentState)
        {
            case GameState.Menu:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                LogDebug("[GameStateManager] Curseur DÉVERROUILLÉ (Menu)");

                if (CursorPrefabManager.Instance != null)
                {
                    Canvas menuC = menuCanvas != null ? menuCanvas.GetComponent<Canvas>() : null;
                    if (menuC != null)
                        CursorPrefabManager.Instance.SetParentCanvas(menuC);
                    CursorPrefabManager.Instance.Show();
                }
                break;

            case GameState.Game:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                LogDebug("[GameStateManager] Curseur VERROUILLÉ (Game)");

                if (CursorPrefabManager.Instance != null)
                {
                    CursorPrefabManager.Instance.Hide();
                }
                break;

            case GameState.GameOver:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                LogDebug("[GameStateManager] Curseur DÉVERROUILLÉ (GameOver)");

                if (CursorPrefabManager.Instance != null)
                {
                    GameObject goCanvasObj = hasWon ? gameOverWinCanvas : gameOverLoseCanvas;
                    Canvas goCanvas = goCanvasObj != null ? goCanvasObj.GetComponent<Canvas>() : null;
                    if (goCanvas != null)
                        CursorPrefabManager.Instance.SetParentCanvas(goCanvas);
                    CursorPrefabManager.Instance.Show();
                }
                break;
        }
    }



 
    public GameState GetCurrentState()
    {
        return currentState;
    }


    public bool IsPlaying()
    {
        return currentState == GameState.Game;
    }



    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }

    [ContextMenu("Debug: Afficher état Canvas")]
    private void DebugShowCanvasState()
    {
        Debug.Log("=== ÉTAT DES CANVAS ===");
        DebugCanvas(menuCanvas, "Menu Canvas");
        DebugCanvas(gameCanvas, "Game Canvas");
        DebugCanvas(powerUpCanvas, "PowerUp Canvas");
        DebugCanvas(gameOverWinCanvas, "GameOver Win Canvas");
        DebugCanvas(gameOverLoseCanvas, "GameOver Lose Canvas");
    }

    private void DebugCanvas(GameObject canvasObj, string name)
    {
        if (canvasObj == null)
        {
            Debug.Log($"{name}: NULL");
            return;
        }

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        Debug.Log($"{name}:");
        Debug.Log($"  - GameObject active: {canvasObj.activeSelf}");
        Debug.Log($"  - Canvas enabled: {(canvas != null ? canvas.enabled.ToString() : "N/A")}");
    }

    [ContextMenu("Debug: Afficher état complet")]
    private void DebugShowCurrentState()
    {
        Debug.Log("=== ÉTAT ACTUEL DU GAMESTATEMANAGER ===");
        Debug.Log($"État: {currentState}");
        Debug.Log($"Curseur: LockState={Cursor.lockState}, Visible={Cursor.visible}");
        Debug.Log($"Scene Manager: {(sceneManager != null ? sceneManager.name + " (active: " + sceneManager.activeSelf + ")" : "NULL")}");
        Debug.Log($"Power Up Manager: {(powerUpManager != null ? powerUpManager.name + " (active: " + powerUpManager.activeSelf + ")" : "NULL")}");
        Debug.Log($"Player: {(player != null ? player.name + " (active: " + player.activeSelf + ")" : "NULL")}");
        Debug.Log($"Menu Camera: {(menuCamera != null ? menuCamera.name + " (enabled: " + menuCamera.enabled + ")" : "NULL")}");
        Debug.Log($"Player Camera: {(playerCamera != null ? playerCamera.name + " (enabled: " + playerCamera.enabled + ")" : "NULL")}");
        
        DebugShowCanvasState();
    }



    private void ResetPlayer()
    {
        if (player == null)
        {
            LogDebug("[GameStateManager] Player est NULL, impossible de réinitialiser");
            return;
        }

        PlayerStats playerStats = player.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.ResetStats();
            LogDebug("[GameStateManager] PlayerStats réinitialisés");
        }
        else
        {
            LogDebug("[GameStateManager] PlayerStats non trouvé sur le joueur");
        }

        // 2. Replacer le joueur au point de spawn
        Vector3 spawnPosition = GetSpawnPosition();
        
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.Respawn(spawnPosition);
            LogDebug($"[GameStateManager] Joueur replacé au spawn : {spawnPosition}");
        }
        else
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                player.transform.position = spawnPosition;
                controller.enabled = true;
                LogDebug($"[GameStateManager] Joueur replacé au spawn (via CharacterController) : {spawnPosition}");
            }
            else
            {
                player.transform.position = spawnPosition;
                LogDebug($"[GameStateManager] Joueur replacé au spawn (via Transform) : {spawnPosition}");
            }
        }
    }

    private Vector3 GetSpawnPosition()
    {
        if (playerSpawnPoint != null)
        {
            LogDebug($"[GameStateManager] Utilisation du spawn point : {playerSpawnPoint.name}");
            return playerSpawnPoint.position;
        }
        else
        {
            LogDebug($"[GameStateManager] Utilisation de la position par défaut : {defaultSpawnPosition}");
            return defaultSpawnPosition;
        }
    }


    public void SetWaveTransitionMode(bool isInTransition)
    {
        if (enableDebugLogs)
            Debug.Log($"[GameStateManager] SetWaveTransitionMode: {isInTransition}");

        if (isInTransition)
        {
            SetCanvasActive(gameCanvas, false);
            
            if (enableDebugLogs)
                Debug.Log("[GameStateManager] Canvas de jeu masqué pour transition de vague");
        }
        else
        {
            if (currentState == GameState.Game)
            {
                SetCanvasActive(gameCanvas, true);
                
                if (enableDebugLogs)
                    Debug.Log("[GameStateManager] Canvas de jeu réaffiché après transition");
            }
        }
    }


    private IEnumerator ActivateGameObjectsSequentially()
    {
        bool shouldActivate = currentState == GameState.Game;

        if (shouldActivate)
        {
            LogDebug("[GameStateManager] Activation séquentielle des GameObjects...");

            if (sceneManager != null)
            {
                sceneManager.SetActive(true);
                LogDebug($"[GameStateManager] ✓ Scene Manager activé: {sceneManager.name}");
                yield return null; 
            }

            if (powerUpManager != null)
            {
                powerUpManager.SetActive(true);
                LogDebug($"[GameStateManager] ✓ Power Up Manager activé: {powerUpManager.name}");
                yield return null; 
            }

            if (player != null)
            {
                player.SetActive(true);
                LogDebug($"[GameStateManager] ✓ Joueur activé: {player.name}");
                
                yield return null; 
                ResetPlayer();
            }

            LogDebug("[GameStateManager] Activation séquentielle terminée !");
        }
        else
        {
            if (menuCamera != null)
                menuCamera.enabled = true;

            UpdateCameraState();

            if (player != null) player.SetActive(false);
            if (powerUpManager != null) powerUpManager.SetActive(false);
            if (sceneManager != null) sceneManager.SetActive(false);
        }
    }


    public void StartGame()
    {
        LogDebug("[GameStateManager] StartGame() appelé");

        if (levelData != null)
            levelData.level = 1;

        CleanupEnemies();

        SetState(GameState.Game);
    }
}