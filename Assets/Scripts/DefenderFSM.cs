using CRBT;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthController))]
public class DefenderFSM : MonoBehaviour
{
    [Range(1, 100)]
    public float attackRange = 10f;
    [Range(1, 100)]
    public float attackDamage = 5f;
    [Range(1, 100)]
    public float surroundedRange = 5f;
    [Range(1, 20)]
    public int surroundedThreshold = 3;
    [Range(1, 20)]
    public float fleeDistance = 3f;
    [Range(1, 10)]
    public float rotationSpeed = 5f;

    [Range(0.1f, 10)]
    public float reactionTime = 0.5f;

    public int hordeLimit = 1;

    //to privatize
    public GameObject target;
    public Vector3 destination;
    public bool onDefensivePosition = true;
    public float arrivingRange = 1f;
    public GameObject allyToHelp;

    public GameObject[] enemiesSurrounding;


    public GameObject projectile;


    private int navMeshAreaMask;
    private FSM fsm;
    private GameObject[] defensivePositions;
    private NavMeshAgent agent;
    private DefendersCooperationController dcc;
    private GameObject fort;

    private GameObject[] defenders;
    private bool hordeStatus;

    // Start is called before the first frame update
    void Start()
    {
        defensivePositions = GameObject.FindGameObjectsWithTag("DefensivePosition");
        agent = GetComponent<NavMeshAgent>();
        dcc = GameObject.Find("DefendersCooperationController").GetComponent<DefendersCooperationController>();
        navMeshAreaMask = 1<<NavMesh.GetAreaFromName("Fort");
        defenders = GameObject.FindGameObjectsWithTag("Defender");
        GetComponent<HealthController>().SetOnHealthDroppedToZero(() => OnKilled());

        /*horde control bt
        BTAction pickAnEnemy = new BTAction(PickAnEnemy);
        BTSequence attackSequence = new BTSequence(new IBTTask[] { new BTCondition(IsTargetInSight), new BTAction(AttackTarget) });
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
        BTSelector defendWallsBTRoot = new BTSelector(new IBTTask[] { hordeControl, backToDefPosSequence, helpOrAttackSelector});

        BehaviorTree defendWallsBT = new BehaviorTree(defendWallsBTRoot);*/

        FSMState defendWalls = new FSMState();
        /*defendWalls.stayActions.Add(() => defendWallsBT.Step());*/
        defendWalls.stayActions.Add(PickAnEnemy);
        defendWalls.stayActions.Add(AttackTarget);
        defendWalls.stayActions.Add(() => hordeStatus = VerifyHordeStatus());
        defendWalls.exitActions.Add(() => { target = null;  agent.ResetPath(); });

        FSMState reenterDefensivePosition = new FSMState();
        reenterDefensivePosition.enterActions.Add(FindAnEmptyDefensivePosition);
        reenterDefensivePosition.enterActions.Add(MoveToDestination);
        reenterDefensivePosition.exitActions.Add(() => { agent.ResetPath(); });
        reenterDefensivePosition.enterActions.Add(() => Debug.Log(gameObject.name + "Entering def pos reenter"));

        FSMState helpWithHorde = new FSMState();
        /*anyoneNeedsHelp,
            lockDefenderToHelp,
            setAsDestination,
            moveToDestination,
            hasArrivedOnHelpCheck*/
        //deve diventare setta alleato da aiutare e nella transizione a questo stato si controlla se qualcuno ha bisogno

        helpWithHorde.enterActions.Add(() => GetAllyToHelpWithHorde());
        helpWithHorde.enterActions.Add(() => CommunicateHelping());
        helpWithHorde.enterActions.Add(() => destination = allyToHelp != null ? allyToHelp.transform.position : Vector3.zero);
        helpWithHorde.enterActions.Add(() => agent.SetDestination(destination));
        helpWithHorde.stayActions.Add(() =>
        {
            if (! agent.hasPath)
            {
                PickAnEnemy();
                AttackTarget();
            }
        });
        helpWithHorde.exitActions.Add(() => { target = null; allyToHelp = null; agent.ResetPath(); });
        helpWithHorde.enterActions.Add(() => Debug.Log(gameObject.name + "Entering horde help"));
        

        FSMTransition defendToHelpWithHorde = new FSMTransition(DoesAnyoneNeedHelpWithHorde);
        defendWalls.AddTransition(defendToHelpWithHorde, helpWithHorde);

        FSMTransition helpWithHordeToDefend = new FSMTransition(HasHordeBeenDestroyed);
        helpWithHorde.AddTransition(helpWithHordeToDefend, defendWalls);

        //RIPRENDERE DA QUI SERVE:
        //FULL REFACTORING PERCHè è FATTO TUTTO MALE PER VEDERE SE FUNZIONAVA
        //DUE FSM UNA PER YARD UNA PER WALL VANNO INTERFACCIATE IN STILE HFSM MA SEMPLIFICAND
        //OVVERO DUE FSM SI PASSA DA UNA ALL'ALTRA CON SEMPLICI TRANSIZIONI GESTITE DALLE FSM CHE ESCONO DALLO STATO ATTUALE E RIENTRANO NELLO STATO IDENTIFICATO COME INIZIALE

        FSMTransition defendWallsToReenterDefPos = new FSMTransition(IsOutsideDefensivePosition);
        defendWalls.AddTransition(defendWallsToReenterDefPos, reenterDefensivePosition);

        FSMTransition reenterDefPosToDefendWalls = new FSMTransition(() => !IsOutsideDefensivePosition());
        reenterDefensivePosition.AddTransition(reenterDefPosToDefendWalls, defendWalls);
        
        FSMState defendYard = new FSMState();
        defendYard.exitActions.Add(() => { target = null; allyToHelp = null; });
        defendYard.enterActions.Add(() => Debug.Log(gameObject.name + " Entering defend yard"));
        defendYard.enterActions.Add(() => { agent.ResetPath();  });

        defendYard.stayActions.Add(AttackEnemyInsideFort);

        FSMState flee = new FSMState();
        flee.enterActions.Add(() => agent.autoBraking = false);
        flee.enterActions.Add(() => Debug.Log(gameObject.name + " Entering flee"));


        flee.stayActions.Add(Flee);
        flee.exitActions.Add(() => agent.autoBraking = true);

        FSMState helpAlly = new FSMState();
        helpAlly.stayActions.Add(() =>
        {
            if (allyToHelp == null)
            {
                allyToHelp = Utils.GetNearestObject(transform.position, GetNearbySurroundedAllies());
                if (allyToHelp == null) return;
            }
            float distanceToally = Vector3.Distance(transform.position, allyToHelp.transform.position);
            if (distanceToally > attackRange)
            {
                
                Vector3 directionTowardAlly = (allyToHelp.transform.position - transform.position).normalized;
                agent.SetDestination(transform.position + directionTowardAlly * (distanceToally - attackRange));
                return;
            }
            target = PickEnemyWhoIsSurroundingAlly();
            AttackTarget();
            


        });
        helpAlly.exitActions.Add(() => allyToHelp = null);
        helpAlly.enterActions.Add(() => Debug.Log(gameObject.name + " Entering help"));

        FSMTransition defendToHelp = new FSMTransition(() => dcc.IsAnyoneSurrounded().Length != 0);
        defendYard.AddTransition(defendToHelp, helpAlly);
        FSMTransition helpToFlee = new FSMTransition(AmISurrounded);
        helpAlly.AddTransition(helpToFlee, flee);
        FSMTransition helpToDefend = new FSMTransition(() => ! dcc.IsStillSurrounded(allyToHelp));
        helpAlly.AddTransition(helpToDefend, defendYard);


        FSMTransition fromDefendToFlee = new FSMTransition(AmISurrounded);
        defendYard.AddTransition(fromDefendToFlee, flee);

        FSMTransition fromFleeToDefend = new FSMTransition(AmIClear);
        flee.AddTransition(fromFleeToDefend, defendYard);

        FSMTransition wallsToYard = new FSMTransition(() => !IsFortClear());
        FSMTransition yardToWalls = new FSMTransition(IsFortClear);

        defendWalls.AddTransition(wallsToYard, defendYard);
        defendYard.AddTransition(yardToWalls, defendWalls);

        defendWalls.enterActions.Add(() => Debug.Log(gameObject.name + " Entering defend walls"));
        fsm = new FSM(defendWalls);
        StartCoroutine(UpdateFSM());

    }

