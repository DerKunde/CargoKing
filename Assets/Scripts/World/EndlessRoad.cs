using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.World
{
    public class EndlessRoad : MonoBehaviour
    {
        public Transform carTransform;
        public GameObject roadPrefab;
        public int numberOfSegments = 4;
        public float segmentLength = 50f;

        private List<GameObject> activeSegments = new List<GameObject>();
        private float nextSpawnX = 0f;

        private void Start()
        {
            for(int i = 0; i < numberOfSegments; i++)
            {
                SpawnSegment();
            }
        }

        private void Update()
        {
            if(carTransform.position.x + segmentLength < activeSegments[0].transform.position.x)
            {
                MoveSegmentToFront();
            }
        }

        private void SpawnSegment()
        {
            GameObject go = Instantiate(roadPrefab, new Vector3(nextSpawnX,0,0), Quaternion.identity);
            activeSegments.Add(go);
            nextSpawnX -= segmentLength;
        }

        private void MoveSegmentToFront()
        {
            GameObject oldestSegment = activeSegments[0];
            activeSegments.RemoveAt(0);

            oldestSegment.transform.position = new Vector3(nextSpawnX,0,0);
            activeSegments.Add(oldestSegment);

            nextSpawnX -= segmentLength;
        }
    }
}
