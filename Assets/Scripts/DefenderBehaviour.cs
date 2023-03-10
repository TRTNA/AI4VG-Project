using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthController))]
public class DefenderBehaviour : MonoBehaviour
{
    [Header("FSM parameters")]
    [Range(0.1f, 10f)]
    public float reactionTime = 0.5f;

    [Header("Attack parameters")]
    [Range(1, 100)]
    public float attackRange = 10f;
    [Range(1, 100)]
    public float attackDamage = 5f;
    public GameObject projectile;

    [Header("Hordes parameters")]
    [Range(1, 100)]
    public int hordeThreshold = 5;

    [Header("Surrounded parameters")]
    [Range(1, 20)]
    public float surroundedRange = 5f;
    [Range(1, 20)]
    public int surroundedThreshold = 5;
    [Range(1, 20)]
    public float fleeDistance = 3f;

    [Header("Movement parameters")]
    [Range(1, 10)]
    public float speed = 10f;
    [Range(1, 10)]
    public float rotationSpeed = 5f;
    

    private GameObject[] enemiesInRange;
    private GameObject[] surroundingEnemies;
    private GameObject[] defensivePositions;
    private GameObject[] defenders;

    private Collider[] colliders = new Collider[100];
    private GameObject target;
    public GameObject allyToHelp;
    private Vector3 destination;

    private int attackerLayerMask;
    private int fortNavMeshAreaMask;
    private float onAreaSamplePositionOffset = 1f;
    private float fleeSamplePositionOffset = 3f;

    private SubMachineState fsm;

    public bool hordeStatus;
    private bool isAnAllyHelping;
    private bool onDefensivePosition = true;

    private HealthController hc;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = speed;
        fortNavMeshAreaMask = 1 << NavMesh.GetAreaFromName("Fort");
        attackerLayerMask = 1 << LayerMask.NameToLayer("Attacker");
        defenders = GameObject.FindGameObjectsWithTag("Defender");
        defensivePositions = GameObject.FindGameObjectsWithTag("DefensivePosition");
        hc = GetComponent<HealthController>();

        //exclude itself from the list of defenders
        List<GameObject> temp = new List<GameObject>();
        foreach (var defender in defenders)
        {
            if (defender != gameObject) temp.Add(defender);
        }
        defenders = temp.ToArray();

        hc.SetOnHealthDroppedToZero(OnKilled);

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
        hasATarget.AddLink(true, isTargetAlive);

        //to attack inside fort
        hasATargetInsideFort.AddLink(false, acquireTargetInsideFort);
        hasATargetInsideFort.AddLink(true, isTargetAlive);

        isTargetAlive.AddLink(false, releaseTargetDtAction);
        isTargetAlive.AddLink(true, isInRange);

        isInRange.AddLink(false, releaseTargetDtAction);
        isInRange.AddLink(true, isTargetInsSight);

        isTargetInsSight.AddLink(false, releaseTargetDtAction);
        isTargetInsSight.AddLink(true, attack);

        

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

        hasATargetSurroundingAlly.AddLink(true, isTargetAlive);
        hasATargetSurroundingAlly.AddLink(false, pickEnemySurroundingAlly);

        DecisionTree helpSurroundedAllyDt = new DecisionTree(hasAllyToHelpD);

        #endregion
        #region Attack after arriving on destination DT
        DTDecision hasArrivedToDestination = new DTDecision(ArrivedToDestination);
        //the rest is up to attackEnemyInsideFort subtree (root = hasATargetInsideFort)
        hasArrivedToDestination.AddLink(true, hasATarget);

        DecisionTree helpWithHordeDt = new DecisionTree(hasArrivedToDestination);
        #endregion

        //FSM
        #region FSM plain states
        //FSM actions
        FSMAction resetAgentPath = () => agent.ResetPath();
        FSMAction releaseTarget = () => target = null;
        FSMAction setAllyToHelpAsDestination = () => destination = allyToHelp != null ? allyToHelp.transform.position : destination;
        FSMAction attackTarget = () => attackDt.walk();
        FSMAction helpWithHordeAction = () => helpWithHordeDt.walk();
        FSMAction resetAllyToHelp = () => allyToHelp = null;

        //FSM states
        HFSMState attackFromWalls = new HFSMState();
        attackFromWalls.stayActions = new List<FSMAction> { attackTarget, SeekHelpFromAllies };
        attackFromWalls.enterActions.Add(() => Debug.Log(gameObject.name + "Entering attack from walls"));
        attackFromWalls.exitActions = new List<FSMAction> { releaseTarget, resetAgentPath, () => isAnAllyHelping = false };
        attackFromWalls.exitActions.Add(() => Debug.Log(gameObject.name + "exiting attack from walls"));


        HFSMState returnToDefPos = new HFSMState();
        returnToDefPos.enterActions = new List<FSMAction> { FindAnEmptyDefensivePosition, MoveToDestination };
        returnToDefPos.enterActions.Add(() => Debug.Log(gameObject.name + "entering returnToDefPos"));
        returnToDefPos.exitActions.Add(() => Debug.Log(gameObject.name + "exiting returnToDefPos"));

