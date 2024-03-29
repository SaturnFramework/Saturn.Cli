module CodeGeneration
open System.IO
open System

open Models
open Strings
open Paths

let generateModel name names (fields : Parameter []) =
    let id = fields.[0].name
    let fields = fields |> Array.map (fun f -> sprintf "%s: %s" f.name f.FSharpType) |> String.concat "\n  "

    sprintf """namespace %s

[<CLIMutable>]
type %s = {
  %s
}

module Validation =
  let validate v =
    let validators = [
      fun u -> if isNull u.%s then Some ("%s", "%s shouldn't be empty") else None
    ]

    validators
    |> List.fold (fun acc e ->
      match e v with
      | Some (k,v) -> Map.add k v acc
      | None -> acc
    ) Map.empty
"""     names name fields id id (upper id)

let generateRepository name names (fields : Parameter []) =
    let id = fields.[0].name
    let getAllQuery = sprintf "SELECT %s FROM %s" (fields |> Array.map (fun f -> f.name) |> String.concat ", ") names
    let getByIdQuery = sprintf "SELECT %s FROM %s WHERE %s=@%s" (fields |> Array.map (fun f -> f.name) |> String.concat ", ") names id id
    let updateQuery = sprintf "UPDATE %s SET %s WHERE %s=@%s" names (fields |> Array.map (fun f -> f.name + " = @" + f.name) |> String.concat ", ") id id
    let insertQuery = sprintf "INSERT INTO %s(%s) VALUES (%s)" names (fields |> Array.map (fun f -> f.name) |> String.concat ", ") (fields |> Array.map (fun f -> "@" + f.name) |> String.concat ", ")
    let deleteQuery = sprintf "DELETE FROM %s WHERE %s=@%s" names id id

    sprintf """namespace %s

open Database
open Microsoft.Data.Sqlite
open System.Threading.Tasks

module Database =
  let getAll connectionString : Task<Result<%s seq, exn>> =
    task {
      use connection = new SqliteConnection(connectionString)
      return! query connection "%s" None
    }

  let getById connectionString id : Task<Result<%s option, exn>> =
    task {
      use connection = new SqliteConnection(connectionString)
      return! querySingle connection "%s" (Some <| dict ["id" => id])
    }

  let update connectionString v : Task<Result<int,exn>> =
    task {
      use connection = new SqliteConnection(connectionString)
      return! execute connection "%s" v
    }

  let insert connectionString v : Task<Result<int,exn>> =
    task {
      use connection = new SqliteConnection(connectionString)
      return! execute connection "%s" v
    }

  let delete connectionString id : Task<Result<int,exn>> =
    task {
      use connection = new SqliteConnection(connectionString)
      return! execute connection "%s" (dict ["id" => id])
    }

"""  names name getAllQuery name getByIdQuery updateQuery insertQuery deleteQuery

