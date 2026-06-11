using UnityEngine;
using UnityEngine.SceneManagement;

public class win : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Scene to load after the player reaches this object. Leave empty to load the next scene in Build Settings.")]
    public string nextSceneName;

    [Header("Player Check")]
    public bool requirePlayerTag = false;
    public string playerTag = "Player";

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        TryWin(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryWin(collision.gameObject);
    }

    private void TryWin(GameObject other)
    {
        if (hasTriggered || LevelSceneTransition.IsTransitioning)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            return;
        }

        string sceneToLoad = ResolveNextSceneName();
        if (string.IsNullOrWhiteSpace(sceneToLoad))
        {
            Debug.LogWarning("No next scene found. Set Next Scene Name on win or add the next scene to Build Settings.", this);
            return;
        }

        hasTriggered = LevelSceneTransition.LoadScene(sceneToLoad, null);
    }

    private bool IsPlayer(GameObject other)
    {
        if (other == null)
        {
            return false;
        }

        if (requirePlayerTag && !other.CompareTag(playerTag))
        {
            return false;
        }

        return other.GetComponentInParent<PlayerRbController>() != null
            || (!requirePlayerTag && other.CompareTag(playerTag));
    }

    private string ResolveNextSceneName()
    {
        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            return nextSceneName;
        }

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;
        if (nextIndex < 0 || nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return string.Empty;
        }

        string scenePath = SceneUtility.GetScenePathByBuildIndex(nextIndex);
        return System.IO.Path.GetFileNameWithoutExtension(scenePath);
    }
}
