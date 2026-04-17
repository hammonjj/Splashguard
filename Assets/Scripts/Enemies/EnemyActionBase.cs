using BitBox.Library;

namespace Bitbox.Splashguard.Enemies
{
    public abstract class EnemyActionBase : MonoBehaviourBase
    {
        protected EnemyContext Context { get; private set; }

        public virtual bool CanBeInterrupted => true;
        public virtual string DebugStatus => string.Empty;

        public void BindContext(EnemyContext context)
        {
            Context = context;
            OnContextBound();
        }

        public abstract float Score();
        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Tick(float deltaTime) { }

        protected virtual void OnContextBound() { }
    }
}
