namespace Aardvark.Base

open OpenHardwareMonitor
open OpenHardwareMonitor.Hardware

module Hardware =


    let private computer = Computer(CPUEnabled = true, GPUEnabled = true)

    let test() =
        computer.Open()
        let cpu = 
            computer.Hardware |> Array.tryFind (fun h ->
                h.HardwareType = HardwareType.GpuAti || h.HardwareType = HardwareType.GpuNvidia
            )

        match cpu with
            | Some cpu ->

                for s in cpu.Sensors do
                    printfn "%s: %A %A" s.Name s.Value s.SensorType

            | None ->
                printfn "no CPU found"