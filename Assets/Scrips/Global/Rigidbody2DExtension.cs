using UnityEngine;

// 这是一个静态扩展类，用于解决版本兼容问题
public static class Rigidbody2DExtension
{
    // 获取速度
    public static Vector2 GetVelocity(this Rigidbody2D rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    // 设置速度
    public static void SetVelocity(this Rigidbody2D rb, Vector2 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }
}