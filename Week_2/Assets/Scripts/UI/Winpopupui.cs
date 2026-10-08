using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Win popup: plays the show animation and raises events for the Next and Replay buttons.</summary>
public class WinPopupUI : MonoBehaviour
{
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _replayButton;
    [SerializeField] private float _showDuration = 0.4f;

    public event Action OnNextClicked;
    public event Action OnReplayClicked;

    private void Awake()
    {
        if (_nextButton != null) _nextButton.onClick.AddListener(HandleNextClicked);
        if (_replayButton != null) _replayButton.onClick.AddListener(HandleReplayClicked);
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (_nextButton != null) _nextButton.onClick.RemoveListener(HandleNextClicked);
        if (_replayButton != null) _replayButton.onClick.RemoveListener(HandleReplayClicked);
    }

    /// <summary>Shows the popup with a scale-in animation.</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        transform.DOKill();
        transform.localScale = Vector3.zero;
        transform.DOScale(1f, _showDuration).SetEase(Ease.OutBack);
    }

    /// <summary>Hides the popup immediately.</summary>
    public void Hide()
    {
        transform.DOKill();
        gameObject.SetActive(false);
    }

    private void HandleNextClicked()
    {
        OnNextClicked?.Invoke();
    }

    private void HandleReplayClicked()
    {
        OnReplayClicked?.Invoke();
    }
}