# Mermaid sampler

A single note exercising seven of the Mermaid diagram types that Slate's K-milestone pipeline supports. Each diagram is preceded by a one-sentence framing so the structured-description has context to work from, and every diagram has a title where the syntax allows.

## flowchart

A minimal request/response flowchart with one decision point, useful as the baseline shape against which the other diagram types contrast.

```mermaid
%% Title: Cache-aware request handler
flowchart TD
    A["Request received"] --> B{"In cache?"}
    B -->|"Yes"| C["Return cached response"]
    B -->|"No"| D["Query upstream"]
    D --> E["Store in cache"]
    E --> F["Return fresh response"]
```

## sequenceDiagram

A short interaction between three participants showing the temporal ordering of an authenticated API call.

```mermaid
%% Title: Authenticated API request
sequenceDiagram
    autonumber
    participant C as Client
    participant A as Auth service
    participant S as API server

    C->>A: POST /token (credentials)
    A-->>C: 200 access_token
    C->>S: GET /resource (Bearer token)
    S->>A: Verify token
    A-->>S: token_valid
    S-->>C: 200 resource payload
```

## classDiagram

A small class hierarchy for a notional document model, showing inheritance, composition, and a couple of method signatures.

```mermaid
%% Title: Document model
classDiagram
    class Document {
        +String id
        +String title
        +DateTime created
        +render() String
    }
    class TextDocument {
        +String body
        +wordCount() int
    }
    class CodeDocument {
        +String language
        +String source
        +highlight() String
    }
    Document <|-- TextDocument
    Document <|-- CodeDocument
    Document "1" o-- "many" Tag
    class Tag {
        +String name
    }
```

## stateDiagram-v2

The lifecycle of a single editable note, showing the transitions between unsaved, saved, and conflict states.

```mermaid
%% Title: Note edit lifecycle
stateDiagram-v2
    [*] --> Clean
    Clean --> Dirty: edit
    Dirty --> Saving: save command
    Saving --> Clean: write succeeded
    Saving --> Conflict: remote changed
    Conflict --> Saving: merge resolved
    Conflict --> Dirty: discard remote
    Clean --> [*]
```

## erDiagram

The minimal relational schema behind a notes app: notes, tags, and the many-to-many join between them.

```mermaid
%% Title: Notes and tags schema
erDiagram
    NOTE ||--o{ NOTE_TAG : "tagged with"
    TAG  ||--o{ NOTE_TAG : "applied to"
    NOTE {
        string id PK
        string title
        string body
        datetime created_at
        datetime updated_at
    }
    TAG {
        string id PK
        string name UK
    }
    NOTE_TAG {
        string note_id FK
        string tag_id FK
    }
```

## gantt

A short delivery timeline for the milestones in the current development sprint.

```mermaid
%% Title: Sprint timeline
gantt
    title Sprint 14 — citations and code highlighting
    dateFormat  YYYY-MM-DD
    axisFormat  %m-%d

    section Citations
    Pandoc parser           :done,    p1, 2026-05-04, 5d
    hayagriva render        :active,  p2, 2026-05-11, 7d
    CSL style switching     :         p3, after p2, 4d

    section Code highlighting
    Tree-sitter integration :done,    c1, 2026-05-04, 6d
    Semantic span emission  :active,  c2, 2026-05-12, 6d
    Syntect fallback        :         c3, after c2, 3d
```

## pie

A breakdown of where the past week's engineering time actually went, useful mostly as a sanity check on the calendar.

```mermaid
%% Title: Engineering time, past week
pie title Engineering time, past week
    "Citations work"        : 42
    "Code highlighting"     : 23
    "Bug fixes"             : 15
    "Code review"           : 12
    "Meetings"              : 8
```
