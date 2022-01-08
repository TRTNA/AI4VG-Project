using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefendersCooperationController : MonoBehaviour, IObserver
{
    public float updateRate = 1f;

    public Dictionary<DefensivePosition, GameObject> defensivePositions = new Dictionary<DefensivePosition, GameObject>();
    private GameObject[] defenders;
    public Dictionary<GameObject, bool> hordeStatus;
    public Dictionary<GameObject, GameObject> whoIsHelpingWho;

    // Start is called before the first frame update
    void Start()
    {
        foreach (var defPosition in GameObject.FindGameObjectsWithTag("DefensivePosition"))
        {
            var dp = defPosition.GetComponent<DefensivePosition>();
            defensivePositions.Add(dp, dp.OccupiedBy());
        }
        defenders = GameObject.FindGameObjectsWithTag("Defender");
        hordeStatus = new Dictionary<GameObject, bool>();
        whoIsHelpingWho = new Dictionary<GameObject, GameObject>();
        foreach (var def in defenders)
        {
            hordeStatus.Add(def, false);
            whoIsHelpingWho.Add(def, null);
            def.GetComponent<HealthController>().AddObserver(this);
        }
        foreach (var def in defensivePositions.Keys)
        {
            def.GetComponent<DefensivePosition>().AddObserver(this);
        }
        StartCoroutine(UpdateHordeStatus());
    }


    public bool IsAllyStillSurrounded(GameObject ally)
    {
        return ally != null && ally.GetComponent<DefenderFSM>().IsSurrounded();
    }

    public GameObject ReserveNearestEmptyDefensivePosition(GameObject defender)
    {
        List<GameObject> emptyDefensivePositions = new List<GameObject>();
        foreach (var item in defensivePositions)
        {
            if (item.Value == null) emptyDefensivePositions.Add(item.Key.gameObject);
        }
        GameObject nearest = Utils.GetNearestObject(defender.transform.position, emptyDefensivePositions.ToArray());
        if (nearest == null) return nearest;
        //reservation only if nearest def position is not null
        defensivePositions[nearest.GetComponent<DefensivePosition>()] = defender;
        return nearest;
    }


    IEnumerator UpdateHordeStatus()
    {
        while (true)
        {
            List<GameObject> temp = new List<GameObject>();
            foreach (var def in defenders)
            {
                if (def != null) temp.Add(def);
            }
            defenders = temp.ToArray();
            foreach (var def in defenders)
            {
                hordeStatus[def] = def.GetComponent<DefenderFSM>().HasHorde();
                //if has not a horde, nobody has to help it
                if (! hordeStatus[def])
                {
                    whoIsHelpingWho[def] = null;
                }
                
            }
            yield return new WaitForSeconds(updateRate);
        }
    }

    public void OnNotify(GameObject subject, object status)
    {
        DefensivePosition defPosition;
        if (subject.TryGetComponent<DefensivePosition>(out defPosition))
        {
            defensivePositions[defPosition] = defPosition.OccupiedBy();
        }
        
        if (status.GetType() == typeof(int) && (int)status == 0)
        {
            subject.GetComponent<HealthController>().RemoveObserver(this);
            List<GameObject> temp = new List<GameObject>(defenders);
            temp.Remove(subject);
            defenders = temp.ToArray();
            return;
        }
    }
}
