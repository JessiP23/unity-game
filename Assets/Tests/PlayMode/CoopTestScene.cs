using System.Collections;
using NightSupermarket.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace NightSupermarket.Tests
{
    /// <summary>Existing co-op regressions explicitly opt in; the shipped scene defaults to solo.</summary>
    public static class CoopTestScene
    {
        public static IEnumerator Load()
        {
            void Configure(Scene scene, LoadSceneMode mode)
            {
                var root = Object.FindAnyObjectByType<PrototypeRoot>();
                if (root == null) return;
                root.rules = Object.Instantiate(root.rules);
                root.rules.mannequinPlayers = 4;
            }
            SceneManager.sceneLoaded += Configure;
            try { yield return SceneManager.LoadSceneAsync("Prototype"); }
            finally { SceneManager.sceneLoaded -= Configure; }
        }
    }
}
