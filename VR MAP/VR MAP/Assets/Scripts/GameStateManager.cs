using UnityEngine;
using System;
using System.Collections;
using UnityEngine.SceneManagement;


public class GameStateManager : MonoBehaviour
{
    // Énumération des différents états du jeu
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
    [Tooltip("1. Scene Manager contenant SeasonalSpawnManager et autres systèmes de jeu")]
    [SerializeField] private GameObject sceneManager;
    [Tooltip("2. Power Up Manager (gestion des power-ups)")]
    [SerializeField] private GameObject powerUpManager;
    [Tooltip("3. GameObject du joueur (PlayerController) - activé en dernier")]
    [SerializeField] private GameObject player;

    [Header("Camera References")]
    [Tooltip("Caméra utilisée pour le menu (fixe)")]
    [SerializeField] private Camera menuCamera;
    [Tooltip("Caméra du joueur (utilisée en jeu)")]
    [SerializeField] private Camera playerCamera;

    [Header("References")]
    [SerializeField] private LevelData levelData;

    [Header("Player Spawn")]
    [Tooltip("Position de spawn du joueur au démarrage de la partie")]
    [SerializeField] private Transform playerSpawnPoint;
    [Tooltip("Position par défaut si aucun spawn point n'est défini")]
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(0f, 1f, 0f);

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Game Result")]
    private bool hasWon = false;

    // Événement déclenché lors d'un changement d'état
    public event Action<GameState, GameState> OnStateChanged;

