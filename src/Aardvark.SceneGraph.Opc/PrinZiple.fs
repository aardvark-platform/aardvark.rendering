namespace Aardvark.Prinziple

open System
open System.IO
open System.Xml
open System.Xml.Linq
open Aardvark.Base
open FSharp.Data.Adaptive
open ICSharpCode.SharpZipLib
open ICSharpCode.SharpZipLib.Zip

module Prinziple = 
  // TODO @thomasortner: check this!!!!
  ICSharpCode.SharpZipLib.Zip.ZipStrings.CodePage <- 437

  let private readAllBytes' (reader : BinaryReader) =

    let bufferSize = 4096;     
    use ms = new MemoryStream()
        
    let buffer : byte[] = Array.zeroCreate bufferSize //(fun _ -> byte(0))
    let mutable count = 0
    
    while (count <- reader.Read(buffer, 0, buffer.Length); count <> 0) do
        ms.Write(buffer, 0, count);

    ms.ToArray()
    
  //"D:\_WORK\Aardwork\Exomars\_Scenes\_NEWVIEWER\TestScene\Surfaces\Cape_Desire_RGB\OPC_000_000"
  //"hierarchy.cache"
  //check if path contains zipfile (lookup in zipTable)
  //yes:
  //split path in zipfile and entry
  //no:
  //normal file read

  let mutable private zipTable : HashMap<string,string> = HashMap.empty

  let splitPath (path : string) =    
    let p = zipTable |> HashMap.filter(fun _ v -> path.StartsWith v) |> HashMap.toList |> List.map snd |> List.tryHead    
    match p with
    | Some zipped -> Some (Path.ChangeExtension(zipped, ".opc"), path.[zipped.Length+1..path.Length-1])
    | None -> None

  let registerIfZipped dir =
    let dir   = Path.GetFullPath(dir)
    let zPath = Path.ChangeExtension(dir, ".opc")
    if File.Exists zPath then
      zipTable <- HashMap.add dir dir zipTable    
    dir
 
  let openRead path =
    let split = path |> Path.GetFullPath |> splitPath
    match split with
    | Some(zip,entry) ->
      let entryPath = entry.Replace("\\","/")
      let file = new ZipFile(File.OpenRead(zip))
      let e = file.GetEntry(entryPath)
      file.GetInputStream(e)
    | None ->
      File.OpenRead(path) :> Stream

  let loadBytesFromZip zipPath (entryPath:string) =
    let entryPath = entryPath.Replace("\\","/")
    let file = new ZipFile(File.OpenRead(zipPath))
    let e = file.GetEntry(entryPath)
    use s = file.GetInputStream(e)    
    use r = new BinaryReader(s)
    r |> readAllBytes'

  let loadXmlFromZip zipPath (entryPath : string) =
    let entryPath = entryPath.Replace("\\","/")
    let file = new ZipFile(File.OpenRead(zipPath))
    let e = file.GetEntry(entryPath)    
    use s = file.GetInputStream(e)    
    XDocument.Load(s)

  let loadXmlFromZip' zipPath (entryPath : string) =
    let xDoc = loadXmlFromZip zipPath entryPath
    let x = new XmlDocument()
    use reader = xDoc.CreateReader()
    x.Load(reader)
    x

    
  let readXDoc path =    
    let split = path |> Path.GetFullPath |> splitPath
    match split with
    | Some(zip,entry) -> loadXmlFromZip zip entry
    | None -> XDocument.Load(path)

  let readXmlDoc (path:string) =
    let split = path |> Path.GetFullPath |> splitPath
    match split with
    | Some(zip,entry) -> loadXmlFromZip' zip entry
    | None -> 
      let doc = new XmlDocument()
      doc.Load path; doc
  
  let readAllBytes path =    
    let split = path |> Path.GetFullPath |> splitPath
    match split with
    | Some(zip,entry) -> loadBytesFromZip zip entry
    | None -> File.readAllBytes path
    
  let exists path = 
    let split = path |> Path.GetFullPath |> splitPath
    match split with
    | Some(zip,entry) ->
      let entryPath = entry.Replace("\\","/")
      let file = new ZipFile(File.OpenRead(zip))
      let e = file.GetEntry(entryPath)
      e <> Unchecked.defaultof<_>
    | None -> File.Exists path
    

  //let loadFromZip' path =
  //  let file = new ZipFile(File.OpenRead(@"D:\_WORK\Aardwork\Exomars\_Scenes\_NEWVIEWER\TestScene\Surfaces\Cape_Desire_RGB\OPC_000_000.zip"))
  //  let e = file.GetEntry("hierarchy.cache")
  //  use s = file.GetInputStream(e)
  //  //s.Seek(2L, SeekOrigin.Begin) |> ignore

  //  use r = new BinaryReader(s)
  //  r |> readAllBytes'

