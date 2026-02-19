# Architecture Visual Guide

Visual diagrams and illustrations for understanding Jaunty's architecture.

---

## System Architecture Overview

```mermaid
flowchart TB
    subgraph User["User Code"]
        UC["IDbConnection.Query<T>()"]
    end
    
    subgraph API["Public API Layer"]
        A1["Query<T>()"]
        A2["QueryPartial<T>()"]
        A3["Insert<T>()"]
        A4["Update<T>()"]
        A5["Delete<T>()"]
    end
    
    subgraph Core["Core Execution"]
        C1["QueryCore"]
        C2["ExecuteReader"]
        C3["DrDispatcher"]
    end
    
    subgraph Metadata["Metadata System"]
        M1["MetadataCache<T>"]
        M2["MetadataBuilder"]
        M3["Compiled Setters"]
    end
    
    subgraph Params["Parameter System"]
        P1["SqlParameterParser"]
        P2["ParameterBinder"]
        P3["ParameterCache"]
    end
    
    subgraph DB["Database"]
        D1["DbConnection"]
        D2["DbCommand"]
        D3["DbDataReader"]
    end
    
    UC --> API
    API --> Core
    Core --> Metadata
    Core --> Params
    Core --> DB
    
    style User fill:#9cf
    style API fill:#e1f5ff
    style Core fill:#fff4e1
    style Metadata fill:#f0e1ff
    style Params fill:#e1ffe1
    style DB fill:#ffe1e1
```

---

## Query Execution Lifecycle

```mermaid
sequenceDiagram
    participant User
    participant API
    participant Core
    participant Meta
    participant Param
    participant DB
    
    User->>API: Query<Product>(sql, params)
    
    Note over API: Validate arguments<br/>Check connection
    
    API->>Core: QueryCore<T>(sql, params)
    
    Note over Core: Manage connection state<br/>Open if closed
    
    Core->>Param: Extract & bind parameters
    
    Note over Param: Parse SQL for @params<br/>Bind to DbCommand
    
    Param->>DB: ExecuteReader()
    
    Note over DB: Execute SQL<br/>Return DataReader
    
    DB->>Core: IDataReader
    
    Core->>Meta: DrDispatcher.Resolve<T>()
    
    Note over Meta: Check custom mapper<br/>Check special types<br/>Build from MetadataCache
    
    Meta->>Core: Func<IDataReader, T>
    
    Core->>Core: while reader.Read()<br/>map(reader)
    
    Core->>API: List<T>
    
    API->>User: List<T>
    
    Note over User: Receive mapped entities
```

---

## Metadata Initialization Flow

```mermaid
flowchart TD
    Start["First Query<Product>()"] --> Check{"MetadataCache<br/>initialized?"}
    
    Check -->|No| Static["Static constructor runs"]
    Check -->|Yes| Use["Use cached metadata"]
    
    Static --> Build["MetadataBuilder.Build<T>()"]
    Build --> Attr["Read [Table], [Column] attributes"]
    Attr --> Config["Apply JauntyConfig resolvers"]
    Config --> Entity["Create EntityMetadata"]
    
    Entity --> Loop["For each property"]
    Loop --> Expr["Build expression tree"]
    Expr --> Compile["Compile to delegate"]
    Compile --> Context["Create PropertyContext<T>"]
    Context --> Dict["Add to ColumnToIndex"]
    Dict --> More{"More properties?"}
    More -->|Yes| Loop
    More --> Freeze
    
    Freeze{"NET8_0_OR_GREATER?"} -->|Yes| Frozen["ToFrozenDictionary()"]
    Freeze -->|No| Dict2["ToDictionary()"]
    
    Frozen --> Complete["MetadataCache<T> ready"]
    Dict2 --> Complete
    
    Complete --> Use
    
    Use --> Query["Execute query"]
    Query --> Return["Return List<T>"]
    
    style Start fill:#9cf
    style Complete fill:#9f9
    style Return fill:#9cf
    style Expr fill:#ff9
    style Compile fill:#ff9
```

---

## Parameter Binding Flow

```mermaid
flowchart LR
    SQL["SQL with @params"] --> Parse["SqlParameterParser"]
    
    subgraph Parse ["Extraction"]
        P1["State machine<br/>Skip comments, strings"]
        P2["Extract @param names"]
        P3["Deduplicate"]
    end
    
    Parse --> Cache["SqlParameterParserCache"]
    Cache --> Validate{"Validate count"}
    
    Validate -->|Mismatch| Throw["Throw ArgumentException"]
    Validate -->|Match| Bind["ParameterBinder"]
    
    Bind --> Props["Get object properties"]
    Props --> Loop["For each property"]
    Loop --> Create["Create DbParameter"]
    Create --> SetVal["Set Value or DBNull"]
    SetVal --> Add["Add to DbCommand"]
    
    Add --> Exec["Execute command"]
    Throw --> Error["Error returned"]
    
    style SQL fill:#9cf
    style Parse fill:#ff9
    style Cache fill:#ff9
    style Bind fill:#f96
    style Exec fill:#9f9
    style Throw fill:#f99
```

---

## Connection State Management

```mermaid
stateDiagram-v2
    [*] --> CheckState
    
    state CheckState {
        [*] --> WasClosed: connection.State
        WasClosed --> OpenIfNeeded: true
        WasClosed --> ExecuteDirect: false
    }
    
    OpenIfNeeded --> Open: connection.Open()
    Open --> Execute
    
    ExecuteDirect --> Execute
    
    state Execute {
        [*] --> CreateCommand
        CreateCommand --> BindParams
        BindParams --> ExecuteReader
        ExecuteReader --> ReadResults
        ReadResults --> [*]
    }
    
    Execute --> CheckClose
    
    state CheckClose {
        [*] --> ShouldClose: wasClosed && stillOpen
        ShouldClose --> Close: true
        ShouldClose --> LeaveOpen: false
    }
    
    Close --> [*]: connection.Close()
    LeaveOpen --> [*]
    
    note right of CheckState
        Jaunty respects connection state:
        - Opens if closed
        - Closes if it opened
        - Leaves open if already open
    end note
```

