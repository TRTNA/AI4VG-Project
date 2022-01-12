using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(HealthController))]
public class AttackerFSM : MonoBehaviour
{
    [Header("FSM settings")]
    [Range(0.1f, 5f)]
    public float reactionTime = 0.5f;

    [Header("Generic settings")]
    public float speed = 3.0f;

    [Header("Attacking settings")]
    public float attackDamage = 10f;
    public float attackRange = 5.0f;

    private FSM fsm;

    private NavMeshAgent agent;
    private Animator anim;

    private GameObject[] defenders;
    private GameObject nearestGate;
    public GameObject target;
    private Vector3 destination;
    private const float RandomEnemyPickingProbability = 0.5f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = speed;

        GetComponent<HealthController>().SetOnHealthDroppedToZero(OnKilled);
        anim = GetComponent<Animator>();
        defenders = GameObject.FindGameObjectsWithTag("Defender");

        FSMState breaching = new FSMState();
        breaching.enterActions.Add(FindNearestGate);
        DecisionTree breachingDT = CreateBreachingDecisionTree();
        breaching.stayActions.Add(() => breachingDT.walk());
        breaching.exitActions.Add(() => target = null);


        FSMState attacking = new FSMState();
        DecisionTree attackingDT = CreateAttackingDecisionTree();
        attacking.stayActions.Add(() => attackingDT.walk());

        FSMTransition breachingToAttacking = new FSMTransition(NearestGateIsWithingInteractionRateAndBreached);
        breaching.AddTransition(breachingToAttacking, attacking);

        fsm = new FSM(breaching);
        StartCoroutine(UpdateFSM());
    }

    private DecisionTree CreateBreachingDecisionTree()
    {
        DTDecision isGateInRange = new DTDecision((bundle) => IsObjectInRange(nearestGate));

        DTAction walkToGate = new DTAction(MoveToDestination);

        DTDecision isGateBreached = new DTDecision(IsGateBreached);

        DTAction attackGate = new DTAction((bundle) =>
        {
            target = nearestGate;
            return AttackTarget(null);
        });

        isGateBreached.AddLink(false, attackGate);
        isGateInRange.AddLink(false, walkToGate);
        isGateInRange.AddLink(true, isGateBreached);

        return new DecisionTree(isGateInRange);
    }

    private DecisionTree CreateAttackingDecisionTree()
    {
        DTDecision hasATarget = new DTDecision((b) => target != null);
        DTDecision isTargetWithinInteractionRange = new DTDecision((bundle) => IsObjectInRange(target));
        DTAction pickAnEnemy = new DTAction(PickAnEnemy);
        DTAction seekTarget = new DTAction((bundle) =>
        {
            if (target != null)
            {
                destination = target.transform.position;
                MoveToDestination(null);
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

    private bool NearestGateIsWithingInteractionRateAndBreached()
    {
        return (bool)IsGateBreached(null) && IsObjectInRange(nearestGate);
    }


    private object IsGateBreached(object bundle)
    {
        return nearestGate.gameObject.GetComponent<Gate>().IsOpen();
    }

    private void FindNearestGate()
    {
        nearestGate = Utils.GetNearestObject(transform.position, GameObject.FindGameObjectsWithTag("Gate"));
        destination = nearestGate.transform.position;
    }

    private object MoveToDestination(object bundle)
    {
        anim.Play("Base Layer.Run");
        return agent.SetDestination(destination);
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

    //probability to pick a random enemy instead of the closest one
    private object PickAnEnemy(object bundle)
    {
        defenders = RemoveDeadDefenders(defenders);
        if (defenders.Length == 0) return false;
 
        if (Random.Range(0f, 1f) < RandomEnemyPickingProbability)
        {
            target = Utils.GetNearestObject(transform.position, defenders);
            return true;
        }
        target = defenders[Random.Range(0, defenders.Length)];
        return true;
        
    }

    private object AttackTarget(object bundle)
    {
        if (target == null || ! target.TryGetComponent<HealthController>(out var hc)) return false;

        anim.Play("Base Layer.Attack");
        hc.TakeDamage(attackDamage);
        if (hc.Health != 0) return true;
        target = null;
        return false;
    }


    private bool IsObjectInRange(GameObject obj)
    {
        return obj != null && Vector3.Distance(transform.position, obj.transform.position) < attackRange;
    }


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
}
