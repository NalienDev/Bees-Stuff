using UnityEngine;

public class UICubeTemplate : MonoBehaviour
{
    [SerializeField] private Texture2D _texture;
    [SerializeField] private Texture2D _topTexture;
    [SerializeField] private Material _templateMaterial;
    [SerializeField] private Color _topColor;
    private Renderer _renderer;

    private void Start()
    {
        if (_texture == null)
        {
            Debug.LogError("You forgot to assign atleast a top texture");
            return;
        }

        _renderer = GetComponent<Renderer>();
        _texture.filterMode = FilterMode.Point;

        Material mat = new Material(_templateMaterial);
        mat.mainTexture = _texture;

        if (_topTexture != null)
        {
            _topTexture.filterMode = FilterMode.Point;
            mat.SetTexture("_TopTex", _topTexture);
            mat.SetFloat("_HasTopTex", 1f);
            // dividir por 255 para converter para 0-1
            mat.SetColor("_TopColor", _topColor);
        }
        else
        {
            mat.SetFloat("_HasTopTex", 0f);
        }

        _renderer.material = mat;
    }
}
