using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IObserver
{
    void OnNotify(GameObject subject, object status);
}

public interface ISubject
{
    void Notify();
    void AddObserver(IObserver o);
    void RemoveObserver(IObserver o);
}

