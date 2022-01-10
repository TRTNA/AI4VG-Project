using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthController))]
public class DefenderFSM : MonoBehaviour
{
    [Header("Attacking")]
    [Range(1, 100)]    public float attackRange = 10f;
    [Range(1, 100)]    public float attackDamage = 5f;
    [Header("Hordes")]
    [Range(1, 100)]    public int hordeLimit = 5;
    [Header("Surrounded")]
    [Range(1, 20)]     public float surroundedRange = 5f;
    [Range(1, 20)]     public int surroundedThreshold = 5;
    [Range(1, 20)]     public float fleeDistance = 3f;

    [Header("Movement parameters")] [Range(1, 10)]
    public float speed = 10f;
    [Range(1, 10)]     public float rotationSpeed = 5f;
    [Header("FSM")]
    [Range(0.1f, 10f)] public float reactionTime = 0.5f;

    private GameObject[] enemiesInRange;
    private GameObject[] surroundingEnemies;
    private GameObject[] defensivePositions;
    private GameObject[] defenders;

    private GameObject target;
    private GameObject allyToHelp;

    private Vector3 destination;

    private int fortNavMeshAreaMask;
    private FSM fsm;
    private NavMeshAgent agent;

    private bool hordeStatus;
    private bool isAnAllyHelping;
    private bool onDefensivePosition = true;

    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        fortNavMeshAreaMask = 1 << NavMesh.GetAreaFromName("Fort");
        defenders = GameObject.FindGameObjectsWithTag("Defender");
        defensivePositions = GameObject.FindGameObjectsWithTag("DefensivePosition");
        List<GameObject> temp = new List<GameObject>();
        foreach (var defender in defenders)
        {
            if (defender != gameObject) temp.Add(defender);
        }

        defenders = temp.ToArray();
        GetComponent<HealthController>().SetOnHealthDroppedToZero(OnKilled);

        //DTs
        #region Attacking (generic and inside fort) DT
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
        #region Attack after arriving on destination DT
        DTDecision hasArrivedToDestination = new DTDecision(ArrivedToDestination);
        //the rest is up to attackEnemyInsideFort subtree (root = hasATargetInsideFort)
        hasArrivedToDestination.AddLink(true, hasATargetInsideFort);

        DecisionTree helpWithHordeDt = new DecisionTree(hasArrivedToDestination); 
        #endregion

        //FSM actions
        FSMAction acquireTargetFSMAction = new FSMAction(() => PickAnEnemy(null));
        FSMAction resetAgentPath = () => agent.ResetPath();
        FSMAction releaseTarget = () => target = null;
        FSMAction setAllyToHelpAsDestination = () => destination = allyToHelp != null ? allyToHelp.transform.position : destination;
        FSMAction attackTarget = () => attackDt.walk();
        FSMAction helpWithHordeAction = () => helpWithHordeDt.walk();
        FSMAction resetAllyToHelp = () => allyToHelp = null;

        //FSM states
        FSMState attackFromWalls = new FSMState();
        attackFromWalls.stayActions = new List<FSMAction> { UpdateHordeStatus, acquireTargetFSMAction, attackTarget, SeekHelpFromAllies };
        attackFromWalls.exitActions = new List<FSMAction> { releaseTarget, resetAgentPath };

        FSMState returnToDefPos = new FSMState();
        returnToDefPos.enterActions = new List<FSMAction> { FindAnEmptyDefensivePosition, MoveToDestination };
        returnToDefPos.exitActions.Add(resetAgentPath);

        FSMState hordeHelp = new FSMState();
        hordeHelp.enterActions = new List<FSMAction>{ setAllyToHelpAsDestination, SetAgentDestination };
        hordeHelp.stayActions = new List<FSMAction>{ acquireTargetFSMAction, helpWithHordeAction };
        hordeHelp.exitActions = new List<FSMAction>{ releaseTarget, resetAllyToHelp, resetAgentPath };

        FSMState defendYard = new FSMState();
        defendYard.enterActions.Add(resetAgentPath);
        defendYard.stayActions = new List<FSMAction>() { UpdateSurroundingEnemies, () => attackInsideFortDt.walk() };
        defendYard.exitActions = new List<FSMAction>() { releaseTarget, resetAllyToHelp };

        FSMState flee = new FSMState();
        flee.enterActions.Add(() => agent.autoBraking = false);
        flee.stayActions.Add(UpdateSurroundingEnemies);
        flee.stayActions.Add(Flee);
        flee.exitActions.Add(() => agent.autoBraking = true);

