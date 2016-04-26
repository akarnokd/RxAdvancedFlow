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

        /// <summary>
        /// Prevents modifying the plugin actions.
        /// </summary>
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

        /// <summary>
        /// Static sink for exceptions that can't be delivered to a consumer
        /// due to protocol reasons.
        /// </summary>
        /// <remarks>
        /// Use SetOnErrorAction to change the default behavior, which is to write to the console.
        /// </remarks>
        /// <param name="e">The Exception instance</param>
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
                    Console.WriteLine(e1.ToString());
                }
            }
        }
    }
}
