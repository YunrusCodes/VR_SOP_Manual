using UnityEngine;

namespace Inspection.App
{
    public static class Log
    {
        public static bool Verbose;

        public static void V(string msg)
        {
            if (Verbose) Debug.Log($"[Inspection] {msg}");
        }

        public static void I(string msg) => Debug.Log($"[Inspection] {msg}");
        public static void W(string msg) => Debug.LogWarning($"[Inspection] {msg}");
        public static void E(string msg) => Debug.LogError($"[Inspection] {msg}");
    }
}
