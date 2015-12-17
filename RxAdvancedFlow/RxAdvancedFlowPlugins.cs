using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow
{
    /// <summary>
    /// Manages the plugins for the entire RxAdvancedFlow library.
    /// </summary>
    public static class RxAdvancedFlowPlugins
    {
        static volatile Action<Exception> OnErrorAction;

        static volatile bool lockdown;

        public static void Lockdown()
        {
            lockdown = true;
        }

        public static void SetOnErrorAction(Action<Exception> action)
        {
            if (!lockdown)
            {
                OnErrorAction = action;
            }
        }

        public static void OnError(Exception e)
        {
            if (OnErrorAction == null)
            {
                Console.WriteLine(e.ToString());
            }
            else
            {
                try {
                    OnErrorAction(e);
                } catch (Exception e1)
                {
                    Console.WriteLine(e.ToString());
                    Console.WriteLine(e2.ToString());
                }
            }
        }
    }
}
