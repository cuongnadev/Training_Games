using UnityEngine;
using UnityEngine.UI;

namespace BusJam
{
    public sealed class UIManager : MonoBehaviour
    {
        [Header("Optional existing UI")]
        [SerializeField] private HUDController hud;
        [SerializeField] private WinPopupUI winPopup;
        [SerializeField] private LosePopupUI losePopup;

        private Text _runtimeLevelLabel;
        private GameObject _runtimeResultPanel;
        private Text _runtimeResultTitle;
        private Button _primaryButton;
        private Text _primaryButtonText;
        private Button _secondaryButton;
        private Text _secondaryButtonText;
        private LevelManager _levelManager;

        private void Awake()
        {
            if (hud == null) hud = FindObjectOfType<HUDController>(true);
            if (winPopup == null) winPopup = FindObjectOfType<WinPopupUI>(true);
            if (losePopup == null) losePopup = FindObjectOfType<LosePopupUI>(true);
            _levelManager = FindObjectOfType<LevelManager>();

            if (GetComponent<Canvas>() != null)
                BuildRuntimeUI();
        }

        public void SetLevel(int levelNumber)
        {
            if (hud != null) hud.SetLevel(levelNumber);
            if (_runtimeLevelLabel != null) _runtimeLevelLabel.text = $"LEVEL {levelNumber}";
            HideResultPopups();
        }

        public void ShowWin()
        {
            if (winPopup != null) winPopup.Show();
            ShowRuntimeResult("LEVEL COMPLETE!", "NEXT LEVEL", OnPrimaryClicked, true);
        }

        public void ShowLose()
        {
            if (losePopup != null) losePopup.Show();
            ShowRuntimeResult("NO MORE MOVES", "TRY AGAIN", OnPrimaryClicked, false);
        }

        public void HideResultPopups()
        {
            if (winPopup != null) winPopup.Hide();
            if (losePopup != null) losePopup.Hide();
            if (_runtimeResultPanel != null) _runtimeResultPanel.SetActive(false);
        }

        private void BuildRuntimeUI()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) return;

            var labelObject = CreateUIObject("RuntimeLevelLabel", transform);
            _runtimeLevelLabel = labelObject.AddComponent<Text>();
            ConfigureText(_runtimeLevelLabel, 34, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.04f, 0.94f);
            labelRect.anchorMax = new Vector2(0.96f, 0.99f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _runtimeLevelLabel.text = "LEVEL 1";

            _runtimeResultPanel = CreateUIObject("RuntimeResultPanel", transform);
            var panelImage = _runtimeResultPanel.AddComponent<Image>();
            panelImage.color = new Color(0.04f, 0.08f, 0.15f, 0.94f);
            var panelRect = _runtimeResultPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.12f, 0.34f);
            panelRect.anchorMax = new Vector2(0.88f, 0.66f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var titleObject = CreateUIObject("ResultTitle", _runtimeResultPanel.transform);
            _runtimeResultTitle = titleObject.AddComponent<Text>();
            ConfigureText(_runtimeResultTitle, 42, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            var titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.04f, 0.54f);
            titleRect.anchorMax = new Vector2(0.96f, 0.94f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            _primaryButton = CreateButton(_runtimeResultPanel.transform, "PrimaryButton", out _primaryButtonText);
            SetRect(_primaryButton.GetComponent<RectTransform>(), new Vector2(0.12f, 0.12f), new Vector2(0.52f, 0.48f));
            _primaryButton.onClick.AddListener(OnPrimaryClicked);

            _secondaryButton = CreateButton(_runtimeResultPanel.transform, "RetryButton", out _secondaryButtonText);
            SetRect(_secondaryButton.GetComponent<RectTransform>(), new Vector2(0.56f, 0.12f), new Vector2(0.90f, 0.48f));
            _secondaryButtonText.text = "RESTART";
            _secondaryButton.onClick.AddListener(OnSecondaryClicked);

            _runtimeResultPanel.SetActive(false);
        }

        private void ShowRuntimeResult(string title, string primary, UnityEngine.Events.UnityAction action, bool won)
        {
            if (_runtimeResultPanel == null) return;
            _runtimeResultTitle.text = title;
            _primaryButtonText.text = primary;
            _primaryButton.onClick.RemoveAllListeners();
            _primaryButton.onClick.AddListener(action);
            _secondaryButton.gameObject.SetActive(true);
            _secondaryButtonText.text = "RESTART";
            _runtimeResultPanel.SetActive(true);
        }

        private void OnPrimaryClicked()
        {
            if (_levelManager == null) _levelManager = FindObjectOfType<LevelManager>();
            if (_levelManager == null) return;

            if (_runtimeResultTitle != null && _runtimeResultTitle.text == "LEVEL COMPLETE!")
                _levelManager.NextLevel();
            else
                _levelManager.RestartLevel();
        }

        private void OnSecondaryClicked()
        {
            if (_levelManager == null) _levelManager = FindObjectOfType<LevelManager>();
            if (_levelManager != null) _levelManager.RestartLevel();
        }

        private static GameObject CreateUIObject(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Button CreateButton(Transform parent, string objectName, out Text label)
        {
            var go = CreateUIObject(objectName, parent);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.55f, 0.95f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textObject = CreateUIObject("Label", go.transform);
            label = textObject.AddComponent<Text>();
            ConfigureText(label, 24, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return button;
        }

        private static void ConfigureText(Text text, int size, TextAnchor anchor, Color color, FontStyle style)
        {
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
