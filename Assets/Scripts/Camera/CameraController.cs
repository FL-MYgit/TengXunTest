using UnityEngine;

/// <summary>
/// 让摄像机在 2D 平面内跟随 NPC。
/// 只同步 X/Y，始终保留摄像机原有 Z，否则摄像机与精灵处于同一平面时将看不到场景。
/// </summary>
public class CameraController : MonoBehaviour
{
    public NPCController NPC;

    private float cameraDepth;

    private void Awake()
    {
        cameraDepth = transform.position.z;
    }

    private void LateUpdate()
    {
        if (NPC == null) return;
        Vector3 npcPosition = NPC.transform.position;
        transform.position = new Vector3(npcPosition.x, npcPosition.y, cameraDepth);
    }
}
