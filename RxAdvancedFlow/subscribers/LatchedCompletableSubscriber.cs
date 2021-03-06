﻿using System;
using System.Threading;

namespace RxAdvancedFlow.subscribers
{
    /// <summary>
    /// An implementation of the ICompletableSubscriber interface which let's one
    /// wait blockingly for the terminal events of a ICompletable source.
    /// </summary>
    public sealed class LatchedCompletableSubscriber : ICompletableSubscriber
    {
        Exception err;

        readonly CountdownEvent cdl;

        public LatchedCompletableSubscriber()
        {
            this.cdl = new CountdownEvent(1);
        }

        public void OnComplete()
        {
            cdl.Signal();
        }

        public void OnError(Exception e)
        {
            err = e;
            cdl.Signal();
        }

        public void OnSubscribe(IDisposable d)
        {
            // no need for the disposable
        }

        public void Await()
        {
            cdl.Wait();

            Exception e = err;
            if (e != null)
            {
                throw e;
            }
        }

        public bool Await(TimeSpan timeout)
        {
            bool r = cdl.Wait(timeout);

            if (r)
            {
                Exception e = err;
                if (e != null)
                {
                    throw e;
                }
            }

            return r;
        }

        public bool IsTerminated()
        {
            return cdl.CurrentCount == 0;
        }

        public Exception GetException()
        {
            if (cdl.CurrentCount == 0)
            {
                return err;
            }

            return null;
        }
    }
}
