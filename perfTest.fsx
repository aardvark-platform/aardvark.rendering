//#load "packages/FSharp.Charting/FSharp.Charting.fsx"
#r "System.Windows.Forms.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "packages/FSharp.Charting/lib/net40/FSharp.Charting.dll"


open System
open System.Drawing
open System.IO
open System.Diagnostics
open FSharp.Charting
open System.Windows.Forms
open System.Windows.Forms.DataVisualization.Charting

#if INTERACTIVE
let args = Environment.GetCommandLineArgs() |> Array.skip 2
#else
let args = Environment.GetCommandLineArgs() |> Array.skip 1
#endif

let bin = Path.Combine(Environment.CurrentDirectory, "bin", "Release")

let exec (cmd : string) =
    let info = 
        ProcessStartInfo(
            FileName = Path.Combine(bin, cmd),
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




let execute (file : string) (outFile : string) (args : string[]) =
    let time = exec file
    let now = DateTime.Now
    let now = now.ToString("yyyy-MM-dd HH:mm:ss.fffffff")
    let all = 
        Array.concat [
            [| Environment.MachineName; now |]
            args
            [| time |]
        ]
    
    let line = all |> String.concat ";"
    File.AppendAllLines(outFile, [line])
    ()

let main () =
    match args with
        | [| "show"; file |] ->
            let lines = File.ReadAllLines file
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

//            let chart = 
//                Chart.Column(
//                    points |> Seq.map snd,
//                    Title = "Execution Time [ms]",
//                    Labels = (points |> Seq.map (fun (d,_) -> d.ToString("yyyy-MM-dd HH:mm:ss")))
//                )
            
            let f = chart.ShowChart()
            Application.Run f
            ()
        | _ ->
            execute args.[0] args.[1] (Array.skip 2 args)

do main()


