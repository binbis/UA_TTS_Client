# UA_TTS_Client documentation

> **Purpose:** the index of the project documentation: what to read, for whom, and where each fact lives.
> **Audience:** users, developers and AI agents.
> **Source of truth:** this index. Each topic is owned by the file named in the table below.
> **Verified:** 2026-11-03.

## Where to start

| You are | Read first | Then |
|---|---|---|
| User | [`../README.md`](../README.md) | [Installation](user/Installation.md), [User guide](user/User_Guide.md), [Troubleshooting](user/Troubleshooting.md) |
| Developer | [Build and run](development/Build_and_Run.md) | [Architecture overview](architecture/Overview.md), [Project structure](architecture/Project_Structure.md), [Extension points](development/Extension_Points.md), [Code standards](Code_Standards_Prompt.md) |
| AI agent | [`../AGENTS.md`](../AGENTS.md) | [Project structure](architecture/Project_Structure.md), [Flows](architecture/Flows.md), [Pitfalls](reference/Pitfalls.md), then the owner file from the table below |

## All documents

| Path | Contents |
|---|---|
| `README.md` | Project entry point: what it is, quick start, links |
| `AGENTS.md` | Entry point for AI agents: rules, commands, short list of pitfalls |
| `Doc/Roadmap.md` | Backlog, out-of-scope decisions, open items |
| `Doc/Code_Standards_Prompt.md` | Coding standards: C#, Avalonia, MVVM, DI, documentation |
| `Doc/user/Installation.md` | Requirements and installation of the server and the client, Linux and Windows |
| `Doc/user/User_Guide.md` | Interface, Quick TTS, `Alt+Q`, batches, export, settings, glossary |
| `Doc/user/Configuration.md` | `config.json`: fields, defaults, location |
| `Doc/user/Troubleshooting.md` | Known limitations and typical problems |
| `Doc/architecture/Overview.md` | Components, layers, threads, platforms, key design choices |
| `Doc/architecture/Flows.md` | Startup, hotkey, playback queue, export, settings: order of operations |
| `Doc/architecture/Text_Chunking.md` | Text split modes and the exact algorithm |
| `Doc/architecture/Project_Structure.md` | Code map: files, contracts, implementations, composition root |
| `Doc/server/Server.md` | Python server: startup, synthesis pipeline, data files, environment |
| `Doc/server/API.md` | HTTP contract of the server with examples |
| `Doc/reference/Native_Libraries.md` | Verified APIs: ManagedBass, SharpHook, TextCopy, wl-paste |
| `Doc/reference/UI_and_DI.md` | Verified facts: Avalonia, CommunityToolkit.Mvvm, DI |
| `Doc/reference/Pitfalls.md` | Pitfalls with symptoms and rules |
| `Doc/development/Build_and_Run.md` | Commands, project facts, verification approach, reference environment |
| `Doc/development/Extension_Points.md` | Recipes: new service, setting, tab, endpoint, export format, chunking mode |
| `Doc/development/Manual_Test_Scenarios.md` | Manual verification checklist (no test project) |

## Single source of truth

| Topic | Owner |
|---|---|
| Agent rules and commands | `AGENTS.md` |
| Coding standards | `Doc/Code_Standards_Prompt.md` |
| Installation steps | `Doc/user/Installation.md` |
| UI behavior and labels | `Doc/user/User_Guide.md` |
| Config fields and defaults | `Doc/user/Configuration.md` |
| Limitations and problems | `Doc/user/Troubleshooting.md` |
| Architecture and order of operations | `Doc/architecture/Overview.md`, `Doc/architecture/Flows.md` |
| Chunking algorithm | `Doc/architecture/Text_Chunking.md` |
| Code map | `Doc/architecture/Project_Structure.md` |
| Server internals | `Doc/server/Server.md` |
| HTTP contract | `Doc/server/API.md` |
| Native library APIs | `Doc/reference/Native_Libraries.md` |
| UI and DI rules | `Doc/reference/UI_and_DI.md` |
| Pitfalls | `Doc/reference/Pitfalls.md` |
| Backlog and out of scope | `Doc/Roadmap.md` |

## Conventions

- Language: English. UI texts, log messages and error messages stay in Ukrainian, as they are in the code.
- Every document in `Doc/` starts with a header: `Purpose`, `Audience`, `Source of truth`, `Verified` (date). Update `Verified` when you check the content against the code.
- A fact lives in one place. Other documents link to it instead of copying it.
- Reference documents contain no step or iteration numbers and no change history. Open work goes to `Roadmap.md`; history is in `git log`.
- Code is referenced by its path from the repository root, for example `Infrastructure/Api/TtsApiClient.cs`, and is not copied. Signatures appear only for key contracts.
- Inline references to documents, such as `server/API.md`, are relative to `Doc/`. Markdown links are relative to the current file.
- One document covers one topic and stays under about 250 lines.
- When behavior changes, update the owner document in the same change: API to `server/API.md`, config to `user/Configuration.md`, UI to `user/User_Guide.md`, order of operations to `architecture/Flows.md`.