let generateView name names (fields : Parameter []) =

    let tableHeader =
        fields
        |> Seq.map (fun f -> sprintf "th [] [encodedText \"%s\"]" (upper f.name) )
        |> String.concat "\n              "

    let tableContent =
        fields
        |> Seq.map (fun f -> sprintf "td [] [encodedText (string o.%s)]" f.name)
        |> String.concat "\n                "

    let viewItemContent =
        fields
        |> Seq.map (fun f -> sprintf "li [] [ strong [] [encodedText \"%s: \"]; encodedText (string o.%s) ]" (upper f.name) f.name )
        |> String.concat "\n          "

    let formContetn =
        fields
        |> Seq.map (fun f -> sprintf """yield field (fun i -> (string i.%s)) "%s" "%s" """ f.name (upper f.name) f.name)
        |> String.concat "\n          "

    sprintf """namespace %s

open Microsoft.AspNetCore.Http
open Giraffe.ViewEngine
open Saturn

module Views =
  let index (ctx : HttpContext) (objs : %s list) =
    let cnt = [
      div [_class "container "] [
        h2 [ _class "title"] [encodedText "Listing %s"]

        table [_class "table is-hoverable is-fullwidth"] [
          thead [] [
            tr [] [
              %s
              th [] []
            ]
          ]
          tbody [] [
            for o in objs do
              yield tr [] [
                %s
                td [] [
                  a [_class "button is-text"; _href (Links.withId ctx o.id )] [encodedText "Show"]
                  a [_class "button is-text"; _href (Links.edit ctx o.id )] [encodedText "Edit"]
                  a [_class "button is-text is-delete"; attr "data-href" (Links.withId ctx o.id ) ] [encodedText "Delete"]
                ]
              ]
          ]
        ]

        a [_class "button is-text"; _href (Links.add ctx )] [encodedText "New %s"]
      ]
    ]
    App.layout ([section [_class "section"] cnt])


  let show (ctx : HttpContext) (o : %s) =
    let cnt = [
      div [_class "container "] [
        h2 [ _class "title"] [encodedText "Show %s"]

        ul [] [
          %s
        ]
        a [_class "button is-text"; _href (Links.edit ctx o.id)] [encodedText "Edit"]
        a [_class "button is-text"; _href (Links.index ctx )] [encodedText "Back"]
      ]
    ]
    App.layout ([section [_class "section"] cnt])

  let private form (ctx: HttpContext) (o: %s option) (validationResult : Map<string, string>) isUpdate =
    let validationMessage =
      div [_class "notification is-danger"] [
        a [_class "delete"; attr "aria-label" "delete"] []
        encodedText "Oops, something went wrong! Please check the errors below."
      ]

    let field selector lbl key =
      div [_class "field"] [
        yield label [_class "label"] [encodedText (string lbl)]
        yield div [_class "control has-icons-right"] [
          yield input [_class (if validationResult.ContainsKey key then "input is-danger" else "input"); _value (defaultArg (o |> Option.map selector) ""); _name key ; _type "text" ]
          if validationResult.ContainsKey key then
            yield span [_class "icon is-small is-right"] [
              i [_class "fas fa-exclamation-triangle"] []
            ]
        ]
        if validationResult.ContainsKey key then
          yield p [_class "help is-danger"] [encodedText validationResult.[key]]
      ]

    let buttons =
      div [_class "field is-grouped"] [
        div [_class "control"] [
          input [_type "submit"; _class "button is-link"; _value "Submit"]
        ]
        div [_class "control"] [
          a [_class "button is-text"; _href (Links.index ctx)] [encodedText "Cancel"]
        ]
      ]

    let cnt = [
      div [_class "container "] [
        form [ _action (if isUpdate then Links.withId ctx o.Value.id else Links.index ctx ); _method "post"] [
          if not validationResult.IsEmpty then
            yield validationMessage
          %s
          yield buttons
        ]
      ]
    ]
    App.layout ([section [_class "section"] cnt])

  let add (ctx: HttpContext) (o: %s option) (validationResult : Map<string, string>)=
    form ctx o validationResult false

  let edit (ctx: HttpContext) (o: %s) (validationResult : Map<string, string>) =
    form ctx (Some o) validationResult true
"""    names name names tableHeader tableContent name name name viewItemContent name formContetn name name

