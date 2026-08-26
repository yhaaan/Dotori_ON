using System;
using System.Collections.Generic;

namespace TeamOverlay.Backend.Mock
{
    internal sealed class ObservableStream<T> : IObservable<T>, IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<IObserver<T>> _observers = new List<IObserver<T>>();
        private bool _isCompleted;

        public IDisposable Subscribe(IObserver<T> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            lock (_gate)
            {
                if (_isCompleted)
                {
                    observer.OnCompleted();
                    return EmptySubscription.Instance;
                }

                _observers.Add(observer);
                return new Subscription(this, observer);
            }
        }

        public void Publish(T value)
        {
            IObserver<T>[] observers;
            lock (_gate)
            {
                if (_isCompleted)
                {
                    return;
                }

                observers = _observers.ToArray();
            }

            foreach (var observer in observers)
            {
                try
                {
                    observer.OnNext(value);
                }
                catch
                {
                    // One UI subscriber must not prevent other subscribers from
                    // observing a committed backend mutation.
                }
            }
        }

        public void Dispose()
        {
            IObserver<T>[] observers;
            lock (_gate)
            {
                if (_isCompleted)
                {
                    return;
                }

                _isCompleted = true;
                observers = _observers.ToArray();
                _observers.Clear();
            }

            foreach (var observer in observers)
            {
                try
                {
                    observer.OnCompleted();
                }
                catch
                {
                    // Observer exceptions cannot interrupt completion of others.
                }
            }
        }

        private void Unsubscribe(IObserver<T> observer)
        {
            lock (_gate)
            {
                _observers.Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private ObservableStream<T> _owner;
            private IObserver<T> _observer;

            public Subscription(ObservableStream<T> owner, IObserver<T> observer)
            {
                _owner = owner;
                _observer = observer;
            }

            public void Dispose()
            {
                var owner = _owner;
                var observer = _observer;
                _owner = null;
                _observer = null;
                owner?.Unsubscribe(observer);
            }
        }

        private sealed class EmptySubscription : IDisposable
        {
            public static readonly EmptySubscription Instance = new EmptySubscription();

            public void Dispose()
            {
            }
        }
    }
}
