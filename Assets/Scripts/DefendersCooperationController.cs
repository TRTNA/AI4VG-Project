using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefendersCooperationController : MonoBehaviour
{
    public float updateRate = 1f;

    private GameObject[] defensivePositions;
    private GameObject[] defenders;
    private Dictionary<GameObject, bool> hordeStatus;
    private Dictionary<GameObject, GameObject> whoIsHelpingWho;

    // Start is called before the first frame update
    void Start()
    {
        defensivePositions = GameObject.FindGameObjectsWithTag("DefensivePosition");
        defenders = GameObject.FindGameObjectsWithTag("Defender");
        hordeStatus = new Dictionary<GameObject, bool>();
        whoIsHelpingWho = new Dictionary<GameObject, GameObject>();
        foreach (var def in defenders)
        {
            hordeStatus.Add(def, false);
            whoIsHelpingWho.Add(def, null);
        }
        StartCoroutine(UpdateStatus());
    }

    //returns a defender if otherDef has to help, otherwise it returns null
    public GameObject DoesItNeedToHelp(GameObject otherDef)
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

    public bool HelpDefender(GameObject helper, GameObject helpee)
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

    public bool DoesItStillNeedHelp(GameObject helper, GameObject helpee)
    {
        return whoIsHelpingWho[helpee] == helper;
    }

    IEnumerator UpdateStatus()
    {
        while (true)
        {
            defenders = GameObject.FindGameObjectsWithTag("Defender");
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
}
