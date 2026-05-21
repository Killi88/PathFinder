# PathFinder

A WPF desktop application for XML/JSON/YAML editing with XPath/JSONPath/YAMLPath querying. Features a multi-tab editor with syntax highlighting, pretty-printing, hierarchical tree visualization, and a full-document path browser.

![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4) ![WPF](https://img.shields.io/badge/UI-WPF-blue) ![C# 12](https://img.shields.io/badge/C%23-12-239120)

---

## Features

- **Multi-tab editor** with syntax highlighting (XML, JSON, YAML, XSD, XSL, EDIFACT)
- **Customizable syntax colors** — pick your own highlighting colors per theme via Settings → Customize Colors
- **XPath / JSONPath / YAMLPath** query execution with result navigation and **query history** (Up/Down arrows)
- **Pretty-print** formatting for XML, JSON, YAML, and EDIFACT documents
- **Minify** (`Ctrl+Shift+M`) — compress XML, JSON, and EDIFACT to single-line format
- **Find & Replace** (`Ctrl+H`) — non-modal replace window with Find Previous/Next, Replace, Replace All; supports match case, whole words, regex, and **All documents** toggle for cross-tab replace
- **Go to Line** (`Ctrl+G`) — jump to a specific line number
- **Status bar line/column counter** — shows current cursor position (`Ln/Col`)
- **Recent Files** menu — quickly reopen the last 10 files
- **YAML support** — syntax highlighting (6 color categories: Key, Value, Comment, Anchor, Tag, DocMarker), dot-notation YAMLPath querying (`$.store.name`, `[0]` array indexing, `*` wildcard), indentation-based code folding, and content-based auto-detection
- **XML ↔ JSON ↔ YAML conversion** — convert the active document between any two formats in one click; result opens in a new tab
- **Generate Sample XML from XSD** — click **⚙ Sample XML** when an `.xsd` file is active to generate a skeleton XML document with representative sample values; result opens in a new tab
- **Generate Sample JSON from Schema** — click **⚙ Sample JSON** when a JSON Schema or OpenAPI spec is detected to generate a sample JSON document with representative values; supports `$ref` resolution, `allOf`/`oneOf`/`anyOf`, enum values, and format-specific strings
- **Validate XML against XSD** — click **✓ Validate XML** (XSD active) or **✓ Validate vs XSD** (XML active) to validate XML/XSD pairs; select the counterpart from an open tab or browse from disk; errors shown in the Messages panel with navigable line numbers
- **Schema visualization** — view XSD, JSON Schema, and OpenAPI specs as a **Schema Tree** (vertical node-box diagram with connecting lines, type-kind badges, restrictions, documentation); includes a **filter bar** for searching by name, type, documentation, or restriction values; zoomable via `Ctrl+Scroll` or `−`/`+` buttons; auto-detected for JSON/YAML schemas, always available for `.xsd` files
- **Autocomplete** — inline closing-tag suggestions for XML (`</`); press Tab to accept
- **XPath/JSONPath autocomplete** — as you type in the query box, a popup shows matching paths from the All Paths list
- **EDIFACT syntax validation** — real-time error detection with descriptive messages (segment number + problem)
- **EDIFACT definition-aware validation** — validates message structure (segment order, mandatory/optional, max occurrences, segment groups) and field values (mandatory, max length, data type, code lists) against the bundled UN/EDIFACT directory definitions (D96A–D10B)
- **EDIFACT segment tooltips** — hover over any segment tag to see a tooltip with the segment name and field definitions (ID, name, mandatory/conditional, data type, max length)
- **Messages panel** — validation errors and format failures displayed inline with click-to-navigate line support
- **All Paths browser** — enumerate every element, attribute, and value in a document with filtering
- **Hierarchical tree view** — toggle between text and grid view per tab
- **Copy for Excel** — syntax-highlighted HTML clipboard export (XML, JSON, YAML)
- **DCSA schema sorting** — sort JSON properties to match DCSA OpenAPI schema field order (fetched live from SwaggerHub)
- **Dark / Light theme** toggle with full editor and UI theming
- **Encoding support** — UTF-8, UTF-8 BOM, UTF-16 LE BOM, UTF-16 BE BOM with status-bar picker
- **Session restore** — remembers open files, pinned tabs, window layout, and theme
- **Tab management** — pin, drag-reorder, close all / close all but pinned
- **External file change detection** — prompts to reload when files change on disk
- **Import / Export settings** for sharing workspace configuration
- **Windows Explorer integration** — register an "Open with PathFinder" right-click context menu entry via **Help** menu
- **Single-instance** — opening a file from Explorer reuses the running PathFinder window

## Supported File Types

| Extension | Type |
|---|---|
| `.xml` | XML |
| `.json` | JSON |
| `.xsd` | XSD Schema |
| `.xsl` / `.xslt` | XSL Stylesheet |
| `.yaml` / `.yml` | YAML |
| `.edi` | EDIFACT |

---

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows (WPF is Windows-only)
- Python 3 with `requests` and `beautifulsoup4` (only needed to regenerate EDIFACT definitions)

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run --project PathFinder
```

### Test

```bash
dotnet test PathFinder.Tests
```

### Test with Coverage

```bash
dotnet test PathFinder.Tests --collect:"XPlat Code Coverage"
```

### Scrape EDIFACT Definitions

EDIFACT definition-aware validation requires a bundled definitions file. Generate it once with:

```bash
pip install requests beautifulsoup4
python Build/ScrapeEdifactDefinitions.py
```

This scrapes UN/EDIFACT directories (D96A, D97A, D98A, D99A, D99B, D01B, D10B) from edifactory.de and writes `PathFinder/Resources/EdifactDefinitions.json.gz`.

**How it works — scrape once, ship forever:**

1. The scraper fetches message structures, segment definitions, and code lists from edifactory.de and saves them as a compressed JSON file (`EdifactDefinitions.json.gz`).
2. The `.csproj` embeds that file directly into the application `.dll` at build time — no external files are needed at runtime.
3. `EdifactDefinitionService` reads the embedded resource lazily on first use and caches it in memory.

**You only need to re-run the scraper if:**

| Situation | Re-scrape? |
|---|---|
| Normal development / building | ❌ No |
| New developer cloning the repo (if `.json.gz` is committed) | ❌ No |
| Adding support for a new directory (e.g. D02B) | ✅ Yes |
| Refreshing definitions from edifactory.de | ✅ Yes |

**Recommendation:** commit `PathFinder/Resources/EdifactDefinitions.json.gz` to the repository. This way no one ever needs to run the scraper for a normal build. The build still succeeds without the file — definition-aware validation is simply skipped when the resource is not present.

### Publish (Framework-Dependent)

```powershell
dotnet publish PathFinder/PathFinder.csproj -c Release -r win-x64 -o dist/publish/win-x64-framework
Compress-Archive -Path dist/publish/win-x64-framework/* -DestinationPath dist/PathFinder-win-x64-framework.zip -Force
```

Produces a smaller output in `dist\publish\win-x64-framework\` and a zip at `dist\PathFinder-win-x64-framework.zip`. Requires the .NET 8 runtime to be installed on the target machine.

### Build Installer Package (Portable Zip)

```powershell
powershell -ExecutionPolicy Bypass -File .\Build\CreateInstaller.ps1
```

This publishes a self-contained single-file executable and packages it into a portable zip at `dist\PathFinder-win-x64-portable.zip`.

You can also override defaults:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build\CreateInstaller.ps1 -Configuration Debug -Runtime win-arm64
```

Or open `PathFinder.sln` in Visual Studio and press **F5**.

---

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+N` | New file |
| `Ctrl+O` | Open file |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save All |
| `Ctrl+F` | Find |
| `Ctrl+H` | Find & Replace |
| `Ctrl+G` | Go to Line |
| `Ctrl+Shift+F` | Format / pretty-print document |
| `Ctrl+Shift+M` | Minify (XML / JSON / EDI) |
| `Ctrl+T` | Close current tab |
| `Ctrl+Enter` | Execute XPath / JSONPath / YAMLPath expression |
| `Up/Down` | Cycle through query history (when focused in query box) |
| `Ctrl+Scroll` | Zoom editor or right panel |
| `F1` | Open Help window |

---

## Project Structure

```
PathFinder/
├── PathFinder.sln
├── MANUAL.md                       # User manual (Markdown)
├── PathFinder/                     # Main WPF application
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .xaml.cs  # Core UI logic
│   ├── HelpWindow.xaml / .xaml.cs  # In-app help (F1), renders MANUAL.md
│   ├── SplashScreen.xaml / .xaml.cs
│   ├── SyntaxColorsWindow.xaml / .xaml.cs # Custom syntax color picker
│   ├── CodeListWindow.xaml / .xaml.cs  # EDIFACT code list popup with search
│   ├── DcsaSettingsWindow.xaml / .xaml.cs # DCSA schema URL management dialog
│   ├── ReplaceWindow.xaml / .xaml.cs   # Find & Replace dialog (non-modal)
│   ├── Models/
│   │   ├── EdifactDefinition.cs    # EDIFACT scraped definition models
│   │   ├── GridNode.cs             # Tree view node for grid display
│   │   ├── MessageItem.cs          # Messages panel item (error + line number)
│   │   ├── SyntaxColorSettings.cs  # Custom syntax color settings + persistence
│   │   └── XPathResultItem.cs      # Query result item
│   ├── Services/
│   │   ├── XmlService.cs           # XML formatting + XPath execution + sample XML generation from XSD
│   │   ├── JsonService.cs          # JSON formatting + JSONPath execution + sample JSON generation
│   │   ├── YamlService.cs          # YAML formatting + YAMLPath execution + conversion
│   │   ├── ClipboardService.cs     # Syntax-highlighted HTML for Excel
│   │   ├── EdifactService.cs       # EDIFACT formatting + validation
│   │   ├── EdifactDefinitionService.cs  # EDIFACT definition loader
│   │   ├── EdifactStructuralValidator.cs # Segment order/group validation
│   │   ├── EdifactFieldValidator.cs     # Field-level validation
│   │   ├── EncodingService.cs      # File encoding detection & conversion
│   │   └── DcsaService.cs          # DCSA schema fetching, identification & JSON sorting
│   └── Themes/
│       ├── DarkTheme.xaml
│       └── LightTheme.xaml
└── PathFinder.Tests/               # xUnit test suite
    ├── *Tests.cs
    └── TestFiles/                  # Sample documents for tests
```

## Tech Stack

| Category | Technology |
|---|---|
| Language | C# 12, .NET 8.0 Windows |
| UI | WPF (Windows Presentation Foundation) |
| Editor control | AvalonEdit 6.3.0.90 |
| JSON | Newtonsoft.Json 13.0.3 |
| Markdown | Markdig 0.39.1 |
| XML | System.Xml (built-in) |
| YAML | YamlDotNet 16.3.0 |
| Tests | xUnit.v3 3.2.2, Coverlet 6.0.4 |
