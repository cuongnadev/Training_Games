using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusJam
{
  [RequireComponent(typeof(CanvasGroup))]
  public sealed class LevelSelectPopupUI : MonoBehaviour
  {
    public static LevelSelectPopupUI Instance { get; private set; }

    [Header("Window & Backdrop")]
    [SerializeField] private RectTransform window;
    [SerializeField] private Button closeButton;

    [Header("Levels Container")]
    [SerializeField] private LevelDatabaseSO database;
    [SerializeField] private Transform levelsContainer;
    [SerializeField] private GameObject levelButtonPrefab;

    [Header("Visual Colors")]
    [SerializeField] private Color unlockedTextColor = Color.white;
    [SerializeField] private Color lockedTextColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
    [SerializeField] private Color currentLevelColor = new Color(1f, 0.85f, 0.25f, 1f);

    [Header("Animation Settings")]
    [SerializeField] private float openDuration = 0.35f;
    [SerializeField] private float closeDuration = 0.22f;
    [SerializeField] private float initialScale = 0.6f;

    private CanvasGroup _canvasGroup;
    private Tween _fadeTween;
    private Tween _scaleTween;
    private bool _isClosing;
    private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

    private void Awake()
    {
      if (Instance != null && Instance != this)
      {
        Destroy(gameObject);
        return;
      }
      Instance = this;

      _canvasGroup = GetComponent<CanvasGroup>();
      if (_canvasGroup == null)
      {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
      }

      if (closeButton != null) closeButton.onClick.AddListener(() => Hide());
    }

    private void OnDestroy()
    {
      if (Instance == this) Instance = null;
    }

    public void Show()
    {
      _isClosing = false;
      gameObject.SetActive(true);

      if (Instance == null) Instance = this;
      if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

      PopulateLevels();
      KillTweens();

      if (_canvasGroup != null)
      {
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        _fadeTween = _canvasGroup.DOFade(1f, openDuration * 0.8f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
      }

      if (window != null)
      {
        window.localScale = Vector3.one * initialScale;
        _scaleTween = window.DOScale(1f, openDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
      }

      if (AudioManager.Instance != null)
      {
        AudioManager.Instance.Play(Sfx.Popup);
      }
    }

    public void Hide(bool instant = false, Action onComplete = null)
    {
      if (!gameObject.activeInHierarchy)
      {
        onComplete?.Invoke();
        return;
      }

      if (instant)
      {
        KillTweens();
        gameObject.SetActive(false);
        onComplete?.Invoke();
        return;
      }

      _isClosing = true;

      if (_canvasGroup != null)
      {
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
      }

      KillTweens();

      Sequence closeSeq = DOTween.Sequence();
      closeSeq.SetUpdate(true);
      closeSeq.SetLink(gameObject, LinkBehaviour.KillOnDisable);

      if (_canvasGroup != null)
      {
        closeSeq.Join(_canvasGroup.DOFade(0f, closeDuration).SetEase(Ease.InQuad));
      }

      if (window != null)
      {
        closeSeq.Join(window.DOScale(initialScale, closeDuration).SetEase(Ease.InBack));
      }

      closeSeq.OnComplete(() =>
      {
        gameObject.SetActive(false);
        _isClosing = false;
        onComplete?.Invoke();
      });
    }

    private void PopulateLevels()
    {
      if (database == null)
      {
        database = Resources.Load<LevelDatabaseSO>("LevelDatabase");
      }
      if (database == null || levelsContainer == null) return;

      foreach (var b in _spawnedButtons) Destroy(b);
      _spawnedButtons.Clear();

      int maxUnlocked = LevelManager.MaxUnlockedLevelIndex;
      int currentLevel = PlayerPrefs.GetInt("BusJam.LevelIndex", 0);

      for (int i = 0; i < database.Count; i++)
      {
        int levelIndex = i;
        var levelData = database.GetLevel(levelIndex);
        if (levelData == null) continue;

        GameObject btnObj;
        if (levelButtonPrefab != null)
        {
          btnObj = Instantiate(levelButtonPrefab, levelsContainer);
        }
        else
        {
          btnObj = CreateDefaultLevelButton(levelsContainer);
        }

        _spawnedButtons.Add(btnObj);
        btnObj.name = $"LevelBtn_{levelData.LevelNumber}";

        var btn = btnObj.GetComponent<Button>();
        var txt = btnObj.GetComponentInChildren<TMP_Text>();
        var lockIcon = FindChildRecursive(btnObj.transform, "Lock");

        if (txt != null)
        {
          txt.text = levelData.LevelNumber.ToString();
        }

        bool isUnlocked = (levelIndex <= maxUnlocked);
        bool isCurrent = (levelIndex == currentLevel);

        if (btn != null)
        {
          btn.interactable = isUnlocked;
          btn.onClick.RemoveAllListeners();
          if (isUnlocked)
          {
            btn.onClick.AddListener(() => OnLevelSelected(levelIndex));
          }
        }

        if (lockIcon != null)
        {
          lockIcon.gameObject.SetActive(!isUnlocked);
        }

        if (txt != null)
        {
          txt.color = isCurrent ? currentLevelColor : (isUnlocked ? unlockedTextColor : lockedTextColor);
        }
      }
    }

    private void OnLevelSelected(int levelIndex)
    {
      if (_isClosing) return;

      Hide(instant: false, onComplete: () =>
      {
        if (HomePopupUI.Instance != null)
        {
          HomePopupUI.Instance.Hide(instant: true);
        }

        EventBus<SelectLevelRequestedEvent>.Publish(new SelectLevelRequestedEvent(levelIndex));
      });
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
      for (int i = 0; i < parent.childCount; i++)
      {
        var child = parent.GetChild(i);
        if (child.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return child;
        var deeper = FindChildRecursive(child, name);
        if (deeper != null) return deeper;
      }
      return null;
    }

    private GameObject CreateDefaultLevelButton(Transform parent)
    {
      var go = new GameObject("LevelButton", typeof(RectTransform), typeof(Image), typeof(Button));
      go.transform.SetParent(parent, false);

      var rt = go.GetComponent<RectTransform>();
      rt.sizeDelta = new Vector2(90f, 90f);

      var img = go.GetComponent<Image>();
      img.color = new Color(0.2f, 0.45f, 0.85f, 1f);

      var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
      textGo.transform.SetParent(go.transform, false);

      var textRt = textGo.GetComponent<RectTransform>();
      textRt.anchorMin = Vector2.zero;
      textRt.anchorMax = Vector2.one;
      textRt.sizeDelta = Vector2.zero;

      var tmp = textGo.GetComponent<TextMeshProUGUI>();
      tmp.alignment = TextAlignmentOptions.Center;
      tmp.fontSize = 32f;
      tmp.fontStyle = FontStyles.Bold;

      return go;
    }

    private void KillTweens()
    {
      _fadeTween?.Kill();
      _scaleTween?.Kill();
      _fadeTween = null;
      _scaleTween = null;
      if (window != null) window.DOKill();
      if (_canvasGroup != null) _canvasGroup.DOKill();
    }
  }
}