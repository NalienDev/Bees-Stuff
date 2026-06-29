using UnityEngine;

public class UICubeTemplate : MonoBehaviour
{
    [SerializeField] private Texture2D texture;
    [SerializeField] private Texture2D topTexture;
    [SerializeField] private Material templateMaterial;
    [SerializeField] private Color topColor;
    private Renderer renderer;

    private void Start()
    {
        if (texture == null)
        {
            Debug.LogError("You forgot to assign atleast a top texture");
            return;
        }

        renderer = GetComponent<Renderer>();
        texture.filterMode = FilterMode.Point;

        Material mat = new Material(templateMaterial);
        mat.mainTexture = texture;

        if (topTexture != null)
        {
            topTexture.filterMode = FilterMode.Point;
            mat.SetTexture("_TopTex", topTexture);
            mat.SetFloat("_HasTopTex", 1f);

            mat.SetColor("_TopColor", topColor);
        }
        else
        {
            mat.SetFloat("_HasTopTex", 0f);
        }

        renderer.material = mat;
    }
}