    private void OnKilled()
    {
        gameObject.SetActive(false);
        Destroy(gameObject, 1f);
    }

    private bool IsFortClear()
    {
        GameObject[] attackers = GameObject.FindGameObjectsWithTag("Attacker");
        NavMeshHit hit;
        foreach (var attacker in attackers)
        {
            if (NavMesh.SamplePosition(attacker.transform.position,out hit, 0.01f, 1<<NavMesh.GetAreaFromName("Fort")))
            {
                return false;
            }
        }
        return true;
    }

    private void AttackEnemyInsideFort()
    {
        if (target == null) PickAnEnemyInsideFort();
        if (!TargetIsInRange())
        {
            target = null;
            return;
        }
        target.GetComponent<HealthController>().TakeDamage(attackDamage);
        if (target.GetComponent<HealthController>().Health == 0) target = null;
    }

    private GameObject[] GetNearbySurroundedAllies()
    {
        GameObject[] surroundedAllies = dcc.IsAnyoneSurrounded();
        List<GameObject> nearbySurroundedAllies = new List<GameObject>();
        foreach (var ally in surroundedAllies)
        {
            if (ally != gameObject && Vector3.Distance(transform.position, ally.transform.position) < attackRange)
            {
                nearbySurroundedAllies.Add(ally);
            }
        }
        return nearbySurroundedAllies.ToArray();

    }

