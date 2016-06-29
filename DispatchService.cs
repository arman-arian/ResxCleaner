using System;
using System.Windows;
using System.Windows.Threading;

namespace ResxCleaner
{
    public static class DispatchService
    {
        public static void Invoke(Action action)
        {
            Dispatcher dispatchObject = Application.Current.Dispatcher;

            if (dispatchObject == null || dispatchObject.CheckAccess())
            {
                action();
            }
            else
            {
                dispatchObject.Invoke(action);
            }
        }

        public static void BeginInvoke(Action action)
        {
            Dispatcher dispatchObject = Application.Current.Dispatcher;

            dispatchObject.BeginInvoke(action);
        }
    }
}
