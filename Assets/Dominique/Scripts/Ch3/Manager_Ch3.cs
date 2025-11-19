using System.Collections;
using UnityEngine;
using TMPro;

public class Manager_Ch3 : MonoBehaviour
{
    private const int TOTAL = 25; // indices 0..24

    [Header("UI & Audio")]
    public TMP_Text dialogText;
    public AudioSource audioSource;
    public AudioClip checkSound;
    public AudioClip winningSound;

    [Header("Dialog Content (exactly 25)")]
    public AudioClip[] dialogClips = new AudioClip[TOTAL];
    public string[] dialogLines = new string[TOTAL];

    [Header("Autoplay Timing")]
    public float defaultDelay = 5f;
    public float[] perLineDelays = new float[TOTAL];

    [Header("Chapter 3 Props")]
    public GameObject chart;
    public GameObject numberUI;
    public GameObject table;
    public GameObject CompoundX;
    public GameObject SeedUI;

    [Header("UI")]
    public GameObject continueButton;
    public GameObject fiveWeeksButton;
    public GameObject dialogUIPanel;

    [Header("Dialog 18-24: Bar-Line-Graph-UI Panel")]
    public GameObject barLineGraphUIPanel;
    public GameObject lineGraphButton;
    public GameObject barGraphButton;
    public GameObject quizButton;
    
    [Header("Dialog 21-24: Quiz UI Panel")]
    public GameObject quizUIPanel;
    public GameObject correctAnswerUIPanel;
    public GameObject tryAgainUIPanel;
    
    [Header("Graph Objects")]
    public GameObject worldLineGraph;
   // public GameObject peaHighUI;
//    public GameObject redotLineGraph;
    public GameObject barGraph;

    /* ───────── Index 9 gating ───────── */
    [Header("Index 9 Gating")]
    public CompoundXPot[] pots = new CompoundXPot[6];

    [Tooltip("6 checkmark objects that appear when each pot is satisfied.")]
    public GameObject[] checks = new GameObject[6];

    /* ───────── Index 12 Growing Plant Sequence ───────── */
    [Header("Index 12: Growing Plant Sequence")]
    [Tooltip("Objects the player sees normally; will be hidden during the growth FX, then shown again.")]
    public GameObject visibleRoot;

    [Tooltip("The plant object to reveal during growth.")]
    public GameObject plantsObj;

    [Tooltip("Compound X visual root to hide when plant appears.")]
    public GameObject compoundXObj;

    [Tooltip("Seed object to hide when plant appears.")]
    public GameObject seedObj;

    [Tooltip("FairyCenter transform to change during dialog index 12.")]
    public Transform fairyCenter;

    [Tooltip("FairyMove to Right transform target for dialog index 12.")]
    public Transform fairyMoveToRight;

    [Tooltip("Duration for smooth fairy movement to the right position.")]
    public float fairyMoveDuration = 2f;

    [Header("Ruler System")]
    [Tooltip("Ruler object to show at dialog index 15 and hide at index 17.")]
    public GameObject ruler;

    [Tooltip("Ruler snapping area to show at dialog index 15 and hide at index 17.")]
    public GameObject rulerSnappingArea;
    
    [Tooltip("SnapRuler component reference (for hiding peaHeightUI). If null, will try to find it automatically.")]
    public SnapRuler snapRuler;

    [Tooltip("AudioSource used to play the growing SFX.")]
    public AudioSource sfxSource;

    [Tooltip("Clip to play during the growth sequence.")]
    public AudioClip growClip;

    [Tooltip("Temporary black skybox material used during growth blackout.")]
    public Material blackSkyboxMaterial;

    [Tooltip("Seconds to wait before starting growth once index 12 finishes.")]
    public float index12InitialDelay = 5f;

    [Tooltip("Seconds to wait after swapping objects before restoring visibility.")]
    public float index12MidWait = 3f;

    [Tooltip("How long to lerp the skybox back to the original material.")]
    public float skyboxFadeDuration = 2f;

    /* ───────── Internals ───────── */
    readonly bool[] played = new bool[TOTAL];      // hard guard: which indices already played
    Coroutine autoplayRoutine;
    Coroutine lineRoutine;                         // single in-flight PlayLine
    private bool isLinePlaying;            // reentrancy guard
    private bool hasReachedIndex9;
    private bool[] previousCheckStates = new bool[6];
    private int currentDialogIndex = -1;

    // skybox cache
    private Material originalSkyboxMat;
    private bool originalHasExposure;
    private float originalExposureValue;
    private bool originalHasTint;
    private Color originalTintColor;

    /* ───────── Unity ───────── */
    void Awake()
    {
        // Basic validation (helps catch inspector mistakes early)
        if (dialogClips == null || dialogClips.Length != TOTAL ||
            dialogLines == null || dialogLines.Length != TOTAL)
        {
            Debug.LogError($"Manager_Ch3: Need EXACTLY {TOTAL} clips and {TOTAL} lines.");
            enabled = false;
            return;
        }
        if (perLineDelays == null || perLineDelays.Length != TOTAL)
            perLineDelays = new float[TOTAL];

        if (audioSource)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            if (audioSource.volume <= 0f) audioSource.volume = 1f;
        }

