using CRBT;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

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
    private bool allyIsHelping;

    // Start is called before the first frame update
    void Start()
    {
        defensivePositions = GameObject.FindGameObjectsWithTag("DefensivePosition");
        agent = GetComponent<NavMeshAgent>();
        dcc = GameObject.Find("DefendersCooperationController").GetComponent<DefendersCooperationController>();
        navMeshAreaMask = 1<<NavMesh.GetAreaFromName("Fort");
        defenders = GameObject.FindGameObjectsWithTag("Defender");
        List<GameObject> temp = new List<GameObject>();
        foreach (var defender in defenders)
        {
            if (defender != gameObject) temp.Add(defender);
        }

        defenders = temp.ToArray();

        GetComponent<HealthController>().SetOnHealthDroppedToZero(OnKilled);


        BTCondition hasATarget = new BTCondition(HasATarget);
        BTCondition isTargetInSight = new BTCondition(IsTargetInSight);
        BTCondition isTargetInRange = new BTCondition(TargetIsInRange);
        BTAction attack = new BTAction(AttackTarget);
        BTCondition isTargetAlive = new BTCondition(IsTargetAlive);

        BTAction releaseTargetAction = new BTAction(ReleaseTarget);
        BTSequence attackSequence = new BTSequence(new IBTTask[] { hasATarget, isTargetInRange, isTargetInSight, attack, isTargetAlive });

        BTSelector attackOrReleaseTarget = new BTSelector(new IBTTask[] { attackSequence, releaseTargetAction });

        BehaviorTree attackBT = new BehaviorTree(attackOrReleaseTarget);

        FSMAction resetAgentPath = () => agent.ResetPath();
        FSMAction releaseTarget = () => target = null;
        FSMAction setAllyToHelpAsDestination = () => destination = allyToHelp != null ? allyToHelp.transform.position : destination;
        FSMAction attackTarget = () => attackBT.Step();

        FSMState attackEnemiesOutsideFortState = new FSMState();
        attackEnemiesOutsideFortState.stayActions.Add(PickAnEnemy);
        attackEnemiesOutsideFortState.stayActions.Add(attackTarget);
        attackEnemiesOutsideFortState.stayActions.Add(UpdateHordeStatus);
        attackEnemiesOutsideFortState.stayActions.Add(CheckHordeHelp);

        attackEnemiesOutsideFortState.exitActions.Add(releaseTarget);
        attackEnemiesOutsideFortState.exitActions.Add(resetAgentPath);

        FSMState goToDefensivePositionState = new FSMState();
        goToDefensivePositionState.enterActions.Add(() => Debug.Log(gameObject.name + "Entering def pos reenter"));
        goToDefensivePositionState.enterActions.Add(FindAnEmptyDefensivePosition);
        goToDefensivePositionState.enterActions.Add(MoveToDestination);
        goToDefensivePositionState.exitActions.Add(resetAgentPath);


        FSMState helpAllyState = new FSMState();

        BTCondition agentIsStill = new BTCondition(() => agent.velocity == Vector3.zero);

        //provare con test sulla velocity
        BTSequence attackEnemiesWhenArrived = new BTSequence(new IBTTask[]{agentIsStill, attackOrReleaseTarget});
        BehaviorTree helpWithHordeBT = new BehaviorTree(attackEnemiesWhenArrived);

        FSMAction helpWithHordeAction = () => helpWithHordeBT.Step();

        helpAllyState.enterActions.Add(() => Debug.Log(gameObject.name + "Entering horde help"));
        helpAllyState.enterActions.Add(setAllyToHelpAsDestination);
        helpAllyState.enterActions.Add(SetAgentDestination);

        helpAllyState.stayActions.Add(PickAnEnemy);
        helpAllyState.stayActions.Add(helpWithHordeAction);

        FSMAction resetAllyToHelp = () => allyToHelp = null;
        helpAllyState.exitActions = new List<FSMAction>() { releaseTarget, resetAllyToHelp ,resetAgentPath};


        FSMCondition isNotOutsideDefensivePosition = () => !IsOutsideDefensivePosition();
        FSMCondition hasAnAllyToHelpAndHasNotAHorde = () => allyToHelp != null && HasHorde() == false;

        FSMTransition defendToHelpWithHorde = new FSMTransition(hasAnAllyToHelpAndHasNotAHorde); 
        attackEnemiesOutsideFortState.AddTransition(defendToHelpWithHorde, helpAllyState);

        FSMTransition helpWithHordeToDefend = new FSMTransition(HasHordeBeenDestroyed);
        helpAllyState.AddTransition(helpWithHordeToDefend, attackEnemiesOutsideFortState);

        FSMTransition defendWallsToReenterDefPos = new FSMTransition(IsOutsideDefensivePosition);
        attackEnemiesOutsideFortState.AddTransition(defendWallsToReenterDefPos, goToDefensivePositionState);

        FSMTransition reenterDefPosToDefendWalls = new FSMTransition(isNotOutsideDefensivePosition);
        goToDefensivePositionState.AddTransition(reenterDefPosToDefendWalls, attackEnemiesOutsideFortState);

        //defend yard states
        
        FSMState attackEnemiesInsideFortState = new FSMState();
        attackEnemiesInsideFortState.enterActions.Add(() => Debug.Log(gameObject.name + " Entering defend yard"));
        attackEnemiesInsideFortState.enterActions.Add(resetAgentPath);

        attackEnemiesInsideFortState.stayActions.Add(AttackEnemyInsideFort);

        attackEnemiesInsideFortState.exitActions = new List<FSMAction>(){releaseTarget, resetAllyToHelp};

        FSMState fleeState = new FSMState();
        fleeState.enterActions.Add(() => agent.autoBraking = false);
        fleeState.enterActions.Add(() => Debug.Log(gameObject.name + " Entering flee"));

        //NON ATTACCANO QUANDO AIUTANO

        fleeState.stayActions.Add(Flee);

        fleeState.exitActions.Add(() => agent.autoBraking = true);

        BTCall hasAllyToHelp = () => allyToHelp != null;

        BTCondition hasAllyToHelpCondition = new BTCondition(hasAllyToHelp);


        BTCall allyIsToFar = () => Vector3.Distance(transform.position, allyToHelp.transform.position) > attackRange;
        BTAction getCloseToAlly = new BTAction(GetCloseToAlly);

        BTDecoratorFilter getCloseToAllyIfIsTooFar = new BTDecoratorFilter(allyIsToFar, getCloseToAlly);

        BTSequence helpSurroundedAllySequence = new BTSequence(new IBTTask[]
        {
            hasAllyToHelpCondition,
            getCloseToAllyIfIsTooFar,
            new BTAction(PickEnemyWhoIsSurroundingAlly),
            attackEnemiesWhenArrived
        });
        BehaviorTree helpSurroundedAllyBT = new BehaviorTree(helpSurroundedAllySequence);

        FSMState helpSurroundedAllyState = new FSMState();
        helpSurroundedAllyState.enterActions.Add(() => allyToHelp = Utils.GetNearestObject(transform.position, GetSurroundedAllies()));
        helpSurroundedAllyState.stayActions.Add(() => helpSurroundedAllyBT.Step());
        helpSurroundedAllyState.exitActions.Add(resetAllyToHelp);
        helpSurroundedAllyState.enterActions.Add(() => Debug.Log(gameObject.name + " Entering help"));

        FSMTransition defendToHelp = new FSMTransition(() => dcc.IsAnyoneSurrounded().Length != 0);
        attackEnemiesInsideFortState.AddTransition(defendToHelp, helpSurroundedAllyState);
        FSMTransition helpToFlee = new FSMTransition(AmISurrounded);
        helpSurroundedAllyState.AddTransition(helpToFlee, fleeState);
        FSMTransition helpToDefend = new FSMTransition(() => ! dcc.IsStillSurrounded(allyToHelp));
        helpSurroundedAllyState.AddTransition(helpToDefend, attackEnemiesInsideFortState);


        FSMTransition fromDefendToFlee = new FSMTransition(AmISurrounded);
        attackEnemiesInsideFortState.AddTransition(fromDefendToFlee, fleeState);

        FSMTransition fromFleeToDefend = new FSMTransition(AmIClear);
        fleeState.AddTransition(fromFleeToDefend, attackEnemiesInsideFortState);

        FSMTransition wallsToYard = new FSMTransition(() => !IsFortClear());
        FSMTransition yardToWalls = new FSMTransition(IsFortClear);

        attackEnemiesOutsideFortState.AddTransition(wallsToYard, attackEnemiesInsideFortState);
        attackEnemiesInsideFortState.AddTransition(yardToWalls, attackEnemiesOutsideFortState);

        FSMTransition helpWithHordeToDefendYard = new FSMTransition(() => !IsFortClear());
        helpAllyState.AddTransition(helpWithHordeToDefendYard, attackEnemiesInsideFortState);
        FSMTransition reenterDefPositionToDefendYard = new FSMTransition(() => !IsFortClear());
        goToDefensivePositionState.AddTransition(reenterDefPositionToDefendYard, attackEnemiesInsideFortState);

        attackEnemiesOutsideFortState.enterActions.Add(() => Debug.Log(gameObject.name + " Entering defend walls"));
        fsm = new FSM(attackEnemiesOutsideFortState);
        StartCoroutine(UpdateFSM());

    }

    private bool GetCloseToAlly()
    {
        if (agent.hasPath) return true;
        Vector3 directionTowardAlly = (allyToHelp.transform.position - transform.position);
        Vector3 tempDestination = transform.position + directionTowardAlly / 2;
        return agent.SetDestination(tempDestination);
        
    }

    private void SetAgentDestination()
    {
        agent.SetDestination(destination);
    }



    private void CheckHordeHelp()
    {
        //If it hasn't a horde it doesn't need help
        if (HasHorde() == false)
        {
            allyIsHelping = false;
            return;
        }

        //If it has an ally helping it doesn't need help
        if (allyIsHelping) return;
        foreach (var defender in Utils.SortByDistance(transform.position, defenders))
        {
            if (defender.GetComponent<DefenderFSM>().HasHorde()) continue;

            allyIsHelping = defender.GetComponent<DefenderFSM>().AskForHelp(gameObject);
            if (allyIsHelping) break;
        }
    }

    private bool ReleaseTarget()
    {
        target = null;
        return true;
    }

    private bool AskForHelp(GameObject toHelp)
    {
        if (allyToHelp != null || HasHorde()) return false;
        
        allyToHelp = toHelp;
        return true;
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

    private GameObject[] GetSurroundedAllies()
    {
        return dcc.IsAnyoneSurrounded();
    }

    private bool PickEnemyWhoIsSurroundingAlly()
    {
        Collider[] colls = Physics.OverlapSphere(allyToHelp.transform.position, surroundedRange);
        List<GameObject> enemies = new List<GameObject>();
        foreach (var coll in colls)
        {
            if (coll.gameObject.CompareTag("Attacker")) enemies.Add(coll.gameObject);
        }

        if (enemies.Count == 0) return false;
        target = enemies[0];
        return true;
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

    private void UpdateHordeStatus()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange);
        int count = 0;
        foreach (var enemy in enemies)
        {
            if (enemy.gameObject.CompareTag("Attacker")) count++;
        }

        hordeStatus = count > hordeLimit;
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

    


    private void NotUseThis()
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

    private bool AttackTarget()
    {
        HealthController hc;
        if (!target.TryGetComponent<HealthController>(out hc)) return false;
        Debug.Log(gameObject.name + " attacking" + target.name);
        hc.TakeDamage(attackDamage);
        Debug.DrawLine(transform.position, target.transform.position, Color.red, 0.5f);
        return true;
    }

    private void temp()
    {
        BTCondition hasATarget = new BTCondition(HasATarget);
        BTCondition isTargetInSight = new BTCondition(IsTargetInSight);
        BTAction attack = new BTAction(AttackTarget);
        BTCondition isTargetAlive = new BTCondition(IsTargetAlive);

        BTSequence attackSequence = new BTSequence(new IBTTask[] {hasATarget, isTargetInSight, attack, isTargetAlive});
    }

    private bool IsTargetAlive()
    {
        if (!target.TryGetComponent<HealthController>(out var hc)) return false;

        return hc.Health != 0;
    }

    private bool HasATarget()
    {
        return target != null;
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
