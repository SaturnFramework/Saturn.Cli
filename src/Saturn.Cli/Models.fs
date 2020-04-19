module Models

type ParameterType =
  | String
  | Int
  | Float
  | Double
  | Decimal
  | Guid
  | DateTime
  | Bool
with
  static member TryParse (x: string) =
    match x.ToLower() with
    | "string" -> String
    | "int" -> Int
    | "float" -> Float
    | "double" -> Double
    | "decimal" -> Decimal
    | "guid" -> Guid
    | "datetime" -> DateTime
    | "bool" -> Bool
    | _ -> failwithf "Unsupported type - %s" x

type Parameter = {
  name : string
  typ : ParameterType
  nullable : bool
}
with
  member x.FSharpType =
    match x.typ with
    | String -> "string"
    | Int -> "int"
    | Float -> "float"
    | Double -> "double"
    | Decimal -> "decimal"
    | Guid -> "System.Guid"
    | DateTime -> "System.DateTime"
    | Bool -> "bool"

  member x.DbType =
    match x.typ with
    | String -> "TEXT"
    | Int -> "INT"
    | Float -> "FLOAT"
    | Double -> "DOUBLE"
    | Decimal -> "DECIMAL"
    | Guid -> "TEXT"
    | DateTime -> "DATETIME"
    | Bool -> "BOOLEAN"
