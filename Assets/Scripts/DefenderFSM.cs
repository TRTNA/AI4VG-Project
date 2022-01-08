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


    private GameObject[] enemiesInRange;

    private GameObject[] surroundingEnemies;




    private int fortNavMeshAreaMask;
    private FSM fsm;
    private NavMeshAgent agent;
    private DefendersCooperationController dcc;

    private GameObject[] defenders;
    private bool hordeStatus;
    private bool isAnAllyHelping;
    private GameObject target;
    private Vector3 destination;
    private bool onDefensivePosition = true;
    private GameObject allyToHelp;


    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        dcc = GameObject.Find("DefendersCooperationController").GetComponent<DefendersCooperationController>();
        fortNavMeshAreaMask = 1 << NavMesh.GetAreaFromName("Fort");
        defenders = GameObject.FindGameObjectsWithTag("Defender");
        List<GameObject> temp = new List<GameObject>();
        foreach (var defender in defenders)
        {
            if (defender != gameObject) temp.Add(defender);
        }

        defenders = temp.ToArray();

        GetComponent<HealthController>().SetOnHealthDroppedToZero(OnKilled);

        #region DT for attacking (generic and inside fort)
        DTDecision hasATarget = new DTDecision(HasATarget);
        DTDecision hasATargetInsideFort = new DTDecision(HasATarget);
        DTAction acquireTarget = new DTAction(PickAnEnemy);
        DTAction acquireTargetInsideFort = new DTAction(PickAnEnemyInsideFort);
        DTDecision isInRange = new DTDecision(TargetIsInRange);
        DTAction releaseTargetDtAction = new DTAction(ReleaseTarget);
        DTDecision isTargetInsSight = new DTDecision(IsTargetInSight);
        DTDecision isTargetAlive = new DTDecision(IsTargetAlive);
        DTAction attack = new DTAction(AttackTarget);

        //to attack outside fort
        hasATarget.AddLink(false, acquireTarget);
        hasATarget.AddLink(true, isInRange);
        //to attack inside fort
        hasATargetInsideFort.AddLink(true, isInRange);
        hasATargetInsideFort.AddLink(false, acquireTargetInsideFort);

        isInRange.AddLink(false, releaseTargetDtAction);
        isInRange.AddLink(true, isTargetInsSight);

        isTargetInsSight.AddLink(false, releaseTargetDtAction);
        isTargetInsSight.AddLink(true, isTargetAlive);

        isTargetAlive.AddLink(false, releaseTargetDtAction);
        isTargetAlive.AddLink(true, attack);

        DecisionTree attackDt = new DecisionTree(hasATarget);
        DecisionTree attackInsideFortDt = new DecisionTree(hasATargetInsideFort);
        #endregion


        #region Defend fort walls states

        FSMAction acquireTargetFSMAction = new FSMAction(() => PickAnEnemy(null));
        FSMAction resetAgentPath = () => agent.ResetPath();
        FSMAction releaseTarget = () => target = null;
        FSMAction setAllyToHelpAsDestination = () => destination = allyToHelp != null ? allyToHelp.transform.position : destination;
        FSMAction attackTarget = () => attackDt.walk();

        FSMState attackEnemiesOutsideFortState = new FSMState();
        attackEnemiesOutsideFortState.stayActions = new List<FSMAction> { UpdateHordeStatus, acquireTargetFSMAction, attackTarget, SeekHelpFromAllies };
        attackEnemiesOutsideFortState.exitActions = new List<FSMAction> { releaseTarget, resetAgentPath };

        FSMState goToDefensivePositionState = new FSMState();
        goToDefensivePositionState.enterActions = new List<FSMAction> { FindAnEmptyDefensivePosition, MoveToDestination };
        goToDefensivePositionState.exitActions.Add(resetAgentPath);


        FSMState helpAllyState = new FSMState();

        DTDecision hasArrivedToDestination = new DTDecision(HasArrivedToDestination);
        //the rest is up to attackEnemyInsideFort subtree (root = hasATargetInsideFort)
        hasArrivedToDestination.AddLink(true, hasATargetInsideFort);

        DecisionTree helpWithHordeDt = new DecisionTree(hasArrivedToDestination);

        FSMAction helpWithHordeAction = () => helpWithHordeDt.walk();
        FSMAction resetAllyToHelp = () => allyToHelp = null;

        helpAllyState.enterActions = new List<FSMAction>(2) { setAllyToHelpAsDestination, SetAgentDestination };
        helpAllyState.stayActions = new List<FSMAction>(2) { acquireTargetFSMAction, helpWithHordeAction };
        helpAllyState.exitActions = new List<FSMAction>() { releaseTarget, resetAllyToHelp, resetAgentPath };


        FSMCondition isInsideDefensivePosition = () => !IsOutsideDefensivePosition();
        FSMCondition hasAnAllyToHelpAndHasNotAHorde = () => allyToHelp != null && HasHorde() == false;

        FSMTransition defendToHelpWithHorde = new FSMTransition(hasAnAllyToHelpAndHasNotAHorde);
        attackEnemiesOutsideFortState.AddTransition(defendToHelpWithHorde, helpAllyState);

        FSMTransition helpWithHordeToDefend = new FSMTransition(HasHordeBeenDestroyed);
        helpAllyState.AddTransition(helpWithHordeToDefend, attackEnemiesOutsideFortState);

        FSMTransition defendWallsToReenterDefPos = new FSMTransition(IsOutsideDefensivePosition);
        attackEnemiesOutsideFortState.AddTransition(defendWallsToReenterDefPos, goToDefensivePositionState);

        FSMTransition reenterDefPosToDefendWalls = new FSMTransition(isInsideDefensivePosition);
        goToDefensivePositionState.AddTransition(reenterDefPosToDefendWalls, attackEnemiesOutsideFortState);

        #endregion

        #region Defend fort yard states
        FSMState attackEnemiesInsideFortState = new FSMState();
        attackEnemiesInsideFortState.enterActions.Add(resetAgentPath);
        attackEnemiesInsideFortState.stayActions = new List<FSMAction>() { UpdateSurroundingEnemies, () => attackInsideFortDt.walk() };
        attackEnemiesInsideFortState.exitActions = new List<FSMAction>() { releaseTarget, resetAllyToHelp };

        FSMState fleeState = new FSMState();
        fleeState.enterActions.Add(() => agent.autoBraking = false);
        fleeState.stayActions.Add(UpdateSurroundingEnemies);
        fleeState.stayActions.Add(Flee);
        fleeState.exitActions.Add(() => agent.autoBraking = true);


        #region Help surrounded ally DT
        DTDecision hasAllyToHelpD = new DTDecision((b) => allyToHelp != null);
        DTDecision isAllyToHelpTooFarD = new DTDecision((b) =>
            Vector3.Distance(transform.position, allyToHelp.transform.position) > attackRange);
        DTAction getCloseToAllyD = new DTAction((b) => GetCloserToAlly());
        DTDecision hasATargetSurroundingAlly = new DTDecision(HasATarget);
        DTAction pickEnemySurroundingAlly = new DTAction((b) => PickEnemyWhoIsSurroundingAlly());

        hasAllyToHelpD.AddLink(true, isAllyToHelpTooFarD);

        isAllyToHelpTooFarD.AddLink(true, getCloseToAllyD);
        isAllyToHelpTooFarD.AddLink(false, hasATargetSurroundingAlly);

        hasATargetSurroundingAlly.AddLink(true, isInRange);
        hasATargetSurroundingAlly.AddLink(false, pickEnemySurroundingAlly);

        DecisionTree helpSurroundedAllyDt = new DecisionTree(hasAllyToHelpD);

        #endregion
        FSMState helpSurroundedAllyState = new FSMState();
        helpSurroundedAllyState.enterActions.Add(() => allyToHelp = Utils.GetNearestObject(transform.position, GetSurroundedAllies()));
        helpSurroundedAllyState.stayActions = new List<FSMAction> { UpdateSurroundingEnemies, () => helpSurroundedAllyDt.walk() };
        helpSurroundedAllyState.exitActions.Add(resetAllyToHelp);

        FSMTransition defendToHelp = new FSMTransition(() => GetSurroundedAllies().Length != 0 && !IsSurrounded());
        attackEnemiesInsideFortState.AddTransition(defendToHelp, helpSurroundedAllyState);
        FSMTransition helpToFlee = new FSMTransition(IsSurrounded);
        helpSurroundedAllyState.AddTransition(helpToFlee, fleeState);
        FSMTransition helpToDefend = new FSMTransition(IsAllyToHelpStillSurrounded);
        helpSurroundedAllyState.AddTransition(helpToDefend, attackEnemiesInsideFortState);

        FSMTransition fromDefendToFlee = new FSMTransition(IsSurrounded);
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

        StartCoroutine(UpdateFsm());

    }

    private bool IsAllyToHelpStillSurrounded()
    {
        return allyToHelp == null || ! allyToHelp.GetComponent<DefenderFSM>().IsSurrounded();
    }

    private object HasArrivedToDestination(object bundle)
    {
        return Vector3.Distance(transform.position, agent.destination) < agent.stoppingDistance + 0.01f;
    }


    public GameObject[] GetEnemiesInRange()
    {
        return enemiesInRange;
    }

    public GameObject[] GetSurroundingEnemies()
    {
        return surroundingEnemies;
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

    private object PickAnEnemy(object bundle)
    {
        if (enemiesInRange == null)
        {
            Debug.LogWarning("Enemies scanning must be run BEFORE picking an enemy");
            return false;
        }
        target = enemiesInRange.Length != 0 ? enemiesInRange[0] : null;
        return true;
    }

    private object PickAnEnemyInsideFort(object bundle)
    {
        if (enemiesInRange == null)
        {
            Debug.LogWarning("Enemies scanning must be run BEFORE picking an enemy");
            return false;
        }
        NavMeshHit hit;
        foreach (var enemy in enemiesInRange)
        {
            if (NavMesh.SamplePosition(enemy.transform.position, out hit, 0.001f, fortNavMeshAreaMask))
            {
                target = enemy.gameObject;
                return true;
            }
        }
        return false;
    }

    private bool PickEnemyWhoIsSurroundingAlly()
    {
        GameObject[] enemiesSurroundingAlly = allyToHelp.GetComponent<DefenderFSM>().GetSurroundingEnemies();
        if (enemiesSurroundingAlly == null || enemiesSurroundingAlly.Length == 0) return false;
        target = enemiesSurroundingAlly[0];
        return true;
    }

    #endregion

    #region Target status method

    private object HasATarget(object bundle)
    {
        return target != null;
    }

    private object TargetIsInRange(object bundle)
    {
        if (target == null) return false;
        return Vector3.Distance(target.transform.position, transform.position) < attackRange;
    }

    private object IsTargetInSight(object bundle)
    {
        if (target == null) return false;
        RaycastHit hit;
        return Physics.Linecast(transform.position, target.transform.position, out hit) && hit.collider.gameObject.CompareTag("Attacker");
    }

    private object IsTargetAlive(object bundle)
    {
        if (target == null) return true;
        return target.TryGetComponent<HealthController>(out var hc) && hc.Health != 0;
    }

    private object AttackTarget(object bundle)
    {
        HealthController hc;
        if (target == null) return true;
        if (!target.TryGetComponent<HealthController>(out hc)) return false;
        hc.TakeDamage(attackDamage);
        Debug.DrawLine(transform.position, target.transform.position, Color.red, 0.5f);
        return true;
    }

    private object ReleaseTarget(object bundle)
    {
        target = null;
        return true;
    }

    #endregion


    #region Horde method

    private void UpdateHordeStatus()
    {
        hordeStatus = enemiesInRange != null && enemiesInRange.Length > hordeLimit;
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
            isAnAllyHelping = false;
            return;
        }

        //If it has an ally helping it doesn't need help
        if (isAnAllyHelping) return;

        foreach (var defender in Utils.SortByDistance(transform.position, defenders))
        {
            if (!defender.GetComponent<DefenderFSM>().HasHorde())
            {
                isAnAllyHelping = defender.GetComponent<DefenderFSM>().CanHelpAllyWithHorde(gameObject);
                if (isAnAllyHelping) break;
            }
        }
    }


    private bool CanHelpAllyWithHorde(GameObject ally)
    {
        //if i'm already helping or if i've an horde i can't help ally
        if (allyToHelp != null || HasHorde()) return false;

        allyToHelp = ally;
        return true;
    }

    #endregion

    #region Help surrounded allies method

    private bool GetCloserToAlly()
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
            if (ally != null && ally.GetComponent<DefenderFSM>().IsSurrounded()) surroundedAllies.Add(ally);
        }
        return surroundedAllies.ToArray();
    }

    #endregion

    #region Surrounded method



    private void Flee()
    {
        if (surroundingEnemies == null || surroundingEnemies.Length == 0) return;

        Vector3 fleeDirection = (-1 * GetAverageEnemiesDirection(surroundingEnemies)).normalized;
        NavMeshHit hit;
        float[] rotationValues = { 0, -10, 30, -90, 90, 180 };
        foreach (var rotationValue in rotationValues)
        {
            Vector3 dir = transform.position + Quaternion.Euler(0, rotationValue, 0) * fleeDirection * fleeDistance;
            if (NavMesh.SamplePosition(dir, out hit, 5f, fortNavMeshAreaMask))
            {
                Debug.DrawLine(transform.position, hit.position, Color.green, 5f);
                agent.SetDestination(hit.position);
                return;
            }
        }
        agent.SetDestination(Vector3.zero);

    }

    private Vector3 GetAverageEnemiesDirection(GameObject[] enemies)
    {
        Vector3 averageEnemiesDirection = new Vector3();
        foreach (var enemy in enemies)
        {
            Vector3 dir = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, Vector3.up);
            averageEnemiesDirection += dir;
        }

        averageEnemiesDirection /= enemies.Length;
        return averageEnemiesDirection;
    }

    public bool IsSurrounded()
    {
        return surroundingEnemies != null && surroundingEnemies.Length >= surroundedThreshold;
    }

    public void UpdateSurroundingEnemies()
    {
        List<GameObject> temp = new List<GameObject>();
        foreach (var enemy in enemiesInRange)
        {
            if (enemy != null &&
                Vector3.Distance(transform.position, enemy.transform.position) < surroundedRange) temp.Add(enemy);
        }
        surroundingEnemies = temp.ToArray();
    }

    public bool AmIClear()
    {
        //i'm either not surrounded and arrived at my flee destination
        return !IsSurrounded() && Vector3.Distance(transform.position, agent.destination) < agent.stoppingDistance + 0.01f;
    }

    #endregion

    private GameObject[] ScanForEnemies()
    {
        List<GameObject> temp = new List<GameObject>();
        Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange);

        foreach (var enemy in enemies)
        {
            if (enemy.gameObject.CompareTag("Attacker")) temp.Add(enemy.gameObject);
        }

        return temp.ToArray();
    }


    private bool IsFortClear()
    {
        GameObject[] attackers = GameObject.FindGameObjectsWithTag("Attacker");
        NavMeshHit hit;
        foreach (var attacker in attackers)
        {
            if (NavMesh.SamplePosition(attacker.transform.position, out hit, 0.01f, fortNavMeshAreaMask))
            {
                return false;
            }
        }
        return true;
    }

    IEnumerator UpdateFsm()
    {
        while (true)
        {
            yield return new WaitForSeconds(reactionTime);

            //Helpful for all states
            enemiesInRange = ScanForEnemies();

            fsm.Update();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DefensivePosition"))
        {
            var dp = other.gameObject.GetComponent<DefensivePosition>();
            if (!dp.IsOccupied())
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