let generateViewsController (name: string) (names : string) (_ : Parameter []) =
  sprintf """namespace %s

open Microsoft.AspNetCore.Http
open Config
open Saturn

module Controller =

  let indexAction (ctx : HttpContext) =
    task {
      let cnf = Controller.getConfig ctx
      let! result = Database.getAll cnf.connectionString
      match result with
      | Ok result ->
        return! Controller.renderHtml ctx (Views.index ctx (List.ofSeq result))
      | Error ex ->
        return raise ex
    }

  let showAction (ctx: HttpContext) (id : string) =
    task {
      let cnf = Controller.getConfig ctx
      let! result = Database.getById cnf.connectionString id
      match result with
      | Ok (Some result) ->
        return! Controller.renderHtml ctx (Views.show ctx result)
      | Ok None ->
        return! Controller.renderHtml ctx (NotFound.layout)
      | Error ex ->
        return raise ex
    }

  let addAction (ctx: HttpContext) =
    task {
      return! Controller.renderHtml ctx (Views.add ctx None Map.empty)
    }

  let editAction (ctx: HttpContext) (id : string) =
    task {
      let cnf = Controller.getConfig ctx
      let! result = Database.getById cnf.connectionString id
      match result with
      | Ok (Some result) ->
        return! Controller.renderHtml ctx (Views.edit ctx result Map.empty)
      | Ok None ->
        return! Controller.renderHtml ctx (NotFound.layout)
      | Error ex ->
        return raise ex
    }

  let createAction (ctx: HttpContext) =
    task {
      let! input = Controller.getModel<%s> ctx
      let validateResult = Validation.validate input
      if validateResult.IsEmpty then

        let cnf = Controller.getConfig ctx
        let! result = Database.insert cnf.connectionString input
        match result with
        | Ok _ ->
          return! Controller.redirect ctx (Links.index ctx)
        | Error ex ->
          return raise ex
      else
        return! Controller.renderHtml ctx (Views.add ctx (Some input) validateResult)
    }

  let updateAction (ctx: HttpContext) (id : string) =
    task {
      let! input = Controller.getModel<%s> ctx
      let validateResult = Validation.validate input
      if validateResult.IsEmpty then
        let cnf = Controller.getConfig ctx
        let! result = Database.update cnf.connectionString input
        match result with
        | Ok _ ->
          return! Controller.redirect ctx (Links.index ctx)
        | Error ex ->
          return raise ex
      else
        return! Controller.renderHtml ctx (Views.edit ctx input validateResult)
    }

  let deleteAction (ctx: HttpContext) (id : string) =
    task {
      let cnf = Controller.getConfig ctx
      let! result = Database.delete cnf.connectionString id
      match result with
      | Ok _ ->
        return! Controller.redirect ctx (Links.index ctx)
      | Error ex ->
        return raise ex
    }

  let resource = controller {
    index indexAction
    show showAction
    add addAction
    edit editAction
    create createAction
    update updateAction
    delete deleteAction
  }

"""      names name name

let generateJsonController (name: string) (names : string) (_ : Parameter []) =
    sprintf """namespace %s

open Microsoft.AspNetCore.Http
open Config
open Saturn

module Controller =

  let indexAction (ctx : HttpContext) =
    task {
      let cnf = Controller.getConfig ctx
      let! result = Database.getAll cnf.connectionString
      match result with
      | Ok result ->
        return result
      | Error ex ->
        return raise ex
    }

  let showAction (ctx: HttpContext) (id : string) =
    task {
      let cnf = Controller.getConfig ctx
      let! result = Database.getById cnf.connectionString id
      match result with
      | Ok (Some result) ->
        return! Response.ok ctx result
      | Ok None ->
        return! Response.notFound ctx "Value not found"
      | Error ex ->
        return raise ex
    }

  let createAction (ctx: HttpContext) =
    task {
      let! input = Controller.getModel<%s> ctx
      let validateResult = Validation.validate input
      if validateResult.IsEmpty then

        let cnf = Controller.getConfig ctx
        let! result = Database.insert cnf.connectionString input
        match result with
        | Ok _ ->
          return! Response.ok ctx ""
        | Error ex ->
          return raise ex
      else
        return! Response.badRequest ctx "Validation failed"
    }

  let updateAction (ctx: HttpContext) (id : string) =
    task {
      let! input = Controller.getModel<%s> ctx
      let validateResult = Validation.validate input
      if validateResult.IsEmpty then
        let cnf = Controller.getConfig ctx
        let! result = Database.update cnf.connectionString input
        match result with
        | Ok _ ->
          return! Response.ok ctx ""
        | Error ex ->
          return raise ex
      else
        return! Response.badRequest ctx "Validation failed"
    }

  let deleteAction (ctx: HttpContext) (id : string) =
    task {
      let cnf = Controller.getConfig ctx
      let! result = Database.delete cnf.connectionString id
      match result with
      | Ok _ ->
        return! Response.ok ctx ""
      | Error ex ->
        return raise ex
    }

  let resource = controller {
    index indexAction
    show showAction
    create createAction
    update updateAction
    delete deleteAction
  }

"""  names name name


let generateMigration (name: string) (names : string) (fields : Parameter []) =
    let dir = projDirPath </> ".." </> "Migrations"
    let fsproj = Directory.GetFiles(dir, "*.fsproj", SearchOption.TopDirectoryOnly).[0]
    let id = sprintf "%i%02i%02i%02i%02i" DateTime.Now.Year DateTime.Now.Month DateTime.Now.Day DateTime.Now.Hour DateTime.Now.Minute
    let fn = sprintf "%s.%s.fs" id name
    let fields = fields |> Array.map (fun f -> sprintf "%s %s NOT NULL" f.name f.DbType) |> String.concat ",\n      "
    let content =
        sprintf """namespace Migrations
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
"""      id names names names fields names


    Files.generateFile(dir </> fn, content)
    let ctn =
        File.ReadAllLines fsproj
        |> Seq.map (fun f -> if f.Trim().StartsWith """<Compile Include="Program.fs" />""" then sprintf "    <Compile Include=\"%s\" />\n%s" fn f else f  )
        |> String.concat "\n"
    Files.updateFile(fsproj, ctn)


    ()

