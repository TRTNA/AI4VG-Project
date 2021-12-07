using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackingBehaviour : MonoBehaviour, IObserver
{
    private GameObject target;

    public float attackCooldown = 2f;
    public float damagesPerAttack = 10f;

    private IEnumerator coroutine;

    public void AttackTarget(GameObject target)
    {
        StopAttackingTarget();
        this.target = target;
        coroutine = AttackTarget();
        StartCoroutine(coroutine);

    }

    public bool IsAttacking()
    {
        return coroutine != null;
    }

    public void StopAttackingTarget()
    {
        if (coroutine != null) StopCoroutine(coroutine);
        if (target != null) target.GetComponent<HealthController>().RemoveObserver(this);
        target = null;
        coroutine = null;
    }

    public float TargetHealth()
    {
        if (target != null)
        {
            Debug.Log("Requesting health of " + target.name);

            return target.GetComponent<HealthController>().Health;
        }
        return -1f;
    }

    public void OnNotify(GameObject subject, object status)
    {
        if (status.GetType() == typeof(int) && ((int)status).Equals(0))
        {
            Debug.Log("Stopping coroutine");
            StopCoroutine(coroutine);
            coroutine = null;
            target = null;
        }
    }

    IEnumerator AttackTarget()
    {
        target.GetComponent<HealthController>().AddObserver(this);
        while (true)
        {
            target.SendMessage("TakeDamage", damagesPerAttack);
            yield return new WaitForSeconds(attackCooldown);
        }
    }
}
