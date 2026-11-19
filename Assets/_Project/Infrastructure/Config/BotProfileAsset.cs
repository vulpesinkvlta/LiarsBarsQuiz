using LiarsBar.Core.Bots;
using UnityEngine;

namespace LiarsBar.Infrastructure.Config
{
    /// <summary>ScriptableObject-обёртка над BotProfile: профили ботов правятся в инспекторе без перекомпиляции.</summary>
    [CreateAssetMenu(fileName = "BotProfile", menuName = "LiarsBar/Bot Profile")]
    public sealed class BotProfileAsset : ScriptableObject
    {
        [SerializeField] private BotProfile _profile = new BotProfile();

        /// <summary>Копия профиля: изменения во время игры не попадают в ассет.</summary>
        public BotProfile Profile => _profile.Clone();

        public void Set(BotProfile profile) => _profile = profile.Clone();

        private void OnValidate()
        {
            foreach (var error in _profile.Validate())
                Debug.LogWarning($"[{name}] {error}", this);
        }
    }
}
