using System;
using UnityEngine;

namespace BusJam
{
    public class GameFeedbackListener : MonoBehaviour
    {
        [SerializeField] private Camera cam;

        private void Awake()
        {
            EnsureCamera();
        }
        
        private void OnEnable() => EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
        private void OnDisable() => EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);
        
        private void EnsureCamera()
        {
            if (cam == null)
            {
                cam = Camera.main;
            }
        }

        private void OnStateChanged(GameStateChangedEvent e)
        {
            EnsureCamera();
            if (e.State == GameState.Won)
            {
                GameFeedback.Win(cam);
            }
            else if (e.State == GameState.Lost)
            {
                GameFeedback.Lose();
            }
        }
    }
}