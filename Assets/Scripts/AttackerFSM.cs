using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class AttackerFSM : MonoBehaviour
{
    public Transform[] fortsGates = new Transform[4];
    [Range(1, 10)]
    public int reactionTime;

    public float walkSpeed = 3.0f;
    public float runSpeed = 7.0f;

    public float interactionRay = 2.0f; //cambiare nome
    private FSM fsm;
    private Transform nearestGate;
    private GameObject target;

    private Animation anim;

    // Start is called before the first frame update
    void Start()
    {
        anim = GetComponent<Animation>();
        for (int i = 0; i < 4; i++)
        {
            fortsGates[i] = GameObject.Find("Fort/Gate" + i).transform;
        }

        FSMState approaching = new FSMState();
        approaching.enterActions.Add(FindNearestGate);
        approaching.stayActions.Add(WalkToDestination);

        FSMState breaching = new FSMState();
        breaching.enterActions.Add(Breach);
        breaching.stayActions.Add(Attack);

        FSMState searching = new FSMState();
        searching.stayActions.Add(Search);

        FSMTransition approachingToBreachingTransition = new FSMTransition(NearestGateIsCloseAndActive);
        approaching.AddTransition(approachingToBreachingTransition, breaching);

        FSMTransition breachingToSearchingTransition = new FSMTransition(NearestGateIsCloseAndBreached);
        breaching.AddTransition(breachingToSearchingTransition, searching);

        FSMTransition approachingToSearchingTransition = new FSMTransition(NearestGateIsCloseAndBreached);
        approaching.AddTransition(approachingToSearchingTransition, searching);

        FSMState attacking = new FSMState();
        attacking.stayActions.Add(Attack);

        FSMState chasing = new FSMState();
        chasing.stayActions.Add(Chase);

        FSMTransition searchingToAttackingTransition = new FSMTransition(TargetIsSetAndClose);
        searching.AddTransition(searchingToAttackingTransition, attacking);

        FSMTransition searchingToChasingTransition = new FSMTransition(TargetIsSetButNotClose);
        searching.AddTransition(searchingToChasingTransition, chasing);

        FSMTransition chasingToAttackingTransition = new FSMTransition(TargetIsSetAndClose);
        chasing.AddTransition(chasingToAttackingTransition, attacking);

        FSMTransition attackingToChaseTransition = new FSMTransition(TargetIsSetButNotClose);
        attacking.AddTransition(attackingToChaseTransition, chasing);

        fsm = new FSM(approaching);
        StartCoroutine(UpdateFSM());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private bool TargetIsSetAndClose()
    {
        return target != null && Vector3.Distance(target.transform.position, gameObject.transform.position) < interactionRay;
    }

    private bool TargetIsSetButNotClose()
    {
        return target != null && Vector3.Distance(target.transform.position, gameObject.transform.position) > interactionRay;
    }

    private bool NearestGateIsCloseAndActive()
    {
        return nearestGate.gameObject.activeInHierarchy && Vector3.Distance(gameObject.transform.position, nearestGate.position) < interactionRay;
    }

    private bool NearestGateIsCloseAndBreached()
    {
        return ! nearestGate.gameObject.activeInHierarchy && Vector3.Distance(gameObject.transform.position, nearestGate.position) < interactionRay;
    }

    private void Search()
    {
        //albero di decisione per scegliere un target e firare transizione?
        anim.Play("Idle");
        Debug.Log(gameObject.name + " is searching...");
    }

    private void Breach()
    {
        //implementare breaching
        target = nearestGate.gameObject;
        Debug.Log(gameObject.name + " is breaching a gate!");
    }

    private void Attack()
    {
        //implementare attacco a target
        anim.Play("Attack1");
        Debug.Log(gameObject.name + " is attacking!");
    }

    private void Chase()
    {
        if(target != null && ! gameObject.GetComponent<NavMeshAgent>().hasPath)
        {
            anim.Play("Run");
            GetComponent<NavMeshAgent>().speed = runSpeed;
            gameObject.GetComponent<NavMeshAgent>().destination = target.transform.position;
        }
    }


    private void WalkToDestination()
    {
        if (! gameObject.GetComponent<NavMeshAgent>().hasPath)
        {
            anim.Play("Walk");
            GetComponent<NavMeshAgent>().speed = walkSpeed;
            gameObject.GetComponent<NavMeshAgent>().destination = nearestGate.position;
        }
    }

    private void FindNearestGate()
    {
        nearestGate = GetNearestGatePosition();
    }

    private Transform GetNearestGatePosition()
    {
        ArrayList distances = new ArrayList();
        foreach (Transform gate in fortsGates)
        {
            distances.Add((gate.position - gameObject.transform.position).sqrMagnitude);
        }
        return fortsGates[distances.IndexOf(distances.ToArray().Min())];
    }

    IEnumerator UpdateFSM()
    {
        while(true)
        {
            fsm.Update();
            yield return new WaitForSeconds(reactionTime);
        }
    }
}
