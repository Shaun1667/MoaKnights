using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>스테이지 한 개. 새 스테이지 = 에셋 하나. 맵 글자는 GridMap 설명을 따른다.</summary>
    [CreateAssetMenu(fileName = "Stage", menuName = "MoaKnights/Stage")]
    public sealed class StageAsset : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string[] _mapRows;

        public string Id => _id;
        public string[] MapRows => _mapRows;
    }
}
