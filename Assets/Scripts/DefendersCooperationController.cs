using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefendersCooperationController : MonoBehaviour
{
    public GameObject[] defensivePositions;
    public GameObject[] defenders;
    // Start is called before the first frame update
    void Start()
    {
        defensivePositions = GameObject.FindGameObjectsWithTag("DefensivePosition");
        defenders = GameObject.FindGameObjectsWithTag("Defender");
    }

    public GameObject GetNearestEmptyDefensivePosition(GameObject defender)
    {
        GameObject defensivePosition = Utils.GetNearestObjectIf(defender.transform.position, defensivePositions, (go) => ! IsDefensivePositionOccupied(go));
        return defensivePosition;
    }

    private bool IsDefensivePositionOccupied(GameObject defensivePosition)
    {
        return defensivePosition.GetComponent<DefensivePosition>().IsOccupied();
    }
}
