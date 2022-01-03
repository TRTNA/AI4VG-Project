using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefendersCooperationController : MonoBehaviour, IObserver
{
    public float updateRate = 1f;

    public Dictionary<DefensivePosition, GameObject> defensivePositions;
    private GameObject[] defenders;
    public Dictionary<GameObject, bool> hordeStatus;
    public Dictionary<GameObject, GameObject> whoIsHelpingWho;

    // Start is called before the first frame update
    void Start()
    {
        defensivePositions = new Dictionary<DefensivePosition, GameObject>();
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
        StartCoroutine(UpdateStatus());
    }

    //returns a defender if otherDef has to help, otherwise it returns null
    public GameObject DoesItNeedHelpWithHorde(GameObject otherDef)
    {
        List<GameObject> toHelp = new List<GameObject>();
        foreach (var item in hordeStatus)
        {
            if (item.Value)
            {
                if (item.Key != otherDef) toHelp.Add(item.Key);
            }
        }
        return Utils.GetNearestObject(otherDef.transform.position, toHelp.ToArray());
    }

    public bool HelpDefenderWithHorde(GameObject helper, GameObject helpee)
    {
        if (whoIsHelpingWho[helpee] == null)
        {
            whoIsHelpingWho[helpee] = helper;
            return true;
        }
        GameObject otherHelper = whoIsHelpingWho[helpee];
        //if otherHelper is closer to helpee then let it help, otherwise new helper will help
        if ((otherHelper.transform.position - helpee.transform.position).sqrMagnitude < (helper.transform.position - helpee.transform.position).sqrMagnitude)
        {
            return false;
        } else
        {
            whoIsHelpingWho[helpee] = helper;
            return true;
        }
    }

    public bool DoesItStillNeedHelpWithHorde(GameObject helper, GameObject helpee)
    {
        return whoIsHelpingWho[helpee] == helper;
    }

    public GameObject[] IsAnyoneSurrounded()
    {
        List<GameObject> surroundedAllies = new List<GameObject>();
        foreach (var ally in defenders)
        {
            if (ally != null && ally.GetComponent<DefenderFSM>().AmISurrounded()) surroundedAllies.Add(ally);
        }
        return surroundedAllies.ToArray();
    }

    public bool IsStillSurrounded(GameObject ally)
    {
        return ally != null ? ally.GetComponent<DefenderFSM>().AmISurrounded() : false;
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

    IEnumerator UpdateStatus()
    {
        while (true)
        {
            List<GameObject> temp = new List<GameObject>();
            foreach (var def in defenders)
            {
                if (temp != null) temp.Add(def);
            }
            defenders = temp.ToArray();
            bool hasHorde = false;
            foreach (var def in defenders)
            {
                hasHorde = def.GetComponent<DefenderFSM>().HasHorde();
                hordeStatus[def] = hasHorde;
                //if has not a horde, nobody has to help it
                if (! hasHorde)
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
