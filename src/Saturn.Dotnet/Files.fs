module Files

open System.IO

let generateFile (path, ctn) =
    let path = Path.GetFullPath path
    printfn "Generated %s ..." path
    File.WriteAllText(path,ctn)

let updateFile (path, ctn) =
    let path = Path.GetFullPath path
    printfn "Updated %s ..." path
    File.WriteAllText(path,ctn)
