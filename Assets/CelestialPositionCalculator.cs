using System;
using UnityEngine;

/// <summary>
/// 地球（baseplate）を固定した状態で、指定した日時における
/// 太陽と月の「見かけの位置（Vector3）」だけを計算するスクリプト。
/// 実際に天体オブジェクトを動かしたり回転させたりする処理は含めていません。
///
/// 考え方:
///   1. 指定日時から太陽・月の赤道座標(赤経RA・赤緯Dec)を天文計算で求める
///   2. 観測地点(緯度・経度)と地方恒星時から、地平座標(方位角Az・高度角Alt)に変換する
///   3. 方位角・高度角を、earthBaseplateを基準にしたローカル方向ベクトルに変換し、
///      距離をかけてワールド座標のVector3にする
///
/// 座標の向き（earthBaseplateのローカル軸を基準）:
///   +Y = 天頂（真上）
///   +Z = 北
///   +X = 東
/// この向きに合わせてbaseplateを回転させておくと、緯度経度どおりの空が再現できます。
/// </summary>
public class CelestialPositionCalculator : MonoBehaviour
{
    [Header("固定された地球（観測地点の基準トランスフォーム）")]
    [Tooltip("+Y=天頂, +Z=北, +X=東 となるように配置・回転しておく")]
    public Transform earthBaseplate;

    [Header("観測地点の緯度・経度（度）")]
    public double latitude = 35.0;    // 北緯はプラス
    public double longitude = 135.0;  // 東経はプラス

    [Header("時間設定")]
    [Tooltip("ONならリアルタイム(UTC)を使用。OFFなら下の日時を使う")]
    public bool useRealTimeUtc = true;
    public int year = 2026, month = 9, day = 6, hour = 12, minute = 0, second = 0;
    [Tooltip("useRealTimeUtcがOFFのときの時間経過の倍速")]
    public float timeScale = 1f;

    [Header("見た目の配置距離（Unity単位）")]
    public float sunVisualDistance = 5000f;
    public float moonVisualDistance = 1000f;
    [Tooltip("ONなら実際の遠地点/近地点の距離比に応じて月の見た目距離を変動させる")]
    public bool useRealMoonDistanceRatio = true;
    public float Ratio = 0.1f;

    [Header("配置するオブジェクト（任意）")]
    [Tooltip("計算した位置に自動でPositionを反映させたい太陽オブジェクト。未設定でも計算自体は行われます")]
    public Transform sunObject;
    [Tooltip("計算した位置に自動でPositionを反映させたい月オブジェクト。未設定でも計算自体は行われます")]
    public Transform moonObject;

    // ---- 計算結果（読み取り専用） ----
    public Vector3 SunPosition { get; private set; }
    public Vector3 MoonPosition { get; private set; }
    public double SunAzimuthDeg { get; private set; }
    public double SunAltitudeDeg { get; private set; }
    public double MoonAzimuthDeg { get; private set; }
    public double MoonAltitudeDeg { get; private set; }
    public double MoonDistanceKm { get; private set; }

    private double simulatedSeconds = 0.0;

    void Update()
    {
        DateTime utc;
        if (useRealTimeUtc)
        {
            utc = DateTime.UtcNow;
        }
        else
        {
            simulatedSeconds += Time.deltaTime * timeScale;
            DateTime baseTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            utc = baseTime.AddSeconds(simulatedSeconds);
        }

        UpdatePositions(utc);
    }

    /// <summary>指定したUTC日時から太陽・月の位置を計算して更新する</summary>
    public void UpdatePositions(DateTime utc)
    {
        if (earthBaseplate == null) return;

        double jd = ToJulianDate(utc);
        double T = (jd - 2451545.0) / 36525.0;
        double eps = MeanObliquity(T);
        double lst = LocalSiderealTime(jd, longitude);

        // ---- 太陽 ----
        (double sunRA, double sunDec) = SunEquatorial(T, eps);
        (double sunAz, double sunAlt) = Horizontal(sunRA, sunDec, lst, latitude);
        SunAzimuthDeg = sunAz;
        SunAltitudeDeg = sunAlt;
        SunPosition = HorizontalToWorldPosition(sunAz, sunAlt, sunVisualDistance);

        // ---- 月 ----
        (double moonRA, double moonDec, double moonDistKm) = MoonEquatorial(T, eps);
        (double moonAz, double moonAlt) = Horizontal(moonRA, moonDec, lst, latitude);
        MoonAzimuthDeg = moonAz;
        MoonAltitudeDeg = moonAlt;
        MoonDistanceKm = moonDistKm;

        float moonDist = moonVisualDistance;
        if (useRealMoonDistanceRatio)
        {
            // 平均距離(約384400km)を基準に、近地点/遠地点の比率を見た目の距離に反映
            moonDist = moonVisualDistance * (float)(moonDistKm / 384400.0);
        }
        MoonPosition = HorizontalToWorldPosition(moonAz, moonAlt, moonDist);

        // ---- アタッチされたオブジェクトの位置を更新（位置のみ。回転は変更しない） ----
        if (sunObject != null) sunObject.position = SunPosition;
        if (moonObject != null) moonObject.position = MoonPosition;
    }

    // ================= 地平座標 → ワールド座標 =================