        if (chart) chart.SetActive(false);
        if (numberUI) numberUI.SetActive(false);

        if (checks != null)
            foreach (var c in checks)
                if (c) c.SetActive(false);

        // Hide ruler system initially
        if (ruler) ruler.SetActive(false);
        if (rulerSnappingArea) rulerSnappingArea.SetActive(false);

        // Cache original skybox info
        originalSkyboxMat = RenderSettings.skybox;
        if (originalSkyboxMat != null)
        {
            originalHasExposure = originalSkyboxMat.HasProperty("_Exposure");
            if (originalHasExposure) originalExposureValue = originalSkyboxMat.GetFloat("_Exposure");

            originalHasTint = originalSkyboxMat.HasProperty("_Tint");
            if (originalHasTint) originalTintColor = originalSkyboxMat.GetColor("_Tint");
        }
    }

    void Start()
    {
        if (pots != null)
        {
            for (int i = 0; i < pots.Length; i++)
                if (pots[i] != null)
                    pots[i].OnStatusChanged += UpdateChecksUI;
        }

        if (continueButton) continueButton.SetActive(false);
        if (fiveWeeksButton) fiveWeeksButton.SetActive(false);
        
        // Hide Bar-Line-Graph-UI panel initially
        if (barLineGraphUIPanel) barLineGraphUIPanel.SetActive(false);
        if (lineGraphButton) lineGraphButton.SetActive(false);
        if (barGraphButton) barGraphButton.SetActive(false);
        if (quizButton) quizButton.SetActive(false);
        
        // Hide Quiz UI panels initially
        if (quizUIPanel) quizUIPanel.SetActive(false);
        if (correctAnswerUIPanel) correctAnswerUIPanel.SetActive(false);
        if (tryAgainUIPanel) tryAgainUIPanel.SetActive(false);
        
        // Hide graph objects initially
        if (worldLineGraph) worldLineGraph.SetActive(false);
  //      if (peaHighUI) peaHighUI.SetActive(false);
 //       if (redotLineGraph) redotLineGraph.SetActive(false);
        if (barGraph) barGraph.SetActive(false);

        // Set skybox to black at start
        SetSkyboxToBlack();

        autoplayRoutine = StartCoroutine(AutoPlayAll());
    }

    /* ───────── Public API ───────── */

    /// <summary>Request to play a specific index from outside (e.g., SnapRuler triggers 17).</summary>
    public void PlayDialogByIndex(int index)
    {
        if (!IsValidIndex(index)) return;
        if (played[index])
        {
            Debug.Log($"Manager_Ch3: Index {index} already played. Ignoring.");
            return;
        }
        if (isLinePlaying)
        {
            Debug.Log($"Manager_Ch3: A line is currently playing (idx {currentDialogIndex}). Ignoring request for {index}.");
            return;
        }
        // stop autoplay if it's still running (external request takes control)
        StopAutoplay();

        lineRoutine = StartCoroutine(PlayLine(index));
    }

    public void PlayDialogIndex12()
    {
        PlayDialogByIndex(12);
    }

    /// <summary>
    /// Force play a dialog index, bypassing order checks and stopping autoplay (useful for grab-triggered dialogs)
    /// </summary>
    public void ForcePlayDialogByIndex(int index)
    {
        if (!IsValidIndex(index)) return;
        if (played[index])
        {
            Debug.Log($"Manager_Ch3: Index {index} already played. Ignoring ForcePlayDialogByIndex request.");
            return;
        }
        
        // Stop autoplay if it's still running (external request takes control)
        StopAutoplay();
        
        // Stop any currently playing line
        if (lineRoutine != null)
        {
            StopCoroutine(lineRoutine);
            lineRoutine = null;
            isLinePlaying = false;
            currentDialogIndex = -1;
        }
        
        lineRoutine = StartCoroutine(PlayLine(index));
    }

    /// <summary>
    /// Called when X is placed in a pot. Updates the checks UI.
    /// </summary>
    public void NotifyXPlaced(XPotTrigger potTrigger)
    {
        if (potTrigger == null) return;
        
        Debug.Log($"[Manager_Ch3] NotifyXPlaced called for pot {potTrigger.gameObject.name}. HasCorrectAmount: {potTrigger.HasCorrectAmount}");
        
        // Update checks UI to reflect current state
        UpdateChecksUI();
    }

    public void StopAutoplay()
    {
        if (autoplayRoutine != null)
        {
            StopCoroutine(autoplayRoutine);
            autoplayRoutine = null;
        }
        if (audioSource) audioSource.Stop();
    }

    public void OnFiveWeeksButtonClicked()
    {
        if (fiveWeeksButton) fiveWeeksButton.SetActive(false);
        if (dialogUIPanel)  dialogUIPanel.SetActive(false);
        if (blackSkyboxMaterial)
        {
            RenderSettings.skybox = blackSkyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }
        StartCoroutine(Index11To12Sequence());
    }

    public void OnContinueButtonClicked()
    {
        if (sfxSource && growClip)
        {
            sfxSource.loop = false;
            sfxSource.Stop();
            sfxSource.clip = growClip;
            sfxSource.time = 0f;
            sfxSource.Play();
        }
    }

    public void OnLineGraphButtonClicked()
    {
        // Show line graph objects
        worldLineGraph.SetActive(true);
//       if (peaHighUI) peaHighUI.SetActive(true);
//        if (redotLineGraph) redotLineGraph.SetActive(true);
        
        // Hide bar graph
        if (barGraph) barGraph.SetActive(false);
        
        // Hide peaHeightUI (ruler measurement UI) when showing line graph
        SnapRuler rulerToUse = snapRuler;
        if (rulerToUse == null)
        {
            // Try to find it if not assigned
            rulerToUse = FindFirstObjectByType<SnapRuler>();
            if (rulerToUse == null)
            {
                // Try finding inactive objects too
                SnapRuler[] allRulers = FindObjectsByType<SnapRuler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (allRulers != null && allRulers.Length > 0)
                {
                    rulerToUse = allRulers[0];
                }
            }
        }
        
        if (rulerToUse != null)
        {
            rulerToUse.HidePeaHeightUI();
            Debug.Log("<color=green>Manager_Ch3: Called HidePeaHeightUI() on SnapRuler when showing line graph</color>");
        }
        else
        {
            Debug.LogWarning("<color=orange>Manager_Ch3: SnapRuler not found in scene when trying to hide peaHeightUI. Please assign it in the Inspector.</color>");
        }
        
        // Hide UI panel and show quiz button
       // if (barLineGraphUIPanel) barLineGraphUIPanel.SetActive(false);
       // if (quizButton) quizButton.SetActive(true);
        
        // Activate dialog 19
        PlayDialogByIndex(19);
    }

    public void OnBarGraphButtonClicked()
    {
        // Show bar graph
        barGraph.SetActive(true);
        
        // Hide line graph objects
        if (worldLineGraph) worldLineGraph.SetActive(false);
 //       if (peaHighUI) peaHighUI.SetActive(false);
 //       if (redotLineGraph) redotLineGraph.SetActive(false);
        
        // Show peaHeightUI (ruler measurement UI) when showing bar graph
        SnapRuler rulerToUse = snapRuler;
        if (rulerToUse == null)
        {
            // Try to find it if not assigned
            rulerToUse = FindFirstObjectByType<SnapRuler>();
            if (rulerToUse == null)
            {
                // Try finding inactive objects too
                SnapRuler[] allRulers = FindObjectsByType<SnapRuler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (allRulers != null && allRulers.Length > 0)
                {
                    rulerToUse = allRulers[0];
                }
            }
        }
        
        if (rulerToUse != null)
        {
            rulerToUse.ShowPeaHeightUI();
            Debug.Log("<color=green>Manager_Ch3: Called ShowPeaHeightUI() on SnapRuler when showing bar graph</color>");
        }
        else
        {
            Debug.LogWarning("<color=orange>Manager_Ch3: SnapRuler not found in scene when trying to show peaHeightUI. Please assign it in the Inspector.</color>");
        }
        
        // Hide UI panel and show quiz button
     //   if (barLineGraphUIPanel) barLineGraphUIPanel.SetActive(false);
     //   if (quizButton) quizButton.SetActive(true);
        
        // Activate dialog 20
        PlayDialogByIndex(20);
    }

    public void OnQuizButtonClicked()
    {
        quizUIPanel.SetActive(true);
        if (barLineGraphUIPanel) barLineGraphUIPanel.SetActive(false);
        if (barGraph) barGraph.SetActive(false);
        if (worldLineGraph) worldLineGraph.SetActive(false);
        
        // Setup Quiz UI panel for interaction (same as barLineGraphUIPanel)
        if (quizUIPanel)
        {
            // Activate the panel and ensure all parents are active
            Transform parent = quizUIPanel.transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }
            
            // Setup canvas and input for the panel
            SetupUIForInteraction(quizUIPanel);
            
            // Setup all buttons inside the Quiz panel for interaction
            UnityEngine.UI.Button[] buttons = quizUIPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var button in buttons)
            {
                if (button != null && button.gameObject != null)
                {
                    SetupUIForInteraction(button.gameObject);
                }
            }
        }
        
        // Activate dialog 21
        PlayDialogByIndex(21);
    }

    public void OnQuizLineGraphButtonClicked()
    {
        // Hide Quiz UI panel and show Correct Answer UI panel
        if (quizUIPanel) quizUIPanel.SetActive(false);
        if (worldLineGraph) worldLineGraph.SetActive(true);
        if (correctAnswerUIPanel)
        {
            correctAnswerUIPanel.SetActive(true);
            
            // Setup Correct Answer UI panel for interaction
            Transform parent = correctAnswerUIPanel.transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }
            
            // Setup canvas and input for the panel
            SetupUIForInteraction(correctAnswerUIPanel);
            
            // Setup all buttons inside the Correct Answer panel for interaction
            UnityEngine.UI.Button[] buttons = correctAnswerUIPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var button in buttons)
            {
                if (button != null && button.gameObject != null)
                {
                    SetupUIForInteraction(button.gameObject);
                }
            }
        }
        
        // Activate dialog 22
        PlayDialogByIndex(22);
    }

    public void OnQuizBarGraphButtonClicked()
    {
        // Hide Quiz UI panel and show Try Again UI panel
        if (quizUIPanel) quizUIPanel.SetActive(false);
        if (worldLineGraph) worldLineGraph.SetActive(false);
        if (barGraph) barGraph.SetActive(true);
        if (tryAgainUIPanel)
        {
            tryAgainUIPanel.SetActive(true);
            
            // Setup Try Again UI panel for interaction
            Transform parent = tryAgainUIPanel.transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }
            
            // Setup canvas and input for the panel
            SetupUIForInteraction(tryAgainUIPanel);
            
            // Setup all buttons inside the Try Again panel for interaction
            UnityEngine.UI.Button[] buttons = tryAgainUIPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var button in buttons)
            {
                if (button != null && button.gameObject != null)
                {
                    SetupUIForInteraction(button.gameObject);
                }
            }
        }
        
        // Activate dialog 23
        PlayDialogByIndex(23);
    }

    public void OnCorrectAnswerNextButtonClicked()
    {
        // Hide Correct Answer UI panel
        if (correctAnswerUIPanel) correctAnswerUIPanel.SetActive(false);
        
        // Activate dialog 24
        PlayDialogByIndex(24);
    }

    public void OnBackToQuizButtonClicked()
    {
        if (worldLineGraph) worldLineGraph.SetActive(false);
        if (barGraph) barGraph.SetActive(false);
        // Hide Try Again UI panel and show Quiz UI panel
        if (tryAgainUIPanel) tryAgainUIPanel.SetActive(false);
        if (quizUIPanel)
        {
            quizUIPanel.SetActive(true);
            
            // Setup Quiz UI panel for interaction again (in case it was reset)
            Transform parent = quizUIPanel.transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }
            
            // Setup canvas and input for the panel
            SetupUIForInteraction(quizUIPanel);
            
            // Setup all buttons inside the Quiz panel for interaction
            UnityEngine.UI.Button[] buttons = quizUIPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var button in buttons)
            {
                if (button != null && button.gameObject != null)
                {
                    SetupUIForInteraction(button.gameObject);
                }
            }
        }
        
        // Activate dialog 21
        PlayDialogByIndex(21);
    }

    /* ───────── Core flows ───────── */

    IEnumerator AutoPlayAll()
    {
        // Standard autoplay 0..11, then pause at 11
        for (int i = 0; i < TOTAL; i++)
        {
            // Stop autoplay at index 11 and show 5weeksButton
            if (i == 11)
            {
                yield return PlayLine(i);
                if (fiveWeeksButton)
                {
                    // Activate the button and ensure all parents are active
                    fiveWeeksButton.SetActive(true);
                    
                    // Setup canvas and input for the button
                    SetupUIForInteraction(fiveWeeksButton);
                    
                    // Start detailed debugging coroutine
                    StartCoroutine(DebugButtonClickability(fiveWeeksButton));
                }
                autoplayRoutine = null;
                yield break; // stop here
            }

            // normal step
            yield return PlayLine(i);

            float delay = perLineDelays[i] > 0f ? perLineDelays[i] : defaultDelay;
            if (delay > 0f && i < TOTAL - 1)
                yield return new WaitForSeconds(delay);
        }

        if (continueButton) continueButton.SetActive(true);
        autoplayRoutine = null;
    }

    IEnumerator ContinueRange(int startInclusive, int endInclusive)
    {
        for (int i = Mathf.Max(0, startInclusive); i <= Mathf.Min(TOTAL - 1, endInclusive); i++)
        {
            if (played[i]) continue; // skip any already-played line just in case
            yield return PlayLine(i);

            if (i < endInclusive)
            {
                float delay = perLineDelays[i] > 0f ? perLineDelays[i] : defaultDelay;
                if (delay > 0f) yield return new WaitForSeconds(delay);
            }
        }

        if (endInclusive >= TOTAL - 1 && continueButton) continueButton.SetActive(true);
    }

    IEnumerator Index11To12Sequence()
    {
        if (visibleRoot) visibleRoot.SetActive(false);

        // Note: comment said 5s previously; your latest code used 3s. Keeping 3s.
        yield return new WaitForSeconds(3f);

        if (dialogUIPanel) dialogUIPanel.SetActive(true);

        // Play 12, then growth, then continue 13..16
        yield return PlayLine(12);
        yield return GrowingSequenceIndex12();
        yield return ContinueRange(13, 16);
    }

    /* ───────── Single-line player (now debounced) ───────── */

    IEnumerator PlayLine(int idx)
    {
        if (!IsValidIndex(idx)) yield break;
        if (played[idx])
        {
            Debug.Log($"Manager_Ch3: PlayLine({idx}) ignored; already played.");
            yield break;
        }
        if (isLinePlaying)
        {
            Debug.LogWarning($"Manager_Ch3: PlayLine reentry guarded. Currently playing {currentDialogIndex}, asked for {idx}.");
            yield break;
        }

        isLinePlaying = true;
        currentDialogIndex = idx;

        // Text
        if (dialogText) dialogText.text = dialogLines[idx];

        // Audio (or fallback)
        if (audioSource && dialogClips[idx])
        {
            audioSource.Stop();
            audioSource.clip = dialogClips[idx];
            audioSource.time = 0f;
            audioSource.Play();
            yield return new WaitForSeconds(audioSource.clip.length);
        }
        else
        {
            yield return new WaitForSeconds(3f);
        }

        // Mark played BEFORE any post-actions to guard reentry
        played[idx] = true;

        // Post actions & gates
        PostLineActions(idx);

        // Special moments
        if (idx == 9)
        {
            hasReachedIndex9 = true;
            if (pots != null && pots.Length > 0 && pots[0] != null && pots[0].requiredCount == 0)
                pots[0].ForceSatisfied();

            yield return WaitForAllPotsSatisfied();
        }
        else if (idx == 10)
        {
            if (audioSource && winningSound) audioSource.PlayOneShot(winningSound);
        }
        else if (idx == 17)
        {
            // IMPORTANT: do NOT play 17 again here.
            // Stop autoplay at 17 - dialogs 18-24 are only triggered by button presses
            // No automatic continuation
        }

        isLinePlaying = false;
        currentDialogIndex = -1;
    }

    /* ───────── Post-line visuals ───────── */
    void PostLineActions(int idx)
    {
        if (idx == 7)
        {
            if (chart) chart.SetActive(true);
        }
        else if (idx == 9)
        {
            if (chart) chart.SetActive(false);
            if (numberUI) numberUI.SetActive(true);
        }
        else if (idx == 12)
        {
            if (numberUI) numberUI.SetActive(false);
            if (table)    table.SetActive(false);
            if (CompoundX) CompoundX.SetActive(false);
            if (SeedUI)   SeedUI.SetActive(false);

            if (checks != null)
                foreach (var check in checks)
                    if (check) check.SetActive(false);
        }
        else if (idx == 15)
        {
            if (ruler) ruler.SetActive(true);
            if (rulerSnappingArea) rulerSnappingArea.SetActive(true);
        }
        else if (idx == 17)
        {
            if (ruler) ruler.SetActive(false);
            if (rulerSnappingArea) rulerSnappingArea.SetActive(false);
            // Show Bar-Line-Graph-UI panel when dialog 17 is reached
            if (barLineGraphUIPanel)
            {
                // Activate the panel and ensure all parents are active
                barLineGraphUIPanel.SetActive(true);
                Transform parent = barLineGraphUIPanel.transform.parent;
                while (parent != null)
                {
                    if (!parent.gameObject.activeSelf)
                    {
                        parent.gameObject.SetActive(true);
                    }
                    parent = parent.parent;
                }
                
                // Setup canvas and input for the panel
                SetupUIForInteraction(barLineGraphUIPanel);
            }
            
            // Setup buttons for interaction
            if (lineGraphButton)
            {
                lineGraphButton.SetActive(true);
                SetupUIForInteraction(lineGraphButton);
            }
            if (barGraphButton)
            {
                barGraphButton.SetActive(true);
                SetupUIForInteraction(barGraphButton);
            }
        }
        else if (idx == 21)
        {
            // Quiz UI panel is already shown by OnQuizButtonClicked() - no need to show it again
        }
    }

    /* ───────── Gates & helpers ───────── */
    
    /// <summary>
    /// Sets up a UI GameObject for interaction by ensuring parent hierarchy is active,
    /// assigning camera to World Space Canvas, enabling GraphicRaycaster, and enabling mouse input.
    /// </summary>
    void SetupUIForInteraction(GameObject uiObject)
    {
        if (uiObject == null) return;
        
        // Activate parent hierarchy if needed
        Transform parent = uiObject.transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
            {
                parent.gameObject.SetActive(true);
            }
            parent = parent.parent;
        }
        
        // Find and assign camera to World Space Canvas for mouse clicks to work
        Canvas canvas = uiObject.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            if (canvas.worldCamera == null)
            {
                // Try to find the main camera
                Camera mainCam = Camera.main;
                if (mainCam == null)
                {
                    mainCam = FindFirstObjectByType<Camera>();
                }
                if (mainCam != null)
                {
                    canvas.worldCamera = mainCam;
                    Debug.Log($"<color=green>Manager_Ch3: Assigned camera '{mainCam.name}' to World Space Canvas '{canvas.name}'</color>");
                }
                else
                {
                    Debug.LogWarning("Manager_Ch3: No camera found to assign to World Space Canvas!");
                }
            }
            
            // Ensure GraphicRaycaster is enabled and configured
            UnityEngine.UI.GraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = true;
                Debug.Log($"<color=green>Manager_Ch3: GraphicRaycaster on '{canvas.name}' is enabled</color>");
            }
            else
            {
                Debug.LogWarning($"Manager_Ch3: No GraphicRaycaster found on Canvas '{canvas.name}'!");
            }
        }
        
        // Ensure button's graphic has raycast target enabled (if it's a button)
        UnityEngine.UI.Button button = uiObject.GetComponent<UnityEngine.UI.Button>();
        if (button != null)
        {
            if (button.targetGraphic != null)
            {
                button.targetGraphic.raycastTarget = true;
                Debug.Log($"<color=green>Manager_Ch3: Button '{uiObject.name}' raycast target enabled</color>");
            }
            
            // Ensure button is interactable
            if (!button.interactable)
            {
                button.interactable = true;
                Debug.Log($"<color=yellow>Manager_Ch3: Button '{uiObject.name}' was not interactable, enabling it</color>");
            }
        }
        
        // Disable XR Interactable component if present (for PC mode compatibility)
        var xrInteractable = uiObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (xrInteractable != null)
        {
            // Don't disable it completely, but ensure it doesn't block UI clicks
            // The XR Interactable should only work in VR mode
            Debug.Log($"<color=cyan>Manager_Ch3: Found XR Interactable on '{uiObject.name}' - will work in VR, UI buttons should work in PC mode</color>");
        }
        
        // Check EventSystem and ensure mouse input is enabled
        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
        {
            Debug.Log($"<color=green>Manager_Ch3: EventSystem found: '{eventSystem.name}'</color>");
            
            // Check for XR UI Input Module and ensure mouse input is enabled
            var xrInputModule = eventSystem.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            if (xrInputModule != null)
            {
                // Use reflection to check/enable mouse input since it might be a private field
                var enableMouseInputField = typeof(UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule)
                    .GetField("m_EnableMouseInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (enableMouseInputField != null)
                {
                    bool currentValue = (bool)enableMouseInputField.GetValue(xrInputModule);
                    if (!currentValue)
                    {
                        enableMouseInputField.SetValue(xrInputModule, true);
                        Debug.Log($"<color=yellow>Manager_Ch3: Enabled mouse input on XRUIInputModule</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=green>Manager_Ch3: Mouse input already enabled on XRUIInputModule</color>");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("Manager_Ch3: No EventSystem found in scene!");
        }
    }
    
    IEnumerator WaitForAllPotsSatisfied()
    {
        if (pots == null || pots.Length == 0) yield break;
        while (!AllPotsAreSatisfied()) yield return null;
        UpdateChecksUI();
    }

    bool AllPotsAreSatisfied()
    {
        for (int i = 0; i < pots.Length; i++)
        {
            var p = pots[i];
            if (p == null) continue;
            if (!p.isX) return false;
        }
        return true;
    }

    void UpdateChecksUI()
    {
        if (pots == null || checks == null) return;
        int n = Mathf.Min(pots.Length, checks.Length);
        for (int i = 0; i < n; i++)
        {
            if (!checks[i]) continue;

            bool shouldBeActive = (i == 0)
                ? hasReachedIndex9 && pots[i] != null && pots[i].isX
                : (pots[i] != null && pots[i].isX);

            // sfx when becoming active
            if (shouldBeActive && !previousCheckStates[i])
                if (audioSource && checkSound) audioSource.PlayOneShot(checkSound);

            checks[i].SetActive(shouldBeActive);
            previousCheckStates[i] = shouldBeActive;
        }
    }

    /* ───────── Index 12 Growth ───────── */
    IEnumerator GrowingSequenceIndex12()
    {
        if (index12InitialDelay > 0f)
            yield return new WaitForSeconds(index12InitialDelay);

        if (visibleRoot) visibleRoot.SetActive(false);

        if (sfxSource && growClip)
        {
            sfxSource.loop = false;
            sfxSource.Stop();
            sfxSource.clip = growClip;
            sfxSource.time = 0f;
            sfxSource.Play();
        }

        if (plantsObj)    plantsObj.SetActive(true);
        if (compoundXObj) compoundXObj.SetActive(false);
        if (seedObj)      seedObj.SetActive(false);

        if (fairyCenter && fairyMoveToRight)
            yield return SmoothMoveFairy();

        if (index12MidWait > 0f)
            yield return new WaitForSeconds(index12MidWait);

        if (visibleRoot) visibleRoot.SetActive(true);
        yield return LerpSkyboxBack();
    }

    IEnumerator LerpSkyboxBack()
    {
        if (originalSkyboxMat == null)
        {
            RenderSettings.skybox = null;
            DynamicGI.UpdateEnvironment();
            yield break;
        }

        RenderSettings.skybox = originalSkyboxMat;
        DynamicGI.UpdateEnvironment();

        float t = 0f;
        float dur = Mathf.Max(0.01f, skyboxFadeDuration);

        bool canFadeExposure = originalHasExposure;
        float startExposure = 0.0f;
        float endExposure   = canFadeExposure ? originalExposureValue : 1.0f;

        bool canFadeTint = originalHasTint;
        Color startTint = Color.black;
        Color endTint   = canFadeTint ? originalTintColor : Color.white;

        if (canFadeExposure) originalSkyboxMat.SetFloat("_Exposure", startExposure);
        if (canFadeTint)     originalSkyboxMat.SetColor("_Tint", startTint);

        while (t < dur)
        {
            float a = t / dur;

            if (canFadeExposure)
                originalSkyboxMat.SetFloat("_Exposure", Mathf.Lerp(startExposure, endExposure, a));

            if (canFadeTint)
                originalSkyboxMat.SetColor("_Tint", Color.Lerp(startTint, endTint, a));

            t += Time.deltaTime;
            yield return null;
        }

        if (canFadeExposure) originalSkyboxMat.SetFloat("_Exposure", endExposure);
        if (canFadeTint)     originalSkyboxMat.SetColor("_Tint", endTint);

        DynamicGI.UpdateEnvironment();
    }

    IEnumerator SmoothMoveFairy()
    {
        if (!fairyCenter || !fairyMoveToRight) yield break;

        Vector3 startLocalPosition = fairyCenter.localPosition;
        Quaternion startLocalRotation = fairyCenter.localRotation;

        Vector3 targetLocalPosition = fairyMoveToRight.localPosition;
        Quaternion targetLocalRotation = fairyMoveToRight.localRotation;

        float elapsedTime = 0f;
        float duration = Mathf.Max(0.1f, fairyMoveDuration);

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            t = t * t * (3f - 2f * t); // SmoothStep

            fairyCenter.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, t);
            fairyCenter.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        fairyCenter.localPosition = targetLocalPosition;
        fairyCenter.localRotation = targetLocalRotation;
    }

    /* ───────── Utilities ───────── */
    bool IsValidIndex(int idx) => idx >= 0 && idx < TOTAL;
    
    /// <summary>
    /// Sets the skybox to black using the blackSkyboxMaterial.
    /// </summary>
    public void SetSkyboxToBlack()
    {
        if (blackSkyboxMaterial != null)
        {
            RenderSettings.skybox = blackSkyboxMaterial;
            DynamicGI.UpdateEnvironment();
            Debug.Log("<color=cyan>Manager_Ch3: Skybox set to black</color>");
        }
        else
        {
            Debug.LogWarning("<color=orange>Manager_Ch3: blackSkyboxMaterial is not assigned. Cannot set skybox to black.</color>");
        }
    }
    
    IEnumerator DebugButtonClickability(GameObject buttonObj)
    {
        if (buttonObj == null) yield break;
        
        UnityEngine.UI.Button button = buttonObj.GetComponent<UnityEngine.UI.Button>();
        Canvas canvas = buttonObj.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode == RenderMode.WorldSpace ? canvas.worldCamera : Camera.main;
        
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        
        Debug.Log($"<color=cyan>=== BUTTON CLICKABILITY DEBUG START ===</color>");
        Debug.Log($"<color=cyan>Button: {buttonObj.name}</color>");
        Debug.Log($"<color=cyan>Button Active: {buttonObj.activeSelf}</color>");
        Debug.Log($"<color=cyan>Button ActiveInHierarchy: {buttonObj.activeInHierarchy}</color>");
        
        if (button != null)
        {
            Debug.Log($"<color=cyan>Button Component: Found</color>");
            Debug.Log($"<color=cyan>Button Interactable: {button.interactable}</color>");
            Debug.Log($"<color=cyan>Button Enabled: {button.enabled}</color>");
            if (button.targetGraphic != null)
            {
                Debug.Log($"<color=cyan>Button Target Graphic: {button.targetGraphic.name}</color>");
                Debug.Log($"<color=cyan>Button Raycast Target: {button.targetGraphic.raycastTarget}</color>");
            }
        }
        else
        {
            Debug.LogWarning($"<color=red>Button Component: NOT FOUND!</color>");
        }
        
        if (canvas != null)
        {
            Debug.Log($"<color=cyan>Canvas: {canvas.name}</color>");
            Debug.Log($"<color=cyan>Canvas Render Mode: {canvas.renderMode}</color>");
            Debug.Log($"<color=cyan>Canvas World Camera: {(canvas.worldCamera != null ? canvas.worldCamera.name : "NULL")}</color>");
            Debug.Log($"<color=cyan>Canvas Enabled: {canvas.enabled}</color>");
            
            UnityEngine.UI.GraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
            {
                Debug.Log($"<color=cyan>GraphicRaycaster: Found</color>");
                Debug.Log($"<color=cyan>GraphicRaycaster Enabled: {raycaster.enabled}</color>");
            }
            else
            {
                Debug.LogWarning($"<color=red>GraphicRaycaster: NOT FOUND!</color>");
            }
        }
        
        if (cam != null)
        {
            Debug.Log($"<color=cyan>Camera: {cam.name}</color>");
            Debug.Log($"<color=cyan>Camera Position: {cam.transform.position}</color>");
            Debug.Log($"<color=cyan>Camera Forward: {cam.transform.forward}</color>");
        }
        else
        {
            Debug.LogWarning($"<color=red>Camera: NOT FOUND!</color>");
        }
        
        // Check button position in world and screen space
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        if (buttonRect != null && cam != null)
        {
            Vector3 worldPos = buttonRect.position;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            Vector3 viewportPos = cam.WorldToViewportPoint(worldPos);
            
            Debug.Log($"<color=cyan>Button World Position: {worldPos}</color>");
            Debug.Log($"<color=cyan>Button Screen Position: {screenPos}</color>");
            Debug.Log($"<color=cyan>Button Viewport Position: {viewportPos}</color>");
            Debug.Log($"<color=cyan>Button On Screen: {viewportPos.x >= 0 && viewportPos.x <= 1 && viewportPos.y >= 0 && viewportPos.y <= 1 && viewportPos.z > 0}</color>");
            Debug.Log($"<color=cyan>Button Distance from Camera: {Vector3.Distance(cam.transform.position, worldPos):F2}m</color>");
        }
        
        // Monitor for clicks for 30 seconds
        float debugDuration = 30f;
        float elapsed = 0f;
        int clickCount = 0;
        
        // Add a temporary click listener to detect if button is being clicked
        if (button != null)
        {
            UnityEngine.Events.UnityAction clickAction = () => {
                clickCount++;
                Debug.Log($"<color=green>*** BUTTON CLICKED! Count: {clickCount} ***</color>");
            };
            button.onClick.AddListener(clickAction);
            
            Debug.Log($"<color=cyan>=== Monitoring button clicks for {debugDuration} seconds ===</color>");
            Debug.Log($"<color=cyan>Move mouse over button and click to test</color>");
            
            while (elapsed < debugDuration)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
                
                // Check mouse position and raycast every frame
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0))
                {
                    Vector3 mousePos = Input.mousePosition;
                    Debug.Log($"<color=yellow>Mouse Click Detected at Screen: {mousePos}</color>");
                    
                    // Check if EventSystem detects UI
                    UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
                    if (es != null)
                    {
                        var pointerData = new UnityEngine.EventSystems.PointerEventData(es);
                        pointerData.position = mousePos;
                        
                        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                        es.RaycastAll(pointerData, results);
                        
                        Debug.Log($"<color=yellow>EventSystem Raycast Results: {results.Count} hits</color>");
                        bool buttonHit = false;
                        GameObject blockingObject = null;
                        
                        for (int i = 0; i < results.Count; i++)
                        {
                            var result = results[i];
                            Debug.Log($"<color=yellow>  [{i}] GameObject: {result.gameObject.name}, Distance: {result.distance:F2}</color>");
                            
                            // Check if this is the button or a child of the button
                            if (result.gameObject == buttonObj || result.gameObject.transform.IsChildOf(buttonObj.transform))
                            {
                                buttonHit = true;
                                Debug.Log($"<color=green>  *** BUTTON HIT IN RAYCAST! ***</color>");
                            }
                            
                            // Check if something is blocking the button (closer than button)
                            if (i == 0 && result.distance < 1.0f && result.gameObject != buttonObj && !result.gameObject.transform.IsChildOf(buttonObj.transform))
                            {
                                blockingObject = result.gameObject;
                                Debug.Log($"<color=orange>  ⚠ POTENTIAL BLOCKER: {result.gameObject.name} at distance {result.distance:F2}</color>");
                                
                                // Check if blocker has raycast target enabled
                                var graphic = result.gameObject.GetComponent<UnityEngine.UI.Graphic>();
                                if (graphic != null)
                                {
                                    Debug.Log($"<color=orange>    Blocker Raycast Target: {graphic.raycastTarget}</color>");
                                    if (graphic.raycastTarget)
                                    {
                                        Debug.Log($"<color=red>    ⚠⚠⚠ BLOCKER IS INTERCEPTING CLICKS! Consider disabling raycastTarget on '{result.gameObject.name}'</color>");
                                    }
                                }
                            }
                        }
                        
                        if (buttonHit && blockingObject != null)
                        {
                            Debug.Log($"<color=red>⚠⚠⚠ BUTTON IS HIT BUT BLOCKED BY: {blockingObject.name}</color>");
                            Debug.Log($"<color=red>   Solution: Disable raycastTarget on '{blockingObject.name}' or move it behind the button</color>");
                        }
                        
                        // Check if button's onClick would be called
                        if (buttonHit && button != null)
                        {
                            // Try to manually check if button would receive the event
                            var firstResult = results[0];
                            if (firstResult.gameObject == buttonObj || firstResult.gameObject.transform.IsChildOf(buttonObj.transform))
                            {
                                Debug.Log($"<color=green>Button is FIRST in raycast results - should receive click!</color>");
                            }
                            else
                            {
                                Debug.Log($"<color=red>Button is NOT first in raycast results - '{firstResult.gameObject.name}' will receive click instead!</color>");
                            }
                        }
                    }
                }
            }
            
            button.onClick.RemoveListener(clickAction);
            Debug.Log($"<color=cyan>=== BUTTON CLICKABILITY DEBUG END ===</color>");
            Debug.Log($"<color=cyan>Total clicks detected: {clickCount}</color>");
        }
    }
}
