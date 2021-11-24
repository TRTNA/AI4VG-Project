using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ITarget
{
    void TakeDamage(float damage);
    float GetHealth();
}
