using System;
namespace NightSupermarket.Core
{
    /// <summary>Typed synchronous events with explicit subscription lifetimes.</summary>
    public sealed class EventStream<T>
    {
        private event Action<T> Handlers;
        public IDisposable Subscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Handlers += handler;
            return new Subscription(() => Handlers -= handler);
        }
        public void Publish(T value) => Handlers?.Invoke(value);
        private sealed class Subscription : IDisposable
        {
            private Action dispose;
            public Subscription(Action dispose) { this.dispose = dispose; }
            public void Dispose() { var action = dispose; dispose = null; action?.Invoke(); }
        }
    }
}
