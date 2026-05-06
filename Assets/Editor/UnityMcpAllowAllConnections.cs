// Forces Unity MCP Bridge to accept unlimited direct (external) MCP client connections,
// bypassing the default license-aware MaxDirectConnectionsResolver which can return 0 on
// editor licenses without the AI add-on entitlement (the cause of "Up to 0 direct
// connections allowed at a time" / "Connection revoked").
//
// Uses reflection so it works across Assistant package versions (2.0 → 2.7+) and silently
// no-ops if the API surface changes. Drop into any Editor/ folder; runs on every assembly
// reload via [InitializeOnLoad].

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Inspection.EditorTools
{
    [InitializeOnLoad]
    internal static class UnityMcpAllowAllConnections
    {
        const string LogPrefix = "[UnityMcpAllowAllConnections]";

        static UnityMcpAllowAllConnections()
        {
            try
            {
                var bridgeType = FindType("UnityMCPBridge");
                if (bridgeType == null)
                {
                    Debug.Log($"{LogPrefix} UnityMCPBridge type not found — Assistant package may not be installed.");
                    return;
                }

                var resolverProp = bridgeType.GetProperty(
                    "MaxDirectConnectionsResolver",
                    BindingFlags.Public | BindingFlags.Static);

                if (resolverProp == null || !resolverProp.CanWrite)
                {
                    Debug.LogWarning($"{LogPrefix} MaxDirectConnectionsResolver property not writable on {bridgeType.FullName}. API surface may have changed.");
                    return;
                }

                // The property type is Func<int>. Build it with reflection so we don't need a hard ref.
                var funcType = resolverProp.PropertyType;
                var unlimited = (Func<int>)(() => -1);

                Delegate boxed;
                try
                {
                    boxed = Delegate.CreateDelegate(funcType, unlimited.Target, unlimited.Method);
                }
                catch
                {
                    // Property type wasn't Func<int> — try direct assignment as fallback.
                    boxed = unlimited;
                }

                resolverProp.SetValue(null, boxed);

                var notify = bridgeType.GetMethod(
                    "NotifyMaxDirectConnectionsPolicyChanged",
                    BindingFlags.Public | BindingFlags.Static);
                notify?.Invoke(null, null);

                Debug.Log($"{LogPrefix} Patched {bridgeType.FullName}.MaxDirectConnectionsResolver → -1 (unlimited).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Failed to patch MCP connection limit: {ex.Message}");
            }
        }

        static Type FindType(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (t.Name == simpleName) return t;
                }
            }
            return null;
        }
    }
}
