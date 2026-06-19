using UnityEngine;
using System.Collections;

public class AuraEffectPlayer : MonoBehaviour
{
    [Header("── デバッグ ──")]
    [Tooltip("OFFにするとオーラエフェクトを完全に無効化（負荷確認用）")]
    [SerializeField] private bool enableAura = true;

    [SerializeField] private GameObject auraEffect;

    private bool isPlaying = false;

    /// <summary>オーラを再生する（接敵時に呼ぶ）</summary>
    public void PlayAura()
    {
        if (!enableAura) return; // ★ デバッグOFF時は何もしない
        if (isPlaying || auraEffect == null) return;
        StartCoroutine(PlayAuraEffect());
    }

    private IEnumerator PlayAuraEffect()
    {
        auraEffect.SetActive(true);
        yield return null;

        var ps = auraEffect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in ps)
        {
            p.Clear();
            p.Play(true);
        }
        isPlaying = true;
    }

    /// <summary>オーラをフェードアウトして消す（BecomeAlly・離脱時に呼ぶ）</summary>
    public void StopAura()
    {
        if (!isPlaying || auraEffect == null) return;
        isPlaying = false;

        var ps = auraEffect.GetComponentsInChildren<ParticleSystem>();
        foreach (var p in ps)
            p.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        StartCoroutine(DisableWhenDone());
    }

    private IEnumerator DisableWhenDone()
    {
        var ps = auraEffect.GetComponentsInChildren<ParticleSystem>();

        bool anyAlive = true;
        while (anyAlive)
        {
            anyAlive = false;
            foreach (var p in ps)
            {
                if (p != null && p.IsAlive())
                {
                    anyAlive = true;
                    break;
                }
            }
            yield return null;
        }

        if (auraEffect != null)
            auraEffect.SetActive(false);
    }
}