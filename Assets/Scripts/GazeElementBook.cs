using System.Collections.Generic;
using UnityEngine;

public class GazeElementBook : MonoBehaviour
{
    private Transform spine, left, right;
    private readonly List<Material> materials = new List<Material>();

    public void Initialize(Material reference, Color coverColor, Material paperReference)
    {
        Material cover = MakeMaterial(reference, coverColor, "Element Book Cover");
        Material paper = MakeMaterial(paperReference != null ? paperReference : reference,
            new Color(0.94f, 0.91f, 0.78f), "Element Book Pages");
        Material ink = MakeMaterial(reference, new Color(0.12f, 0.17f, 0.22f), "Spellbook Ink");
        Material trim = MakeMaterial(reference, new Color(0.78f, 0.62f, 0.25f), "Spellbook Trim");
        spine = new GameObject("Book Spine").transform;
        spine.SetParent(transform, false);
        Cube("Spine", spine, new Vector3(0f, 0f, 0f), new Vector3(0.025f, 0.36f, 0.065f), cover);
        for (int side = 0; side < 2; side++)
        {
            Transform leaf = new GameObject(side == 0 ? "Left Cover and Pages" : "Right Cover and Pages").transform;
            leaf.SetParent(spine, false);
            if (side == 0) left = leaf; else right = leaf;
            float sign = side == 0 ? -1f : 1f;
            Cube("Cover", leaf, new Vector3(0.12f, 0f, sign * 0.032f), new Vector3(0.24f, 0.36f, 0.014f), cover);
            for (int page = 0; page < 4; page++)
                Cube("Paper", leaf, new Vector3(0.12f, 0f, sign * (0.006f + page * 0.005f)),
                    new Vector3(0.218f, 0.337f, 0.0045f), paper);
            float face = sign * 0.002f;
            Cube("Element Heading", leaf, new Vector3(0.12f, 0.11f, face), new Vector3(0.13f, 0.013f, 0.002f), trim);
            for (int row = 0; row < 6; row++)
                Cube("Spell Line", leaf, new Vector3(0.115f, 0.065f - row * 0.031f, face),
                    new Vector3(0.13f - (row % 3) * 0.018f, 0.005f, 0.002f), ink);
            Cube("Cover Border Top", leaf, new Vector3(0.12f, 0.155f, sign * 0.041f), new Vector3(0.2f, 0.008f, 0.003f), trim);
            Cube("Cover Border Bottom", leaf, new Vector3(0.12f, -0.155f, sign * 0.041f), new Vector3(0.2f, 0.008f, 0.003f), trim);
            Transform emblem = Cube("Element Seal", leaf, new Vector3(0.12f, 0f, sign * 0.041f), new Vector3(0.065f, 0.065f, 0.004f), trim);
            emblem.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        SetOpen(0f);
    }

    private Material MakeMaterial(Material reference, Color color, string label)
    {
        Material material = new Material(reference) { name = label };
        foreach (string texture in material.GetTexturePropertyNames()) material.SetTexture(texture, null);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        materials.Add(material);
        return material;
    }

    private Transform Cube(string label, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = label;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        Collider collider = cube.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube.transform;
    }

    public void SetOpen(float progress)
    {
        float t = Mathf.Clamp01(progress);
        if (left == null || right == null) return;
        left.localRotation = Quaternion.Euler(0f, -165f * t, 0f);
        right.localRotation = Quaternion.Euler(0f, -15f * t, 0f);
        spine.localPosition = Vector3.left * (0.12f * (1f - t));
    }

    private void OnDestroy()
    {
        foreach (Material material in materials) if (material != null) Destroy(material);
    }
}
