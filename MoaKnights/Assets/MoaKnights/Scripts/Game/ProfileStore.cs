using System;
using System.IO;
using System.Linq;
using MoaKnights.Core.Meta;
using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>
    /// 플레이어 프로필(가진 능력자 · 재화 · 뽑기 난수)을 PC에 JSON 파일 하나로 저장한다.
    /// 규칙 코드는 파일을 모르고, 여기서 스냅샷을 읽고 쓴다.
    /// </summary>
    public static class ProfileStore
    {
        public const string FileName = "profile.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// 저장 파일을 읽는다. 없거나 읽을 수 없으면 새 프로필(시작 능력자 · 재화).
        /// 능력자 목록에서 빠진 ID는 지우고, 가진 능력자가 하나도 없으면 시작 능력자를 다시 준다.
        /// </summary>
        public static PlayerProfile Load(BattleBalanceAsset balance, string path, int seed)
        {
            PlayerProfile profile = null;
            if (File.Exists(path))
            {
                try
                {
                    profile = PlayerProfile.FromSnapshot(JsonUtility.FromJson<ProfileSnapshot>(File.ReadAllText(path)));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Profile] Could not read " + path + ", starting a new profile: " + e.Message);
                }
            }

            if (profile == null) profile = BattleDefinitionFactory.CreateNewProfile(balance, seed);
            profile.RemoveUnitsWhere(id => balance.Units.All(u => u.Id != id));
            if (profile.Owned.Count == 0)
                foreach (var id in balance.StartingUnits) profile.AddUnit(id);
            return profile;
        }

        /// <summary>임시 파일에 쓴 뒤 바꿔 넣어, 쓰는 중에 꺼져도 예전 파일이 남게 한다.</summary>
        public static void Save(PlayerProfile profile, string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(profile.ToSnapshot(), true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
