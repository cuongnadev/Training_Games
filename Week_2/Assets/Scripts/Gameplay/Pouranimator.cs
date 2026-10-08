using System;
using DG.Tweening;
using UnityEngine;

/// <summary>Plays the pour animation: fly to the target, tilt, transfer layers, return home.</summary>
public class PourAnimator : MonoBehaviour
{
    [Header("Tween")]
    [SerializeField] private float _moveTime = 0.35f;
    [SerializeField] private float _tiltAngle = 70f;
    [SerializeField] private float _tiltTime = 0.3f;
    [SerializeField] private float _pourStep = 0.4f;
    [Tooltip("Where the source mouth stops relative to the target mouth (world units). Y raises it above the target, X shifts it toward the source side.")]
    [SerializeField] private Vector2 _pourOffset = new Vector2(0.2f, 0.6f);

    [Header("Stream (optional)")]
    [SerializeField] private LineRenderer _stream;

    private bool _streaming;

    private void OnDestroy()
    {
        DOTween.Kill(this);
    }

    /// <summary>
    /// Pours <paramref name="amount"/> layers of <paramref name="color"/> from <paramref name="src"/>
    /// into <paramref name="dst"/>, then calls <paramref name="onComplete"/> when the bottle is back home.
    /// </summary>
    public void Play(BottleController src, BottleController dst, Color color, int amount, Action onComplete)
    {
        // Tilt toward the target. Positive Z rotation is counter-clockwise, hence the minus sign.
        float dir = src.HomePos.x < dst.HomePos.x ? 1f : -1f;
        float angle = -dir * _tiltAngle;

        // The bottle rotates around its center, so rotate the mouth offset too and solve for the
        // center position that puts the source mouth on the target mouth plus the pour offset.
        Vector3 mouthOffset = src.MouthPos - src.transform.position;
        Vector3 targetMouth = dst.MouthPos + new Vector3(-dir * _pourOffset.x, _pourOffset.y, 0f);
        Vector3 pourPos = targetMouth - Quaternion.Euler(0f, 0f, angle) * mouthOffset;

        src.transform.DOKill();
        Transform t = src.transform;
        float halfStep = _pourStep * 0.5f;

        Sequence seq = DOTween.Sequence().SetId(this);
        seq.Append(t.DOMove(pourPos, _moveTime).SetEase(Ease.OutQuad));
        seq.Append(t.DORotate(new Vector3(0f, 0f, angle), _tiltTime));
        seq.AppendCallback(() =>
        {
            AudioManager.Play(SfxType.Pour);
            BeginStream(color);
        });

        // Transfer one layer per step so the water appears to flow gradually.
        for (int i = 0; i < amount; i++)
        {
            seq.AppendInterval(halfStep);
            seq.AppendCallback(() =>
            {
                src.PopTop();
                dst.Push(color);
            });
            seq.AppendInterval(halfStep);
        }

        seq.AppendCallback(EndStream);
        seq.Append(t.DORotate(Vector3.zero, _tiltTime));
        seq.Append(t.DOMove(src.HomePos, _moveTime).SetEase(Ease.InOutQuad));
        seq.OnUpdate(() => UpdateStream(src, dst));
        seq.OnComplete(() =>
        {
            if (onComplete != null) onComplete();
        });
    }

    /// <summary>Stops any running pour and hides the stream.</summary>
    public void Stop()
    {
        DOTween.Kill(this);
        EndStream();
    }

    private void BeginStream(Color color)
    {
        if (_stream == null) return;
        _stream.startColor = color;
        _stream.endColor = color;
        _stream.positionCount = 2;
        _stream.enabled = true;
        _streaming = true;
    }

    // Keeps the stream attached to the moving source mouth and the rising water surface.
    private void UpdateStream(BottleController src, BottleController dst)
    {
        if (!_streaming || _stream == null) return;
        _stream.SetPosition(0, src.MouthPos);
        _stream.SetPosition(1, dst.WaterTopPos);
    }

    private void EndStream()
    {
        _streaming = false;
        if (_stream != null) _stream.enabled = false;
    }
}