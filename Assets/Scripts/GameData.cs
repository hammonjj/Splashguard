using System.Collections.Generic;
using System.Linq;
using BitBox.Toymageddon;

namespace Bitbox.Toymageddon
{
    public static class GameData
    {
        public static List<CharacterSelectionData> CharacterSelectionData { get; } = new List<CharacterSelectionData>();

        public static CharacterSelectionData GetCharacterSelectionDataForPlayer(int playerIndex)
        {
            return GetOrCreateCharacterSelectionData(playerIndex);
        }

        public static void EnsureCharacterSelectionEntry(int playerIndex)
        {
            GetOrCreateCharacterSelectionData(playerIndex);
        }

        public static bool RemoveCharacterSelectionDataForPlayer(int playerIndex)
        {
            var data = CharacterSelectionData.FirstOrDefault(item => item.PlayerIndex == playerIndex);
            if (data == null)
            {
                return false;
            }

            return CharacterSelectionData.Remove(data);
        }

        public static void ClearCharacterSelectionSession()
        {
            CharacterSelectionData.Clear();
        }

        private static CharacterSelectionData GetOrCreateCharacterSelectionData(int playerIndex)
        {
            CharacterSelectionData data = CharacterSelectionData.FirstOrDefault(item => item.PlayerIndex == playerIndex);
            if (data != null)
            {
                return data;
            }

            data = new CharacterSelectionData
            {
                PlayerIndex = playerIndex
            };

            CharacterSelectionData.Add(data);
            return data;
        }
    }
}
