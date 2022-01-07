using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(HealthController))]
public class AttackerFSM : MonoBehaviour, IObserver
{
    #region public variables declarations
    [Header("FSM settings")]
    [Range(1, 10)]
    public int reactionTime = 1;

    [Header("Generic settings")]
    public float walkSpeed = 3.0f;
    public float runSpeed = 7.0f;
    public float interactionRange = 5.0f;

    [Header("Attacking settings")]
    public float damage = 10f;

    [Header("Wandering settings")]
    public string constrainedAreaTag;
    public string navMeshAreaName;
    public float circleForwardOffset = 5f;
    public float circleRay = 5f;
    #endregion

    #region private variables declarations
    private FSM fsm;

    private NavMeshAgent agent;
    private Animator anim;

    private GameObject[] fortsGates;
    private GameObject[] defenders;
    private GameObject nearestGate;
    public GameObject target;
    private Vector3 destination;

    private Transform constrainedAreaCenter;
    private int navMeshAreaMask;

    private bool registeredToTarget = false;
    private bool endGame = false;
    private readonly float randomEnemyPickingProbability = 0.5f;
    #endregion

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        constrainedAreaCenter = GameObject.FindGameObjectWithTag(constrainedAreaTag).transform;
        navMeshAreaMask = 1 << NavMesh.GetAreaFromName(navMeshAreaName);
        GetComponent<HealthController>().SetOnHealthDroppedToZero(OnKilled);
        anim = GetComponent<Animator>();
        fortsGates = GameObject.FindGameObjectsWithTag("Gate");
        defenders = GameObject.FindGameObjectsWithTag("Defender");

        #region STATE: breaching
        FSMState breaching = new FSMState();
        breaching.enterActions.Add(FindNearestGate);
        DecisionTree breachingDT = CreateBreachingDecisionTree();
        breaching.stayActions.Add(() => breachingDT.walk());
        breaching.exitActions.Add(() => target = null);
        #endregion

        FSMState idle = new FSMState();
        idle.enterActions.Add(() =>
        {
            target = null;
            agent.isStopped = true;
            
        });
        idle.stayActions.Add(() =>
        {
            target = null;
            agent.isStopped = true;
            anim.Play("Base Layer.Idle1");


        });


        #region STATE: attacking
        FSMState attacking = new FSMState();
        DecisionTree attackingDT = CreateAttackingDecisionTree();
        attacking.stayActions.Add(() => attackingDT.walk());
        #endregion

        #region TRANSITIONS
        FSMTransition breachingToAttacking = new FSMTransition(NearestGateIsWithingInteractionRateAndBreached);
        breaching.AddTransition(breachingToAttacking, attacking);
        FSMTransition toIdle = new FSMTransition(() => endGame);
        attacking.AddTransition(toIdle, idle);
        #endregion

