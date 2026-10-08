using UnityEngine;

/// <summary>
/// Sphere がゴールに触れた瞬間にコード生成される一発限りのパーティクル。
/// Prefab を別途用意する必要がない（バイナリアセットなしで動く）ように
/// ParticleSystem を実行時に構築する。
///
/// 使い方：GoalEffect.Spawn(position, color);
/// </summary>
public static class GoalEffect
{
    // 全エフェクトで共有するマテリアル（毎回 new すると破棄されずにリークする）
    private static Material s_material;

    /// <summary>
    /// 指定位置に一発限りのエフェクトを生成する。再生終了後に自動で破棄される。
    /// </summary>
    public static GameObject Spawn(Vector3 position, Color color)
    {
        var go = new GameObject("GoalEffect");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();

        // ParticleSystem は AddComponent 直後に自動 Play 状態になるため、
        // 設定変更前に必ず Stop + Clear する。これを忘れると
        // 「Setting the duration while system is still playing is not supported」
        // という警告が出る。
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 短い 1 回きりの放出。再生が終わったら GameObject ごと自動破棄する
        var main = ps.main;
        main.duration        = 0.3f;
        main.loop            = false;
        main.startLifetime   = 0.6f;
        main.startSpeed      = 4f;
        main.startSize       = 0.15f;
        main.startColor      = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction      = ParticleSystemStopAction.Destroy;
        main.playOnAwake     = false;

        // 時間あたりの放出は 0 にして、開始時に 30 粒を 1 回だけ Burst
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

        // 発生位置を球体分散にする
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.2f;

        // 単色のままフェードアウト
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // サイズは 1.0 → 0.2 に縮小して消える
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

        // Sprites/Default はバイナリアセットなしで使えるシンプルなシェーダー
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (s_material == null) s_material = new Material(Shader.Find("Sprites/Default"));
        renderer.sharedMaterial = s_material;
        renderer.renderMode     = ParticleSystemRenderMode.Billboard;

        ps.Play();

        // 保険：StopAction が働かなかった場合でも 2 秒後に確実に破棄する
        Object.Destroy(go, 2.0f);
        return go;
    }
}
