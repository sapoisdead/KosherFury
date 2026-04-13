using UnityEngine;
using System.Collections;

namespace BehaviourTree
{
    public abstract class Tree : MonoBehaviour
    {
        private Node root;

        private void Start()
        {
            StartCoroutine(WaitForPlayerAndSetup());
        }

        private IEnumerator WaitForPlayerAndSetup()
        {
            // Aspetta finché il PlayerManager e il player non sono disponibili
            while (PlayerManager.Instance == null || PlayerManager.Instance.PlayerTransform == null)
                yield return null;

            root = SetupTree();
        }

        private void Update()
        {
            root?.Evaluate();
        }

        protected abstract Node SetupTree();
    }
}
