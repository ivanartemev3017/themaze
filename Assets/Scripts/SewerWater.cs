using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class SewerWater : MonoBehaviour
{
    public Vector2 scrollSpeed = new Vector2(0.015f, 0.008f);

    Renderer _renderer;
    Vector2 _offset;

    void Start() => _renderer = GetComponent<Renderer>();

    void Update()
    {
        _offset += scrollSpeed * Time.deltaTime;
        _renderer.material.SetTextureOffset("_BaseMap", _offset);
    }
}
