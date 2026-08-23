using UnityEngine;
using Gallop;

/// <summary>
/// Visualize collision shapes to debug their positions.
/// Add this to a character to see collision shapes in Scene view.
/// </summary>
public class CySpringCollisionVisualizer : MonoBehaviour
{
    [Header("Settings")]
    public bool showCollisions = true;
    public bool showHeadCollisions = true;
    public bool showBodyCollisions = true;
    
    private CySpringController _controller;
    
    void Start()
    {
        var container = GetComponent<UmaContainerCharacter>();
        if (container == null) return;
        
        var field = typeof(UmaContainerCharacter).GetField("_cySpringController", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            _controller = field.GetValue(container) as CySpringController;
        }
    }
    
    void OnDrawGizmos()
    {
        if (_controller == null || !showCollisions) return;
        
        if (showHeadCollisions)
            DrawCollisions(CySpringController.Parts.Head, Color.cyan);
        
        if (showBodyCollisions)
            DrawCollisions(CySpringController.Parts.Body, Color.green);
    }
    
    void DrawCollisions(CySpringController.Parts part, Color color)
    {
        var method = typeof(CySpringController).GetMethod("GetCollisionRuntimeData",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (method == null) return;
        
        var collisions = method.Invoke(_controller, new object[] { part }) as CySpringCollisionRuntimeData[];
        if (collisions == null) return;
        
        Gizmos.color = color;
        for (int i = 0; i < collisions.Length; i++)
        {
            var c = collisions[i];
            if (c == null) continue;
            
            Transform t = c.TargetTransform;
            if (t == null)
            {
                // Try to find the transform by name
                string name = c.Name;
                if (!string.IsNullOrEmpty(name))
                {
                    t = FindTransformByName(transform.root, name);
                }
            }
            
            if (t == null) continue;
            
            Vector3 worldPos = t.TransformPoint(c.Offset);
            
            switch (c.CollisionType)
            {
                case CySpringCollisionData.CollisionType.Sphere:
                    Gizmos.DrawWireSphere(worldPos, c.Radius);
                    break;
                    
                case CySpringCollisionData.CollisionType.Capsule:
                    Vector3 worldPos2 = t.TransformPoint(c.Offset2);
                    Gizmos.DrawWireSphere(worldPos, c.Radius);
                    Gizmos.DrawWireSphere(worldPos2, c.Radius);
                    Gizmos.DrawLine(worldPos, worldPos2);
                    break;
            }
        }
    }
    
    Transform FindTransformByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        
        if (root.name == name) return root;
        
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindTransformByName(root.GetChild(i), name);
            if (found != null) return found;
        }
        
        return null;
    }
}
