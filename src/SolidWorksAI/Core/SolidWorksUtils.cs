using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Runtime.InteropServices;

namespace SolidWorksAI.Core;

public static class SolidWorksUtils
{
    public static void SafeRelease(object? obj)
    {
        if (obj != null && Marshal.IsComObject(obj))
        {
            Marshal.ReleaseComObject(obj);
        }
    }

    public static string ToTurkishMessage(int errorCode)
    {
        if (errorCode == (int)swFileLoadError_e.swFileNotFoundError) return "Dosya bulunamadı.";
        if (errorCode == (int)swFileLoadError_e.swGenericError) return "Bilinmeyen bir hata oluştu.";
        if (errorCode == (int)swFileLoadError_e.swFutureVersion) return "Dosya daha yeni bir SolidWorks sürümü ile oluşturulmuş.";
        
        return $"Yükleme hatası kodu: {errorCode}";
    }

    public static string ToSaveErrorMessage(int errorCode)
    {
        if (errorCode == (int)swFileSaveError_e.swGenericSaveError) return "Kaydetme sırasında genel hata.";
        if (errorCode == (int)swFileSaveError_e.swReadOnlySaveError) return "Dosya salt okunur olduğu için kaydedilemedi.";
        if (errorCode == (int)swFileSaveError_e.swFileNameEmpty) return "Dosya adı boş olamaz.";
        
        return $"Kaydetme hatası kodu: {errorCode}";
    }

    public static int MapMateType(string type)
    {
        return (type.ToUpper()) switch
        {
            "COINCIDENT" => (int)swMateType_e.swMateCOINCIDENT,
            "CONCENTRIC" => (int)swMateType_e.swMateCONCENTRIC,
            "PARALLEL" => (int)swMateType_e.swMatePARALLEL,
            "PERPENDICULAR" => (int)swMateType_e.swMatePERPENDICULAR,
            "TANGENT" => (int)swMateType_e.swMateTANGENT,
            "DISTANCE" => (int)swMateType_e.swMateDISTANCE,
            "ANGLE" => (int)swMateType_e.swMateANGLE,
            _ => (int)swMateType_e.swMateCOINCIDENT
        };
    }

    public static string GetDefaultTemplatePath(ISldWorks sw, int type)
    {
        var pref = type switch
        {
            1 => (int)swUserPreferenceStringValue_e.swDefaultTemplatePart,
            2 => (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly,
            3 => (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing,
            _ => -1
        };

        if (pref == -1) return "";
        return sw.GetUserPreferenceStringValue(pref) ?? "";
    }

    public static bool SelectStandardPlane(IModelDoc2 doc, string planeName)
    {
        // Plane index: Front=1, Top=2, Right=3
        int targetIndex = planeName.ToUpper() switch
        {
            "FRONT PLANE" or "FRONT" or "ÖN DÜZLEM" or "ÖN" or "BACK" or "ARKA" => 1,
            "TOP PLANE" or "TOP" or "ÜST DÜZLEM" or "ÜST" or "BOTTOM" or "ALT" => 2,
            "RIGHT PLANE" or "RIGHT" or "SAĞ DÜZLEM" or "SAĞ" or "LEFT" or "SOL" => 3,
            _ => 0
        };

        if (targetIndex == 0) return false;

        // Try selecting by common names first as a shortcut
        string[] commonNames = { "Front Plane", "Ön Düzlem", "Top Plane", "Üst Düzlem", "Right Plane", "Sağ Düzlem" };
        foreach (var name in commonNames)
        {
            if (doc.Extension.SelectByID2(name, "PLANE", 0, 0, 0, false, 0, null, 0)) return true;
        }

        // Fallback to index-based traversal (most robust)
        Feature swFeat = (Feature)doc.FirstFeature();
        int currentPlaneIndex = 0;

        while (swFeat != null)
        {
            if (swFeat.GetTypeName2() == "RefPlane")
            {
                currentPlaneIndex++;
                if (currentPlaneIndex == targetIndex)
                {
                    return swFeat.Select2(false, 0);
                }
            }
            swFeat = (Feature)swFeat.GetNextFeature();
        }
        return false;
    }

    public static bool SelectFaceAt(IModelDoc2 doc, double x, double y, double z)
    {
        // SelectByID2 handles coordinate based selection
        return doc.Extension.SelectByID2("", "FACE", x, y, z, false, 0, null, 0);
    }

    public static bool SelectFaceByDirection(IModelDoc2 doc, string direction)
    {
        // Get the last feature (usually the one just created)
        Feature swFeat = (Feature)doc.FeatureByPositionReverse(0);
        if (swFeat == null) return false;

        object[]? faces = swFeat.GetFaces() as object[];
        if (faces == null || faces.Length == 0) return false;

        IFace2? bestFace = null;
        double bestVal = direction.Contains("-") ? double.MaxValue : double.MinValue;

        int axis = direction.ToUpper().Contains("X") ? 0 : direction.ToUpper().Contains("Y") ? 1 : 2;
        bool isMax = !direction.Contains("-");
        
        // Handle descriptive names
        if (direction.ToUpper() == "TOP") { axis = 1; isMax = true; }
        else if (direction.ToUpper() == "BOTTOM") { axis = 1; isMax = false; }
        else if (direction.ToUpper() == "FRONT") { axis = 2; isMax = true; }
        else if (direction.ToUpper() == "BACK") { axis = 2; isMax = false; }

        foreach (var fObj in faces)
        {
            IFace2 face = (IFace2)fObj;
            double[] box = (double[])face.GetBox();
            // box: [minX, minY, minZ, maxX, maxY, maxZ]
            double currentVal = isMax ? box[axis + 3] : box[axis];

            if (isMax)
            {
                if (currentVal > bestVal) { bestVal = currentVal; bestFace = face; }
            }
            else
            {
                if (currentVal < bestVal) { bestVal = currentVal; bestFace = face; }
            }
        }

        if (bestFace != null)
        {
            return ((IEntity)bestFace).Select4(false, null);
        }

        return false;
    }

    public static double ToMeters(double value, string unit = "MM")
    {
        return (unit.ToUpper()) switch
        {
            "MM" => value / 1000.0,
            "CM" => value / 100.0,
            "INCH" or "IN" => value * 0.0254,
            _ => value
        };
    }
}
