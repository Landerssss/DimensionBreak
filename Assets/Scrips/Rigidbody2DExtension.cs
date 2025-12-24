using UnityEngine;

// 必须是静态类
public static class Rigidbody2DExtension
{
    // --- 获取速度 ---
    // 使用方法: rb.GetVelocity();
    public static Vector2 GetVelocity(this Rigidbody2D rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    // --- 设置速度 ---
    // 使用方法: rb.SetVelocity(new Vector2(10, 0));
    public static void SetVelocity(this Rigidbody2D rb, Vector2 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }
}
