using System;
using UnityEngine;

namespace CityBuilder
{
    /// <summary>
    /// Singleton that tracks XP and current level.
    /// Call AddXP() from buildings or game events to trigger level-ups.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Configuration")]
        [Tooltip("XP required to reach each level. Index 0 = XP to reach level 2, index 1 = XP to reach level 3, etc.")]
        [SerializeField] private int[] _xpThresholds = { 100, 250, 500, 1000, 2000, 4000 };

        private int _currentLevel = 1;
        private int _currentXP;

        public int CurrentLevel => _currentLevel;
        public int CurrentXP => _currentXP;
        public int MaxLevel => _xpThresholds.Length + 1;
        public bool IsMaxLevel => _currentLevel >= MaxLevel;

        /// <summary>Fired when the player gains XP. Parameters: current XP, XP threshold for next level.</summary>
        public event Action<int, int> OnXPChanged;

        /// <summary>Fired when the player levels up. Parameter: new level.</summary>
        public event Action<int> OnLevelUp;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Add XP. Automatically triggers a level-up if the threshold is reached.</summary>
        public void AddXP(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return;

            _currentXP += amount;

            int threshold = GetXPThresholdForCurrentLevel();
            OnXPChanged?.Invoke(_currentXP, threshold);

            if (_currentXP >= threshold)
                LevelUp();
        }

        /// <summary>Returns XP needed to reach the next level from the current level. Returns 0 at max level.</summary>
        public int GetXPThresholdForCurrentLevel()
        {
            int index = _currentLevel - 1;
            return index < _xpThresholds.Length ? _xpThresholds[index] : 0;
        }

        /// <summary>Returns progress from 0 to 1 towards the next level.</summary>
        public float GetLevelProgress()
        {
            int threshold = GetXPThresholdForCurrentLevel();
            return threshold > 0 ? Mathf.Clamp01((float)_currentXP / threshold) : 1f;
        }

        private void LevelUp()
        {
            _currentLevel++;
            _currentXP = 0;
            Debug.Log($"[LevelManager] Level up! Now level {_currentLevel}");
            OnLevelUp?.Invoke(_currentLevel);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Add 50 XP")]
        private void DebugAdd50XP() => AddXP(50);

        [ContextMenu("Debug: Force Level Up")]
        private void DebugForceLevelUp()
        {
            if (!IsMaxLevel) LevelUp();
        }
#endif
    }
}
