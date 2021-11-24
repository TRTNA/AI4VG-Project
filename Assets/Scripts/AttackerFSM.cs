using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class AttackerFSM : MonoBehaviour
{
    public GameObject[] fortsGates = new GameObject[4];
    [Range(1, 10)]
    public int reactionTime = 1;

    public float walkSpeed = 3.0f;
    public float runSpeed = 7.0f;
    public float attackCooldown = 10.0f;

    public float interactionRay = 2.0f; //cambiare nome
    private FSM fsm;
    private Transform nearestGate;
    private GameObject target;

    private bool attackingCoroutineRunning = false;

    private Animation anim;

    // Start is called before the first frame update
    void Start()
    {
        anim = GetComponent<Animation>();
        anim.playAutomatically = false;
        for (int i = 0; i < 4; i++)
        {
            fortsGates[i] = GameObject.Find("Fort/Gate" + i);
        }

        FSMState approaching = new FSMState();
        approaching.enterActions.Add(FindNearestGate);
        approaching.stayActions.Add(WalkToDestination);

        FSMState breaching = new FSMState();
        breaching.enterActions.Add(Breach);
        breaching.stayActions.Add(Attack);

        FSMState searching = new FSMState();
        searching.enterActions.Add(FreeTarget);
        searching.stayActions.Add(Search);

        FSMTransition approachingToBreachingTransition = new FSMTransition(NearestGateIsCloseToCharacterAndCanBeBreached);
        approaching.AddTransition(approachingToBreachingTransition, breaching);

        FSMTransition breachingToSearchingTransition = new FSMTransition(NearestGateIsBreachedAndIsCloseToCharacter);
        breaching.AddTransition(breachingToSearchingTransition, searching);

        FSMTransition approachingToSearchingTransition = new FSMTransition(NearestGateIsBreachedAndIsCloseToCharacter);
        approaching.AddTransition(approachingToSearchingTransition, searching);

        FSMState attacking = new FSMState();
        attacking.stayActions.Add(Attack);

        FSMState chasing = new FSMState();
        chasing.stayActions.Add(Chase);

        FSMTransition searchingToAttackingTransition = new FSMTransition(TargetIsClose);
        searching.AddTransition(searchingToAttackingTransition, attacking);

        FSMTransition searchingToChasingTransition = new FSMTransition(TargetIsFar);
        searching.AddTransition(searchingToChasingTransition, chasing);

        FSMTransition chasingToAttackingTransition = new FSMTransition(TargetIsClose);
        chasing.AddTransition(chasingToAttackingTransition, attacking);

        FSMTransition attackingToChaseTransition = new FSMTransition(TargetIsFar);
        attacking.AddTransition(attackingToChaseTransition, chasing);

        FSMTransition attackingToSearchingTransition = new FSMTransition(TargetIsUnavailable);
        attacking.AddTransition(attackingToSearchingTransition, searching);
        

        fsm = new FSM(approaching);
        StartCoroutine(UpdateFSM());
    }

    private bool TargetIsUnavailable()
    {
        return target == null;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private bool TargetIsClose()
    {
        return target != null && Vector3.Distance(target.transform.position, gameObject.transform.position) < interactionRay;
    }

    private bool TargetIsFar()
    {
        return target != null && Vector3.Distance(target.transform.position, gameObject.transform.position) > interactionRay;
    }

    private bool NearestGateIsCloseToCharacterAndCanBeBreached()
    {
        return ! nearestGate.gameObject.GetComponent<Gate>().IsOpen() && Vector3.Distance(gameObject.transform.position, nearestGate.position) < interactionRay;
    }

    private bool NearestGateIsBreachedAndIsCloseToCharacter()
    {
        return nearestGate.gameObject.GetComponent<Gate>().IsOpen() && Vector3.Distance(gameObject.transform.position, nearestGate.position) < interactionRay;
    }

    private void Search()
    {
        //albero di decisione per scegliere un target e firare transizione?
        anim.Play("Idle");
        Debug.Log(gameObject.name + " is searching...");
    }

    private void Breach()
    {
        Debug.Log(gameObject.name + " is breaching a gate!");
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

    private void Chase()
    {
        if(target != null && ! gameObject.GetComponent<NavMeshAgent>().hasPath)
        {
            anim.Play("Run");
            GetComponent<NavMeshAgent>().speed = runSpeed;
            gameObject.GetComponent<NavMeshAgent>().destination = target.transform.position;
        }
    }

    private void FreeTarget()
    {
        target = null;
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
        target = nearestGate.gameObject;
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
        while(true && target != null)
        {
            if (target != null && target.gameObject.GetComponent<HealthController>().GetHealth() == 0)
            {
                target = null;
                attackingCoroutineRunning = false;
                anim.Play("Idle");
                yield break;
            }
            anim.Play("Attack1");
            Debug.Log(gameObject.name + " is attacking!");
            target.SendMessage("TakeDamage", 10);
            yield return new WaitForSeconds(attackCooldown);

        }
        anim.Play("Idle");
        attackingCoroutineRunning = false;
    }
}