    private GameObject PickEnemyWhoIsSurroundingAlly()
    {
        Collider[] colls = Physics.OverlapSphere(allyToHelp.transform.position, surroundedRange);
        List<GameObject> enemies = new List<GameObject>();
        foreach (var coll in colls)
        {
            if (coll.gameObject.CompareTag("Attacker")) enemies.Add(coll.gameObject);
        }
        if (enemies.Count == 0) return null;
        return enemies[0];
    }

    private bool TargetIsInRange()
    {
        if (target != null) return Vector3.Distance(target.transform.position, transform.position) < attackRange;
        return false;
    }

    private void Flee()
    {
        Collider[] objectsAround = Physics.OverlapSphere(transform.position, surroundedRange);
        List<GameObject> enemies = new List<GameObject>();
        int count = 0;
        foreach (var coll in objectsAround)
        {
            if (coll.gameObject.CompareTag("Attacker"))
            {
                enemies.Add(coll.gameObject);
                count++;

            }
        }
        enemiesSurrounding = enemies.ToArray();

        //temporary
        if (enemiesSurrounding == null || enemiesSurrounding.Length == 0) agent.SetDestination(Vector3.zero);

        Vector3 averageEnemiesDirection = new Vector3();
        for (int i = 0; i < enemiesSurrounding.Length; i++)
        {
            Vector3 dir = Vector3.ProjectOnPlane(enemiesSurrounding[i].transform.position - transform.position, Vector3.up);
            averageEnemiesDirection += dir;
        }

        averageEnemiesDirection /= enemiesSurrounding.Length;
        Vector3 fleeDirection = (-averageEnemiesDirection).normalized;
        Vector3 fleeDestination = transform.position + fleeDirection * fleeDistance;
        NavMeshHit hit;

        if (NavMesh.SamplePosition(fleeDestination, out hit, 3f, navMeshAreaMask))
        {
            Debug.DrawLine(transform.position, hit.position, Color.green, 5f);
            agent.SetDestination(hit.position);
            return;
        }

        Vector3 left = transform.position + Quaternion.Euler(0, -90, 0) * fleeDirection * fleeDistance;

        if (NavMesh.SamplePosition(left, out hit, 3f, navMeshAreaMask))
        {
            Debug.DrawLine(transform.position, hit.position, Color.green, 5f);

             agent.SetDestination(hit.position);
            return;
        }

        Vector3 right = transform.position + Quaternion.Euler(0, 90, 0) * fleeDirection * fleeDistance;
        Debug.DrawLine(transform.position, right, Color.red, 5f);

        if (NavMesh.SamplePosition(right, out hit, 3f ,navMeshAreaMask))
        {
            Debug.DrawLine(transform.position, hit.position, Color.green, 5f);

             agent.SetDestination(hit.position);
            return;
        }

        //temporary
        agent.SetDestination(Vector3.zero);

    }

