using UnityEngine;

/// Snap-модуль дома. Pivot — центр низа; сокет top — где встаёт следующий модуль.
public class HouseModule : MonoBehaviour
{
    public Transform top;
    public Transform[] decorSockets;     // окна, вывески, ящики, бочки, трубы
    public Renderer[] tintRenderers;     // что красить цветом стен/крыши
}

