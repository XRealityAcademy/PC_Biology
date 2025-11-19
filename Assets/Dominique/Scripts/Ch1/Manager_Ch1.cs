using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class Manager_Ch1 : MonoBehaviour
{
    /*──────── Tunables ───────────*/
    private const int TOTAL = 25;                        // 0...24
    [Range(1, 10)] public int firstAutoCount = 4;        // autoplay at start

    [Header("Gameplay Gating")]
    [Tooltip("How many pots must receive seeds after index 10.")]
    public int requiredSeedPots = 6;

    [Tooltip("Tag used by seed-pot triggers (legacy OnTriggerEnter on THIS Manager).")]
    public string seedPotTriggerTag = "SeedPotTrigger";

    [Tooltip("Tag used by the watering trigger zone (legacy OnTriggerEnter on THIS Manager).")]
    public string wateringTriggerTag = "WaterZone";

    [Header("Scene Transition")]
    [Tooltip("Scene name to load after dialog 11 finishes (e.g., 'chapter 3')")]
    public string nextSceneName = "chapter 3";
    
    [Tooltip("Delay in seconds before switching scenes after dialog 11 finishes")]
    public float sceneSwitchDelay = 2f;

    /* === Strict per-pot seeding (does NOT change 0–9) === */
    [Header("Strict Seeding (exactly 6 unique pots)")]
    [Tooltip("Assign the SIX SeedPotTrigger components (one on each pot).")]
    public SeedPotTrigger[] seedPots = new SeedPotTrigger[6];

    /*──────── Outline refs ───────*/
    [Header("Item Outlines")]
    public Outline potOutline;
    public Outline seedOutline;
    public Outline xOutline;
    public Outline rulerOutline;
    public Outline waterCanOutline;
    // (chatBotOutline removed per your current version)

    Outline[] outlines;
    int currentItemIndex = -1;   // starts at “none”

    /*──────── Dialog UI / Audio ───*/
    [Header("UI & Audio")]
    public TMP_Text dialogText;
    public AudioSource audioSource;

    [Header("Dialog Content (exactly 25)")]
    public AudioClip[] dialogClips = new AudioClip[TOTAL];
    public string[] dialogLines = new string[TOTAL];

    [Header("Per-Dialog Delays (seconds)")]
    [Tooltip("Delay after each dialog index before the next one plays (size = 25).")]
    public float[] delayAfterLine = new float[TOTAL];

    public GameObject continueButton;
    readonly bool[] played = new bool[TOTAL];
    bool waterDoneEarly = false; // remembers if watering happened before we started waitingForWater


    /*──────── Progress State ─────*/
    bool waitingForSeeds = false;        // becomes true after index 10 finishes
    bool waitingForWater = false;        // becomes true after index 12 finishes



    /* === Track unique pots that received their first seed === */
    readonly HashSet<SeedPotTrigger> uniqueSeededPots = new HashSet<SeedPotTrigger>();

    /*──────── Constants ───────────*/
    static readonly Color Pink = ParseHex("#FF0047");
    const float ActiveWidth = 10f, HiddenWidth = 0f;
    int nextAllowedIndex = 0;

    /*──────── Unity ───────────────*/
 void Awake()
    {
        outlines = new[] {
            potOutline, seedOutline, xOutline,
            rulerOutline, waterCanOutline
        };

        if (dialogClips.Length != TOTAL || dialogLines.Length != TOTAL)
        {
            Debug.LogError($"Manager_Ch1: need EXACTLY {TOTAL} clips + {TOTAL} lines.");
            enabled = false;
            return;
        }

        if (delayAfterLine == null || delayAfterLine.Length != TOTAL)
        {
            delayAfterLine = new float[TOTAL]; // ensure correct size
        }

        // Set sensible defaults only if unset (≈ 0)
        if (Mathf.Approximately(delayAfterLine[9], 0f)) delayAfterLine[9] = 1.0f;   // 9→10
        if (Mathf.Approximately(delayAfterLine[11], 0f)) delayAfterLine[11] = 5.0f; // 11→12
        for (int i = 14; i < TOTAL; i++)
            if (Mathf.Approximately(delayAfterLine[i], 0f)) delayAfterLine[i] = 5.0f; // 14→24

        foreach (var o in outlines) if (o) SetOutlineHidden(o);

        if (audioSource)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.enabled = true;
            audioSource.spatialBlend = 0f;
            if (audioSource.volume <= 0f) audioSource.volume = 1f;
        }

        // Wire seed pots so they can call back
        for (int i = 0; i < seedPots.Length; i++)
            if (seedPots[i]) seedPots[i].SetManager(this);
    }
    void Start()
    {
        continueButton?.SetActive(false);
        StartCoroutine(AutoplayFirstN(firstAutoCount));   // 0–(firstAutoCount-1)
    }

    /*──────── Public API (kept) ─────────*/
    public void PlayDialogByIndex(int index) => TryPlay(index);

    /// <summary>
    /// Force play a dialog index, bypassing order checks (useful for grab-triggered dialogs)
    /// </summary>
    public void ForcePlayDialogByIndex(int index)
    {
        if (index < 0 || index >= played.Length || played[index]) return;

        // Show corresponding item outline for Dialog 5–10 (indices 4..9)
        if (index >= 4 && index <= 9) SwitchOutline(index - 4);

        StartCoroutine(PlayLine(index));
    }

    /// Legacy seed callback (kept for backward compatibility)
    public void NotifySeedPlaced()
        => Debug.LogWarning("NotifySeedPlaced() legacy call ignored. Use SeedPotTrigger per pot.");

    /// Strict per-pot version — counts UNIQUE pots. Call from SeedPotTrigger.
    public void NotifySeedPlaced(SeedPotTrigger pot)
    {
        Debug.Log($"[Manager_Ch1] NotifySeedPlaced called. waitingForSeeds: {waitingForSeeds}, pot: {(pot != null ? pot.name : "null")}");
        
        if (!waitingForSeeds || pot == null)
        {
            Debug.Log($"[Manager_Ch1] NotifySeedPlaced: Skipping - waitingForSeeds={waitingForSeeds}, pot is null={pot == null}");
            return;
        }

        bool recognized = false;
        for (int i = 0; i < seedPots.Length; i++)
            if (seedPots[i] == pot) { recognized = true; break; }
        
        if (!recognized)
        {
            Debug.Log($"[Manager_Ch1] NotifySeedPlaced: Pot {pot.name} not recognized in seedPots array");
            return;
        }

        if (uniqueSeededPots.Add(pot))
        {
            Debug.Log($"[Manager_Ch1] Seed placed in pot {pot.name}. Total seeded pots: {uniqueSeededPots.Count}/{requiredSeedPots}");
            
            if (uniqueSeededPots.Count >= requiredSeedPots)  // typically 6
            {
                Debug.Log($"[Manager_Ch1] All {requiredSeedPots} seeds placed! Setting waitingForWater = true");
                waitingForSeeds = false;
                waitingForWater = true;   // switch to watering phase; do not play 11 yet

                // If the player already watered early, honor it now.
                if (waterDoneEarly)
                {
                    Debug.Log($"[Manager_Ch1] Water was done early, triggering dialog 11 now");
                    waterDoneEarly = false;
                    waitingForWater = false;
                    TryPlay(11);
                   // yield break; // optional: stop further post-line logic
                }
            }
        }
        else
        {
            Debug.Log($"[Manager_Ch1] Pot {pot.name} already had a seed (duplicate ignored)");
        }
    }

    /// Watering done (kept API). Called by WateringZoneTrigger below.
    public void NotifyWateringDone()
    {
        Debug.Log($"[Manager_Ch1] NotifyWateringDone called. Seeded pots: {uniqueSeededPots.Count}/{requiredSeedPots}, waitingForSeeds: {waitingForSeeds}, waitingForWater: {waitingForWater}");
        
        // Check if all required seeds are placed
        if (uniqueSeededPots.Count >= requiredSeedPots)
        {
            Debug.Log($"[Manager_Ch1] All seeds placed! Triggering dialog 11");
            // All seeds are placed - trigger dialog 11
            waitingForWater = false;
            waitingForSeeds = false;
            TryPlay(11); // jump to index 11
        }
        else if (waitingForSeeds)
        {
            Debug.Log($"[Manager_Ch1] Watering happened early (only {uniqueSeededPots.Count}/{requiredSeedPots} seeds placed). Remembering for later.");
            // Watering happened before all seeds are placed - remember it
            waterDoneEarly = true;
        }
        else
        {
            Debug.LogWarning($"[Manager_Ch1] NotifyWateringDone: Not enough seeds placed ({uniqueSeededPots.Count}/{requiredSeedPots}) and not waiting for seeds. Dialog 11 will not trigger.");
        }
        // If waitingForWater is true but seed count is wrong, something is out of sync
        // In that case, don't trigger (seeds must be placed first)
    }

    /*──────── Collision Hooks ─────*/
    void OnTriggerEnter(Collider other)
    {
        // Existing legacy paths (kept)
        if (!string.IsNullOrEmpty(seedPotTriggerTag) && other.CompareTag(seedPotTriggerTag))
        {
            NotifySeedPlaced();
        }
        else if (!string.IsNullOrEmpty(wateringTriggerTag) && other.CompareTag(wateringTriggerTag))
        {
            // Keep this if you still use a tag-based watering zone
            NotifyWateringDone();
        }
    }

    /*──────── Internals ──────────*/
    void TryPlay(int idx)
    {
        // NEW: block out-of-order jumps (e.g., 8->13 or 10->13)
        if (idx > nextAllowedIndex)
        {
            Debug.Log($"Manager_Ch1: Blocking out-of-order request for {idx}. Next allowed is {nextAllowedIndex}.");
            return;
        }

        if (idx < 0 || idx >= played.Length || played[idx]) return;

        // Show corresponding item outline for Dialog 5–10 (indices 4..9)
        if (idx >= 4 && idx <= 9) SwitchOutline(idx - 4);

        StartCoroutine(PlayLine(idx));
    }

    IEnumerator AutoplayFirstN(int count)
    {
        yield return null;
        yield return new WaitForSeconds(0.05f);

        if (Time.timeScale == 0f) Time.timeScale = 1f;
        int safe = Mathf.Clamp(count, 0, TOTAL);

        for (int i = 0; i < safe; i++)
            yield return PlayLine(i);
    }

    IEnumerator PlayLine(int idx)
    {
        played[idx] = true;
        if (idx + 1 > nextAllowedIndex) nextAllowedIndex = idx + 1;

        if (dialogText) dialogText.text = dialogLines[idx];

        if (audioSource && dialogClips[idx])
        {
            audioSource.Stop();
            audioSource.clip = dialogClips[idx];
            audioSource.time = 0f;
            audioSource.Play();

            float len = audioSource.clip.length;
            float t = 0f;
            while (t < len) { t += Time.unscaledDeltaTime; yield return null; }
        }
        else
        {
            Debug.LogWarning($"Manager_Ch1: Missing clip at index {idx}. Using 3s fallback.");
            yield return new WaitForSeconds(3f);
        }

        // Optional per-index delay AFTER the clip finishes
        if (delayAfterLine[idx] > 0f)
            yield return new WaitForSeconds(delayAfterLine[idx]);

        // ====== Post-line gating / ordering ======
        if (idx == 8)
        {
            // NEW: ensure 8 -> 9 happens in order
            StartCoroutine(PlayAfterDelay(9, 0f));
        }
        else if (idx == 9)
        {
            StartCoroutine(PlayAfterDelay(10, 0f));
        }
        else if (idx == 10)
        {
            waitingForSeeds = true;
            uniqueSeededPots.Clear();

            foreach (var p in seedPots)
                if (p && p.IsSeeded) uniqueSeededPots.Add(p);

            // If requirement already satisfied, switch immediately to watering phase
            if (uniqueSeededPots.Count >= requiredSeedPots)
            {
                waitingForSeeds = false;
                waitingForWater = true;
            }
            // TryPlay(11);

            // If watering happened early, advance now to 11
            //     if (waterDoneEarly)
            //     {
            //         waterDoneEarly = false;
            //         waitingForWater = false;
            //         TryPlay(11);
            //         yield break; // optional: stop further post-line logic
            //     }
            // }

            // NEW: safety net — if both phases are already satisfied, advance to 11
            // if (!waitingForSeeds && (!waitingForWater || waterDoneEarly))
            // {
            //     waterDoneEarly = false;
            //     waitingForWater = false;
            //     TryPlay(11);
            //     yield break; // optional
            // }
        }
        else if (idx == 11)
        {
            // Dialog 11 is the last dialog - switch scenes after delay
            Debug.Log($"[Manager_Ch1] Dialog 11 finished. Switching to scene '{nextSceneName}' in {sceneSwitchDelay} seconds.");
            StartCoroutine(SwitchSceneAfterDelay());
        }
        else if (idx > 11 && idx < TOTAL - 1)
        {
            // After idx finishes (and its delay runs), queue the next index
            // This should not be reached if we stop at 11, but kept for safety
            StartCoroutine(PlayAfterDelay(idx + 1, 0f)); // no second delay
        }

        if (idx == TOTAL - 1 && continueButton) continueButton.SetActive(true);
    }

    IEnumerator PlayAfterDelay(int index, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        TryPlay(index);
    }

    IEnumerator SwitchSceneAfterDelay()
    {
        yield return new WaitForSeconds(sceneSwitchDelay);
        
        Debug.Log($"[Manager_Ch1] Loading scene: {nextSceneName}");
        
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("[Manager_Ch1] Next scene name is empty! Cannot switch scenes.");
            yield break;
        }
        
        try
        {
            SceneManager.LoadScene(nextSceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Manager_Ch1] Failed to load scene '{nextSceneName}': {e.Message}");
        }
    }




    /*──────── Outline helpers ────*/
    void SwitchOutline(int newIndex)
    {
        if (currentItemIndex >= 0 && currentItemIndex < outlines.Length && outlines[currentItemIndex])
            SetOutlineHidden(outlines[currentItemIndex]);

        currentItemIndex = newIndex;
        if (currentItemIndex >= 0 && currentItemIndex < outlines.Length && outlines[currentItemIndex])
            SetOutlineRed(outlines[currentItemIndex]);
    }

    void SetOutlineRed(Outline o)
    {
        if (!o) return;
        o.OutlineColor = Pink;
        o.OutlineWidth = ActiveWidth;
        o.OutlineMode = Outline.Mode.OutlineAll;
        o.enabled = true;
    }

    void SetOutlineHidden(Outline o)
    {
        if (!o) return;
        o.OutlineWidth = HiddenWidth;
        o.OutlineMode = Outline.Mode.OutlineHidden;
        o.enabled = true;
    }

    static Color ParseHex(string hex)
    {
        Color c; ColorUtility.TryParseHtmlString(hex, out c); return c;
    }

    public void NotifyWaterCanGrabbed()
    {
        if (waterCanOutline)
        {
            waterCanOutline.OutlineWidth = 0f;
            waterCanOutline.OutlineMode = Outline.Mode.OutlineHidden;
            waterCanOutline.enabled = true;
        }
    }


}
