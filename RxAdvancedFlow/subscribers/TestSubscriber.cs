using Reactive.Streams;
using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.subscribers;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RxAdvancedFlow.subscribers
{
    public class TestSubscriber<T> : ISubscriber<T>, ISubscription {

        readonly ISubscriber<T> sdelegate;

        readonly List<T> values;

        readonly List<Exception> errors;

        readonly CountdownEvent cdl;

        SingleArbiterStruct arbiter;

        int completions;

        int subscriptions;


        public TestSubscriber() : this(new EmptySubscriber<T>(), long.MaxValue)
        {

        }
    

        public TestSubscriber(ISubscriber<T> sdelegate) :
            this(sdelegate, long.MaxValue)
        {

        }


        public TestSubscriber(long initialRequest) :
            this(new EmptySubscriber<T>(), initialRequest)
        {

        }


        public TestSubscriber(ISubscriber<T> sdelegate, long initialRequest) {
            this.sdelegate = sdelegate;
            this.values = new List<T>();
            this.errors = new List<Exception>();
            this.cdl = new CountdownEvent(1);
            this.arbiter.InitRequest(initialRequest);
        }


        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                arbiter.Request(n);
            }
        }


        public void Cancel()
        {
            arbiter.Cancel();
        }


        public void OnSubscribe(ISubscription s)
        {
            subscriptions++;
            if (!arbiter.Set(s))
            {
                if (!arbiter.IsCancelled())
                {
                    errors.Add(new InvalidOperationException("subscription already set"));
                }
            }
        }


        public virtual void OnNext(T t)
        {
            values.Add(t);
            sdelegate.OnNext(t);
        }


        public virtual void OnError(Exception t)
        {
            errors.Add(t);
            sdelegate.OnError(t);
            cdl.Signal();
        }


        public virtual void OnComplete()
        {
            completions++;
            sdelegate.OnComplete();
            cdl.Signal();
        }

        public bool IsCancelled()
        {
            return arbiter.IsCancelled();
        }

        /// <summary>
        /// Prepares and throws an AssertionError exception based on the message, cause,
        /// the active state and the potential errors so far.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="cause"></param>
        void AssertionError(String message/*, Exception cause = null*/)
        {
            StringBuilder b = new StringBuilder();

            if (cdl.CurrentCount != 0)
            {
                b.Append("(active) ");
            }
            b.Append(message);

            List<Exception> err = new List<Exception>(errors);
            if (err.Count != 0)
            {
                b.Append(" (+ ")
                .Append(err.Count)
                .Append(" errors)");
            }

            throw new AggregateException(b.ToString(), err);
        }

        public TestSubscriber<T> AssertNoValues()
        {
            if (values.Count != 0)
            {
                AssertionError("No values expected but received: [length = " + values.Count + "] " + values);
            }
            return this;
        }

        public TestSubscriber<T> AssertValueCount(int n)
        {
            int s = values.Count;
            if (s != n)
            {
                AssertionError("Different value count: expected = " + n + ", actual = " + s);
            }
            return this;
        }

        string ValueAndClass<E>(E o)
        {
            if (o == null)
            {
                return null;
            }
            return o + " (" + o.GetType().Name + ")";
        }

        public TestSubscriber<T> AssertValue(T value)
        {
            int s = values.Count;
            if (s == 0)
            {
                AssertionError("No values received");
            }
            if (s == 1)
            {
                if (!EqualityComparer<T>.Default.Equals(value, values[0]))
                {
                    AssertionError("Values differ: expected = " + ValueAndClass(value) + ", actual = " + ValueAndClass(values[0]));
                }
            }
            if (s > 1)
            {
                AssertionError("More values received: expected = " + ValueAndClass(value)
                + ", actual = "
                + "[" + s + "] "
                + values);
            }
            return this;
        }

        public TestSubscriber<T> AssertValues(params T[] values)
        {
            int s = this.values.Count;

            if (s != values.Length)
            {
                AssertionError("Different value count: expected = [length = " + values.Length + "] " + string.Join(", ", values)
                + ", actual = [length = " + s + "] " + string.Join(", ", this.values));
            }
            for (int i = 0; i < s; i++)
            {
                T t1 = values[i];
                T t2 = this.values[i];

                if (!EqualityComparer<T>.Default.Equals(t1, t2))
                {
                    AssertionError("Values at " + i + " differ: expected = " + ValueAndClass(t1) + ", actual = " + ValueAndClass(t2));
                }
            }

            return this;
        }

        public TestSubscriber<T> AssertValueSequence(IEnumerable<T> expectedSequence)
        {

            IEnumerator<T> actual = values.GetEnumerator();
            IEnumerator<T> expected = expectedSequence.GetEnumerator();

            int i = 0;

            for (;;)
            {
                bool n1 = actual.MoveNext();
                bool n2 = expected.MoveNext();

                if (n1 && n2)
                {
                    T t1 = actual.Current;
                    T t2 = expected.Current;

                    if (!EqualityComparer<T>.Default.Equals(t1, t2))
                    {
                        AssertionError("The " + i + " th elements differ: expected = " + ValueAndClass(t2) + ", actual =" + ValueAndClass(t1));
                    }
                    i++;
                }
                else
                if (n1 && !n2)
                {
                    AssertionError("Actual contains more elements" + values);
                    break;
                }
                else
                if (!n1 && n2)
                {
                    AssertionError("Actual contains fewer elements: " + values);
                    break;
                }
                else {
                    break;
                }

            }

            return this;
        }

        public TestSubscriber<T> AssertComplete()
        {
            int c = completions;
            if (c == 0)
            {
                AssertionError("Not completed");
            }
            if (c > 1)
            {
                AssertionError("Multiple completions: " + c);
            }

            return this;
        }

        public TestSubscriber<T> AssertNotComplete()
        {
            int c = completions;
            if (c == 1)
            {
                AssertionError("Completed");
            }

            if (c > 1)
            {
                AssertionError("Multiple completions: " + c);
            }

            return this;
        }

        public TestSubscriber<T> AssertTerminated()
        {
            if (cdl.CurrentCount != 0)
            {
                AssertionError("Not terminated");
            }
            return this;
        }

        public TestSubscriber<T> AssertNotTerminated()
        {
            if (cdl.CurrentCount == 0)
            {
                AssertionError("Terminated");
            }
            return this;
        }

        public TestSubscriber<T> AssertNoError()
        {
            int s = errors.Count;
            if (s == 1)
            {
                AssertionError("Error present: " + ValueAndClass(errors[0]));
            }
            if (s > 1)
            {
                AssertionError("Multiple errors: " + s);
            }
            return this;
        }

        public TestSubscriber<T> AssertError(Exception e)
        {
            int s = errors.Count;
            if (s == 0)
            {
                AssertionError("No error");
            }
            if (s == 1)
            {
                if (!object.Equals(e, errors[0]))
                {
                    AssertionError("Errors differ: expected = " + ValueAndClass(e) + ", actual = " + ValueAndClass(errors[0]));
                }
            }
            if (s > 1)
            {
                AssertionError("Multiple errors: " + s);
            }

            return this;
        }

        public TestSubscriber<T> AssertError(Type clazz)
        {
            int s = errors.Count;
            if (s == 0)
            {
                AssertionError("No error");
            }
            if (s == 1)
            {
                Exception e = errors[0];
                if (e == null || clazz.IsAssignableFrom(e.GetType()))
                {
                    AssertionError("Error class incompatible: expected = " + clazz.Name
                    + ", actual = " + ValueAndClass(e));
                }
            }
            if (s > 1)
            {
                AssertionError("Multiple errors: " + s);
            }

            return this;
        }

        public TestSubscriber<T> AssertErrorMessage(string message)
        {
            int s = errors.Count;
            if (s == 0)
            {
                AssertionError("No error");
            }
            if (s == 1)
            {
                if (!Equals(message, errors[0].Message))
                {
                    AssertionError("Error class incompatible: expected = \"" + message
                            + "\", actual = \"" + errors[0].Message + "\"");
                }
            }
            if (s > 1)
            {
                AssertionError("Multiple errors: " + s);
            }

            return this;
        }

        public TestSubscriber<T> AssertErrorCause(Exception e)
        {
            int s = errors.Count;
            if (s == 0)
            {
                AssertionError("No error");
            }
            if (s == 1)
            {
                Exception cause = errors[0].InnerException;
                if (!Equals(e, cause))
                {
                    AssertionError("Errors differ: expected = " + ValueAndClass(e) + ", actual = " + ValueAndClass(cause));
                }
            }
            if (s > 1)
            {
                AssertionError("Multiple errors: " + s);
            }

            return this;
        }

        public TestSubscriber<T> AssertErrorCause(Type clazz)
        {
            int s = errors.Count;
            if (s == 0)
            {
                AssertionError("No error");
            }
            if (s == 1)
            {
                Exception cause = errors[0].InnerException;
                if (cause == null || clazz.IsAssignableFrom(cause.GetType()))
                {
                    AssertionError("Error class incompatible: expected = " + clazz.Name + ", actual = " + ValueAndClass(cause));
                }
            }
            if (s > 1)
            {
                AssertionError("Multiple errors: " + s);
            }

            return this;
        }


        public TestSubscriber<T> AssertSubscribed()
        {
            int s = subscriptions;

            if (s == 0)
            {
                AssertionError("OnSubscribe not called");
            }
            if (s > 1)
            {
                AssertionError("OnSubscribe called multiple times: " + s);
            }

            return this;
        }

        public TestSubscriber<T> AssertNotSubscribed()
        {
            int s = subscriptions;

            if (s == 1)
            {
                AssertionError("OnSubscribe called once");
            }
            if (s > 1)
            {
                AssertionError("OnSubscribe called multiple times: " + s);
            }

            return this;
        }

        public void Await()
        {
            if (cdl.CurrentCount == 0)
            {
                return;
            }
            cdl.Wait();
        }

        public bool Await(TimeSpan timeout)
        {
            if (cdl.CurrentCount == 0)
            {
                return true;
            }
            return cdl.Wait(timeout);
        }

        public List<T> Values()
        {
            return values;
        }

        public List<Exception> Errors()
        {
            return errors;
        }

        public int Completions()
        {
            return completions;
        }

        public int Subscriptions()
        {
            return subscriptions;
        }
    }
}
