using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>스테이지 목록 한 곳. 순서가 곧 진행 순서이고 "다음 스테이지"는 이 순서를 따른다. 새 스테이지 = 에셋 하나 + 여기 한 줄.</summary>
    [CreateAssetMenu(fileName = "StageCatalog", menuName = "MoaKnights/Stage Catalog")]
    public sealed class StageCatalogAsset : ScriptableObject
    {
        [SerializeField] private StageAsset[] _stages;

        public int Count => _stages == null ? 0 : _stages.Length;

        public StageAsset this[int index] => _stages[index];

        /// <summary>범위 밖이면 첫 스테이지로 맞춘다.</summary>
        public int Clamp(int index) => index < 0 || index >= Count ? 0 : index;
    }
}
