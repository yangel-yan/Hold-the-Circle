#if ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK
using System;
using ByteDance.PICO.XR;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// MR mode: turn the player's real-world spatial mesh into wave-blocking
    /// cover. PICO's <c>PXR_SpatialMeshManager</c> spawns a child GameObject
    /// per mesh chunk — the SDK's MeshPrefab already ships with a
    /// MeshCollider, so all we need to do is tag each chunk with a
    /// <see cref="WaveBlocker"/> component. The existing combat code
    /// (EnergyWave / EnemyProjectile) then absorbs hits on real walls and
    /// furniture without any further changes.
    ///
    /// IMPORTANT: subscribing to PICO spatial-mesh events from inside the
    /// Editor goes through native code paths that aren't valid without a
    /// connected headset, which can crash Play mode. We hard gate on
    /// platform so this is a no-op anywhere except on the device.
    /// </summary>
    public class SpatialMeshCoverSystem : MonoBehaviour
    {
        private bool m_subscribed;

        public void Initialize()
        {
            if (m_subscribed) return;
            if (!IsRealDevice())
            {
                Debug.Log("[SpatialMeshCoverSystem] Skipped: spatial mesh only runs on real PICO devices.", this);
                return;
            }

            try
            {
                PXR_SpatialMeshManager.MeshAdded += HandleMeshAdded;
                m_subscribed = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpatialMeshCoverSystem] Failed to subscribe to spatial mesh events: {ex.Message}", this);
            }
        }

        private void OnDestroy()
        {
            if (!m_subscribed) return;
            PXR_SpatialMeshManager.MeshAdded -= HandleMeshAdded;
            m_subscribed = false;
        }

        private static bool IsRealDevice()
        {
#if UNITY_EDITOR
            return false;
#else
            return Application.platform == RuntimePlatform.Android;
#endif
        }

        // Tag the SDK-provided chunk so EnergyWave / EnemyProjectile recognize
        // it as cover. Idempotent: pooled chunks reused across sessions won't
        // accumulate duplicate WaveBlocker components.
        private void HandleMeshAdded(Guid id, GameObject chunk)
        {
            if (chunk == null) return;
            if (chunk.GetComponent<WaveBlocker>() == null)
            {
                chunk.AddComponent<WaveBlocker>();
            }
        }
    }
}
#else
using UnityEngine;

namespace HoldTheCircle
{
    // Stub: when neither PICO plugin define is set (e.g. opening the project
    // without the SDK), this component compiles to a no-op so the rest of the
    // game logic still builds.
    public class SpatialMeshCoverSystem : MonoBehaviour
    {
        public void Initialize() { }
    }
}
#endif
