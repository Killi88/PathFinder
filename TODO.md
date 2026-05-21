# PathFinder — Feature Roadmap

## Quick Wins

- [x] **Go to Line (Ctrl+G)** — dialog to jump to a specific line number
- [x] **Status Bar Line/Column Counter** — show Ln/Col as cursor moves (AvalonEdit Caret.Line/Column)
- [x] **Recent Files Menu** — File → Recent Files submenu (last 10-15 files, persisted in settings.json)
- [x] **Schema Statistics Summary** — show element/type counts when viewing Schema Tree (GetStatistics already exists)
- [x] **XPath/JSONPath/YAMLPath Query History** — Up/Down arrows in query box cycle through recent queries

## High-Value, Medium Effort

- [x] **Find & Replace (Ctrl+H)** — non-modal replace window with Find Previous/Next, Replace, Replace All; supports match case, whole words, and regex
- [x] **JSON/XML/YAML Minify (Ctrl+Shift+M)** — compress XML and JSON to single-line format
- [x] **XML ↔ YAML Conversion** — two toolbar buttons complete the full conversion matrix (XML↔JSON↔YAML)
- [x] **XPath/JSONPath Autocomplete** — popup suggests matching paths from GetAllPaths data as user types in query box
- [x] **EDIFACT Segment Tooltips** — hover over segment tag → tooltip with name and field definitions from EdifactDefinitionService
- [x] **Generate Sample JSON from JSON Schema** — generates sample JSON from JSON Schema or OpenAPI specs with $ref resolution, allOf/oneOf/anyOf, enum, format strings

## Ambitious

- [ ] **JSON Schema Validation** — validate JSON against JSON Schema (complement to XML-vs-XSD validation)
- [ ] **EDIFACT → JSON/XML Conversion** — parse EDIFACT segments into structured JSON or XML
- [ ] **Base64 Encode/Decode** — detect and decode Base64 content within values
- [ ] **EDIFACT Message Structure Navigator** — tree view of expected message structure from bundled definitions
- [ ] **Document Statistics** — line count, char count, file size, format-specific counts (elements, segments, etc.)
