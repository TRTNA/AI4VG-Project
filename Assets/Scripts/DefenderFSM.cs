using CRBT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
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

    

    public GameObject[] enemiesSurrounding;




    private int navMeshAreaMask;
    private FSM fsm;
    private NavMeshAgent agent;
    private DefendersCooperationController dcc;

    private GameObject[] defenders;
    private bool hordeStatus;
    private bool allyIsHelping;
    private GameObject target;
    private Vector3 destination;
    private bool onDefensivePosition = true;
    private GameObject allyToHelp;

    // Start is called before the first frame update
    void Start()
    {
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

        #region DT for attacking (generic and inside fort)
        DTDecision hasATarget = new DTDecision((b) => HasATarget());
        DTDecision hasATargetInsideFort = new DTDecision((b) => HasATarget());
        DTAction acquireTarget = new DTAction((b) =>
        {
            PickAnEnemy();
            return null;
        });
        DTAction acquireTargetInsideFort = new DTAction((b) =>
        {
            PickAnEnemyInsideFort();
            return null;
        });
        DTDecision isInRange = new DTDecision((b) => TargetIsInRange());
        DTAction releaseTargetDTAction = new DTAction((b) => ReleaseTarget());
        DTDecision isTargetInsSight = new DTDecision((b) => IsTargetInSight());
        DTDecision isTargetAlive = new DTDecision((b) => IsTargetAlive());
        DTAction attack = new DTAction((b) => AttackTarget());

        //to attack outside fort
        hasATarget.AddLink(false, acquireTarget);
        hasATarget.AddLink(true, isInRange);
        //to attack inside fort
        hasATargetInsideFort.AddLink(true, isInRange);
        hasATargetInsideFort.AddLink(false, acquireTargetInsideFort);

        isInRange.AddLink(false, releaseTargetDTAction);
        isInRange.AddLink(true, isTargetInsSight);

        isTargetInsSight.AddLink(false, releaseTargetDTAction);
        isTargetInsSight.AddLink(true, isTargetAlive);

        isTargetAlive.AddLink(false, releaseTargetDTAction);
        isTargetAlive.AddLink(true, attack);

        DecisionTree attackDT = new DecisionTree(hasATarget);
        DecisionTree attackInsideFortDT = new DecisionTree(hasATargetInsideFort);
        #endregion


        #region Defend fort walls states
        FSMAction resetAgentPath = () => agent.ResetPath();
        FSMAction releaseTarget = () => target = null;
        FSMAction setAllyToHelpAsDestination = () => destination = allyToHelp != null ? allyToHelp.transform.position : destination;
        FSMAction attackTarget = () => attackDT.walk();

        FSMState attackEnemiesOutsideFortState = new FSMState();
        attackEnemiesOutsideFortState.stayActions = new List<FSMAction> { PickAnEnemy, attackTarget, UpdateHordeStatus, SeekHelpFromAllies };
        attackEnemiesOutsideFortState.exitActions = new List<FSMAction> { releaseTarget, resetAgentPath };

        FSMState goToDefensivePositionState = new FSMState();
        goToDefensivePositionState.enterActions = new List<FSMAction> { FindAnEmptyDefensivePosition, MoveToDestination };
        goToDefensivePositionState.exitActions.Add(resetAgentPath);


        FSMState helpAllyState = new FSMState();

        DTDecision hasArrivedD = new DTDecision((b) => Vector3.Distance(transform.position, agent.destination) < agent.stoppingDistance + 0.01f);
        hasArrivedD.AddLink(true, hasATargetInsideFort);
        DecisionTree helpWithHordeDT = new DecisionTree(hasArrivedD);
        FSMAction helpWithHordeAction = () => helpWithHordeDT.walk();
        FSMAction resetAllyToHelp = () => allyToHelp = null;

        helpAllyState.enterActions = new List<FSMAction>(2) { setAllyToHelpAsDestination, SetAgentDestination };
        helpAllyState.stayActions = new List<FSMAction>(2) { PickAnEnemy, helpWithHordeAction };
        helpAllyState.exitActions = new List<FSMAction>() { releaseTarget, resetAllyToHelp, resetAgentPath };


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

        #endregion

        #region Defend fort yard states
        FSMState attackEnemiesInsideFortState = new FSMState();
        attackEnemiesInsideFortState.enterActions.Add(resetAgentPath);
        attackEnemiesInsideFortState.stayActions.Add(() => attackInsideFortDT.walk());
        attackEnemiesInsideFortState.exitActions = new List<FSMAction>() { releaseTarget, resetAllyToHelp };

        FSMState fleeState = new FSMState();
        fleeState.enterActions.Add(() => agent.autoBraking = false);
        fleeState.stayActions.Add(Flee);
        fleeState.exitActions.Add(() => agent.autoBraking = true);


        #region Help surrounded ally DT
        DTDecision hasAllyToHelpD = new DTDecision((b) => allyToHelp != null);
        DTDecision isAllyToHelpTooFarD = new DTDecision((b) =>
            Vector3.Distance(transform.position, allyToHelp.transform.position) > attackRange);
        DTAction getCloseToAllyD = new DTAction((b) => GetCloseToAlly());
        DTDecision hasATargetSurroundingAlly = new DTDecision((b) => HasATarget());
        DTAction pickEnemySurroundingAlly = new DTAction((b) => PickEnemyWhoIsSurroundingAlly());

        hasAllyToHelpD.AddLink(true, isAllyToHelpTooFarD);

        isAllyToHelpTooFarD.AddLink(true, getCloseToAllyD);
        isAllyToHelpTooFarD.AddLink(false, hasATargetSurroundingAlly);

        hasATargetSurroundingAlly.AddLink(true, isInRange);
        hasATargetSurroundingAlly.AddLink(false, pickEnemySurroundingAlly);

        DecisionTree helpSurroundedAllyDT = new DecisionTree(hasAllyToHelpD);

        #endregion
        FSMState helpSurroundedAllyState = new FSMState();
        helpSurroundedAllyState.enterActions.Add(() => allyToHelp = Utils.GetNearestObject(transform.position, GetSurroundedAllies()));
        helpSurroundedAllyState.stayActions.Add(() => helpSurroundedAllyDT.walk());
        helpSurroundedAllyState.exitActions.Add(resetAllyToHelp);

        FSMTransition defendToHelp = new FSMTransition(() => dcc.IsAnyoneSurrounded().Length != 0 && !AmISurrounded());
        attackEnemiesInsideFortState.AddTransition(defendToHelp, helpSurroundedAllyState);
        FSMTransition helpToFlee = new FSMTransition(AmISurrounded);
        helpSurroundedAllyState.AddTransition(helpToFlee, fleeState);
        FSMTransition helpToDefend = new FSMTransition(() => !dcc.IsStillSurrounded(allyToHelp));
        helpSurroundedAllyState.AddTransition(helpToDefend, attackEnemiesInsideFortState);

        FSMTransition fromDefendToFlee = new FSMTransition(AmISurrounded);
        attackEnemiesInsideFortState.AddTransition(fromDefendToFlee, fleeState);

        FSMTransition fromFleeToDefend = new FSMTransition(AmIClear);
        fleeState.AddTransition(fromFleeToDefend, attackEnemiesInsideFortState); 
        #endregion

        FSMCondition isFortInvaded = new FSMCondition(() => !IsFortClear());

        FSMTransition wallsToYard = new FSMTransition(isFortInvaded);
        FSMTransition yardToWalls = new FSMTransition(IsFortClear);

        attackEnemiesOutsideFortState.AddTransition(wallsToYard, attackEnemiesInsideFortState);
        attackEnemiesInsideFortState.AddTransition(yardToWalls, attackEnemiesOutsideFortState);

        FSMTransition helpWithHordeToDefendYard = new FSMTransition(isFortInvaded);
        helpAllyState.AddTransition(helpWithHordeToDefendYard, attackEnemiesInsideFortState);

        FSMTransition reenterDefPositionToDefendYard = new FSMTransition(isFortInvaded);
        goToDefensivePositionState.AddTransition(reenterDefPositionToDefendYard, attackEnemiesInsideFortState);

        fsm = new FSM(attackEnemiesOutsideFortState);

        StartCoroutine(UpdateFSM());

    }




    #region Moving method

    private void SetAgentDestination()
    {
        agent.SetDestination(destination);
    }

    private bool IsOutsideDefensivePosition()
    {
        return !onDefensivePosition;
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
        if (!agent.hasPath)
        {
            agent.SetDestination(destination);
        }
    }

    #endregion

    #region Target selection method

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

    #endregion

    #region Target status method

    private bool HasATarget()
    {
        return target != null;
    }

    private bool TargetIsInRange()
    {
        if (target != null) return Vector3.Distance(target.transform.position, transform.position) < attackRange;
        return false;
    }

    private bool IsTargetInSight()
    {
        if (target == null) return false;
        RaycastHit hit;
        return Physics.Linecast(transform.position, target.transform.position, out hit) && hit.collider.gameObject.CompareTag("Attacker");
    }

    private bool IsTargetAlive()
    {
        if (target == null) return true;
        return target.TryGetComponent<HealthController>(out var hc) && hc.Health != 0;
    }

    private bool AttackTarget()
    {
        HealthController hc;
        if (target == null) return true;
        if (!target.TryGetComponent<HealthController>(out hc)) return false;
        Debug.Log(gameObject.name + " attacking" + target.name);
        hc.TakeDamage(attackDamage);
        Debug.DrawLine(transform.position, target.transform.position, Color.red, 0.5f);
        return true;
    }

    private bool ReleaseTarget()
    {
        target = null;
        return true;
    }

    #endregion


    #region Horde method

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

    private bool HasHordeBeenDestroyed()
    {
        return !allyToHelp.GetComponent<DefenderFSM>().HasHorde();
    }


    private void SeekHelpFromAllies()
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

            allyIsHelping = defender.GetComponent<DefenderFSM>().CanHelpAllyWithHorde(gameObject);
            if (allyIsHelping) break;
        }
    }


    private bool CanHelpAllyWithHorde(GameObject ally)
    {
        //if i'm already helping or i've an horde i can't help ally
        if (allyToHelp != null || HasHorde()) return false;

        allyToHelp = ally;
        return true;
    }

    #endregion

    #region Help surrounded allies method

    private bool GetCloseToAlly()
    {
        if (agent.hasPath) return true;
        Vector3 directionTowardAlly = (allyToHelp.transform.position - transform.position);
        Vector3 tempDestination = transform.position + directionTowardAlly / 2;
        return agent.SetDestination(tempDestination);

    }


    private GameObject[] GetSurroundedAllies()
    {
        var surroundedAllies = new List<GameObject>();
        foreach (var ally in defenders)
        {
            if (ally != null && ally.GetComponent<DefenderFSM>().AmISurrounded()) surroundedAllies.Add(ally);
        }
        return surroundedAllies.ToArray();
    }

    #endregion

    #region Surrounded method

    //TODO refactor this
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

        if (NavMesh.SamplePosition(fleeDestination, out hit, 5f, navMeshAreaMask))
        {
            Debug.DrawLine(transform.position, hit.position, Color.green, 5f);
            agent.SetDestination(hit.position);
            return;
        }

        Vector3 left = transform.position + Quaternion.Euler(0, -90, 0) * fleeDirection * fleeDistance;

        if (NavMesh.SamplePosition(left, out hit, 5f, navMeshAreaMask))
        {
            Debug.DrawLine(transform.position, hit.position, Color.green, 5f);

            agent.SetDestination(hit.position);
            return;
        }

        Vector3 right = transform.position + Quaternion.Euler(0, 90, 0) * fleeDirection * fleeDistance;

        if (NavMesh.SamplePosition(right, out hit, 5f, navMeshAreaMask))
        {
            Debug.DrawLine(transform.position, hit.position, Color.green, 5f);

            agent.SetDestination(hit.position);
            return;
        }

        Vector3 forward = transform.position + -fleeDirection * fleeDistance;

        if (NavMesh.SamplePosition(forward, out hit, 5f, navMeshAreaMask))
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
            if (!coll.gameObject.CompareTag("Attacker")) continue;
            enemies.Add(coll.gameObject);
            count++;
        }
        enemiesSurrounding = enemies.ToArray();
        return count >= surroundedThreshold;
    }

    public bool AmIClear()
    {
        return !AmISurrounded() && Vector3.Distance(transform.position, agent.destination) < agent.stoppingDistance + 0.01f;
    }

    #endregion




    private bool IsFortClear()
    {
        GameObject[] attackers = GameObject.FindGameObjectsWithTag("Attacker");
        NavMeshHit hit;
        foreach (var attacker in attackers)
        {
            if (NavMesh.SamplePosition(attacker.transform.position, out hit, 0.01f, 1 << NavMesh.GetAreaFromName("Fort")))
            {
                return false;
            }
        }
        return true;
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


    private void OnKilled()
    {
        gameObject.SetActive(false);
        Destroy(gameObject, 1f);
    }

    private void FixedUpdate()
    {
        if (target != null)
        {
            var targetRotation = Quaternion.LookRotation(target.transform.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        
    }


    public void OnDrawGizmosSelected()
    {
        Color lowAlphaRed = Color.red;
        lowAlphaRed.a = 0.1f;
        Gizmos.color = lowAlphaRed;
        Gizmos.DrawSphere(transform.position, attackRange);
        if (target != null) Gizmos.DrawLine(transform.position, target.transform.position);
    }


}
