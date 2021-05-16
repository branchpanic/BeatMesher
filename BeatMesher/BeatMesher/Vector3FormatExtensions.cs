using UnityEngine;

namespace BeatMesher
{
    public static class Vector3FormatExtensions
    {
        public static string AsCsv(this Vector3 v) => string.Join(",", v.x, v.y, v.z);
        public static string AsObjVertex(this Vector3 v) => $"v {v.x} {v.y} {v.z}";
    }
}