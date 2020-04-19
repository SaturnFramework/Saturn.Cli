module Paths

open System.IO

let fsProjPath =
    let cwd = Directory.GetCurrentDirectory()
    Directory.GetFiles(cwd, "*.fsproj", SearchOption.AllDirectories)
    |> Seq.filter (fun n -> n.Contains "paket-files" |> not)
    |> Seq.filter (fun n -> n.Contains "Migrations" |> not)
    |> Seq.head

let projDirPath =
    Path.GetDirectoryName fsProjPath

let (</>) a b = Path.Combine(a,b)

