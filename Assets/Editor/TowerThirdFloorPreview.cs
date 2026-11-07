using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Renders the decoration in an isolated preview scene without loading gameplay.
[InitializeOnLoad]
public static class TowerThirdFloorPreview
{
    private const string PrefabPath = "Assets/Object/Tower Third Floor Decor.prefab";
    private const string RequestPath = "Temp/TowerThirdFloorPreview.request";

    static TowerThirdFloorPreview()
    {
        EditorApplication.update += ProcessRequest;
    }

    private static void ProcessRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        try { RenderPreview(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/TowerThirdFloorPreview.error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("VR Assignment/Preview Third Floor Decoration")]
    public static void RenderPreview()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Third floor decoration prefab is missing.");
        PreviewRenderUtility preview = new PreviewRenderUtility();
        Texture2D image = null;
        try
        {
            GameObject furniture = UnityEngine.Object.Instantiate(prefab);
            furniture.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            preview.AddSingleGO(furniture);
            foreach (Transform part in furniture.GetComponentsInChildren<Transform>(true))
            {
                int expectedLayer = part.GetComponent<GazeOrrery>() != null ? 0 : 2;
                if (part.gameObject.layer != expectedLayer || GameObjectUtility.GetStaticEditorFlags(part.gameObject) != 0)
                    throw new InvalidOperationException("Unexpected decoration layer or Static flags: " + part.name);
            }
            foreach (Collider collider in furniture.GetComponentsInChildren<Collider>(true))
                if (!(collider is BoxCollider) || !collider.isTrigger || collider.GetComponent<GazeOrrery>() == null)
                    throw new InvalidOperationException("Only the orrery may contain a gaze trigger: " + collider.name);
            foreach (MonoBehaviour behaviour in furniture.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is GazeOrrery))
                    throw new InvalidOperationException("Unexpected decoration behaviour: " + behaviour.name);
            foreach (MeshFilter part in furniture.GetComponentsInChildren<MeshFilter>(true))
                if (part.sharedMesh == null) throw new InvalidOperationException("Missing primitive mesh: " + part.name);
            foreach (Renderer part in furniture.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in part.sharedMaterials)
                {
                    if (material == null) throw new InvalidOperationException("Missing material: " + part.name);
                    foreach (string texture in material.GetTexturePropertyNames())
                        if (material.GetTexture(texture) != null)
                            throw new InvalidOperationException("Textured decoration material: " + material.name);
                }

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.position = new Vector3(0f, -0.09f, 0f);
            floor.transform.localScale = new Vector3(10.7675f, 0.09f, 10.9496f);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Tower Interior/Dark Oak.mat");
            preview.AddSingleGO(floor);
            preview.camera.nearClipPlane = 0.05f;
            preview.camera.farClipPlane = 80f;
            preview.camera.fieldOfView = 52f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            preview.ambientColor = new Color(0.45f, 0.45f, 0.45f);
            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(45f, 35f, 0f);
            preview.lights[1].intensity = 0.7f;
            preview.lights[1].transform.rotation = Quaternion.Euler(35f, 215f, 0f);
            Vector3[] positions = { new Vector3(8f, 7.5f, 9f), new Vector3(0f, 2.3f, 4.25f) };
            Vector3[] targets = { new Vector3(0f, 0.6f, 0f), new Vector3(0f, 1.0f, -1.5f) };
            for (int i = 0; i < positions.Length; i++)
            {
                preview.camera.transform.position = positions[i];
                preview.camera.transform.LookAt(targets[i]);
                preview.BeginStaticPreview(new Rect(0f, 0f, 1400f, 1000f));
                preview.Render(true);
                image = preview.EndStaticPreview();
                File.WriteAllBytes("Temp/TowerThirdFloorPreview" + i + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                image = null;
            }
            File.WriteAllText("Temp/TowerThirdFloorPreview.result.txt",
                "PASS: prefab loaded in Unity; all primitive meshes and color-only materials resolved; " +
                "only the orrery has a gaze trigger and behaviour; no Static flags; other decoration uses Ignore Raycast. Rendered two views.");
        }
        finally
        {
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            preview.Cleanup();
        }
    }
}
