using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class CasinoCardUI : UdonSharpBehaviour
{
    [Tooltip("カードを表示するRawImage")]
    public RawImage rawImage;
    [Tooltip("カード裏面の画像（SpriteやTexture）")]
    public Texture backTexture;

    // アトラスの分割数（横13枚、縦4枚）
    private float _tilingX = 1.0f / 13.0f;
    private float _tilingY = 1.0f / 4.0f;

    // 元のアトラス画像を保持しておく
    private Texture _atlasTexture;

    void Start()
    {
        if (rawImage != null) _atlasTexture = rawImage.texture;
    }

    public void SetCard(int cardId)
    {
        if (rawImage == null) return;
        
        // Startが呼ばれる前に対処
        if (_atlasTexture == null) _atlasTexture = rawImage.texture;

        // cardIdが -1 なら裏面を表示
        if (cardId == -1)
        {
            if (backTexture != null)
            {
                rawImage.texture = backTexture;
                // 全体を表示するようにUVをリセット
                rawImage.uvRect = new Rect(0, 0, 1, 1);
            }
            else
            {
                // 裏面画像がない場合は黒くするなどのフェールセーフ
                rawImage.color = Color.black; 
            }
            return;
        }

        // 通常のカード表示
        rawImage.texture = _atlasTexture;
        rawImage.color = Color.white;

        int col = cardId % 13;
        int row = cardId / 13;

        float x = col * _tilingX;
        float y = (3 - row) * _tilingY;

        rawImage.uvRect = new Rect(x, y, _tilingX, _tilingY);
    }
}