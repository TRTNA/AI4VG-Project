using CRBT;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class DefenderFSM : MonoBehaviour
{
    [Range(1, 100)]
    public float attackRange = 10f;
    [Range(1, 100)]
    public float attackDamage = 5f;

    [Range(0.1f, 10)]
    public float reactionTime = 0.5f;

    public int hordeLimit = 1;

    //to privatize
    public GameObject target;
    public Vector3 destination;
    public bool onDefensivePosition = true;
    public float arrivingRange = 1f;
    public GameObject allyToHelp;


    public GameObject projectile;



    private FSM fsm;
    private GameObject[] defensivePositions;
    private NavMeshAgent agent;
    private DefendersCooperationController dcc;

    // Start is called before the first frame update
    void Start()
    {
        defensivePositions = GameObject.FindGameObjectsWithTag("DefensivePosition");
        agent = GetComponent<NavMeshAgent>();
        dcc = GameObject.Find("DefendersCooperationController").GetComponent<DefendersCooperationController>();

        //horde control bt
        BTAction pickAnEnemy = new BTAction(PickAnEnemy);
        BTAction lookAtTarget = new BTAction(LookAtTarget);
        BTSequence attackSequence = new BTSequence(new IBTTask[] { lookAtTarget, new BTCondition(IsTargetInSight), new BTAction(AttackTarget) });
        BTDecoratorUntilFail repeatAttackSequence = new BTDecoratorUntilFail(attackSequence);
        BTSequence hordeControl = new BTSequence(new IBTTask[] { new BTCondition(HasHorde), pickAnEnemy, repeatAttackSequence});
        
        BTAction moveToDestination = new BTAction(MoveToDestination);
        BTCondition hasNotArrivedToDestination = new BTCondition(HasNotArrivedToDestination);

        //back to def pos bt
        BTDecoratorUntilFail hasArrivedCheck = new BTDecoratorUntilFail(hasNotArrivedToDestination);
        BTSequence backToDefPosSequence = new BTSequence(new IBTTask[]
        {
            new BTCondition(IsOutsideDefensivePosition),
            new BTAction(FindAnEmptyDefensivePosition),
            moveToDestination,
            hasArrivedCheck
        });

        //help others with hordes bt
        BTCondition anyoneNeedsHelp = new BTCondition(DoesAnyoneNeedsHelp);
        BTAction lockDefenderToHelp = new BTAction(CommunicateHelping);
        BTAction setAsDestination = new BTAction(SetDefenderToHelpAsDestination);
        BTDecoratorUntilFail hasArrivedOnHelpCheck = new BTDecoratorUntilFail(new BTSequence(new IBTTask[] { new BTCondition(DoesAllyStillNeedsHelp), hasNotArrivedToDestination }));

        BTSequence helpAlliesSequence = new BTSequence(new IBTTask[]
        {
            anyoneNeedsHelp,
            lockDefenderToHelp,
            setAsDestination,
            moveToDestination,
            hasArrivedOnHelpCheck
        });

        //normal attacking
        BTSequence normalAttackSequence = new BTSequence(new IBTTask[]
        {
            pickAnEnemy,
            repeatAttackSequence
        });

        //help or attack selector
        BTSelector helpOrAttackSelector = new BTSelector(new IBTTask[]
        {
            helpAlliesSequence,
            normalAttackSequence
        });


        //main Selector
        BTSelector root = new BTSelector(new IBTTask[] { hordeControl, backToDefPosSequence, helpOrAttackSelector});

        BehaviorTree bt = new BehaviorTree(root);

        FSMState defendWalls = new FSMState();
        defendWalls.stayActions.Add(() => bt.Step());

        fsm = new FSM(defendWalls);
        StartCoroutine(UpdateFSM());

    }

    private bool DoesAnyoneNeedsHelp()
    {
        allyToHelp = dcc.DoesItNeedToHelp(gameObject);
        return allyToHelp != null;
    }

    private bool CommunicateHelping()
    {
        if (allyToHelp != null)
        {
            return dcc.HelpDefender(gameObject, allyToHelp);
        }
        return false;
    }

    private bool SetDefenderToHelpAsDestination()
    {
        if (allyToHelp == null) return false;
        destination = allyToHelp.transform.position;
        return true;
    }

    private bool DoesAllyStillNeedsHelp()
    {
        return dcc.DoesItStillNeedHelp(gameObject, allyToHelp);
    }

    private bool IsOutsideDefensivePosition()
    {
        return ! onDefensivePosition;
    }

    private bool FindAnEmptyDefensivePosition()
    {
        GameObject[] sortedDefPos = Utils.SortByDistance(transform.position, defensivePositions);
        foreach (var defPos in sortedDefPos)
        {
            if (! defPos.GetComponent<DefensivePosition>().IsOccupied())
            {
                destination = defPos.transform.position;
                return true;
            }
        }
        return false;
    }

    private bool MoveToDestination()
    {
        if (destination != null)
        {
            return agent.SetDestination(destination);
        }
        return false;
    }

    private bool HasNotArrivedToDestination()
    {
        if (destination == null) return false;
        return Vector3.Distance(transform.position, destination) > arrivingRange;
    }

    public bool HasHorde()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange);
        int count = 0;
        foreach (var enemy in enemies)
        {
            if (enemy.gameObject.CompareTag("Attacker")) count++;
        }
        return count > hordeLimit;
    }

    private bool PickAnEnemy()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var enemy in enemies)
        {
            if (enemy.gameObject.CompareTag("Attacker"))
            {
                target = enemy.gameObject;
                return true;
            }
        }
        return false;
    }

    public void OnDrawGizmosSelected()
    {
        Color lowAlphaRed = Color.red;
        lowAlphaRed.a = 0.1f;
        Gizmos.color = lowAlphaRed;
        Gizmos.DrawSphere(transform.position, attackRange);
        if (target != null) Gizmos.DrawLine(transform.position, target.transform.position);
    }

    private bool IsTargetInSight()
    {
        if (target == null) return false;
        RaycastHit hit;
        if (Physics.Linecast(transform.position, target.transform.position, out hit))
        {
            return hit.collider.gameObject.CompareTag("Attacker");
        }
        return false;
    }

    private bool LookAtTarget()
    {
        transform.LookAt(target.transform);
        return true;
    }

    private bool AttackTarget()
    {
        if (target == null) return false;
        HealthController hc;
        if (target.TryGetComponent<HealthController>(out hc))
        {
            hc.TakeDamage(attackDamage);
            GameObject proj = Instantiate(projectile, transform.position, transform.rotation);
            proj.GetComponent<Rigidbody>().AddForce((target.transform.position - transform.position).normalized * 30f, ForceMode.Impulse);
            return hc.Health != 0;
        }
        return false;
    }

    IEnumerator UpdateFSM()
    {
        while (true)
        {
            fsm.Update();
            yield return new WaitForSeconds(reactionTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DefensivePosition"))
        {
            var dp = other.gameObject.GetComponent<DefensivePosition>();
            if (! dp.IsOccupied())
            {
                onDefensivePosition = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("DefensivePosition"))
        {
            onDefensivePosition = false;
        }
    }
}
