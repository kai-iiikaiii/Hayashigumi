using UnityEngine;

public class PlayerPOV : MonoBehaviour
{
    // パラメータ
    public Transform neck;                  // プレイヤーの首のTransformを指定
    public float sensitivity = 2.0f;     // マウス感度（視点の移動の速さを調整）
    public float minVertical = -90.0f;   // 視点の最小角度（縦の回転制限）
    public float maxVertical = 90.0f;    // 視点の最大角度（縦の回転制限）

    // 演算用変数
    private float rotationX = 0f;       // 縦方向の回転角度（首の回転）

    // ゲーム開始時に呼ばれる
    void Start()
    {
        // カーソルを非表示＆ロック
        Cursor.lockState = CursorLockMode.Locked;   // カーソルを画面中央に固定
        Cursor.visible = false;                     // カーソルを非表示にする
    }


    // 毎フレーム実行される
    void Update()
    {
        // マウス入力の取得
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;  // 横のマウス移動量を取得し、感度で調整
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;  // 縦のマウス移動量を取得し、感度で調整

        // Player（体）の回転（左右）
        transform.Rotate(0, mouseX, 0);   // プレイヤー（体）の左右の回転をマウスX方向の入力に合わせて行う
       
        // Neck（首）の回転（上下）
        rotationX -= mouseY; // マウスY方向の入力によって縦方向の回転を更新
        rotationX = Mathf.Clamp(rotationX, minVertical, maxVertical);   // 回転角度を指定された範囲に制限
        neck.localRotation = Quaternion.Euler(rotationX, 0, 0);         // 首の回転を設定。縦方向のみ回転させる
    }
}