        fsm = new FSM(breaching);
        StartCoroutine(UpdateFSM());
    }

    #region METHODS: decision tree creation
    private DecisionTree CreateBreachingDecisionTree()
    {
        DTDecision isGateWithinInteractionRange = new DTDecision((bundle) => IsObjectWithinInteractionRange(nearestGate));

        DTAction walkingToGateAction = new DTAction(WalkToDestination);

        DTDecision isGateBreached = new DTDecision(IsGateBreached);

        DTAction attackingGateAction = new DTAction((bundle) =>
        {
            AcquireTarget(nearestGate);
            return AttackTarget(null);
        });
        DTAction goIntoFort = new DTAction(ReachFortsCenter);

        isGateBreached.AddLink(true, goIntoFort);
        isGateBreached.AddLink(false, attackingGateAction);

        isGateWithinInteractionRange.AddLink(false, walkingToGateAction);
        isGateWithinInteractionRange.AddLink(true, isGateBreached);

        return new DecisionTree(isGateWithinInteractionRange);
    }

    private DecisionTree CreateAttackingDecisionTree()
    {
        DTDecision hasATarget = new DTDecision((b) => target != null);
        DTDecision isTargetWithinInteractionRange = new DTDecision((bundle) => IsObjectWithinInteractionRange(target));
        DTAction pickAnEnemy = new DTAction(PickAnEnemy);
        DTAction seekTarget = new DTAction((bundle) =>
        {
            if (target != null)
            {
                destination = target.transform.position;
                RunToDestination();
                return true;
            }
            return false;
        });
        DTAction attackTarget = new DTAction(AttackTarget);


        isTargetWithinInteractionRange.AddLink(true, attackTarget);
        isTargetWithinInteractionRange.AddLink(false, seekTarget);

        hasATarget.AddLink(true, isTargetWithinInteractionRange);
        hasATarget.AddLink(false, pickAnEnemy);

        return new DecisionTree(hasATarget);
    }
    #endregion

    #region METHODS: breaching actions
    private bool NearestGateIsWithingInteractionRateAndBreached()
    {
        return (bool)IsGateBreached(null) && IsObjectWithinInteractionRange(nearestGate);
    }


    private object IsGateBreached(object bundle)
    {
        return nearestGate.gameObject.GetComponent<Gate>().IsOpen();
    }

    private void FindNearestGate()
    {
        nearestGate = Utils.GetNearestObject(transform.position, fortsGates);
        destination = nearestGate.transform.position;
    }
    #endregion

    #region METHODS: movement actions
    private void RunToDestination()
    {
        agent.speed = walkSpeed;
        anim.Play("Base Layer.Run");
        agent.SetDestination(destination);
    }

    private object WalkToDestination(object bundle)
    {
        if (destination != null)
        {
            anim.Play("Base Layer.Walk");
            agent.speed = walkSpeed;
            agent.SetDestination(destination);
            return true;
        }
        return false;
    }



    private object ReachFortsCenter(object bundle)
    {
        GameObject fort = GameObject.Find("Fort/Yard");
        Vector3 randomPoint = UnityEngine.Random.insideUnitCircle * 5f;
        destination = fort.transform.position + randomPoint;
        return WalkToDestination(null);
    }

    private GameObject[] RemoveDeadDefenders(GameObject[] defs)
    {
        List<GameObject> temp = new List<GameObject>();
        foreach (var def in defs)
        {
            if (def != null) temp.Add(def);
        }

        return temp.ToArray();
    }

    #endregion

    //probability to pick a random enemy instead of the closest one
    private object PickAnEnemy(object bundle)
    {
        defenders = RemoveDeadDefenders(defenders);
        if (defenders.Length == 0)
        {
            endGame = true;
            return false;
        }
        if (Random.Range(0f, 1f) > randomEnemyPickingProbability)
        {
            target = defenders[Random.Range(0, defenders.Length)];
            return true;
        }
        else
        {
            target = Utils.GetNearestObject(transform.position, defenders);
            return true;
        }
    }

    #region METHODS: attacking actions
    private void LookForAnEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Defender");
        //todo togliere shuffle
        Utils.Shuffle(ref enemies);
        foreach (GameObject enemy in enemies)
        {
            AcquireTarget(enemy);
        }
    }


    private object AttackTarget(object bundle)
    {
        HealthController hc;
        if (target != null && target.TryGetComponent<HealthController>(out hc))
        {
            if (!registeredToTarget)
            {
                hc.AddObserver(this);
                registeredToTarget = true;
            }
            anim.Play("Base Layer.Attack");
            hc.TakeDamage(damage);
            if (hc.Health == 0)
            {
                target = null;
                return false;
            }
            return true;
        }
        return false;
    }
    #endregion

    #region METHODS: utilities
    private void AcquireTarget(GameObject pTarget)
    {
        HealthController hc;
        if (pTarget.TryGetComponent<HealthController>(out hc))
        {
            hc.AddObserver(this);
            registeredToTarget = true;
        }
        target = pTarget;
    }

    private void ReleaseTarget()
    {
        if (target != null)
        {
            HealthController hc;
            if (target.TryGetComponent<HealthController>(out hc)) hc.RemoveObserver(this);
            registeredToTarget = false;
            target = null;
        }
    }

    private bool IsObjectWithinInteractionRange(GameObject obj)
    {
        return obj != null && Vector3.Distance(transform.position, obj.transform.position) < interactionRange;
    }



    #endregion


    IEnumerator UpdateFSM()
    {
        while(true)
        {
            fsm.Update();
            yield return new WaitForSeconds(reactionTime);
        }
    }

    private void OnKilled()
    {
        Destroy(gameObject);
    }


    public void OnNotify(GameObject subject, object status)
    {
        if ((int)status == 0)
        {
            ReleaseTarget();
        }
    }

    private IEnumerator Die()
    {
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }


}
