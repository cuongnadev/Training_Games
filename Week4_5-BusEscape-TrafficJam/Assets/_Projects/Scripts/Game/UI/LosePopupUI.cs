using UnityEngine;

namespace BusJam
{
    public sealed class LosePopupUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private LevelManager levelManager;

        private void Awake()
        {
            if (panel == null) panel = gameObject;
            if (levelManager == null) levelManager = FindObjectOfType<LevelManager>();
            Hide();
        }

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void OnRetryClicked()
        {
            Hide();
            if (levelManager != null) levelManager.RestartLevel();
        }
    }
}
