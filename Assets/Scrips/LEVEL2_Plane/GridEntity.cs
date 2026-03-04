using UnityEngine;
using System.Collections;

/// <summary>
/// 网格实体基类：坐标、呼吸动画、移动摇摆动画。
/// 所有视觉参数全部 [SerializeField] 暴露到面板。
/// </summary>
public abstract class GridEntity : MonoBehaviour
{
    // ────────────────── 网格位置 ──────────────────
    [Header("=== 网格位置 ===")]
    [SerializeField] protected Vector2Int gridPosition;
    public Vector2Int GridPosition => gridPosition;

    // ────────────────── 呼吸动画 ──────────────────
    [Header("=== 呼吸动画（Scale 缓动） ===")]
    [SerializeField] protected bool enableBreathing = true;
    [Tooltip("呼吸缩放幅度（相对基础 Scale 的偏差）")]
    [SerializeField] protected float breathAmplitude = 0.03f;
    [Tooltip("呼吸速度")]
    [SerializeField] protected float breathSpeed = 2f;

    // ────────────────── 移动摇摆 ──────────────────
    [Header("=== 移动摇摆（Z 轴旋转交替 ±45°） ===")]
    [SerializeField] protected float wobbleAngle = 45f;
    [Tooltip("移动动画时长（秒）")]
    [SerializeField] protected float moveDuration = 0.25f;

    // ────────────────── 内部状态 ──────────────────
    protected Vector3 baseScale;
    private int wobbleDirection = 1; // +1 或 -1，每次移动交替
    public bool IsAnimating { get; protected set; }

    // ══════════════════ 生命周期 ══════════════════

    protected virtual void Awake()
    {
        baseScale = transform.localScale;
    }

    protected virtual void Start()
    {
        // 初始位置同步：把自己放到网格上
        SnapToGrid();
        RegisterToGrid();
    }

    protected virtual void Update()
    {
        if (enableBreathing && !IsAnimating)
        {
            ApplyBreathing();
        }
    }

    // ══════════════════ 网格同步 ══════════════════

    /// <summary>
    /// 瞬间移至当前 GridPosition 的世界坐标
    /// </summary>
    public void SnapToGrid()
    {
        if (GridManager.Instance != null)
            transform.position = GridManager.Instance.GridToWorld(gridPosition);
    }

    /// <summary>
    /// 在 GridManager 中注册占位
    /// </summary>
    protected void RegisterToGrid()
    {
        if (GridManager.Instance != null)
            GridManager.Instance.PlaceEntity(this, gridPosition);
    }

    /// <summary>
    /// 从 GridManager 中移除占位
    /// </summary>
    protected void UnregisterFromGrid()
    {
        if (GridManager.Instance != null)
            GridManager.Instance.RemoveEntity(this);
    }

    // ══════════════════ 移动（含视觉动画） ══════════════════

    /// <summary>
    /// 网格移动一步（带摇摆动画）
    /// </summary>
    protected void MoveToCell(Vector2Int target)
    {
        if (!GridManager.Instance.IsInBounds(target)) return;

        // 更新逻辑坐标
        GridManager.Instance.RemoveEntity(this);
        gridPosition = target;
        GridManager.Instance.PlaceEntity(this, gridPosition);

        // 播放移动动画
        StartCoroutine(AnimateMove(GridManager.Instance.GridToWorld(target)));
    }

    IEnumerator AnimateMove(Vector3 targetWorldPos)
    {
        IsAnimating = true;

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        // 确定本次摇摆方向
        float targetAngle = wobbleAngle * wobbleDirection;
        wobbleDirection *= -1; // 下次反向

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;

            // 位置插值
            transform.position = Vector3.Lerp(startPos, targetWorldPos, t);

            // Z轴旋转：先摆过去，结束时归零
            float angle = Mathf.Sin(t * Mathf.PI) * targetAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            yield return null;
        }

        // 精确到位
        transform.position = targetWorldPos;
        transform.rotation = Quaternion.identity;
        IsAnimating = false;
    }

    // ══════════════════ 呼吸动画 ══════════════════

    void ApplyBreathing()
    {
        float offset = Mathf.Sin(Time.time * breathSpeed) * breathAmplitude;
        transform.localScale = baseScale + Vector3.one * offset;
    }

    // ══════════════════ 销毁 ══════════════════

    protected virtual void OnDestroy()
    {
        UnregisterFromGrid();
    }
}
