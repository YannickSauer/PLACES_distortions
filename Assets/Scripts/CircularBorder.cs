using UnityEngine;


/// Adds a circular black border (vignette) over the rendered image.
/// Attach this to the Main Camera. Adjust BorderRadius in the inspector
/// to control how much of the periphery is masked out.
///
/// BorderRadius = 1.0 -> no border (full image visible)
/// BorderRadius = 0.9 -> small border (10% of the periphery masked)
/// ...
///
/// must be applied to all magnification conditions equally to keep the
/// visual stimulus consistent across groups.
[ExecuteInEditMode]
public class CircularBorder : MonoBehaviour
{
    [Range(0.1f, 1.0f)]
    [Tooltip("How big the visible (non-black) circle is. 1.0 = no border, 0.8 = 20% border ring")]
    public float borderRadius = 0.95f;

    [Range(0.0f, 0.5f)]
    [Tooltip("How soft the edge between visible and black is. 0 = hard edge, 0.5 = very soft falloff")]
    public float edgeSoftness = 0.02f;

    private Material borderMaterial;

    void Awake()
    {
        Shader shader = Shader.Find("Hidden/CircularBorder");
        if (shader == null)
        {
            Debug.LogError("CircularBorder shader not found. Make sure CircularBorder.shader exists in your project.");
            return;
        }
        borderMaterial = new Material(shader);
        borderMaterial.hideFlags = HideFlags.HideAndDontSave;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (borderMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        borderMaterial.SetFloat("_BorderRadius", borderRadius);
        borderMaterial.SetFloat("_EdgeSoftness", edgeSoftness);
        Graphics.Blit(source, destination, borderMaterial);
    }
}
