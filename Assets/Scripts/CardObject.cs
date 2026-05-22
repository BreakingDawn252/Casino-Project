using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CardObject : UdonSharpBehaviour
{
    [Header("Visual References")]
    [Tooltip("カードのメッシュを持つRenderer")]
    public MeshRenderer cardRenderer;
    [Tooltip("マテリアルインデックス")]
    public int materialIndex = 0;

    // アトラスUV計算用定数 (13枚 x 4スーツ)
    private float _tilingX = 1.0f / 13.0f;
    private float _tilingY = 1.0f / 4.0f;

    private MaterialPropertyBlock _propBlock;

    // 移動アニメーション用
    private bool _isMoving = false;
    private Vector3 _startPos;
    private Quaternion _startRot;
    private Vector3 _targetPos;
    private Quaternion _targetRot;
    private float _moveDuration;
    private float _elapsedTime;

    void Start()
    {
        _propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// カードの絵柄を適用します。
    /// MaterialPropertyBlockを使用することで、GPUインスタンシングを維持したまま絵柄を変更します。
    /// </summary>
    /// <param name="cardId">0-51のカードID</param>
    /// <param name="faceUp">表向きかどうか（演出用）</param>
    public void SetCard(int cardId, bool faceUp)
    {
        if (cardRenderer == null) return;
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        int col = cardId % 13;
        int row = cardId / 13;

        float offsetX = col * _tilingX;
        float offsetY = (3 - row) * _tilingY; // テクスチャの原点が左下の場合の反転

        cardRenderer.GetPropertyBlock(_propBlock, materialIndex);
        
        // _MainTex_ST (Tiling.x, Tiling.y, Offset.x, Offset.y) を上書き
        Vector4 st = new Vector4(_tilingX, _tilingY, offsetX, offsetY);
        _propBlock.SetVector("_MainTex_ST", st);
        
        cardRenderer.SetPropertyBlock(_propBlock, materialIndex);
    }

    /// <summary>
    /// 指定された座標と回転に向けて、滑らかなイージングを伴う移動を開始します。
    /// </summary>
    public void MoveTo(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        _startPos = transform.position;
        _startRot = transform.rotation;
        _targetPos = targetPos;
        _targetRot = targetRot;
        _moveDuration = duration;
        _elapsedTime = 0f;
        _isMoving = true;
    }

    void Update()
    {
        if (!_isMoving) return;

        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _moveDuration);

        // SmoothStepイージング: 3t^2 - 2t^3
        float smoothT = t * t * (3f - 2f * t);

        transform.position = Vector3.Lerp(_startPos, _targetPos, smoothT);
        transform.rotation = Quaternion.Slerp(_startRot, _targetRot, smoothT);

        if (t >= 1.0f) _isMoving = false;
    }
}