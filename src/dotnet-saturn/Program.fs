module Program
open System.IO
open System

let fsProjPath =
    let cwd = Directory.GetCurrentDirectory()
    Directory.GetFiles(cwd, "*.fsproj", SearchOption.AllDirectories)
    |> Seq.filter (fun n -> n.Contains "paket-files" |> not)
    |> Seq.head

let projDirPath =
    Path.GetDirectoryName fsProjPath


let (</>) a b = Path.Combine(a,b)

let generateModel name names (fields : (string * string) []) =
    let id = fields.[0] |> fst
    let getAllQuery = sprintf "SELECT %s FROM %s" (fields |> Array.map (fst) |> String.concat ", ") names
    let getByIdQuery = sprintf "SELECT %s FROM %s WHERE %s=@%s" (fields |> Array.map (fst) |> String.concat ", ") names id id
    let updateQuery = sprintf "UPDATE %s SET %s WHERE %s=@%s" names (fields |> Array.map (fun (n,_) -> n + " = @" + n) |> String.concat ", ") id id
    let insertQuery = sprintf "INSERT %s(%s) VALUES (%s)" names (fields |> Array.map (fst) |> String.concat ", ") (fields |> Array.map (fun (n,_) -> "@" + n) |> String.concat ", ")
    let deleteQuery = sprintf "DELETE FROM %s WHERE %s=@%s" names id id
    let fields = fields |> Array.map (fun (n,t) -> sprintf "%s: %s" n t) |> String.concat "\n  "

    sprintf """namespace %s
open System
open Giraffe.Tasks
open System.Threading.Tasks

type %s = {
  %s
}

module Context =
  let getAll () : Task<%s list> =
    task {
      //%s
      return []
    }

  let getById id : Task<%s option> =
    task {
      //%s
      return None
    }

  let update v : Task<Result<unit,exn>> =
    task {
      //%s
      return Ok ()
    }

  let insert v : Task<Result<unit,exn>> =
    task {
      //%s
      return Ok ()
    }

  let delete id : Task<Result<unit,exn>> =
    task {
      //%s
      return Ok ()
    }
"""     names name fields name getAllQuery name getByIdQuery updateQuery insertQuery deleteQuery

let generateMigration (name: string) (names : string) (fields : (string * string) []) =
    let dir = projDirPath </> ".." </> "Migrations"
    let fsproj = Directory.GetFiles(dir, "*.fsproj", SearchOption.TopDirectoryOnly).[0]
    let id = sprintf "%d%d%d%d%d" DateTime.Now.Year DateTime.Now.Month DateTime.Now.Day DateTime.Now.Hour DateTime.Now.Minute
    let fn = sprintf "%s.%s.fs" id name
    let fields = fields |> Array.map (fun (n,t) -> sprintf "%s %s NOT NULL" n t) |> String.concat ",\n      "
    let content = sprintf """namespace Migrations
open SimpleMigrations

[<Migration(%sL, "Create %s")>]
type Create%s() =
  inherit Migration()

  override __.Up() =
    base.Execute(@"CREATE TABLE %s(
      %s
    )")

  override __.Down() =
    base.Execute(@"DROP TABLE %s")
"""                 id names names names fields names

    File.WriteAllText(dir </> fn, content)
    let ctn =
        File.ReadAllLines fsproj
        |> Seq.map (fun f -> if f.Trim().StartsWith """<Compile Include="Program.fs" />""" then sprintf "    <Compile Include=\"%s\" />\n%s" fn f  else f  )
    File.WriteAllLines(fsproj, ctn)


    ()

let generateHtml (name : string) (names : string) (fields : (string * string) []) =
    let dir = projDirPath </> names
    Directory.CreateDirectory(dir) |> ignore

    File.WriteAllText(dir </> "Model.fs", generateModel name names fields)
    File.WriteAllText(dir </> "Views.fs",  sprintf "module %s.Views" names)
    File.WriteAllText(dir </> "Controller.fs",  sprintf "module %s.Controller" names)

    let ctn =
        File.ReadAllLines fsProjPath
        |> Seq.map (fun f -> if f.Trim().StartsWith """<Compile Include="Router.fs" />""" then sprintf "    <Compile Include=\"%s\\%s\" />\n    <Compile Include=\"%s\\%s\" />\n    <Compile Include=\"%s\\%s\" />\n%s" names "Model.fs" names "Views.fs" names "Controller.fs" f  else f  )
    File.WriteAllLines(fsProjPath, ctn)


    generateMigration name names fields

    ()


let generateJson (name : string) (names : string) (fields : (string * string) []) =
    let dir = projDirPath </> names
    Directory.CreateDirectory(dir) |> ignore

    File.WriteAllText(dir </> "Model.fs", generateModel name names fields)
    File.WriteAllText(dir </> "Controller.fs",  sprintf "module %s.Controller" names)

    let ctn =
        File.ReadAllLines fsProjPath
        |> Seq.map (fun f -> if f.Trim().StartsWith """<Compile Include="Router.fs" />""" then sprintf "    <Compile Include=\"%s\\%s\" />\n     <Compile Include=\"%s\\%s\" />\n%s" names "Model.fs" names "Controller.fs" f  else f  )
    File.WriteAllLines(fsProjPath, ctn)


    generateMigration name names fields

    ()

let generateMdl (name : string) (names : string) (fields : (string * string) []) =
    let dir = projDirPath </> names
    Directory.CreateDirectory(dir) |> ignore

    File.WriteAllText(dir </> "Model.fs", generateModel name names fields)

    let ctn =
        File.ReadAllLines fsProjPath
        |> Seq.map (fun f -> if f.Trim().StartsWith """<Compile Include="Router.fs" />""" then sprintf "    <Compile Include=\"%s\\%s\" />\n%s" names "Model.fs" f  else f  )
    File.WriteAllLines(fsProjPath, ctn)


    generateMigration name names fields

    ()

let generateGraphQL (name : string) (names : string) (fields : (string * string) []) =
    ()

let printHelp () =
    printfn "Avaliable commands:\n  * gen, gen.html - generates the data access layer, controler, and server side views\n  * gen.json - generates the data access layer, and controler returning data in JSON format\n  * gen.model - generates data access layer without controller nor views\n"

[<EntryPoint>]
let main argv =

    match Array.tryHead argv |> Option.map (fun n -> n.ToLower()) with
    | Some action ->
        try
            let flags, argv = argv |> Array.partition (fun f -> f.StartsWith "--")
            let name = argv.[1]
            let names = argv.[2]
            let fields = argv.[3 ..] |> Array.map (fun n -> let x = n.Split(':', 2) in x.[0], x.[1])
            match action with
            | "gen" | "gen.html" -> generateHtml name names fields
            | "gen.json" -> generateJson name names fields
            | "gen.model" -> generateMdl name names fields
            // | "gen.graphql" -> generateGraphQL name names fields
            | _ ->
                printfn "Wrong format of input parameters - they should be passed in following format: Command Name Names [field_name:field_type]"
                printfn "---"
                printHelp ()
        with
        | ex ->
            printfn "%s" ex.Message
            printfn "---"
            printHelp ()
    | None ->
        printfn "Missing input parameters - they should be passed in following format: Command Name Names [field_name:field_type]"
        printfn "---"
        printHelp ()

    0 // return an integer exit code