    public bool AmISurrounded()
    {
        Collider[] objectsAround = Physics.OverlapSphere(transform.position, surroundedRange);
        List<GameObject> enemies = new List<GameObject>();
        int count = 0;
        foreach (var coll in objectsAround)
        {
            if (coll.gameObject.CompareTag("Attacker"))
            {
                enemies.Add(coll.gameObject);
                count++;
            }
        }
        enemiesSurrounding = enemies.ToArray();
        return count >= surroundedThreshold;
    }

    public bool AmIClear()
    {
        return !AmISurrounded();
    }

    private void GetAllyToHelpWithHorde()
    {
        if (allyToHelp == null)
        {
            allyToHelp = dcc.DoesItNeedHelpWithHorde(gameObject);
        }
    }

    private bool DoesAnyoneNeedHelpWithHorde()
    {
        foreach (var def in defenders)
        {
            if (def != null && def != gameObject)
            {
                if (def.GetComponent<DefenderFSM>().HasHorde())
                {
                    allyToHelp = def;
                    return true;
                }
            }
        }
        return false;
    }

    private void CommunicateHelping()
    {
        if (allyToHelp != null)
        {
            dcc.HelpDefenderWithHorde(gameObject, allyToHelp);
        }
    }

    private bool SetDefenderToHelpAsDestination()
    {
        if (allyToHelp == null) return false;
        destination = allyToHelp.transform.position;
        return true;
    }

    private bool HasHordeBeenDestroyed()
    {
        return ! allyToHelp.GetComponent<DefenderFSM>().HasHorde();
    }

    private bool IsOutsideDefensivePosition()
    {
        return ! onDefensivePosition;
    }

    private void FindAnEmptyDefensivePosition()
    {
        GameObject defPos = dcc.ReserveNearestEmptyDefensivePosition(gameObject);
        if (defPos != null)
        {
            destination = defPos.transform.position;
        }
    }

    private void MoveToDestination()
    {
        if (destination != null && ! agent.hasPath)
        {
            agent.SetDestination(destination);
        }
    }

    private bool HasNotArrivedToDestination()
    {
        if (destination == null) return false;
        return Vector3.Distance(transform.position, destination) > arrivingRange;
    }

    private bool VerifyHordeStatus()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange);
        int count = 0;
        foreach (var enemy in enemies)
        {
            if (enemy.gameObject.CompareTag("Attacker")) count++;
        }
        return count > hordeLimit;
    }

    public bool HasHorde() { return hordeStatus; }
    

    private void PickAnEnemy()
    {
        if (target != null) return;
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var enemy in enemies)
        {
            if (enemy.gameObject.CompareTag("Attacker"))
            {
                target = enemy.gameObject;
            }
        }
    }

    private bool PickAnEnemyInsideFort()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange);
        NavMeshHit hit;
        foreach (var enemy in enemies)
        {
            if (enemy.gameObject.CompareTag("Attacker") && NavMesh.SamplePosition(enemy.transform.position, out hit, 0.001f, 1 << NavMesh.GetAreaFromName("Fort")))
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


    private void AttackTarget()
    {
        if (target == null) return;
        HealthController hc;
        if (! IsTargetInSight())
        {
            target = null;
            return;
        }
        if (target.TryGetComponent<HealthController>(out hc))
        {
            hc.TakeDamage(attackDamage);
            GameObject proj = Instantiate(projectile, transform.position, transform.rotation);
            proj.GetComponent<Rigidbody>().AddForce((target.transform.position - transform.position).normalized * 30f, ForceMode.Impulse);
            if (hc.Health == 0)
            {
                target = null;
                return;
            }
        }
        target = null;
    }

    IEnumerator UpdateFSM()
    {
        while (true)
        {
            yield return new WaitForSeconds(reactionTime);
            fsm.Update();
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

    private void FixedUpdate()
    {
        if (target != null)
        {
            var targetRotation = Quaternion.LookRotation(target.transform.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        
    }


}
