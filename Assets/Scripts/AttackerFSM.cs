using System.Collections;
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
    private Animation anim;

    private GameObject[] fortsGates;
    private GameObject nearestGate;
    private GameObject target;
    private Vector3 destination;

    private Transform constrainedAreaCenter;
    private int navMeshAreaMask;

    private bool registeredToTarget = false;
    #endregion

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        constrainedAreaCenter = GameObject.FindGameObjectWithTag(constrainedAreaTag).transform;
        navMeshAreaMask = NavMesh.GetAreaFromName(navMeshAreaName);
        GetComponent<HealthController>().SetOnHealthDroppedToZero(OnKilled);
        anim = GetComponent<Animation>();
        anim.playAutomatically = false;
        fortsGates = GameObject.FindGameObjectsWithTag("Gate");

        #region STATE: breaching
        FSMState breaching = new FSMState();
        breaching.enterActions.Add(() => Debug.Log("Entering breaching"));
        breaching.enterActions.Add(FindNearestGate);
        DecisionTree breachingDT = CreateBreachingDecisionTree();
        breaching.stayActions.Add(() => breachingDT.walk());
        #endregion

        #region STATE: wandering
        FSMState wandering = new FSMState();
        wandering.enterActions.Add(() => Debug.Log("Entering wandering"));
        wandering.enterActions.Add(() => SetAgentWanderingParams());
        wandering.stayActions.Add(Wander);
        wandering.stayActions.Add(LookForAnEnemy);
        wandering.exitActions.Add(() => agent.autoBraking = true);
        #endregion

        #region STATE: attacking
        FSMState attacking = new FSMState();
        attacking.enterActions.Add(() => Debug.Log("Entering attacking"));
        DecisionTree attackingDT = CreateAttackingDecisionTree();
        attacking.stayActions.Add(() => attackingDT.walk());
        attacking.exitActions.Add(ReleaseTarget);
        #endregion

        #region TRANSITIONS
        FSMTransition attackingToWandering = new FSMTransition(IsTargetReleased);
        attacking.AddTransition(attackingToWandering, wandering);

        FSMTransition wanderingToAttacking = new FSMTransition(IsTargetAcquired);
        wandering.AddTransition(wanderingToAttacking, attacking);

        FSMTransition breachingToWandering = new FSMTransition(NearestGateIsWithingInteractionRateAndBreached);
        breaching.AddTransition(breachingToWandering, wandering);
        #endregion

        fsm = new FSM(breaching);
        StartCoroutine(UpdateFSM());
    }

    #region METHODS: decision tree creation
    private DecisionTree CreateBreachingDecisionTree()
    {
        DTDecision isGateWithinInteractionRange = new DTDecision((bundle) =>
        {
            return IsObjectWithinInteractionRange(nearestGate);
        });

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
        DTDecision isTargetWithinInteractionRange = new DTDecision((bundle) =>
        {
            return IsObjectWithinInteractionRange(target);
        });
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

        return new DecisionTree(isTargetWithinInteractionRange);
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
        agent.speed = runSpeed;
        anim.Play("Run");
        agent.SetDestination(destination);
    }

    private object WalkToDestination(object bundle)
    {
        if (destination != null)
        {
            anim.Play("Walk");
            agent.speed = walkSpeed;
            agent.SetDestination(destination);
            return true;
        }
        return false;
    }

    private void Wander()
    {
        anim.Play("Walk");
        agent.SetDestination(GetWanderDestination());
    }

    private void SetAgentWanderingParams()
    {
        agent.speed = walkSpeed;
        agent.autoBraking = false;
    }

    private Vector3 GetWanderDestination()
    {
        Vector3 targetCircleCenter = transform.position + Vector3.forward * circleForwardOffset;
        Vector2 randomPoint = UnityEngine.Random.insideUnitCircle * circleRay;
        Vector3 wanderDestination = new Vector3(randomPoint.x, 0, randomPoint.y) + targetCircleCenter;

        NavMeshHit hit;
        bool targetOnArea = !NavMesh.Raycast(transform.position, wanderDestination, out hit, navMeshAreaMask);
        bool agentOnArea = NavMesh.SamplePosition(transform.position, out hit, 0.1f, 8);

        //if agent is on the area and target is not, find a random point on area and return it as destination
        if (!targetOnArea)
        {
            Vector3 randomPositionInConstrainedArea = UnityEngine.Random.insideUnitSphere * circleForwardOffset + constrainedAreaCenter.position;
            NavMesh.SamplePosition(randomPositionInConstrainedArea, out hit, circleForwardOffset, navMeshAreaMask);
            wanderDestination = hit.position;
        }
        //if either agent and target are not on the area return constrained area center as destination
        if (!agentOnArea && !targetOnArea)
        {
            wanderDestination = constrainedAreaCenter.position;
        }
        return wanderDestination;
    }

    private object ReachFortsCenter(object bundle)
    {
        GameObject fort = GameObject.Find("Fort/Yard");
        Vector3 randomPoint = UnityEngine.Random.insideUnitCircle * 5f;
        destination = fort.transform.position + randomPoint;
        return WalkToDestination(null);
    }

    #endregion

    #region METHODS: attacking actions
    private void LookForAnEnemy()
    {
        Vector3 transformPos = transform.position;
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Defender");
        Utils.Shuffle(ref enemies);
        foreach (GameObject enemy in enemies)
        {
            RaycastHit hit;
            bool result = Physics.Linecast(transformPos, enemy.transform.position, out hit);
            if (result && hit.collider.gameObject.CompareTag("Defender"))
            {
                AcquireTarget(hit.collider.gameObject);
            }
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
            anim.Play("Attack1");
            hc.TakeDamage(damage);
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
        return obj != null ? Vector3.Distance(transform.position, obj.transform.position) < interactionRange : false;
    }


    private bool IsTargetAcquired()
    {
        return target != null;
    }

    private bool IsTargetReleased()
    {
        return target == null;
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
        gameObject.GetComponent<AttackerFSM>().enabled = false;
        gameObject.GetComponent<NavMeshAgent>().enabled = false;
        //modify this using Animator instead of Animation
        anim["Death"].wrapMode = WrapMode.Once;
        anim.Play("Death");
        StartCoroutine(Die());
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
        yield return new WaitForSeconds(0.8f);
        Destroy(gameObject);
    }

    /*private void FindAnAlly()
    {
        GameObject[] allies = GameObject.FindGameObjectsWithTag("Attacker");
        if (allies.Length == 0) return;
        RaycastHit hit;
        allies = allies.Where(ally => Physics.Raycast(transform.position, ally.transform.position, out hit, 1000)).ToArray();
        hordeReference = allies.OrderBy(ally => Vector3.Distance(gameObject.transform.position, ally.transform.position)).ElementAt(0);
    }*/

}
