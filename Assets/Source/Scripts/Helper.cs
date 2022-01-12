using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace GameDevWithLukas
{
    [BurstCompile]
    public class Helper
    {
        public static readonly float Epsilon = 0.001f;
        public static float3 Up = new float3(0f, 1f, 0f);
        public static float3 Forward = new float3(0f, 0f, 1f);

        public static float GetYawOfDirection(float3 _direction)
        {
            return math.degrees(math.atan2(_direction.x, _direction.z));
        }


        // TOO COMPLICATED .. .  DOES NOT WORK WITHOUT ON FAUX GRAVITY .. MAYBE ADD ANOTHER SERIES
        public static float GetYawOfQuaternion(Quaternion q)
        {
            float yaw = Mathf.Rad2Deg * math.atan2(2 * q.y * q.w - 2 * q.x * q.z, 1 - 2 * q.y * q.y - 2 * q.z * q.z);

            return yaw;
        }
    }

    public static class VectorExtensions
    {
        public static float3 ToFloat3(this Vector3 v)
        {
            return new float3(v);
        }

        public static Vector3 ToVector3(this float3 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static float SqrMagnitude(this float3 v)
        {
            return v.ToVector3().sqrMagnitude;
        }
    }
}
