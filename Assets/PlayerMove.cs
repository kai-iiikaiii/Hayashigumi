using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    // パラメータ
    public float moveSpeed = 5f;           // 移動速度
    public float gravity = -9.8f;        // 重力加速度
    public CharacterController controller;  // 移動に使うCharacterController

    // 演算用変数
    private Vector3 velocity;       // 加速度を保持する変数
    private bool isGrounded;        // 地面に着地しているかどうかのフラグ変数

    // ゲーム中実行されるUpdate関数
    void Update()
    {
        // 着地状態のチェック
        isGrounded = controller.isGrounded;

        // 着地している場合は落下速度をリセット
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // 地面に着いた場合、速度をリセット
        }

        // 入力の取得
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // ローカル座標をワールド座標に変換して移動方向を計算
        Vector3 moveDirection = transform.TransformDirection(new Vector3(h, 0, v)) * moveSpeed;

        // 重力を加算
        velocity.y += gravity * Time.deltaTime;

        // 移動と重力を一度のcontroller.Moveで処理
        controller.Move((moveDirection + velocity) * Time.deltaTime);
    }
}
