using UnityEngine;
using System.Collections;

/// <summary>
/// ヒットエフェクト管理
/// ApplyKnockbackToPlayerから呼ぶ
/// </summary>
public class HitEffectPlayer : MonoBehaviour
{
    [Header("── ヒットエフェクト ──")]
    [SerializeField] private GameObject hitEffect;
    [Tooltip("エフェクトの位置オフセット（プレイヤー基準）")]
    [SerializeField] private Vector3 hitEffectOffset = Vector3.zero;
    [Tooltip("エフェクトを出すタイミングの遅延（秒）")]
    [SerializeField] private float hitEffectDelay = 0f;

    [SerializeField] private Transform player;

    private void Awake()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                player = go.transform;
        }
    }

    /// <summary>ヒットエフェクトを再生する</summary>
    public void PlayHitEffect()
    {
        Debug.Log($"[HitEffectPlayer] PlayHitEffect呼ばれた StackTrace={System.Environment.StackTrace}");
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }
        if (hitEffect == null || player == null) return;
        StartCoroutine(PlayHitEffectCoroutine());
    }

    private IEnumerator PlayHitEffectCoroutine()
    {
        yield return new WaitForSeconds(hitEffectDelay);

        Debug.Log($"[HitEffectPlayer] hitEffect={hitEffect} player={player}");

        if (hitEffect == null || player == null) yield break;

        Vector3 pos = player.position + hitEffectOffset;
        Debug.Log($"[HitEffectPlayer] 生成位置={pos}");
        var obj = Instantiate(hitEffect, pos, Quaternion.identity);
        var ps = obj.GetComponentsInChildren<ParticleSystem>();
        foreach (var p in ps) p.Play();

        // 再生終了後に削除
        float maxDuration = 0f;
        foreach (var p in ps)
            maxDuration = Mathf.Max(maxDuration, p.main.duration + p.main.startLifetime.constantMax);

        Destroy(obj, maxDuration);
    }
}