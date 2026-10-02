using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public class BottleSetup { public int[] layers; }

    [Header("Level")]
    [SerializeField] private Bottle[] bottles;
    [SerializeField] private Sprite[] palette;
    [SerializeField] private BottleSetup[] setup;

    [Header("Invalid Target Behavior")]
    [Tooltip("On: clicking another bottle that has water switches the source bottle. Off: the source bottle shakes, plays the error sound and returns to its place.")]
    [SerializeField] private bool switchSourceOnInvalid = true;

    [Header("Tween")]
    [SerializeField] private float liftY = 0.6f;
    [SerializeField] private float moveTime = 0.35f;
    [SerializeField] private float tiltAngle = 70f;
    [SerializeField] private float tiltTime = 0.3f;
    [SerializeField] private float pourStep = 0.4f;
    [SerializeField] private Vector2 mouthGap = new Vector2(0f, 0.6f);

    [Header("Stream (optional)")]
    [SerializeField] private LineRenderer stream;
    [SerializeField] private Color[] streamColors;

    [Header("Audio")]
    [SerializeField] private AudioSource sfx;
    [SerializeField] private AudioClip clickClip, pourClip, errorClip, completeClip, winClip;

    [Header("UI")]
    [SerializeField] private GameObject winPopup;

    private Bottle selected;
    private bool isBusy;
    private bool won;
    private bool streaming;
    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
        ResetLevel();
    }

    private void Update()
    {
        if (isBusy || won) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 wp = cam.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(wp);
        if (hit == null) return;
        Bottle b = hit.GetComponent<Bottle>();
        if (b != null) OnBottleClicked(b);
    }

    private void OnBottleClicked(Bottle b)
    {
        if (selected == null)
        {
            if (!b.IsEmpty && !b.Locked) Select(b);
            return;
        }

        if (b == selected)
        {
            Deselect();
            return;
        }

        if (CanPour(selected, b))
        {
            StartPour(selected, b);
            return;
        }

        if (switchSourceOnInvalid && !b.IsEmpty && !b.Locked)
        {
            Deselect();
            Select(b);
            return;
        }

        Reject();
    }

    private void Select(Bottle b)
    {
        selected = b;
        Play(clickClip);
        b.transform.DOKill();
        b.transform.DOMoveY(b.HomePos.y + liftY, 0.2f);
    }

    private void Deselect()
    {
        if (selected == null) return;
        Bottle b = selected;
        selected = null;
        Play(clickClip);
        b.transform.DOKill();
        b.transform.DOMoveY(b.HomePos.y, 0.2f);
    }

    private void Reject()
    {
        isBusy = true;
        Bottle s = selected;
        selected = null;
        Play(errorClip);
        s.transform.DOKill();
        DOTween.Sequence()
            .Append(s.transform.DOShakePosition(0.3f, new Vector3(0.12f, 0f, 0f), 25, 0f, false, true))
            .Append(s.transform.DOMove(s.HomePos, 0.2f))
            .OnComplete(() => isBusy = false);
    }

    private bool CanPour(Bottle src, Bottle dst)
    {
        return src != dst && !src.IsEmpty && !src.Locked && dst.CanReceive(src.TopColor);
    }

    private void StartPour(Bottle src, Bottle dst)
    {
        isBusy = true;

        int colorId = src.TopColor;
        int amount = Mathf.Min(src.TopRunLength(), Bottle.Capacity - dst.Count);

        float dir = src.HomePos.x < dst.HomePos.x ? 1f : -1f;
        float angle = -dir * tiltAngle;

        Vector3 mouthOffset = src.MouthPos - src.transform.position;
        Vector3 targetMouth = dst.MouthPos + new Vector3(-dir * mouthGap.x, mouthGap.y, 0f);
        Vector3 pourPos = targetMouth - Quaternion.Euler(0f, 0f, angle) * mouthOffset;

        src.transform.DOKill();
        Transform t = src.transform;

        Sequence seq = DOTween.Sequence();
        seq.Append(t.DOMove(pourPos, moveTime).SetEase(Ease.OutQuad));
        seq.Append(t.DORotate(new Vector3(0f, 0f, angle), tiltTime));
        seq.AppendCallback(() =>
        {
            Play(pourClip);
            BeginStream(colorId);
        });

        for (int i = 0; i < amount; i++)
        {
            seq.AppendInterval(pourStep * 0.5f);
            seq.AppendCallback(() =>
            {
                src.PopTop();
                dst.Push(colorId);
            });
            seq.AppendInterval(pourStep * 0.5f);
        }

        seq.AppendCallback(EndStream);
        seq.Append(t.DORotate(Vector3.zero, tiltTime));
        seq.Append(t.DOMove(src.HomePos, moveTime).SetEase(Ease.InOutQuad));
        seq.OnUpdate(() => UpdateStream(src, dst));
        seq.OnComplete(() =>
        {
            selected = null;
            AfterPour();
            if (!won) isBusy = false;
        });
    }

    private void BeginStream(int colorId)
    {
        if (stream == null) return;
        if (streamColors != null && colorId < streamColors.Length)
        {
            stream.startColor = streamColors[colorId];
            stream.endColor = streamColors[colorId];
        }
        stream.positionCount = 2;
        stream.enabled = true;
        streaming = true;
    }

    private void UpdateStream(Bottle src, Bottle dst)
    {
        if (!streaming || stream == null) return;
        stream.SetPosition(0, src.MouthPos);
        stream.SetPosition(1, dst.WaterTopPos);
    }

    private void EndStream()
    {
        streaming = false;
        if (stream != null) stream.enabled = false;
    }

    private void AfterPour()
    {
        bool allDone = true;
        foreach (Bottle b in bottles)
        {
            if (!b.Locked && IsSolved(b))
            {
                b.Locked = true;
                b.PlayCompleteFx();
                Play(completeClip);
            }
            if (!b.IsEmpty && !b.Locked) allDone = false;
        }
        if (allDone) Win();
    }

    private bool IsSolved(Bottle b)
    {
        if (b.IsEmpty || !b.IsMono()) return false;
        int id = b.TopColor, total = 0;
        foreach (Bottle o in bottles) total += o.CountOf(id);
        return total == b.Count;
    }

    private void Win()
    {
        won = true;
        isBusy = true;
        DOVirtual.DelayedCall(0.8f, () =>
        {
            Play(winClip);
            if (winPopup == null) return;
            winPopup.SetActive(true);
            winPopup.transform.localScale = Vector3.zero;
            winPopup.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        });
    }

    public void ResetLevel()
    {
        DOTween.KillAll();
        isBusy = false;
        won = false;
        selected = null;
        EndStream();
        if (winPopup != null) winPopup.SetActive(false);

        BottleSetup[] data = (setup != null && setup.Length == bottles.Length) ? setup : DefaultSetup();
        for (int i = 0; i < bottles.Length; i++)
        {
            bottles[i].transform.position = bottles[i].HomePos;
            bottles[i].transform.rotation = Quaternion.identity;
            bottles[i].Init(data[i].layers, palette);
        }
    }

    private BottleSetup[] DefaultSetup()
    {
        return new[]
        {
            new BottleSetup { layers = new[] { 0, 1, 2 } },
            new BottleSetup { layers = new[] { 1, 2, 0 } },
            new BottleSetup { layers = new int[0] },
            new BottleSetup { layers = new int[0] },
        };
    }

    private void Play(AudioClip c)
    {
        if (sfx != null && c != null) sfx.PlayOneShot(c);
    }
}