let generateHtml (name : string) (names : string) (fields : Parameter []) =
    let dir = projDirPath </> names
    Directory.CreateDirectory(dir) |> ignore
    let modelFn = (sprintf "%sModel.fs" names)
    let viewsFn = (sprintf "%sViews.fs" names)
    let controllerFn = (sprintf "%sController.fs" names)
    let repositoryFn = (sprintf "%sRepository.fs" names)

    Files.generateFile(dir </> modelFn, generateModel name names fields)
    Files.generateFile(dir </> viewsFn,  generateView name names fields)
    Files.generateFile(dir </> repositoryFn, generateRepository name names fields)
    Files.generateFile(dir </> controllerFn,  generateViewsController name names fields)


    let ctn =
        File.ReadAllLines fsProjPath
        |> Seq.map (fun f -> if f.Trim().StartsWith """<Compile Include="Router.fs" />""" then sprintf "    <Compile Include=\"%s\\%s\" />\n    <Compile Include=\"%s\\%s\" />\n    <Compile Include=\"%s\\%s\" />\n    <Compile Include=\"%s\\%s\" />\n%s" names modelFn names viewsFn names repositoryFn names controllerFn f  else f  )
        |> String.concat "\n"
    Files.updateFile(fsProjPath, ctn)

    generateMigration name names fields

    printfn """
Controller generated. You need to add new controller to one of the routers in Router.fs file with path you want.

For example:

    forward "/%s" %s.Controller.resource


"""   (names.ToLower()) names


    ()


let generateJson (name : string) (names : string) (fields : Parameter []) =
    let dir = projDirPath </> names
    Directory.CreateDirectory(dir) |> ignore

    let modelFn = (sprintf "%sModel.fs" names)
    let controllerFn = (sprintf "%sController.fs" names)
    let repositoryFn = (sprintf "%sRepository.fs" names)


    Files.generateFile(dir </> modelFn, generateModel name names fields)
    Files.generateFile(dir </> repositoryFn, generateRepository name names fields)
    Files.generateFile(dir </> controllerFn, generateJsonController name names fields)

    let ctn =
        File.ReadAllLines fsProjPath
        |> Seq.map (fun f -> if f.Trim().StartsWith """<Compile Include="Router.fs" />""" then sprintf "    <Compile Include=\"%s\\%s\" />\n     <Compile Include=\"%s\\%s\" />\n     <Compile Include=\"%s\\%s\" />\n%s" names modelFn names repositoryFn names controllerFn f  else f  )
        |> String.concat "\n"
    Files.updateFile(fsProjPath, ctn)


    generateMigration name names fields

    printfn """
Controller generated. You need to add new controller to one of the routers in Router.fs file with path you want.

For example:

    forward "/%s" %s.Controller.resource


"""   (names.ToLower()) names

    ()

let generateMdl (name : string) (names : string) (fields : Parameter []) =
    let dir = projDirPath </> names
    Directory.CreateDirectory(dir) |> ignore

    let modelFn = (sprintf "%sModel.fs" names)
    let repositoryFn = (sprintf "%sRepository.fs" names)

    Files.generateFile(dir </> modelFn, generateModel name names fields)
    Files.generateFile(dir </> repositoryFn, generateRepository name names fields)


    let ctn =
        File.ReadAllLines fsProjPath
        |> Seq.map (fun f -> if f.Trim().StartsWith """<Compile Include="Router.fs" />""" then sprintf "    <Compile Include=\"%s\\%s\" />\n    <Compile Include=\"%s\\%s\" />\n%s" names modelFn names repositoryFn  f  else f  )
        |> String.concat "\n"
    Files.updateFile(fsProjPath, ctn)


    generateMigration name names fields

    ()

let generateGraphQL (name : string) (names : string) (fields : Parameter []) =
    ()
