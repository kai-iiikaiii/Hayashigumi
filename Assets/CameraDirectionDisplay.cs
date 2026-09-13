using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 指定したカメラが水平方向のどちらを向いているかを求め、
/// UI Textに16方位（北・北北東…）と角度で表示するスクリプト。
///
/// earthBaseplateのローカル軸を「+X=東, +Z=北, +Y=天頂」の基準として使うので、
/// CelestialPositionCalculatorと同じbaseplateを指定してください。
/// </summary>
public class CameraDirectionDisplay : MonoBehaviour
{
    [Header("向きを調べたいカメラ")]
    public Camera viewerCamera;

    [Header("方角の基準（+X=東, +Z=北 となっているTransform）")]
    public Transform earthBaseplate;

    [Header("表示先のUI Text（例: '北（32°）'）")]
    public Text directionText;

    /// <summary>カメラの水平方位角（度・北0°、時計回り）</summary>
    public double AzimuthDeg { get; private set; }

    /// <summary>16方位の日本語表記（例: '北北東'）</summary>
    public string CompassDirection { get; private set; }

    void Update()
    {
        UpdateDirection();
    }

    /// <summary>カメラの向いている方角を計算し、directionTextに反映する</summary>
    public void UpdateDirection()
    {
        if (viewerCamera == null || earthBaseplate == null) return;

        // カメラの前方向を、earthBaseplateのローカル軸(+X=東,+Z=北)基準に変換する
        Vector3 localForward = earthBaseplate.InverseTransformDirection(viewerCamera.transform.forward);

        // 高度方向(Y)は無視し、水平面(X,Z)だけで方位角を求める
        double az = Mod360(Rad2Deg(Math.Atan2(localForward.x, localForward.z)));

        AzimuthDeg = az;
        CompassDirection = AzimuthToCompass(az);

        if (directionText != null)
        {
            directionText.text = $"{CompassDirection}（{az:F0}°）";
        }
    }

    /// <summary>方位角(度)を16方位の日本語表記に変換する</summary>
    private static string AzimuthToCompass(double azimuthDeg)
    {
        string[] names =
        {
            "北", "北北東", "北東", "東北東",
            "東", "東南東", "南東", "南南東",
            "南", "南南西", "南西", "西南西",
            "西", "西北西", "北西", "北北西"
        };

        int index = (int)Math.Round(azimuthDeg / 22.5) % 16;
        return names[index];
    }

    private static double Rad2Deg(double rad) => rad * 180.0 / Math.PI;
    private static double Mod360(double deg)
    {
        double m = deg % 360.0;
        return m < 0 ? m + 360.0 : m;
    }
}
