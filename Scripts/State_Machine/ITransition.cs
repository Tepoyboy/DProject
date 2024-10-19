using UnityUtils;

namespace State_Machine {
    public interface ITransition {
        IState To { get; }
        IPredicate Condition { get; }
    }
}