using UnityEditor;
using UnityEditor.AI;

public class HideNavMesh
{
    public static void Execute()
    {
        NavMeshVisualizationSettings.showNavigation = 0;
    }
}
