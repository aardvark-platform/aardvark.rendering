namespace Aardvark.Base.Rendering

// TODO: Delete this once Aardvark.Base.AverageWindow is compatible
type AverageWindow(maxCount : int) =
    let values = Array.zeroCreate maxCount
    let mutable index = 0
    let mutable count = 0
    let mutable sum = 0.0

    member x.Insert(v : float) =
        let newSum =
            if count < maxCount then
                count <- count + 1
                sum + v
            else
                sum + v - values.[index]

        sum <- newSum
        values.[index] <- v
        index <- (index + 1) % maxCount

        x.Value

    member x.Count = count

    member x.Value =
        if count = 0 then 0.0
        else sum / float count