        FSMState surroundedAllyHelp = new FSMState();
        surroundedAllyHelp.enterActions.Add(() => allyToHelp = Utils.GetNearestObject(transform.position, GetSurroundedAllies()));
        surroundedAllyHelp.stayActions = new List<FSMAction> { UpdateSurroundingEnemies, () => helpSurroundedAllyDt.walk() };
        surroundedAllyHelp.exitActions.Add(resetAllyToHelp);

        //FSM conditions
        FSMCondition isFortInvaded = new FSMCondition(() => !IsFortClear());
        FSMCondition isInsideDefensivePosition = () => !IsOutsideDefensivePosition();
        FSMCondition hasAnAllyToHelpAndHasNotAHorde = () => allyToHelp != null && HasHorde() == false;

        //FSM transitions
        FSMTransition canHelpWithHorde = new FSMTransition(hasAnAllyToHelpAndHasNotAHorde);
        FSMTransition hordeDestroyed = new FSMTransition(HasHordeBeenDestroyed);
        FSMTransition outsideDefPos = new FSMTransition(IsOutsideDefensivePosition);
        FSMTransition insideDefPos = new FSMTransition(isInsideDefensivePosition);

        FSMTransition canHelpSurroundedAlly = new FSMTransition(() => GetSurroundedAllies().Length != 0 && !AmISurrounded());
        FSMTransition allyToHelpIsClear = new FSMTransition(IsAllyToHelpClear);
        FSMTransition surrounded = new FSMTransition(AmISurrounded);
        FSMTransition clear = new FSMTransition(AmIClear);

        FSMTransition fortInvaded = new FSMTransition(isFortInvaded);
        FSMTransition fortClear = new FSMTransition(IsFortClear);


        attackFromWalls.AddTransition(canHelpWithHorde, hordeHelp);
        attackFromWalls.AddTransition(outsideDefPos, returnToDefPos);
        attackFromWalls.AddTransition(fortInvaded, defendYard);

        hordeHelp.AddTransition(hordeDestroyed, returnToDefPos);
        hordeHelp.AddTransition(fortInvaded, defendYard);

        returnToDefPos.AddTransition(insideDefPos, attackFromWalls);
        returnToDefPos.AddTransition(fortInvaded, defendYard);

        surroundedAllyHelp.AddTransition(surrounded, flee);
        surroundedAllyHelp.AddTransition(allyToHelpIsClear, defendYard);

        flee.AddTransition(clear, defendYard);

        defendYard.AddTransition(canHelpSurroundedAlly, surroundedAllyHelp);
        defendYard.AddTransition(surrounded, flee);
        defendYard.AddTransition(fortClear, returnToDefPos);

        fsm = new FSM(attackFromWalls);
        StartCoroutine(UpdateFsm());
    }

    public GameObject[] GetSurroundingEnemies()
    {
        return surroundingEnemies;
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
    public bool HasHorde() { return hordeStatus; }

    public bool AmISurrounded()
    {
        return surroundingEnemies != null && surroundingEnemies.Length >= surroundedThreshold;
    }

    public bool AmIClear()
    {
        //i'm both not surrounded and arrived at my flee destination
        return !AmISurrounded() && Vector3.Distance(transform.position, agent.destination) < agent.stoppingDistance + 0.01f;
    }

    #region Moving methods

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
        foreach (var defPos in Utils.SortByDistance(transform.position, defensivePositions))
        {
            bool reserved = defPos.GetComponent<DefensivePosition>().Reserve(gameObject);
            if (reserved)
            {
                destination = defPos.transform.position;
                return;
            }
        }
    }

    private void MoveToDestination()
    {
        if (!agent.hasPath)
        {
            agent.SetDestination(destination);
        }
    }
    private object ArrivedToDestination(object bundle)
    {
        return Vector3.Distance(transform.position, agent.destination) < agent.stoppingDistance + 0.01f;
    }

    #endregion

    #region Target selection methods
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

    #region Target status methods
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

    #region Horde methods
    private void UpdateHordeStatus()
    {
        hordeStatus = enemiesInRange != null && enemiesInRange.Length > hordeLimit;
    }


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

    #region Help surrounded allies methods
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
            if (ally != null && ally.GetComponent<DefenderFSM>().AmISurrounded()) surroundedAllies.Add(ally);
        }
        return surroundedAllies.ToArray();
    }
    private bool IsAllyToHelpClear()
    {
        return allyToHelp == null || !allyToHelp.GetComponent<DefenderFSM>().AmISurrounded();
    }
    #endregion

    #region Surrounded methods
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



    #endregion

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
    private void OnKilled()
    {
        gameObject.SetActive(false);
        Destroy(gameObject, 1f);
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
