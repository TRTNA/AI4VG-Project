using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//Class that implements random wander inside a contrained area
public class ConstrainedWanderBehaviour : MonoBehaviour
{
    public float circleForwardOffset = 5f;
    public float circleRay = 5f;

    public string constrainedAreaTag;
    public string navMeshAreaName;

    public Transform constrainedAreaCenter;
    public int navMeshAreaMask;


    // Start is called before the first frame update
    void Start()
    {
       
        constrainedAreaCenter = GameObject.FindGameObjectWithTag(constrainedAreaTag).transform;
        navMeshAreaMask = NavMesh.GetAreaFromName(navMeshAreaName);

    }

    public Vector3 GetWanderDestination()
    {
        Vector3 targetCircleCenter = transform.position + Vector3.forward * circleForwardOffset;
        Vector2 randomPoint = Random.insideUnitCircle * circleRay;
        Vector3 wanderDestination = new Vector3(randomPoint.x, 0, randomPoint.y) + targetCircleCenter;

        NavMeshHit hit;
        bool targetOnArea = ! NavMesh.Raycast(transform.position, wanderDestination, out hit, navMeshAreaMask);
        bool agentOnArea = NavMesh.SamplePosition(transform.position, out hit, 0.1f, 8);

        //if agent is on the area and target is not, find a random point on area and return it as destination
        if (agentOnArea && ! targetOnArea)
        {
            Vector3 randomPositionInConstrainedArea = Random.insideUnitSphere * circleForwardOffset + constrainedAreaCenter.position;
            NavMesh.SamplePosition(randomPositionInConstrainedArea, out hit, circleForwardOffset, navMeshAreaMask);
            wanderDestination = hit.position;
        }
        //if either agent and target are not on the area return constrained area center as destination
        if (! agentOnArea && ! targetOnArea)
        {
            wanderDestination = constrainedAreaCenter.position;
        }
        return wanderDestination;
    }

    
}
