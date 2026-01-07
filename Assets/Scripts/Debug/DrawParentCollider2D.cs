using UnityEngine;

#if UNITY_EDITOR
[ExecuteAlways]
public class DrawParentCollider2D : MonoBehaviour
{
    public BoxCollider2D parentCollider;
    public Color color = Color.green;

    void OnDrawGizmos()
    {
        if (!parentCollider) return;

        Gizmos.color = color;

        Transform t = parentCollider.transform;
        Vector2 size = parentCollider.size;
        Vector2 offset = parentCollider.offset;

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            t.position,
            t.rotation,
            t.lossyScale
        );

        Gizmos.DrawWireCube(offset, size);
        Gizmos.matrix = old;
    }
}
#endif
