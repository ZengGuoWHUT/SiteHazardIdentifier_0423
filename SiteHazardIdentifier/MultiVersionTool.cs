using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteHazardIdentifier
{
    public static class MultiVerTool
    {
        public static ElementId String2ElementId(string elemIdString)
        {
#if Revit2024 || Revit2025 || Revit2026
            return new ElementId(long.Parse(elemIdString));
#else
            return new ElementId(int.Parse(elemIdString));
#endif
        }
        public static void ModifyElemColor(Element elem, View view, Color color)
        {
            OverrideGraphicSettings setting = view.GetElementOverrides(elem.Id);

#if Revit2018
             setting.SetProjectionFillColor(color);
#elif Revit2019 || Revit2020 || Revit2021 || Revit2022 || Revit2023 || Revit2024 || Revit2025 || Revit2026
            setting.SetSurfaceForegroundPatternColor(color);
#endif
            view.SetElementOverrides(elem.Id, setting);
        }
        public static void ModifyElemFillPatternId(Element elem, View view, ElementId patternId)
        {
            
            OverrideGraphicSettings setting = view.GetElementOverrides(elem.Id);

#if Revit2018
             setting.SetProjectionFillPatternId(patternId);
#elif Revit2019 || Revit2020 || Revit2021 || Revit2022 || Revit2023 || Revit2024 || Revit2025 || Revit2026
            setting.SetSurfaceForegroundPatternId(patternId);
#endif
            view.SetElementOverrides(elem.Id, setting);
        }

        public static void ModifyElemTansparency(Element elem, View view, int transparency)
        {

            OverrideGraphicSettings setting = view.GetElementOverrides(elem.Id);
            setting.SetSurfaceTransparency(transparency);
            view.SetElementOverrides(elem.Id, setting);
        }
    }
}
