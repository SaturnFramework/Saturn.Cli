module Strings

open System

let upper (s: string) =
    s |> Seq.mapi (fun i c -> match i with | 0 -> (Char.ToUpper(c)) | _ -> c)  |> String.Concat
