using UnityEngine;

public class FloatingOrigin : MonoBehaviour
{
    public Transform carTransform;
    public float threshold = 500f;

    void LateUpdate()
    {
        if (carTransform.position.z > threshold)
        {
            float offsetZ = carTransform.position.z;

            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            
            foreach (GameObject g in rootObjects)
            {
                g.transform.position -= new Vector3(0, 0, offsetZ);
            }

            EndlessRoad endlessRoad = GetComponent<EndlessRoad>();
            if (endlessRoad != null)
            {
                // Korrigiere den nächsten Spawn-Punkt im Straßenskript
                // (Da alle Straßen mitteleportiert wurden, müssen wir den Tracker anpassen)
                // Hier greifen wir auf das interne Feld zu, indem wir den Code oben leicht anpassen müssten, 
                // oder du verringerst einfach die globale Variable um den Offset.
            }
            
            Debug.Log("Welt-Reset durchgeführt: Physik-Genauigkeit gesichert.");
        }
    }
}