    private void Awake()
    {
        LogDebug("[GameStateManager] === AWAKE START ===");

        if (levelData == null)
            levelData = FindObjectOfType<LevelData>();

        // Configurer les caméras dès le départ
        ConfigureCameras();

        // Désactiver tous les GameObjects gérés au démarrage
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

    /// <summary>
    /// Configure les caméras pour qu'elles soient sur le même display avec les bonnes priorités
    /// </summary>
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
            LogDebug("[GameStateManager] ⚠️ Menu Camera est NULL");
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
            LogDebug("[GameStateManager] ⚠️ Player Camera est NULL");
        }
    }

    private void Start()
    {
        LogDebug("[GameStateManager] === START BEGIN ===");

        // Initialiser le jeu en mode Menu
        SetState(GameState.Menu);

        LogDebug("[GameStateManager] === START END ===");
    }

    #region State Transitions

    /// <summary>
    /// Change l'état actuel du jeu et déclenche l'événement
    /// </summary>
    private void SetState(GameState newState)
    {
        GameState previousState = currentState;
        currentState = newState;

        LogDebug($"[GameStateManager] ===== CHANGEMENT D'ÉTAT: {previousState} → {newState} =====");

        // Déclencher l'événement de changement d'état AVANT les mises à jour
        OnStateChanged?.Invoke(previousState, newState);

        // Mettre à jour les canvas
        UpdateCanvasVisibility();

        // Gérer l'activation séquentielle : Scene Manager → Power Up Manager → Player
        StartCoroutine(ActivateGameObjectsSequentially());

        // Gérer les caméras
        UpdateCameraState();

        // Gérer le curseur selon l'état
        UpdateCursorState();

        LogDebug($"[GameStateManager] ===== FIN CHANGEMENT D'ÉTAT =====");
    }

    /// <summary>
    /// Active les GameObjects dans l'ordre : Scene Manager → Power Up Manager → Player
    /// </summary>
    private IEnumerator ActivateGameObjectsSequentially()
    {
        bool shouldActivate = currentState == GameState.Game;

        if (shouldActivate)
        {
            LogDebug("[GameStateManager] Activation séquentielle des GameObjects...");

            // 1. Activer le Scene Manager (gestion des spawns)
            if (sceneManager != null)
            {
                sceneManager.SetActive(true);
                LogDebug($"[GameStateManager] ✓ Scene Manager activé: {sceneManager.name}");
                yield return null; // Attendre une frame
            }

            // 2. Activer le Power Up Manager
            if (powerUpManager != null)
            {
                powerUpManager.SetActive(true);
                LogDebug($"[GameStateManager] ✓ Power Up Manager activé: {powerUpManager.name}");
                yield return null; // Attendre une frame
            }

            // 3. Activer le joueur en dernier
            if (player != null)
            {
                player.SetActive(true);
                LogDebug($"[GameStateManager] ✓ Joueur activé: {player.name}");
                
                // ✅ CORRECTION : Reset APRÈS activation (pour que Start() soit appelé)
                yield return null; // Attendre que Start() du joueur soit exécuté
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

    /// <summary>
    /// Fonction appelée par le bouton Play du menu
    /// Passe en mode Game
    /// </summary>
    public void StartGame()
    {
        LogDebug("[GameStateManager] StartGame() appelé");

        // 1. ✅ Réinitialiser le niveau AVANT de reset les spawners
        if (levelData != null)
            levelData.level = 1;

        // 2. Détruire tous les ennemis restants
        CleanupEnemies();


        // 4. Changer d'état (active le Scene Manager)
        SetState(GameState.Game);
    }

    /// <summary>
    /// Passe en mode Game Over
    /// </summary>
    /// <param name="playerWon">True si le joueur a gagné, False s'il a perdu</param>
    public void TriggerGameOver(bool playerWon)
    {
        LogDebug($"[GameStateManager] TriggerGameOver({playerWon}) appelé");
        hasWon = playerWon;
        SetState(GameState.GameOver);
    }

    /// <summary>
    /// Fonction pour retourner au menu principal
    /// Appelée depuis le Game Over
    /// </summary>
    public void ReturnToMenu()
    {
        LogDebug("[GameStateManager] Reload de la scène");

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Fonction pour rejouer directement
    /// Appelée depuis le Game Over
    /// </summary>
    public void RestartGame()
    {
        LogDebug("[GameStateManager] Reload de la scène");

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


    /// <summary>
    /// ✅ NOUVEAU : Quitte l'application (appelé par le bouton Quit)
    /// </summary>
    public void QuitGame()
    {
        LogDebug("[GameStateManager] 🚪 QuitGame() appelé");

#if UNITY_EDITOR
        // En mode éditeur, arrêter le Play Mode
        UnityEditor.EditorApplication.isPlaying = false;
        LogDebug("[GameStateManager] ✅ Play Mode arrêté (Editor)");
#else
        // En build, fermer l'application
        Application.Quit();
        LogDebug("[GameStateManager] ✅ Application fermée");
#endif
    }

    /// <summary>
    /// Nettoie tous les ennemis de la scène
    /// </summary>
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

    /// <summary>
    /// ✅ Nettoie tous les spawners et reset le système de spawn
    /// </summary>
    private void ResetSpawners()
    {
        if (sceneManager == null)
        {
            LogDebug("[GameStateManager] ⚠️ Scene Manager NULL, impossible de reset les spawners");
            return;
        }

        // Trouver le SeasonalSpawnManager dans le Scene Manager
        SeasonalSpawnManager seasonalSpawnManager = sceneManager.GetComponentInChildren<SeasonalSpawnManager>();
        if (seasonalSpawnManager == null)
        {
            seasonalSpawnManager = sceneManager.GetComponent<SeasonalSpawnManager>();
        }

        if (seasonalSpawnManager != null)
        {
            // ✅ Un seul appel qui gère tout (saisonniers + miniboss)
            seasonalSpawnManager.ResetSpawns();
            LogDebug("[GameStateManager] ✅ Tous les spawners réinitialisés (saisonniers + miniboss)");
        }
        else
        {
            LogDebug("[GameStateManager] ⚠️ SeasonalSpawnManager introuvable");
        }
    }

    #endregion

    #region Canvas Management

    /// <summary>
    /// Met à jour la visibilité des canvas en fonction de l'état actuel
    /// </summary>
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

    /// <summary>
    /// Log l'état d'un canvas pour debug
    /// </summary>
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

    /// <summary>
    /// Active ou désactive complètement un canvas
    /// </summary>
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

    /// <summary>
    /// Gère les caméras actives selon l'état du jeu
    /// </summary>
    private void UpdateCameraState()
    {
        LogDebug($"[GameStateManager] UpdateCameraState() pour état: {currentState}");

        switch (currentState)
        {
            case GameState.Menu:
                // Menu : activer UNIQUEMENT la caméra menu
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
                // Jeu : activer UNIQUEMENT la caméra joueur
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
                // Game Over utilise la Menu Camera
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

        // IMPORTANT : Vérifier qu'une seule caméra avec AudioListener est active
        VerifyAudioListeners();
    }

    /// <summary>
    /// S'assure qu'un seul AudioListener est actif à la fois
    /// </summary>
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

    #endregion

    #region Cursor Management

    /// <summary>
    /// Gère l'état du curseur selon le mode de jeu
    /// - Affiche le prefab de curseur UI (CursorPrefabManager) en Menu et GameOver.
    /// - Masque le prefab et verrouille le curseur en Game.
    /// </summary>
    private void UpdateCursorState()
    {
        switch (currentState)
        {
            case GameState.Menu:
                // Menu : curseur visible et déverrouillé
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                LogDebug("[GameStateManager] 🖱️ Curseur DÉVERROUILLÉ (Menu)");

                // show prefab cursor attached to menu canvas if available
                if (CursorPrefabManager.Instance != null)
                {
                    Canvas menuC = menuCanvas != null ? menuCanvas.GetComponent<Canvas>() : null;
                    if (menuC != null)
                        CursorPrefabManager.Instance.SetParentCanvas(menuC);
                    CursorPrefabManager.Instance.Show();
                }
                break;

            case GameState.Game:
                // Jeu : curseur verrouillé et invisible (FPS)
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                LogDebug("[GameStateManager] 🖱️ Curseur VERROUILLÉ (Game)");

                // hide prefab cursor
                if (CursorPrefabManager.Instance != null)
                {
                    CursorPrefabManager.Instance.Hide();
                }
                break;

            case GameState.GameOver:
                // Game Over : curseur visible et déverrouillé (pour cliquer sur les boutons)
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                LogDebug("[GameStateManager] 🖱️ Curseur DÉVERROUILLÉ (GameOver)");

                // show prefab cursor attached to the active gameover canvas
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

    #endregion

    #region Getters

    /// <summary>
    /// Retourne l'état actuel du jeu
    /// </summary>
    public GameState GetCurrentState()
    {
        return currentState;
    }

    /// <summary>
    /// Vérifie si le joueur est en mode jeu
    /// </summary>
    public bool IsPlaying()
    {
        return currentState == GameState.Game;
    }

    #endregion

    #region Debug Helpers

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

    #endregion

    #region Player Reset

    /// <summary>
    /// ✅ Réinitialise le joueur (santé + position au spawn point)
    /// ATTENTION : Doit être appelé APRÈS que le joueur soit activé !
    /// </summary>
    private void ResetPlayer()
    {
        if (player == null)
        {
            LogDebug("[GameStateManager] ⚠️ Player est NULL, impossible de réinitialiser");
            return;
        }

        // 1. Réinitialiser les stats de santé
        PlayerStats playerStats = player.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.ResetStats();
            LogDebug("[GameStateManager] ✅ PlayerStats réinitialisés");
        }
        else
        {
            LogDebug("[GameStateManager] ⚠️ PlayerStats non trouvé sur le joueur");
        }

        // 2. Replacer le joueur au point de spawn
        Vector3 spawnPosition = GetSpawnPosition();
        
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.Respawn(spawnPosition);
            LogDebug($"[GameStateManager] ✅ Joueur replacé au spawn : {spawnPosition}");
        }
        else
        {
            // Fallback si pas de PlayerController
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                player.transform.position = spawnPosition;
                controller.enabled = true;
                LogDebug($"[GameStateManager] ✅ Joueur replacé au spawn (via CharacterController) : {spawnPosition}");
            }
            else
            {
                player.transform.position = spawnPosition;
                LogDebug($"[GameStateManager] ✅ Joueur replacé au spawn (via Transform) : {spawnPosition}");
            }
        }
    }

    /// <summary>
    /// Retourne la position de spawn (Transform ou valeur par défaut)
    /// </summary>
    private Vector3 GetSpawnPosition()
    {
        if (playerSpawnPoint != null)
        {
            LogDebug($"[GameStateManager] 📍 Utilisation du spawn point : {playerSpawnPoint.name}");
            return playerSpawnPoint.position;
        }
        else
        {
            LogDebug($"[GameStateManager] 📍 Utilisation de la position par défaut : {defaultSpawnPosition}");
            return defaultSpawnPosition;
        }
    }

    #endregion
}