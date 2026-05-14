using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AutoAssignFlatKitMaterials
{
    // Material GUIDs in Assets/AIMaterial/
    const string PoleGUID         = "dd0b6ef07e924d7988cdf0750224a8f0";
    const string TreeGUID         = "8306dce4255d4b5aa161659b892a44f8";
    const string GreenBeltGUID    = "3ebc3b6809e444a8b9addee0867736e4";
    const string GreenBeltBaseGUID = "199eb835218546da90061511da38c387";
    const string RoadSignGUID     = "180dfde24a9c41e68944f730e2a2c22d";
    const string BuildingGUID     = "7c5eba564a0b4312a4363373e486f959";

    [MenuItem("Tools/FlatKit/Auto-Assign AIMaterials to 第一关2")]
    public static void AssignMaterials()
    {
        Material poleMat         = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath(PoleGUID));
        Material treeMat         = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath(TreeGUID));
        Material greenBeltMat    = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath(GreenBeltGUID));
        Material greenBaseMat    = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath(GreenBeltBaseGUID));
        Material roadSignMat     = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath(RoadSignGUID));
        Material buildingMat     = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath(BuildingGUID));

        if (poleMat == null || treeMat == null || greenBeltMat == null ||
            greenBaseMat == null || roadSignMat == null || buildingMat == null)
        {
            Debug.LogError("One or more AIMaterial materials could not be loaded. Check the AIMaterial folder.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject targetParent = null;

        foreach (GameObject go in roots)
        {
            if (go.name == "第一关2")
            {
                targetParent = go;
                break;
            }
        }

        if (targetParent == null)
        {
            Debug.LogError("Could not find GameObject '第一关2' in the scene.");
            return;
        }

        int assigned = 0;
        AssignRecursive(targetParent.transform);

        Debug.Log($"FlatKit material assignment complete. {assigned} MeshRenderers updated.");

        void AssignRecursive(Transform t)
        {
            foreach (Transform child in t)
            {
                string name = child.name;
                MeshRenderer mr = child.GetComponent<MeshRenderer>();

                if (mr != null)
                {
                    Material mat = GetMaterialForName(name, poleMat, treeMat,
                        greenBeltMat, greenBaseMat, roadSignMat, buildingMat);

                    if (mat != null)
                    {
                        mr.sharedMaterial = mat;
                        assigned++;
                        Debug.Log($"  Assigned {mat.name} → {name}", child);
                    }
                }

                // Recurse into children
                AssignRecursive(child);
            }
        }
    }

    static Material GetMaterialForName(string name,
        Material pole, Material tree, Material greenBelt,
        Material greenBase, Material roadSign, Material building)
    {
        // Order matters: check sub-categories first
        if (name.Contains("绿化带下") || name.Contains("底座")
            || name.Contains("地左") || name.Contains("地右")
            || name.Contains("地板"))
            return greenBase;

        if (name.Contains("绿化带"))
            return greenBelt;

        if (name.Contains("路牌杆"))
            return pole; // 路牌杆 goes with poles

        if (name.Contains("路牌") || name.Contains("广告"))
            return roadSign;

        if (name.Contains("杆"))
            return pole;

        if (name.Contains("树"))
            return tree;

        if (name.Contains("楼"))
            return building;

        return null;
    }
}
