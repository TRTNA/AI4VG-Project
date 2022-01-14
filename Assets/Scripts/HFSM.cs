using System.Collections.Generic;

public interface IFSMState
{
    //Acts like a state
    void AddTransition(FSMTransition transition, IFSMState target);
    FSMTransition VerifyTransitions();
    IFSMState NextState(FSMTransition t);
    void Enter();
    void Stay();
    void Exit();

    //Acts like an FSM
    void Update();
}

public class HFSMState : IFSMState
{
    public List<FSMAction> enterActions = new List<FSMAction>();
    public List<FSMAction> stayActions = new List<FSMAction>();
    public List<FSMAction> exitActions = new List<FSMAction>();

    private Dictionary<FSMTransition, IFSMState> links;

    public HFSMState()
    {
        links = new Dictionary<FSMTransition, IFSMState>();
    }

    public void AddTransition(FSMTransition transition, IFSMState target)
    {
        links[transition] = target;
    }

    public FSMTransition VerifyTransitions()
    {
        foreach (FSMTransition t in links.Keys)
        {
            if (t.myCondition()) return t;
        }
        return null;
    }

    public IFSMState NextState(FSMTransition t)
    {
        return links[t];
    }

    public virtual void Enter() { foreach (FSMAction a in enterActions) a(); }
    public void Stay() { foreach (FSMAction a in stayActions) a(); }
    public void Exit() { foreach (FSMAction a in exitActions) a(); }

    public void Update()
    {
        //nothing to do i'm a state
    }
}

public class SubMachineState : IFSMState
{
    // Current state
    public IFSMState current;
    public IFSMState initial;
    public HFSMState selfState;
    private readonly bool keepMemory;

    public SubMachineState(IFSMState state, bool keepMemory = false)
    {
        selfState = new HFSMState();
        current = state;
        initial = state;
        this.keepMemory = keepMemory;
        current.Enter();
    }

    public void AddTransition(FSMTransition transition, IFSMState target)
    {
        ((IFSMState)selfState).AddTransition(transition, target);
    }

    public void Enter()
    {
        if (keepMemory)
        {
            selfState.Enter();
            current.Enter();
            return;
        }
        selfState.Enter();
        initial.Enter();
    }

    public void Exit()
    {
        current.Exit();
        selfState.Exit();
    }

    public IFSMState NextState(FSMTransition t)
    {
        return selfState.NextState(t);
    }

    public void Stay()
    {
        selfState.Stay();
    }

    public void Update()
    {
        FSMTransition transition = current.VerifyTransitions();
        if (transition != null)
        {
            current.Exit();
            transition.Fire();
            current = current.NextState(transition);
            current.Enter();
        }
        else
        {
            current.Update();
            current.Stay();
        }
    }

    public FSMTransition VerifyTransitions()
    {
        return selfState.VerifyTransitions();
    }
}