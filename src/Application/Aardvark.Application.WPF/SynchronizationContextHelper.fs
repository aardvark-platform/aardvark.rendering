namespace System.Threading


open System
open System.Threading


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

    type BlockingNotifySynchronizationContext(captured : Option<SynchronizationContext>, pre : (IntPtr[] * bool * int) -> Object, post : (IntPtr[] * bool * int * int * Exception * Object) -> unit) =
        inherit SynchronizationContext()

        override x.CreateCopy() =
            BlockingNotifySynchronizationContext(captured |> Option.map (fun c -> c.CreateCopy()), pre, post) :> SynchronizationContext

        override x.Post(cp : SendOrPostCallback, s : Object) =
            match captured with
            | None -> base.Post(cp,s)
            | Some c -> c.Post(cp,s)

        override x.Send(cp : SendOrPostCallback, s : Object) = 
            match captured with
            | None -> base.Send(cp,s)
            | Some c -> c.Post(cp,s)

        override x.Wait(waitHandles : IntPtr[], waitAll : bool, millisecondsTimeout : int) =
            let s = pre(waitHandles,waitAll,millisecondsTimeout)
            let mutable ret = 0
            let mutable e = null
            try
                try
                    match captured with
                    | None -> 
                        ret <- base.Wait(waitHandles, waitAll, millisecondsTimeout)
                        ()
                    | Some s -> 
                        ret <- s.Wait(waitHandles, waitAll, millisecondsTimeout)
                        ()
                with ex -> 
                    e <- ex
                    raise e
            finally 
                post(waitHandles, waitAll, millisecondsTimeout, ret, e, s)
            ret
            

        new(pre, post) = BlockingNotifySynchronizationContext(Some SynchronizationContext.Current, pre, post)