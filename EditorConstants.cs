using Game.MapEditor;
using static Game.MapEditor.MapEditorCreateMenu;

namespace PassengerPlus
{
    internal class EditorConstants
    {
        public static readonly EditorConstants Timetable =
            new EditorConstants(100, "timetable", "Timetable");

        public static readonly EditorConstants TimetableBranch =
            new EditorConstants(101, "timetable/", "Timetable Branch");

        public readonly ItemKind ItemKind;
        public readonly SelectableItem.Kind SelectableKind;
        public readonly string OutlineId;
        public readonly string OutlineName;

        private EditorConstants(int itemKind, string outlineId, string outlineName)
        {
            ItemKind = (ItemKind)itemKind;
            SelectableKind = (SelectableItem.Kind)itemKind;
            OutlineId = outlineId;
            OutlineName = outlineName;
        }

        public static string TimetableIdentifier(int branchIndex)
        {
            return MapEditorOutline.JoinIdentifier("timetable/", branchIndex.ToString(), null, null);
        }
    }
}