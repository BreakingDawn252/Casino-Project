/* * Canvas Name: ChipObject
 * Version: 3
 */
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ChipObject : UdonSharpBehaviour
{
    private Vector3 _startPos;
    private Vector3 _targetPos;
    private Quaternion _startRot;
    private Quaternion _targetRot;
    
    private float _duration;
    private float _timeElapsed;
    private bool _isMoving = false;
    private bool _hideAfterMove = false;

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;

    void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (_renderer == null)
        {
            _renderer = (Renderer)GetComponent(typeof(Renderer));
            _propBlock = new MaterialPropertyBlock();
        }
    }

    public void SetColor(Color newColor)
    {
        InitializeComponents();
        if (_renderer != null && _propBlock != null)
        {
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_Color", newColor);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }

    // ★追加: アニメーションせずに指定位置へ瞬間配置する（手元に出現させる用）
    public void PlaceAt(Vector3 targetPos, Quaternion targetRot)
    {
        _isMoving = false;
        transform.position = targetPos;
        transform.rotation = targetRot;
        gameObject.SetActive(true);
    }

    public void MoveTo(Vector3 targetPos, Quaternion targetRot, float duration, bool hideAfter)
    {
        _startPos = transform.position;
        _startRot = transform.rotation;
        _targetPos = targetPos;
        _targetRot = targetRot;
        _duration = duration;
        
        _timeElapsed = 0f;
        _isMoving = true;
        _hideAfterMove = hideAfter;
        
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (_isMoving)
        {
            _timeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_timeElapsed / _duration);
            
            float easeT = t * t * (3f - 2f * t);
            
            transform.position = Vector3.Lerp(_startPos, _targetPos, easeT);
            transform.rotation = Quaternion.Slerp(_startRot, _targetRot, easeT);

            if (t >= 1f)
            {
                _isMoving = false;
                if (_hideAfterMove)
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }
}