        HFSMState hordeHelp = new HFSMState();
        hordeHelp.enterActions = new List<FSMAction> { () => agent.stoppingDistance += 8f, setAllyToHelpAsDestination, MoveToDestination };
        hordeHelp.enterActions.Add(() => Debug.Log(gameObject.name + "entering hordeHelp"));
        hordeHelp.stayActions = new List<FSMAction> { helpWithHordeAction };
        hordeHelp.exitActions = new List<FSMAction> { () => agent.stoppingDistance -= 8f, releaseTarget, resetAllyToHelp, resetAgentPath };
        hordeHelp.exitActions.Add(() => Debug.Log(gameObject.name + "exiting hordeHelp"));


        HFSMState defendYard = new HFSMState();
        defendYard.enterActions.Add(() => Debug.Log(gameObject.name + "entering defendYard"));
        defendYard.stayActions = new List<FSMAction>() { () => attackInsideFortDt.walk() };
        defendYard.exitActions = new List<FSMAction>() { releaseTarget, resetAllyToHelp };
        defendYard.exitActions.Add(() => Debug.Log(gameObject.name + "exiting defendYard"));

        HFSMState flee = new HFSMState();
        flee.enterActions.Add(() => agent.autoBraking = false);
        flee.enterActions.Add(() => Debug.Log(gameObject.name + "entering flee"));
        flee.stayActions.Add(Flee);
        flee.exitActions.Add(() => agent.autoBraking = true);
        flee.exitActions.Add(() => Debug.Log(gameObject.name + "exiting flee"));


        HFSMState surroundedAllyHelp = new HFSMState();
        surroundedAllyHelp.enterActions.Add(() => Debug.Log(gameObject.name + "entering surroundedAllyHelp"));
        surroundedAllyHelp.enterActions.Add(() => allyToHelp = Utils.GetNearestObject(transform.position, GetSurroundedAllies()));
        surroundedAllyHelp.stayActions = new List<FSMAction> { () => helpSurroundedAllyDt.walk() };
        surroundedAllyHelp.stayActions.Add(() =>
        {
            if (agent.pathPending) Debug.Log(gameObject.name + " path is pending...");
        });
        surroundedAllyHelp.exitActions.Add(resetAllyToHelp);
        surroundedAllyHelp.exitActions.Add(() => Debug.Log(gameObject.name + "exiting surroundedAllyHelp"));
        #endregion

        #region FSM transitions
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
        #endregion

        #region FSM submachines and setup
        attackFromWalls.AddTransition(canHelpWithHorde, hordeHelp);
        attackFromWalls.AddTransition(outsideDefPos, returnToDefPos);

        hordeHelp.AddTransition(hordeDestroyed, returnToDefPos);

        returnToDefPos.AddTransition(insideDefPos, attackFromWalls);

        surroundedAllyHelp.AddTransition(allyToHelpIsClear, defendYard);

        defendYard.AddTransition(canHelpSurroundedAlly, surroundedAllyHelp);

        //SubMachines
        SubMachineState defendWallsSubMachineState = new SubMachineState(returnToDefPos);
        SubMachineState attackEnemiesInsideFortSubMachineState = new SubMachineState(defendYard);
        SubMachineState defendYardSubMachineState = new SubMachineState(attackEnemiesInsideFortSubMachineState);

        defendWallsSubMachineState.selfState.stayActions.Add(UpdateHordeStatus);

        defendYardSubMachineState.selfState.enterActions = new List<FSMAction> { FindAnEmptyDefensivePosition, MoveToDestination };
        defendYardSubMachineState.selfState.stayActions.Add(UpdateSurroundingEnemies);

        defendWallsSubMachineState.AddTransition(fortInvaded, defendYardSubMachineState);
        defendYardSubMachineState.AddTransition(fortClear, defendWallsSubMachineState);
        flee.AddTransition(clear, attackEnemiesInsideFortSubMachineState);
        attackEnemiesInsideFortSubMachineState.AddTransition(surrounded, flee);

        fsm = new SubMachineState(defendWallsSubMachineState);

