using System;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>스테이지 한 개. 새 스테이지 = 에셋 하나. 맵 글자는 GridMap, 경로 글자는 EnemyRoute 설명을 따른다.</summary>
    [CreateAssetMenu(fileName = "Stage", menuName = "MoaKnights/Stage")]
    public sealed class StageAsset : ScriptableObject
    {
        /// <summary>같은 적이 일정한 간격으로 나오는 한 묶음.</summary>
        [Serializable]
        public struct SpawnGroup
        {
            public string EnemyId;
            public int RouteIndex;
            public float StartSeconds;
            public float IntervalSeconds;
            public int Count;
        }

        [SerializeField] private string _id;
        [SerializeField] private string[] _mapRows;
        [SerializeField] private string[] _routes;
        [SerializeField] private SpawnGroup[] _spawnGroups;

        public string Id => _id;
        public string[] MapRows => _mapRows;
        public string[] Routes => _routes;
        public SpawnGroup[] SpawnGroups => _spawnGroups;
    }
}
