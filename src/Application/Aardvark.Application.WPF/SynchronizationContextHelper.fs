namespace System.Threading


open System
open System.Threading
open System.Windows.Threading


/// WindowsBase dispatcher uses synchronizationContext to intercept contention to pump messages. 
/// This module provides functionality to intercept synchronizationContext yet again, to save state prior to pumping
/// we often referred to as 'StackStealing' since pumping messages while performing long running rendering results to broken
/// state whenever threadlocals are used.
/// an awesome blogpost about the topic can be found here: http://joeduffyblog.com/2008/02/27/hooking-clr-blocking-calls-with-synchronizationcontext/
/// which was inspiration for this code as well.
module ContentionInterception = 

    /// delegate object PreWaitNotification(
    ///   IntPtr[] waitHandles, bool WaitAll, int millisecondsTimeout);

    /// delegate void PostWaitNotification(
    ///    IntPtr[] waitHandles, bool WaitAll, int millisecondsTimeout,
    ///    int ret, Exception ex, object state);

    type BlockingNotifySynchronizationContext(captured : SynchronizationContext, pre : (IntPtr[] * bool * int) -> Object, post : (IntPtr[] * bool * int * int * Exception * Object) -> unit) =
        inherit SynchronizationContext()

        do base.SetWaitNotificationRequired()

        override x.CreateCopy() =
            BlockingNotifySynchronizationContext((if isNull captured then null else captured.CreateCopy()), pre, post) :> SynchronizationContext

        override x.Post(cp : SendOrPostCallback, s : Object) =
            if isNull captured then base.Post(cp,s)
            else captured.Post(cp,s)

        override x.Send(cp : SendOrPostCallback, s : Object) = 
            if isNull captured then base.Send(cp,s)
            else captured.Post(cp,s)

        override x.Wait(waitHandles : IntPtr[], waitAll : bool, millisecondsTimeout : int) =
            let state = pre(waitHandles,waitAll,millisecondsTimeout)
            let mutable ret = 0
            let mutable e = null
            try
                try
                    if isNull captured then 
                        ret <- base.Wait(waitHandles, waitAll, millisecondsTimeout)
                    else 
                        ret <- captured.Wait(waitHandles, waitAll, millisecondsTimeout)
                with ex -> 
                    e <- ex
                    raise e
            finally 
                post(waitHandles, waitAll, millisecondsTimeout, ret, e, state)
            ret
            

        new(pre, post) = BlockingNotifySynchronizationContext(SynchronizationContext.Current, pre, post)


