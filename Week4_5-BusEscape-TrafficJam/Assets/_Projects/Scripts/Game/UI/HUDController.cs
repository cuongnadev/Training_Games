using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusJam
{
    public sealed class HUDController : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text passengerLabel;
        [SerializeField] private PassengerQueue passengerQueue;

        private int _lastCount = -1;

        private void Awake()
        {
            if (passengerQueue == null) passengerQueue = FindObjectOfType<PassengerQueue>();
        }

        private void Update()
        {
            if (passengerQueue == null || passengerLabel == null) return;

            int count = passengerQueue.Count;
            if (count == _lastCount) return;

            _lastCount = count;
            SetPassengers(count);
        }

        public void SetLevel(int levelNumber)
        {
            if (levelLabel != null) levelLabel.text = $"Level {levelNumber}";
        }

        public void SetPassengers(int remaining)
        {
            if (passengerLabel != null) passengerLabel.text = $"{remaining}";
        }
    }
}