        StartCoroutine(UpdateFsm()); 
        #endregion
    }

    #region Moving methods

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
        agent.SetDestination(destination);
        agent.isStopped = false;

    }
    private object ArrivedToDestination(object bundle)
    {
        return Vector3.Distance(transform.position, agent.destination) <= agent.stoppingDistance + 0.1f;
    }


    #endregion

    #region Target selection methods
    private GameObject[] ScanForEnemies()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, attackRange, colliders, attackerLayerMask);
        GameObject[] temp = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            temp[i] = colliders[i].gameObject;
        }
        return temp;
    }
    private object PickAnEnemy(object bundle)
    {
        if (enemiesInRange == null)
        {
            return false;
        }
        target = enemiesInRange.Length != 0 ? enemiesInRange[0] : null;
        return true;
    }

    private object PickAnEnemyInsideFort(object bundle)
    {
        if (enemiesInRange == null) { return false;}
        NavMeshHit hit;
        foreach (var enemy in Utils.SortByDistance(transform.position, enemiesInRange))
        {
            if (NavMesh.SamplePosition(enemy.transform.position, out hit, onAreaSamplePositionOffset, fortNavMeshAreaMask))
            {
                target = enemy.gameObject;
                return true;
            }
        }
        return false;
    }

    private bool PickEnemyWhoIsSurroundingAlly()
    {
        GameObject[] enemiesSurroundingAlly = allyToHelp.GetComponent<DefenderBehaviour>().GetSurroundingEnemies();
        if (enemiesSurroundingAlly == null || enemiesSurroundingAlly.Length == 0) return false;
        target = enemiesSurroundingAlly[0];
        return true;
    }

    #endregion

    #region Target status and attack methods
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
        //attacker prefab has center of gravity shifted on the y-axis.
        return ! Physics.Linecast(transform.position, target.transform.position, out hit, ~attackerLayerMask);
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
        hc.TakeDamage(attackDamage, gameObject);

        var proj = Instantiate(projectile, transform.position + transform.forward * 3f, transform.rotation);
        proj.GetComponent<Rigidbody>().AddForce((target.transform.position - transform.position).normalized * 5f);

        return true;
    }
    private object ReleaseTarget(object bundle)
    {
        target = null;
        return true;
    }

    #endregion

    #region Horde methods
    public bool HasHorde() { return hordeStatus; }

    private void UpdateHordeStatus()
    {
        hordeStatus = enemiesInRange != null && enemiesInRange.Length > hordeThreshold;
    }


    private bool HasHordeBeenDestroyed()
    {
        if (allyToHelp == null) return true;
        return !allyToHelp.GetComponent<DefenderBehaviour>().HasHorde();
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
            if (!defender.GetComponent<DefenderBehaviour>().HasHorde())
            {
                isAnAllyHelping = defender.GetComponent<DefenderBehaviour>().CanHelpAllyWithHorde(gameObject);
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
        Vector3 directionTowardAlly = (allyToHelp.transform.position - transform.position);
        Vector3 tempDestination = transform.position + directionTowardAlly / 2;
        return agent.SetDestination(tempDestination);
    }
    private GameObject[] GetSurroundedAllies()
    {
        var surroundedAllies = new List<GameObject>();
        foreach (var ally in defenders)
        {
            if (ally != null && ally.GetComponent<DefenderBehaviour>().AmISurrounded()) surroundedAllies.Add(ally);
        }
        return surroundedAllies.ToArray();
    }
    private bool IsAllyToHelpClear()
    {
        return allyToHelp == null || !allyToHelp.GetComponent<DefenderBehaviour>().AmISurrounded();
    }
    #endregion

    #region Surrounded methods
    private void Flee()
    {
        if (surroundingEnemies == null || surroundingEnemies.Length == 0) return;

        Vector3 fleeDirection = GetAverageEnemiesDirection(surroundingEnemies);
        NavMeshHit hit;
        float[] rotationValues = { 0, -10, 10, -30,  30, -90, 90, 180 };
        foreach (var rotationValue in rotationValues)
        {
            Vector3 fleeDestination = transform.position + Quaternion.Euler(0, rotationValue, 0) * fleeDirection * fleeDistance;
            if (NavMesh.SamplePosition(fleeDestination, out hit, fleeSamplePositionOffset, fortNavMeshAreaMask))
            {
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
            if (enemy != null)
            {
                Vector3 dir = Vector3.ProjectOnPlane(transform.position - enemy.transform.position, Vector3.up);
                averageEnemiesDirection += dir;
            }
            
        }
        return averageEnemiesDirection.normalized;
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

    public bool AmISurrounded()
    {
        return surroundingEnemies != null && surroundingEnemies.Length >= surroundedThreshold;
    }

    public bool AmIClear()
    {
        //i'm both not surrounded and arrived at my flee destination
        return !AmISurrounded() && Vector3.Distance(transform.position, agent.destination) < agent.stoppingDistance + 0.01f;
    }

    #endregion

    private bool IsFortClear()
    {
        GameObject[] attackers = GameObject.FindGameObjectsWithTag("Attacker");
        NavMeshHit hit;
        foreach (var attacker in attackers)
        {
            if (NavMesh.SamplePosition(attacker.transform.position, out hit, onAreaSamplePositionOffset, fortNavMeshAreaMask))
            {
                return false;
            }
        }
        return true;
    }
    private void OnKilled()
    {
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
            if (!dp.IsOccupied() || dp.OccupiedBy() == gameObject)
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

    public void OnValidate()
    {
        if (surroundedRange > attackRange)
        {
            surroundedRange = attackRange;
        }
    }
}
