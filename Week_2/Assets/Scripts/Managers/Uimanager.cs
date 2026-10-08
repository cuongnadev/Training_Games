using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the HUD and popups. It never touches game logic: it only raises events
/// for button presses and exposes methods to show or hide its elements.
/// </summary>
public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private WinPopupUI _winPopup;
    [Tooltip("Optional: shown when no valid move is left.")]
    [SerializeField] private GameObject _stuckPopup;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _undoButton;

    public event Action OnResetClicked;
    public event Action OnUndoClicked;
    public event Action OnNextClicked;
    public event Action OnReplayClicked;

    private void OnEnable()
    {
        if (_resetButton != null) _resetButton.onClick.AddListener(HandleResetClicked);
        if (_undoButton != null) _undoButton.onClick.AddListener(HandleUndoClicked);
        if (_winPopup != null)
        {
            _winPopup.OnNextClicked += HandleNextClicked;
            _winPopup.OnReplayClicked += HandleReplayClicked;
        }
    }

    private void OnDisable()
    {
        if (_resetButton != null) _resetButton.onClick.RemoveListener(HandleResetClicked);
        if (_undoButton != null) _undoButton.onClick.RemoveListener(HandleUndoClicked);
        if (_winPopup != null)
        {
            _winPopup.OnNextClicked -= HandleNextClicked;
            _winPopup.OnReplayClicked -= HandleReplayClicked;
        }
    }

    public void UpdateLevelText(int levelIndex)
    {
        if (_levelText != null)
        {
            _levelText.text = $"LEVEL {levelIndex + 1}";
        }
    }

    public void ShowWin()
    {
        if (_winPopup != null) _winPopup.Show();
    }

    public void HideWin()
    {
        if (_winPopup != null) _winPopup.Hide();
    }

    public void SetStuck(bool stuck)
    {
        if (_stuckPopup != null) _stuckPopup.SetActive(stuck);
    }

    private void HandleResetClicked()
    {
        OnResetClicked?.Invoke();
    }

    private void HandleUndoClicked()
    {
        OnUndoClicked?.Invoke();
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