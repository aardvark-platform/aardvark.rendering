//#load "packages/FSharp.Charting/FSharp.Charting.fsx"
#r "System.Windows.Forms.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "packages/FSharp.Charting/lib/net40/FSharp.Charting.dll"
#r "packages/build/FAKE/tools/FakeLib.dll"



open System
open System.Drawing
open System.IO
open System.Diagnostics
open FSharp.Charting
open System.Windows.Forms
open System.Windows.Forms.DataVisualization.Charting
open Fake

#if INTERACTIVE
let args = Environment.GetCommandLineArgs() |> Array.skip 2
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
#else
let args = Environment.GetCommandLineArgs() |> Array.skip 1
#endif

let bin = Path.Combine(Environment.CurrentDirectory, "bin", "Release")

let execArgs (cmd : string) args =
    let info = 
        ProcessStartInfo(
            FileName = Path.Combine(bin, cmd),
            Arguments = args,
            WorkingDirectory = bin,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        )
    use proc = Process.Start(info)
    proc.WaitForExit()
    if proc.ExitCode = 0 then
        let output = proc.StandardOutput.ReadToEnd().TrimEnd('\r', '\n', ' ', '\t')
        output
    else
        let err = proc.StandardError.ReadToEnd().TrimEnd('\r', '\n', ' ', '\t')
        printfn "ERROR: %s" err
        failwith err

let exec cmd = execArgs cmd ""

let execute (file : string) (outFile : string) =
    Fake.TraceListener.listeners.Clear()
    let status = Fake.Git.CommandHelper.runSimpleGitCommand Environment.CurrentDirectory "log -1 --pretty=format:%an;%ae;%ai;%H HEAD"
    
    printfn "%A" status

    let time = exec file
    let now = DateTime.Now
    let now = now.ToString("yyyy-MM-dd HH:mm:ss.fffffff")
    let all = 
        [
            Environment.MachineName
            now 
            status
            time
        ]
    
    let line = all |> String.concat ";"
    File.AppendAllLines(outFile, [line])
    ()

let show (dataFile : string) =
    let lines = File.ReadAllLines dataFile
    let points = 
        lines |> Array.map (fun l ->
            let parts = l.Split(';')
            let t = parts.[1] |> DateTime.Parse
            let v = parts.[parts.Length-1] |> Double.Parse
            t,v
        )
    let dashGrid = 
        Grid( 
            LineColor = Color.Gainsboro, 
            LineDashStyle = ChartDashStyle.Dash 
        )

    let label = new LabelStyle(Format = "0.000\" ms\"")


    let chart = 
        Chart.Line(points)
            |> Chart.WithArea.AxisY(MajorGrid = dashGrid, LabelStyle = label)
            |> Chart.WithArea.AxisX(MajorGrid = dashGrid)
            |> Chart.WithSeries.Marker(Size = 10, Style = MarkerStyle.Diamond)
            |> Chart.WithTitle(Text = "Execution Time", InsideArea = false)

            
    let f = chart.ShowChart()
    Application.Run f


let testStartupPerformance () =
    let outFile = "startupResults.csv"
    if File.Exists outFile |> not then
        File.AppendAllLines(outFile, [| "config; objects; time; "|])
    for n in [ 1000; 5000; 10000; 15000; 20000; 30000; 50000] do
        for config in 0 .. 5 do
            printfn "running test for n: %d and config: %d" n config
            let r = execArgs "Aardvark.Rendering.GL.Tests.exe" (sprintf "%d %d" n config)
            File.AppendAllLines(outFile, [| r |])

let main () =
    match args with
        | [| "show" | "Show"; file |] ->
            show file
        | [| executable; output|] ->
            execute executable output

        | _ ->
            failwith "asdasd"

//do main()


