﻿namespace StaticQL.Mapping
open System
open System.Data
open System.Collections.Generic
open StaticQL.Mapping.CodeGeneration

type CommandFragment =
    /// A name which should be localized to this command for batching.
    /// For example, if the command creates a temp table, the real name should be chosen dynamically
    /// so it doesn't break when the command is batched with others that create the same-named temp table.
    | LocalName of string
    /// Chunk of raw SQL text.
    | CommandText of string
    /// References parameter by index.
    | Parameter of int
    /// At least one unit of whitespace.
    | Whitespace

[<AbstractClass>]
type Command(fragments : CommandFragment IReadOnlyList, parameters : (obj * DbType) IReadOnlyList) =
    member __.Fragments = fragments
    member __.Parameters = parameters
    
    /// The number of result sets this command will return, if it can be statically determined.
    abstract member ResultSetCount : int option
    default __.ResultSetCount = None
    /// Start processing a result set.
    abstract member BeginResultSet : IDataReader -> unit
    /// Process a single row of the result set.
    abstract member ProcessRow : unit -> unit
    /// Obtain the result object after processing *all* result sets.
    abstract member GetResultObject : unit -> obj

/// Represents multiple result sets as the output from a single command.
[<AbstractClass>]
type ResultSets() =
    abstract member AllResultSets : obj seq

type ResultSets<'a, 'b>(a : 'a, b : 'b) =
    inherit ResultSets()
    member __.ResultSet1 = a
    member __.ResultSet2 = b
    override __.AllResultSets =
        Seq.ofArray [| box a; box b |]

type ResultSets<'a, 'b, 'c>(a : 'a, b : 'b, c : 'c) =
    inherit ResultSets()
    member __.ResultSet1 = a
    member __.ResultSet2 = b
    member __.ResultSet3 = c
    override __.AllResultSets =
        Seq.ofArray [| box a; box b; box c |]

/// A command which can be expected to produce `'output` when run.
[<AbstractClass>]
type Command<'output>(fragments, parameters) =
    inherit Command(fragments, parameters)
    abstract member GetResult : unit -> 'output

type private Command0<'a>(fragments, parameters) =
    inherit Command<'a>(fragments, parameters)
    override __.BeginResultSet(_) = ()
    override __.ProcessRow() = ()
    override __.GetResultObject() = upcast Unchecked.defaultof<'a>
    override __.GetResult() = Unchecked.defaultof<'a>

type private Command1<'a>(fragments, parameters) =
    inherit Command<'a>(fragments, parameters)
    let reader = ReaderTemplate<'a>.Template().CreateReader()
    let mutable row = Unchecked.defaultof<Row>
    let result = lazy reader.ToEntity()
    override __.BeginResultSet(dataReader) =
        reader.ProcessColumns(DataReader.columnMap(dataReader))
        row <- DataReader.DataReaderRow(dataReader)
    override __.ProcessRow() =
        reader.Read(row)
    override __.GetResultObject() = upcast result.Value
    override __.GetResult() = result.Value

type private Command2<'a, 'b>(fragments, parameters) =
    inherit Command<ResultSets<'a, 'b>>(fragments, parameters)
    let aReader = ReaderTemplate<'a>.Template().CreateReader()
    let bReader = ReaderTemplate<'b>.Template().CreateReader()
    let mutable row = Unchecked.defaultof<Row>
    let mutable readers =
        [
            Unchecked.defaultof<EntityReader>
            aReader :> EntityReader
            bReader :> EntityReader
        ]
    let result = lazy ResultSets<'a, 'b>(aReader.ToEntity(), bReader.ToEntity())
    override __.BeginResultSet(dataReader) =
        readers <- List.tail readers
        (List.head readers).ProcessColumns(DataReader.columnMap(dataReader))
        row <- DataReader.DataReaderRow(dataReader)
    override __.ProcessRow() =
        (List.head readers).Read(row)
    override __.GetResultObject() = upcast result.Value
    override __.GetResult() = result.Value

type private Command3<'a, 'b, 'c>(fragments, parameters) =
    inherit Command<ResultSets<'a, 'b, 'c>>(fragments, parameters)
    let aReader = ReaderTemplate<'a>.Template().CreateReader()
    let bReader = ReaderTemplate<'b>.Template().CreateReader()
    let cReader = ReaderTemplate<'c>.Template().CreateReader()
    let mutable row = Unchecked.defaultof<Row>
    let mutable readers =
        [
            Unchecked.defaultof<EntityReader>
            aReader :> EntityReader
            bReader :> EntityReader
            cReader :> EntityReader
        ]
    let result = lazy ResultSets<'a, 'b, 'c>(aReader.ToEntity(), bReader.ToEntity(), cReader.ToEntity())
    override __.BeginResultSet(dataReader) =
        readers <- List.tail readers
        (List.head readers).ProcessColumns(DataReader.columnMap(dataReader))
        row <- DataReader.DataReaderRow(dataReader)
    override __.ProcessRow() =
        (List.head readers).Read(row)
    override __.GetResultObject() = upcast result.Value
    override __.GetResult() = result.Value

type CommandConstructor() =
    static member Command0(fragments, parameters) =
        new Command0<unit>(fragments, parameters) :> _ Command
    static member Command1<'a>(fragments, parameters) =
        new Command1<'a>(fragments, parameters) :> _ Command
    static member Command2<'a, 'b>(fragments, parameters) =
        new Command2<'a, 'b>(fragments, parameters) :> _ Command
    static member Command3<'a, 'b, 'c>(fragments, parameters) =
        new Command3<'a, 'b, 'c>(fragments, parameters) :> _ Command