    private Vector3 HorizontalToWorldPosition(double azimuthDeg, double altitudeDeg, float distance)
    {
        double az = Deg2Rad(azimuthDeg);
        double alt = Deg2Rad(altitudeDeg);

        float x = (float)(Math.Cos(alt) * Math.Sin(az) * Ratio); // 東
        float y = (float)(Math.Sin(alt) * Ratio);                  // 上
        float z = (float)(Math.Cos(alt) * Math.Cos(az) * Ratio); // 北

        Vector3 localDir = new Vector3(x, y, z);
        Vector3 worldDir = earthBaseplate.TransformDirection(localDir);
        return earthBaseplate.position + worldDir * distance;
    }

    // ================= 天文計算（簡易式） =================

    private static double ToJulianDate(DateTime utc)
    {
        int Y = utc.Year;
        int M = utc.Month;
        double D = utc.Day + (utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0) / 24.0;

        if (M <= 2) { Y -= 1; M += 12; }
        int A = Y / 100;
        int B = 2 - A + A / 4;

        return Math.Floor(365.25 * (Y + 4716)) + Math.Floor(30.6001 * (M + 1)) + D + B - 1524.5;
    }

    private static double MeanObliquity(double T)
    {
        // 平均黄道傾斜角（章動は無視した簡易式）
        return 23.439291 - 0.0130042 * T - 1.64e-7 * T * T + 5.04e-7 * T * T * T;
    }

    private static double LocalSiderealTime(double jd, double longitudeDeg)
    {
        double d = jd - 2451545.0;
        double T = d / 36525.0;
        double gmst = 280.46061837 + 360.98564736629 * d + 0.000387933 * T * T - (T * T * T) / 38710000.0;
        return Mod360(gmst + longitudeDeg);
    }

    /// <summary>太陽の赤経・赤緯（度）（Meeusの低精度式）</summary>
    private static (double ra, double dec) SunEquatorial(double T, double eps)
    {
        double L0 = Mod360(280.46646 + 36000.76983 * T + 0.0003032 * T * T);
        double M = Mod360(357.52911 + 35999.05029 * T - 0.0001537 * T * T);
        double Mrad = Deg2Rad(M);

        double C = (1.914602 - 0.004817 * T - 0.000014 * T * T) * Math.Sin(Mrad)
                 + (0.019993 - 0.000101 * T) * Math.Sin(2 * Mrad)
                 + 0.000289 * Math.Sin(3 * Mrad);

        double trueLong = L0 + C;
        double omega = 125.04 - 1934.136 * T;
        double appLong = trueLong - 0.00569 - 0.00478 * Math.Sin(Deg2Rad(omega));
        double epsCorrected = eps + 0.00256 * Math.Cos(Deg2Rad(omega));

        double appLongRad = Deg2Rad(appLong);
        double epsRad = Deg2Rad(epsCorrected);

        double ra = Rad2Deg(Math.Atan2(Math.Cos(epsRad) * Math.Sin(appLongRad), Math.Cos(appLongRad)));
        double dec = Rad2Deg(Math.Asin(Math.Sin(epsRad) * Math.Sin(appLongRad)));

        return (Mod360(ra), dec);
    }

    /// <summary>月の赤経・赤緯（度）と地心距離（km）（主要項のみの簡易式）</summary>
    private static (double ra, double dec, double distKm) MoonEquatorial(double T, double eps)
    {
        double d = T * 36525.0; // J2000からの経過日数

        double L0 = Mod360(218.316 + 13.176396 * d); // 平均黄経
        double M = Mod360(134.963 + 13.064993 * d);  // 平均近点角
        double F = Mod360(93.272 + 13.229350 * d);   // 平均離角（緯度引数）

        double Mrad = Deg2Rad(M);
        double Frad = Deg2Rad(F);

        double lon = L0 + 6.289 * Math.Sin(Mrad);
        double lat = 5.128 * Math.Sin(Frad);
        double dist = 385001.0 - 20905.0 * Math.Cos(Mrad); // km

        double lonRad = Deg2Rad(lon);
        double latRad = Deg2Rad(lat);
        double epsRad = Deg2Rad(eps);

        double ra = Rad2Deg(Math.Atan2(
            Math.Sin(lonRad) * Math.Cos(epsRad) - Math.Tan(latRad) * Math.Sin(epsRad),
            Math.Cos(lonRad)));
        double dec = Rad2Deg(Math.Asin(
            Math.Sin(latRad) * Math.Cos(epsRad) + Math.Cos(latRad) * Math.Sin(epsRad) * Math.Sin(lonRad)));

        return (Mod360(ra), dec, dist);
    }

    /// <summary>赤道座標(RA,Dec)を地平座標(方位角,高度角)に変換</summary>
    private static (double az, double alt) Horizontal(double raDeg, double decDeg, double lstDeg, double latDeg)
    {
        double H = Deg2Rad(Mod360(lstDeg - raDeg));
        double dec = Deg2Rad(decDeg);
        double lat = Deg2Rad(latDeg);

        double sinAlt = Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(H);
        double alt = Math.Asin(sinAlt);

        double azRad = Math.Atan2(
            Math.Sin(H),
            Math.Cos(H) * Math.Sin(lat) - Math.Tan(dec) * Math.Cos(lat));

        // atan2の結果（南基準・西回り）を、北基準・時計回りの方位角に変換
        double az = Mod360(Rad2Deg(azRad) + 180.0);

        return (az, Rad2Deg(alt));
    }

    // ================= ユーティリティ =================

    private static double Deg2Rad(double deg) => deg * Math.PI / 180.0;
    private static double Rad2Deg(double rad) => rad * 180.0 / Math.PI;
    private static double Mod360(double deg)
    {
        double m = deg % 360.0;
        return m < 0 ? m + 360.0 : m;
    }
}