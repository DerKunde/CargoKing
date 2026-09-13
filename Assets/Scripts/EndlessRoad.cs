using System.Collections.Generic;
using UnityEngine;

public class EndlessRoad : MonoBehaviour
{
    public Transform carTransform;
    public GameObject roadPrefab;
    public int numberOfSegments = 4;
    public float segmentLength = 50f;

    private List<GameObject> activeSegments = new List<GameObject>();
    private float nextSpawnZ = 0f;

    private void Start()
    {
        for(int i = 0; i < numberOfSegments; i++)
        {
            SpawnSegment();
        }
    }

    private void Update()
    {
        if(carTransform.position.z - segmentLength > activeSegments[0].transform.position.z)
        {
            MoveSegmentToFront(); 
        }
    }

    private void SpawnSegment()
    {
        GameObject go = Instantiate(roadPrefab, new Vector3(0,0,nextSpawnZ), Quaternion.identity);
        activeSegments.Add(go);
        nextSpawnZ += segmentLength;
    }

    private void MoveSegmentToFront()
    {
        GameObject oldestSegment = activeSegments[0];
        activeSegments.RemoveAt(0);

        oldestSegment.transform.position = new Vector3(0,0,nextSpawnZ);
        activeSegments.Add(oldestSegment);

        nextSpawnZ += segmentLength;
    }
}