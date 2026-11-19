using UnityEngine;

namespace LiarsBar.Bootstrap
{
    /// <summary>
    /// Composition Root: здесь собираются все зависимости и запускается игра.
    /// Пока пустой — наполняется на этапе 6.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            UnityEngine.Application.runInBackground = true;
        }
    }
}
