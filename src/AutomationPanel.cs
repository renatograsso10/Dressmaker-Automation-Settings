using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DressmakerAutomation.SettingsUI
{
    [DefaultExecutionOrder(10000)]
    public sealed class AutomationPanel : MonoBehaviour
    {
        private static readonly Color Ink = Hex(0x28483D);
        private static readonly Color Muted = Hex(0x5F7167);
        private static readonly Color Paper = Hex(0xF5F0E5);
        private static readonly Color Row = Hex(0xE9E7DC);
        private static readonly Color Accent = Hex(0x426653);
        private static readonly Color Off = Hex(0xCDD3C8);
        private static readonly Color Error = Hex(0x9C4037);

        private CoreBridge bridge;
        private Settings draft;
        private int patchStatus;
        private GameObject modal;
        private GameObject canvasObject;
        private TMP_FontAsset font;
        private TextMeshProUGUI feedback;
        private TMP_InputField balance;
        private TMP_InputField setAmount;
        private Button setMoney;
        private Button money, grain, placement, mannequin;
        private GameManager pausedManager;
        private PauseMenu pausedMenu;
        private bool managerWasEnabled;
        private bool openedPause;
        private GameObject previousSelection;
        private bool isBuilt;
        private bool startupHandled;
        private WindowFrame windowFrame;

        internal void Configure(int status)
        {
            patchStatus = status;
            bridge = new CoreBridge();
            StartCoroutine(BuildAfterScene());
            SceneManager.activeSceneChanged += SceneChanged;
        }

        private IEnumerator BuildAfterScene()
        {
            // The bootstrap runs before scene loading. Reuse a font once game UI is loaded.
            while (font == null)
            {
                yield return null;
                font = TMP_Settings.defaultFontAsset;
                if (font == null) font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f != null);
            }
            Build();
            isBuilt = true;
            float stableTime = 0f;
            int sceneHandle = SceneManager.GetActiveScene().handle;
            while (!startupHandled && stableTime < 1f)
            {
                yield return null;
                var scene = SceneManager.GetActiveScene();
                // Initial loading scenes are replaced by TitleScreen; opening earlier would be closed by that transition.
                if (!scene.isLoaded || scene.name != "TitleScreen" || scene.handle != sceneHandle)
                {
                    stableTime = 0f;
                    sceneHandle = scene.handle;
                }
                else stableTime += Time.unscaledDeltaTime;
            }
            if (!startupHandled) Open();
        }

        private void Update()
        {
            if (!isBuilt) return;
            if (Input.GetKeyDown(KeyCode.F8))
            {
                startupHandled = true;
                if (modal.activeSelf) Close();
                else Open();
            }
            else if (modal.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void Build()
        {
            canvasObject = new GameObject("Automation Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = 5;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            modal = new GameObject("Automation Modal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(canvasObject.transform, false);
            modal.layer = 5;
            Stretch((RectTransform)modal.transform);
            modal.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.10f, 0.78f);
            var panel = Box(modal.transform, "Settings Panel", new Rect(0, 0, 690, 780), Paper);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            var dragTitle = Box(panel.transform, "Drag Title", new Rect(0, 7, 600, 141), Color.clear);
            Box(panel.transform, "Top Accent", new Rect(0, 0, 690, 7), Accent);
            Label(panel.transform, "DRESSMAKER  /  YOUR WORKROOM", new Rect(30, 25, 580, 28), 16, Muted, FontStyles.Bold);
            Label(panel.transform, "Automation", new Rect(30, 61, 520, 48), 37, Ink, FontStyles.Bold);
            Label(panel.transform, "Drag title to move · Drag corner to resize", new Rect(30, 114, 620, 28), 19, Muted);
            MakeButton(panel.transform, "Close", "×", new Rect(614, 29, 46, 42), Row, Ink, Close);

            money = Feature(panel.transform, 161, "Infinite money", "Free purchases; balance stays above your chosen minimum.", () => { draft.InfiniteMoney = !draft.InfiniteMoney; Refresh(); });
            grain = Feature(panel.transform, 246, "Align fabric grain", "Rotate dropped pattern pieces for perfect grain alignment.", () => { draft.AutoGrainAlignment = !draft.AutoGrainAlignment; Refresh(); });
            placement = Feature(panel.transform, 331, "Find a free spot", "Move overlapping pieces. Requires grain alignment above.", () => { draft.FindValidPosition = !draft.FindValidPosition; Refresh(); });
            mannequin = Feature(panel.transform, 416, "Match mannequin to customer", "Use the customer's completed measurements automatically.", () => { draft.AutoMannequinSizing = !draft.AutoMannequinSizing; Refresh(); });

            Label(panel.transform, "MINIMUM BALANCE", new Rect(30, 516, 380, 25), 16, Ink, FontStyles.Bold);
            balance = MakeInput(panel.transform, new Rect(452, 516, 207, 45), true);
            Label(panel.transform, "EXACT BALANCE", new Rect(30, 572, 260, 25), 16, Ink, FontStyles.Bold);
            setAmount = MakeInput(panel.transform, new Rect(307, 566, 175, 45));
            setMoney = MakeButton(panel.transform, "Set Money", "Set money", new Rect(492, 566, 167, 45), Accent, Paper, SetMoney);
            feedback = Label(panel.transform, "", new Rect(30, 611, 630, 49), 17, Accent);
            MakeButton(panel.transform, "Defaults", "Restore defaults", new Rect(30, 674, 210, 46), Row, Ink, () => { draft = new Settings(); Refresh(); ShowFeedback("Defaults selected. Save to apply."); });
            MakeButton(panel.transform, "Cancel", "Cancel", new Rect(328, 674, 133, 46), Row, Ink, Close);
            MakeButton(panel.transform, "Save", "Save & apply", new Rect(477, 674, 182, 46), Accent, Paper, Save);
            Label(panel.transform, "F8 / Esc to close  ·  Double-click title to reset size", new Rect(30, 740, 590, 24), 15, Muted);
            var grip = Box(panel.transform, "Resize Corner", new Rect(658, 748, 30, 30), Row);
            for (int i = 0; i < 3; i++)
            {
                var stroke = Box(grip.transform, "Grip Stroke", new Rect(4 + i * 5, 20 - i * 5, 12, 2), Muted);
                stroke.transform.localRotation = Quaternion.Euler(0, 0, 45);
                stroke.GetComponent<Image>().raycastTarget = false;
            }
            windowFrame = panel.AddComponent<WindowFrame>();
            windowFrame.Configure(panelRect, (RectTransform)modal.transform, dragTitle, grip);
            modal.SetActive(false);
        }

        private Button Feature(Transform parent, float y, string title, string description, UnityEngine.Events.UnityAction clicked)
        {
            var row = Box(parent, title + " Row", new Rect(30, y, 629, 76), Row);
            Label(row.transform, title, new Rect(16, 10, 490, 27), 21, Ink, FontStyles.Bold);
            Label(row.transform, description, new Rect(16, 40, 498, 27), 15, Muted);
            return MakeButton(row.transform, title + " Toggle", "ON", new Rect(533, 18, 80, 40), Accent, Paper, clicked);
        }

        public void Open()
        {
            if (!isBuilt || modal.activeSelf) return;
            startupHandled = true;
            try
            {
                draft = bridge.Read();
                Refresh();
                previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                pausedManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
                if (pausedManager != null)
                {
                    pausedMenu = pausedManager.optionsMenu;
                    managerWasEnabled = pausedManager.enabled;
                    // Use the game's input pause. Suppress its Escape handler while this modal owns Escape.
                    if (pausedMenu == null) throw new InvalidOperationException("Game pause menu unavailable.");
                    openedPause = !pausedMenu.IsShowing;
                    if (openedPause) pausedMenu.Show();
                    pausedManager.enabled = false;
                }
                modal.SetActive(true);
                setAmount.SetTextWithoutNotify(pausedManager != null && PlayerProgress.HasSaveLoaded ? PlayerProgress.Current.gold.ToString(CultureInfo.InvariantCulture) : "500");
                RefreshMoneyAction();
                ShowFeedback("Set money: infinite money OFF + loaded game. Applies settings too.");
                balance.ForceLabelUpdate();
                EventSystem.current?.SetSelectedGameObject(money.gameObject);
            }
            catch (Exception error)
            {
                RestoreGameInput();
                Debug.LogError("[DressmakerAutomation.UI] Could not open settings: " + error);
            }
        }

        public void Close()
        {
            if (modal != null && modal.activeSelf) windowFrame?.SaveLayout();
            if (modal != null) modal.SetActive(false);
            RestoreGameInput();
            if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            previousSelection = null;
        }

        private void RestoreGameInput()
        {
            if (pausedManager != null) pausedManager.enabled = managerWasEnabled;
            if (openedPause && pausedMenu != null) pausedMenu.Hide(true);
            pausedManager = null;
            pausedMenu = null;
            openedPause = false;
        }

        private void Save() { TrySave(); }

        private bool TrySave()
        {
            int gold;
            if (!Settings.TryGoldFloor(balance.text, out gold))
            {
                ShowFeedback("Enter a whole number from 0 to 2,147,483,647.", true);
                return false;
            }
            draft.GoldFloor = gold;
            try
            {
                draft.Save(Path.Combine(Application.persistentDataPath, "DressmakerAutomation.cfg"));
                bridge.Apply(draft);
                ShowFeedback("Saved. Changes apply without restarting the game.");
                Debug.Log("[DressmakerAutomation.UI] Settings saved and applied.");
                return true;
            }
            catch (Exception error)
            {
                ShowFeedback("Could not save settings. Check the game log for details.", true);
                Debug.LogError("[DressmakerAutomation.UI] Save failed: " + error);
                return false;
            }
        }

        private void SetMoney()
        {
            if (pausedManager == null || !PlayerProgress.HasSaveLoaded)
            { ShowFeedback("Load a game before setting its balance.", true); return; }
            if (draft.InfiniteMoney)
            { ShowFeedback("Turn infinite money off before setting an exact balance.", true); return; }
            int amount;
            if (!Settings.TryGoldFloor(setAmount.text, out amount))
            { ShowFeedback("Enter a whole number from 0 to 2,147,483,647.", true); return; }
            if (!TrySave()) return;
            try
            {
                PlayerProgress.Current.gold = amount;
                pausedManager.AddGold(0);
                ShowFeedback("Balance set to " + amount.ToString(CultureInfo.InvariantCulture) + ". Save your game to keep it.");
            }
            catch (Exception error)
            {
                ShowFeedback("Balance changed; display refresh failed. Check the game log.", true);
                Debug.LogError("[DressmakerAutomation.UI] Balance display refresh failed: " + error);
            }
        }

        private void RefreshMoneyAction()
        {
            bool available = !draft.InfiniteMoney && pausedManager != null && PlayerProgress.HasSaveLoaded;
            setAmount.interactable = available;
            setMoney.interactable = available;
        }

        private void Refresh()
        {
            RefreshToggle(money, draft.InfiniteMoney, (patchStatus & 1) != 0);
            RefreshToggle(grain, draft.AutoGrainAlignment, (patchStatus & 2) != 0);
            RefreshToggle(placement, draft.FindValidPosition, (patchStatus & 2) != 0 && draft.AutoGrainAlignment);
            RefreshToggle(mannequin, draft.AutoMannequinSizing, (patchStatus & 4) != 0);
            // Preserve an edited balance when switching another feature.
            if (!balance.isFocused) balance.SetTextWithoutNotify(draft.GoldFloor.ToString(CultureInfo.InvariantCulture));
            balance.interactable = draft.InfiniteMoney && (patchStatus & 1) != 0;
            RefreshMoneyAction();
        }

        private void RefreshToggle(Button button, bool value, bool enabled)
        {
            button.interactable = enabled;
            button.GetComponent<Image>().color = value && enabled ? Accent : Off;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.text = !enabled ? "—" : value ? "ON" : "OFF";
            text.color = value && enabled ? Paper : Ink;
        }

        private void ShowFeedback(string text, bool error = false)
        {
            feedback.text = text;
            feedback.color = error ? Error : Accent;
        }

        private TMP_InputField MakeInput(Transform parent, Rect position, bool editsFloor = false)
        {
            var root = Box(parent, editsFloor ? "Minimum Balance Input" : "Exact Balance Input", position, Color.white);
            var input = root.AddComponent<TMP_InputField>();
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(root.transform, false);
            Stretch((RectTransform)viewport.transform);
            ((RectTransform)viewport.transform).offsetMin = new Vector2(12, 4);
            ((RectTransform)viewport.transform).offsetMax = new Vector2(-12, -4);
            var text = Label(viewport.transform, "", new Rect(0, 0, position.width - 24, position.height - 12), 21, Ink);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = (RectTransform)viewport.transform;
            input.textComponent = text;
            input.targetGraphic = root.GetComponent<Image>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 10;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = Ink;
            input.customCaretColor = true;
            input.selectionColor = new Color(0.43f, 0.60f, 0.45f, 0.28f);
            if (editsFloor) input.onValueChanged.AddListener(value => { int number; if (draft != null && Settings.TryGoldFloor(value, out number)) draft.GoldFloor = number; });
            return input;
        }

        private TextMeshProUGUI Label(Transform parent, string text, Rect position, int size, Color color, FontStyles style = FontStyles.Normal)
        {
            var root = new GameObject(text.Length > 0 ? text : "Label", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.layer = 5;
            Position((RectTransform)root.transform, position);
            var label = root.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.text = text;
            label.color = color;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        private Button MakeButton(Transform parent, string name, string text, Rect position, Color background, Color foreground, UnityEngine.Events.UnityAction clicked)
        {
            var root = Box(parent, name, position, background);
            var button = root.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.86f, 0.91f, 0.87f);
            colors.pressedColor = new Color(0.72f, 0.80f, 0.74f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(clicked);
            var label = Label(root.transform, text, new Rect(0, 0, position.width, position.height), 18, foreground, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            Stretch((RectTransform)label.transform);
            return button;
        }

        private static GameObject Box(Transform parent, string name, Rect position, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            root.layer = 5;
            Position((RectTransform)root.transform, position);
            root.GetComponent<Image>().color = color;
            return root;
        }

        private static void Position(RectTransform rect, Rect position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = position.size;
        }

        private static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        }

        private void SceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next) { Close(); }
        private void OnDisable() { Close(); }
        private void OnDestroy() { SceneManager.activeSceneChanged -= SceneChanged; RestoreGameInput(); }
    }
}
