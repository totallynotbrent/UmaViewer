using UnityEngine;
using Gallop;

/// <summary>
/// Diagnostic tool to show all collision shapes and physics parameters.
/// Add this to a character to see what collisions exist.
/// </summary>
public class CySpringCollisionDiagnostic : MonoBehaviour
{
    [Header("Settings")]
    public bool showInConsole = true;
    public bool drawGizmos = true;
    
    void Start()
    {
        if (showInConsole)
            Invoke("LogDiagnostics", 0.5f);
    }
    
    void LogDiagnostics()
    {
        var container = GetComponent<UmaContainerCharacter>();
        if (container == null)
        {
            Debug.LogError("[CySpringCollisionDiagnostic] No UmaContainerCharacter found!");
            return;
        }
        
        // Get the CySpringController via reflection
        var field = typeof(UmaContainerCharacter).GetField("_cySpringController", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
        {
            Debug.LogError("[CySpringCollisionDiagnostic] Cannot find _cySpringController field!");
            return;
        }
        
        var controller = field.GetValue(container) as CySpringController;
        if (controller == null)
        {
            Debug.LogError("[CySpringCollisionDiagnostic] CySpringController is null!");
            return;
        }
        
        Debug.Log("=== CYSPRING COLLISION DIAGNOSTIC ===");
        
        // Check each part
        LogPartCollision(controller, CySpringController.Parts.Head, "HEAD");
        LogPartCollision(controller, CySpringController.Parts.Body, "BODY");
        LogPartCollision(controller, CySpringController.Parts.Tail, "TAIL");
        
        // Check gravity
        var gravityField = typeof(CySpring).GetField("_gravityRate", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (gravityField != null)
        {
            float gravity = (float)gravityField.GetValue(null);
            Debug.Log($"[Gravity] _gravityRate = {gravity}");
        }
        
        // Check stiffness for each part
        LogPartStiffness(controller, CySpringController.Parts.Head, "HEAD");
        LogPartStiffness(controller, CySpringController.Parts.Body, "BODY");
        LogPartStiffness(controller, CySpringController.Parts.Tail, "TAIL");
        
        Debug.Log("=== END DIAGNOSTIC ===");
    }
    
    void LogPartCollision(CySpringController controller, CySpringController.Parts part, string partName)
    {
        var method = typeof(CySpringController).GetMethod("GetCollisionRuntimeData",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (method == null)
        {
            Debug.LogWarning($"[Diagnostic] Cannot find GetCollisionRuntimeData method!");
            return;
        }
        
        var collisions = method.Invoke(controller, new object[] { part }) as CySpringCollisionRuntimeData[];
        
        if (collisions == null || collisions.Length == 0)
        {
            Debug.Log($"[{partName}] NO COLLISIONS!");
            return;
        }
        
        Debug.Log($"[{partName}] {collisions.Length} collisions:");
        for (int i = 0; i < collisions.Length; i++)
        {
            var c = collisions[i];
            if (c == null) continue;
            
            string typeStr = "Unknown";
            switch (c.CollisionType)
            {
                case CySpringCollisionData.CollisionType.Sphere:
                    typeStr = "Sphere";
                    break;
                case CySpringCollisionData.CollisionType.Capsule:
                    typeStr = "Capsule";
                    break;
                case CySpringCollisionData.CollisionType.Plane:
                    typeStr = "Plane";
                    break;
            }
            
            Debug.Log($"  [{i}] {typeStr} '{c.Name}' radius={c.Radius:F4} offset={c.Offset} offset2={c.Offset2}");
        }
    }
    
    void LogPartStiffness(CySpringController controller, CySpringController.Parts part, string partName)
    {
        var springField = typeof(CySpringController).GetField("_springArray", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (springField == null) return;
        
        var springs = springField.GetValue(controller) as CySpring[];
        if (springs == null) return;
        
        int index = (int)part;
        if (index >= springs.Length || springs[index] == null) return;
        
        var spring = springs[index];
        
        // Get global multiplier
        var stiffnessField = typeof(CySpring).GetField("_stiffnessForceRate", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (stiffnessField != null)
        {
            float stiffness = (float)stiffnessField.GetValue(spring);
            Debug.Log($"[{partName}] GlobalStiffnessMultiplier = {stiffness}");
        }
        
        // Get spring rate
        var rateField = typeof(CySpringController).GetField("_springRateArray", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (rateField != null)
        {
            var rates = rateField.GetValue(controller) as float[];
            if (rates != null && index < rates.Length)
            {
                Debug.Log($"[{partName}] SpringRate = {rates[index]}");
            }
        }
        
        // Get root bone native data
        var rootBoneField = typeof(CySpring).GetField("_rootBoneArray", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (rootBoneField != null)
        {
            var rootBones = rootBoneField.GetValue(spring) as CySpringRootBone[];
            if (rootBones != null)
            {
                Debug.Log($"[{partName}] RootBones: {rootBones.Length}");
                for (int i = 0; i < Mathf.Min(rootBones.Length, 5); i++)
                {
                    var rb = rootBones[i];
                    if (rb == null || rb.NativeArray == null || rb.NativeArray.Length == 0) continue;
                    var native = rb.NativeArray[0];
                    Debug.Log($"  [{partName}][{i}] bone={rb.BoneName} StiffnessForce={native.StiffnessForce} DragForce={native.DragForce} Gravity={native.Gravity} CollisionRadius={native.CollisionRadius}");
                }
            }
        }
    }
    
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        
        var container = GetComponent<UmaContainerCharacter>();
        if (container == null) return;
        
        var field = typeof(UmaContainerCharacter).GetField("_cySpringController", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return;
        
        var controller = field.GetValue(container) as CySpringController;
        if (controller == null) return;
        
        // Draw collisions for each part
        DrawPartCollisionGizmos(controller, CySpringController.Parts.Head, Color.cyan);
        DrawPartCollisionGizmos(controller, CySpringController.Parts.Body, Color.green);
        DrawPartCollisionGizmos(controller, CySpringController.Parts.Tail, Color.yellow);
    }
    
    void DrawPartCollisionGizmos(CySpringController controller, CySpringController.Parts part, Color color)
    {
        var method = typeof(CySpringController).GetMethod("GetCollisionRuntimeData",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (method == null) return;
        
        var collisions = method.Invoke(controller, new object[] { part }) as CySpringCollisionRuntimeData[];
        if (collisions == null) return;
        
        Gizmos.color = color;
        for (int i = 0; i < collisions.Length; i++)
        {
            var c = collisions[i];
            if (c == null || c.TargetTransform == null) continue;
            
            Vector3 pos = c.TargetTransform.TransformPoint(c.Offset);
            Vector3 pos2 = c.TargetTransform.TransformPoint(c.Offset2);
            
            switch (c.CollisionType)
            {
                case CySpringCollisionData.CollisionType.Sphere:
                    Gizmos.DrawWireSphere(pos, c.Radius);
                    break;
                case CySpringCollisionData.CollisionType.Capsule:
                    Gizmos.DrawWireSphere(pos, c.Radius);
                    Gizmos.DrawWireSphere(pos2, c.Radius);
                    Gizmos.DrawLine(pos, pos2);
                    break;
            }
        }
    }
}
