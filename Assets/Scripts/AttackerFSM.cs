using System;
using System.Collections;
//TODO da rimuovere ref a linq e fare in altro modo il minimo
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class AttackerFSM : MonoBehaviour, IObserver
{
    public GameObject[] fortsGates = new GameObject[4];
    [Range(1, 10)]
    public int reactionTime = 1;

    public float walkSpeed = 3.0f;
    public float runSpeed = 7.0f;
    public float attackCooldown = 10.0f;

    public float interactionRay = 2.0f; //cambiare nome
    private FSM fsm;
    public Transform nearestGate;
    public GameObject target;

    private bool attackingCoroutineRunning = false;

    private Animation anim;
    public Vector3 destination;
    public GameObject hordeReference;
    private DecisionTree breachingDT;
    private Vector3 finalPosition;
    public float forwardOffset = 3f;
    public float circleRay = 5f;


    // Start is called before the first frame update
    void Start()
    {
        anim = GetComponent<Animation>();
        anim.playAutomatically = false;
        fortsGates = GameObject.FindGameObjectsWithTag("Gate");


        FSMState breaching = new FSMState();
        breaching.enterActions.Add(FindNearestGate);
        breaching.enterActions.Add(SetNearestGateAsDestination);

        FSMState wandering = new FSMState();
        wandering.stayActions.Add(Wander);
        wandering.stayActions.Add(LookForAnEnemy);

        FSMState attacking = new FSMState();
        attacking.enterActions.Add(() => destination = target.transform.position);
        attacking.stayActions.Add(RunToDestination);

        FSMTransition wanderingToAttacking = new FSMTransition(TargetAcquired);
        wandering.AddTransition(wanderingToAttacking, attacking);

        FSMTransition tran = new FSMTransition(NearestGateIsWithingInteractionRateAndBreached);
        breaching.AddTransition(tran, wandering);

        //breaching dt def
        DTDecision isGateWithinInteractionRange = new DTDecision(IsGateWithinInteractionRange);

        DTAction walkingToGateAction = new DTAction(WalkToDestination);

        DTDecision isGateBreached = new DTDecision(IsGateBreached);

        DTAction attackingGateAction = new DTAction(AttackGate);
        DTAction goIntoFort = new DTAction(ReachFortsCenter);

        isGateBreached.AddLink(true, goIntoFort);
        isGateBreached.AddLink(false, attackingGateAction);

        isGateWithinInteractionRange.AddLink(false, walkingToGateAction);
        isGateWithinInteractionRange.AddLink(true, isGateBreached);

        breachingDT = new DecisionTree(isGateWithinInteractionRange);
        //end breaching dt def

        breaching.stayActions.Add(WalkBreachingDT);

        fsm = new FSM(breaching);
        StartCoroutine(UpdateFSM());
    }

    private bool TargetAcquired()
    {
        return target != null;
    }


    private void WalkBreachingDT()
    {
        if (breachingDT != null)
        {
            breachingDT.walk();
        }
    }

    private void RunToDestination()
    {
        GetComponent<NavMeshAgent>().speed = runSpeed;
        anim.Play("Run");
        GetComponent<NavMeshAgent>().SetDestination(destination);
    }

    private bool NearestGateIsWithingInteractionRateAndBreached()
    {
        return (bool)IsGateBreached(null) && (bool)IsGateWithinInteractionRange(null);
    }

    private object ReachFortsCenter(object bundle)
    {
        GameObject fort = GameObject.Find("Fort/Yard");
        Vector3 randomPoint = UnityEngine.Random.insideUnitCircle * 5f;
        destination = fort.transform.position + randomPoint;
        return WalkToDestination(null);
    }

    private void LookForAnEnemy()
    {
        Vector3 transformPos = transform.position;
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Defender");
        Shuffle(ref enemies);
        foreach (GameObject enemy in enemies)
        {
            RaycastHit hit;
            bool result = Physics.Linecast(transformPos, enemy.transform.position, out hit);
            if (result && hit.collider.gameObject.CompareTag("Defender"))
            {
                target = hit.collider.gameObject;
            }
        }
    }

    private void Shuffle<T>(ref T[] objs)
    {
        for (int i = 0; i < objs.Length; i++)
        {
            int rand = UnityEngine.Random.Range(0, objs.Length);
            T temp = objs[i];
            objs[i] = objs[rand];
            objs[rand] = temp;
        }
    }

    private void Wander()
    {
        Vector3 targetCircleCenter = transform.position + Vector3.forward * forwardOffset;
        Vector2 randomPoint = UnityEngine.Random.insideUnitCircle * circleRay;
        Vector3 randomPointAroundCircle = new Vector3(randomPoint.x + targetCircleCenter.x, targetCircleCenter.y, randomPoint.y + targetCircleCenter.z);
        anim.Play("Walk");
        NavMeshHit hit;
        bool targetNotOnYardArea = NavMesh.Raycast(transform.position, randomPointAroundCircle, out hit, 8);
        bool targetOnYardArea = !targetNotOnYardArea;
        bool characterOnYardArea = NavMesh.SamplePosition(transform.position, out hit, 0.1f, 8);
        if (characterOnYardArea && ! targetOnYardArea)
        {
            var yard = GameObject.Find("Fort/Yard");
            Vector3 randomPos = UnityEngine.Random.insideUnitSphere * forwardOffset + yard.transform.position;
            NavMesh.SamplePosition(randomPos, out hit, forwardOffset, 8);
            randomPointAroundCircle = hit.position;
        }
        Debug.DrawLine(transform.position, randomPointAroundCircle, Color.green, 1f);
        GetComponent<NavMeshAgent>().speed = walkSpeed / 2;
        GetComponent<NavMeshAgent>().autoBraking = false;
        GetComponent<NavMeshAgent>().SetDestination(randomPointAroundCircle);

    }



    private object AttackGate(object bundle)
    {
        if (nearestGate != null)
        {
            target = nearestGate.gameObject;
            Attack();
            return true;
        }
        return false;
    }



    /*private void FindAnAlly()
    {
        GameObject[] allies = GameObject.FindGameObjectsWithTag("Attacker");
        if (allies.Length == 0) return;
        RaycastHit hit;
        allies = allies.Where(ally => Physics.Raycast(transform.position, ally.transform.position, out hit, 1000)).ToArray();
        hordeReference = allies.OrderBy(ally => Vector3.Distance(gameObject.transform.position, ally.transform.position)).ElementAt(0);
    }*/

    private object IsGateWithinInteractionRange(object bundle)
    {
        return Vector3.Distance(gameObject.transform.position, nearestGate.position) < interactionRay;
    }

    private object IsGateBreached(object bundle)
    {
        return nearestGate.gameObject.GetComponent<Gate>().IsOpen();
    }

    private void SetNearestGateAsDestination()
    {
        destination = nearestGate.position;
    }

    private object WalkToDestination(object bundle)
    {
        if (destination != null)
        {
            anim.Play("Walk");
            GetComponent<NavMeshAgent>().speed = walkSpeed;
            GetComponent<NavMeshAgent>().SetDestination(destination);
            return true;
        }
        return false;
    }

    private void Attack()
    {
        if (target != null && ! attackingCoroutineRunning)
        {
            Debug.Log("Starting attacking coroutine");
            attackingCoroutineRunning = true;
            StartCoroutine(AttackTarget());
        }
    }


    private void FindNearestGate()
    {
        nearestGate = GetNearestGatePosition();
        SetNearestGateAsDestination();
    }

    private Transform GetNearestGatePosition()
    {
        ArrayList distances = new ArrayList();
        foreach (GameObject gateObject in fortsGates)
        {
            Gate gate = gateObject.GetComponent<Gate>();
            distances.Add((gateObject.transform.position - gameObject.transform.position).sqrMagnitude);
            
        }
        return fortsGates[distances.IndexOf(distances.ToArray().Min())].transform;
    }

    IEnumerator UpdateFSM()
    {
        while(true)
        {
            fsm.Update();
            yield return new WaitForSeconds(reactionTime);
        }
    }

    IEnumerator AttackTarget()
    {
        GameObject targetMemo = target;
        target.GetComponent<HealthController>().AddObserver(this);
        while (target != null)
        {
            anim.Play("Attack1");
            Debug.Log(gameObject.name + " is attacking!");
            target.SendMessage("TakeDamage", 10);
            yield return new WaitForSeconds(attackCooldown);

        }
        targetMemo.GetComponent<HealthController>().RemoveObserver(this);
        anim.Play("Idle");
        attackingCoroutineRunning = false;
    }

    public void OnNotify(GameObject subject, object status)
    {
        if (subject.CompareTag("Gate") && (int)status == 0)
        {
            target = null;
        }
    }
}
