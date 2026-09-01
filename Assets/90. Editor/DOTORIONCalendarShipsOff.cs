using DOTORION.UI;
using UnityEditor;
using UnityEngine;

namespace DOTORION.Editor
{
    /// <summary>
    /// Switches the statistics calendar back off in the prefab.
    ///
    /// TeamStatisticsPanelView decides which of the two daily readings to show
    /// and turns this on only for the month, so the saved state is not what the
    /// person ever sees for long - but a calendar left on ships visible for the
    /// frame between the panel opening and the first bind, over whichever list
    /// belongs there. PrefabAssetTests pins it off for that reason.
    ///
    /// Easy to switch back on by accident just by opening it in the editor, so
    /// this is here to put it back without hand-editing YAML - which Unity
    /// overwrites from memory while the project is open anyway.
    /// </summary>
    public static class DOTORIONCalendarShipsOff
    {
        private const string CalendarPath = "WindowBackground/StatisticsPanel/DailyContent/Calendar";

        [MenuItem("DOTORI ON/Ship Statistics Calendar Off")]
        public static void ShipCalendarOff()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.MainViewPath);
            try
            {
                var calendar = root.transform.Find(CalendarPath);
                if (calendar == null)
                {
                    Debug.LogError("Could not find " + CalendarPath + " in the main view prefab.");
                    return;
                }

                if (!calendar.gameObject.activeSelf)
                {
                    Debug.Log("The statistics calendar already ships off. Nothing changed.");
                    return;
                }

                calendar.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.MainViewPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Statistics calendar set to ship off.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
