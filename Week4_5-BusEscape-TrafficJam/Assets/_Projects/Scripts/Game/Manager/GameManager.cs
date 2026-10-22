using System;
using UnityEngine;

namespace BusJam
{
    public enum GameState
    {
        Loading,
        Playing,
        Won,
        Lost,
        Menu
    }

    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public static bool IsBusy { get; private set; }
        public GameState State { get; private set; } = GameState.Loading;
        public event Action<GameState> StateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            IsBusy = false;
        }

        public void BeginLoading()
        {
            IsBusy = true;
            SetState(GameState.Loading);
        }

        public void FinishLoading()
        {
            IsBusy = false;
            SetState(GameState.Playing);
        }

        public void SetBusy(bool busy) => IsBusy = busy;

        public void Win()
        {
            IsBusy = false;
            SetState(GameState.Won);
        }

        public void Lose()
        {
            IsBusy = false;
            SetState(GameState.Lost);
        }

        public void SetState(GameState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}