---

## Mapper Resolution Priority

```mermaid
flowchart TD
    Start["DrDispatcher.Resolve<T>()"] --> Check1{"options.Mapper<br/>not null?"}
    
    Check1 -->|Yes| UseOpt["Use options.Mapper"]
    Check1 -->|No| Check2{"Special type?<br/>Dictionary, ExpandoObject"}
    
    Check2 -->|Yes| UseSpec["Use special mapper"]
    Check2 -->|No| Check3{"MappedCache<T>.Mapper<br/>not null?"}
    
    Check3 -->|Yes| UseIMapped["Use IMapped<T> mapper"]
    Check3 -->|No| Check4{"MetadataCache<T><br/>available?"}
    
    Check4 -->|Yes| Build["Build from MetadataCache"]
    Check4 -->|No| Error["Throw: no mapper available"]
    
    Build --> GetSetters["MetadataCache<T>.GetSetters()"]
    GetSetters --> CreateFunc["Create mapper function"]
    
    UseOpt --> Return["Return Func<IDataReader, T>"]
    UseSpec --> Return
    UseIMapped --> Return
    CreateFunc --> Return
    
    style Start fill:#9cf
    style Return fill:#9f9
    style Error fill:#f99
    style Check1 fill:#ff9
    style Check2 fill:#ff9
    style Check3 fill:#ff9
```

---

## Performance Hot Paths

```mermaid
flowchart LR
    subgraph Cold["Cold Path (One-time)"]
        C1["Metadata build"]
        C2["Expression compilation"]
        C3["Dictionary creation"]
    end
    
    subgraph Hot["Hot Path (Per-query)"]
        H1["Dictionary lookup O(1)"]
        H2["Delegate invocation"]
        H3["Property setting"]
    end
    
    subgraph Allocations["Allocations"]
        A1["Entity instances"]
        A2["List<T>"]
        A3["Minimal (cached rest)"]
    end
    
    Cold --> Hot
    Hot --> Allocations
    
    style Cold fill:#ff9
    style Hot fill:#9f9
    style Allocations fill:#9cf
```

**Cold Path** (runs once per type):
- Metadata building: ~1-5ms
- Expression compilation: ~1-10ms
- Dictionary creation: ~0.1-1ms

**Hot Path** (runs per query):
- Dictionary lookup: ~10ns
- Delegate invocation: ~50ns
- Property setting: ~100ns per property

---

## Error Flow

```mermaid
flowchart TD
    Start["Query execution"] --> Error{"Error type?"}
    
    Error --> ArgNull["ArgumentNullException"]
    Error --> ArgInvalid["ArgumentException"]
    Error --> InvalidOp["InvalidOperationException"]
    Error --> DbEx["DbException"]
    
    ArgNull --> Msg1["Null required argument"]
    ArgInvalid --> Msg2["Parameter mismatch<br/>Invalid parameter type"]
    InvalidOp --> Msg3["Strict mapping failure<br/>No results / Multiple results<br/>NULL to non-nullable"]
    DbEx --> Msg4["Database error<br/>SQL syntax error"]
    
    Msg1 --> Format["Format error message<br/>with context"]
    Msg2 --> Format
    Msg3 --> Format
    Msg4 --> PassThrough["Pass through as-is"]
    
    Format --> Throw["Throw exception"]
    PassThrough --> Throw
    
    style Start fill:#9cf
    style Throw fill:#f99
    style Format fill:#ff9
    style PassThrough fill:#ff9
```

---

## Multi-Targeting Architecture

```mermaid
flowchart TB
    Source["Source Code"] --> Check{"#if NET8_0_OR_GREATER?"}
    
    Check -->|Yes| Modern["Modern APIs"]
    Check -->|No| Compat["Compatible APIs"]
    
    subgraph Modern ["net8.0 Optimizations"]
        M1["FrozenDictionary<K,V>"]
        M2["ValueTask<T>"]
        M3["string.Create()"]
        M4["Span<T>/ReadOnlySpan<T>"]
        M5["SearchValues<T>"]
    end
    
    subgraph Compat ["netstandard2.0 Compatibility"]
        C1["Dictionary<K,V>"]
        C2["Task<T>"]
        C3["String concatenation"]
        C4["Substring()"]
        C5["Manual character search"]
    end
    
    Modern --> Build8["Build net8.0"]
    Compat --> BuildStd["Build netstandard2.0"]
    
    Build8 --> Pkg8["Jaunty.dll (net8.0)"]
    BuildStd --> PkgStd["Jaunty.dll (netstandard2.0)"]
    
    Pkg8 --> NuGet["Beparey.Jaunty NuGet package"]
    PkgStd --> NuGet
    
    style Source fill:#9cf
    style Check fill:#ff9
    style Modern fill:#9f9
    style Compat fill:#ff9
    style NuGet fill:#9cf
```

---

## See Also

| Document | Purpose |
|----------|---------|
| [`architecture-specification.md`](architecture-specification.md) | Complete architecture spec |
| [`metadata-system-spec.md`](metadata-system-spec.md) | Metadata system details |
| [`parameter-binding-spec.md`](parameter-binding-spec.md) | Parameter binding details |
| [`performance-spec.md`](performance-spec.md) | Performance guide |
