# PathFinder

**XML / JSON / YAML editor with XPath, JSONPath & YAMLPath querying**

---

## Opening Files

PathFinder supports **.xml  .xsd  .xsl  .xslt**, **.json**, **.yaml  .yml**, and **.edi** (EDIFACT) files. You can open them in three ways:

- **Menu →** File → Open File… (`Ctrl+O`)
- **Menu →** File → Recent Files (last 10 opened files)
- **Drag & drop** one or more files from Explorer onto the window
- **Command line** — pass a file path as the first argument when launching PathFinder

Each file opens in its own **tab**. A **●** dot in the tab title indicates unsaved changes. Closing a tab with unsaved changes prompts for confirmation.

Tabs can be **reordered** by dragging them within the tab strip.

### Tab Pinning

Each tab has a 📌 pin button in its header. Click it to **pin** a tab — pinned tabs are excluded from batch-close operations (Close All But This, Close All But Pinned). The pin icon turns gold when active. Right-click a tab header for more options:

- **Pin / Unpin Tab**
- **Close** — close this tab
- **Close All** — close every tab (including pinned)
- **Close All But This** — close all other unpinned tabs
- **Close All But Pinned** — close every unpinned tab

### External Change Detection

If another program writes to an open file while PathFinder has it open, you will be prompted to reload. PathFinder suppresses this notification during its own saves to avoid false positives.

---

## Editor

The left pane is a full-featured code editor powered by **AvalonEdit** with syntax highlighting for XML, JSON, YAML, and EDIFACT.

When you type or paste content into an **untitled** tab, PathFinder automatically detects the content type (XML, JSON, YAML, or EDIFACT) and applies the appropriate syntax highlighting and validation. YAML is detected by a `---` document marker prefix or `key: value` patterns.

### Text View vs. Tree View

Toggle between **Text** and **Tree** view using the buttons at the bottom-left of each tab. Tree view renders the document as a collapsible hierarchy — useful for navigating large files without scrolling. Tree view is available for XML, JSON, and YAML documents.

### Schema Visualization (Schema Tree)

When a **schema file** is detected, an additional view button appears in the bottom bar next to **Text** and **Grid**: **Schema Tree**. This view renders the schema as an interactive hierarchical visualization.

**Supported schema formats:**

- **XSD** (`.xsd` files) — always detected by file extension
- **JSON Schema** — auto-detected by content keywords (`$schema`, `$defs`, `definitions`, `properties` with `type: object`)
- **OpenAPI / Swagger** (JSON or YAML) — auto-detected by `openapi` or `swagger` keywords

#### Schema Tree

A vertical node-box diagram showing the schema structure with connecting lines between parent and child nodes. Each node box shows:

- **Name** — element or property name (bold, color-coded by type)
- **Type** — the schema type name shown inline (e.g. `string`, `ComplexTypeName`)
- **Type-kind badge** — `complex`, `simple`, or `array` shown as a colored rounded rectangle
- **Badges** — colored labels indicating:
  - **Required** (red) / **Optional** (green) — whether the element is mandatory
  - **Choice** (orange) — for `xs:choice`, `oneOf`, or `anyOf` groups
  - **Recursive** (gray) — for self-referencing types (cycle detected)
- **Documentation** — extracted from `xs:documentation` or JSON Schema `description` (italic)
- **Restrictions** — each facet shown on its own line with a colored left border:
  - XSD: enumeration (Values), pattern, length, maxLength, minLength, minInclusive (Min), maxInclusive (Max), minExclusive, maxExclusive, totalDigits, fractionDigits, whiteSpace, union, list
  - JSON Schema: minimum, maximum, exclusiveMinimum, exclusiveMaximum, format, minItems, maxItems, multipleOf, minProperties, maxProperties

Optional elements use dotted borders. Click the ▼/▶ icons to expand or collapse branches. Use `Ctrl+Scroll` or the `−`/`+` buttons to zoom.

#### Schema Filter

A **filter bar** appears above the Schema Tree view. Type a search term to filter nodes by name, type name, documentation, or restriction values. Matching nodes include all their children; ancestor nodes of matches are preserved. Clear the filter with the `✕` button.

#### Schema Statistics

When you switch to Schema Tree view, the status bar shows a summary of the schema contents:

- **XSD schemas:** element count, complex type count, simple type count
- **JSON/YAML schemas:** property count, object count, array count

#### When do the buttons appear?

- For `.xsd` files: **always** — the Schema Tree button is always visible
- For JSON and YAML files: **only when schema content is detected** — if you open a regular JSON or YAML data file, the button remains hidden
- Schema detection re-runs automatically when you edit the document content

### Auto Indent (Pretty-print)

Press `Ctrl+Shift+F` or use **Format → Auto Indent** to re-indent the document with consistent formatting. This works for XML (4-space indentation), JSON (4-space indentation), YAML (2-space indentation), and EDIFACT files.

### Minify

Press `Ctrl+Shift+M` or use **Format → Minify** to compress the document to a single line with no unnecessary whitespace. This is the opposite of pretty-print and works for **XML**, **JSON**, and **EDIFACT** documents. YAML is not supported for minification.

### Code Folding

PathFinder supports code folding for XML, JSON, YAML, and EDIFACT documents. Click the **−** markers in the editor gutter to collapse regions:

- **XML** — folds on element tags
- **JSON** — folds on `{` braces and `[` brackets
- **YAML** — folds on indentation (a line whose next non-empty line has greater indentation starts a fold region)
- **EDIFACT** — folds on UNH/UNT message blocks and UNB/UNZ interchange blocks; when definitions are available, segment groups (e.g. SG1) are also foldable

Foldings are refreshed automatically as you edit.

### Find in Document

Press `Ctrl+F` or **Edit → Find** to open the inline search panel. It supports **case-sensitive matching**, **whole-word matching**, and **regular expressions**. Navigate matches with `F3` / `Shift+F3`, or the ▲ ▼ buttons. Close the panel with `Esc`.

### Find & Replace

Press `Ctrl+H` or **Edit → Find & Replace** to open the **Replace** window. This is a non-modal, always-on-top dialog that lets you find and replace text in the active editor tab.

- **Find Previous / Find Next** — navigate through matches in the document
- **Replace** — replace the current match and move to the next one
- **Replace All** — replace every occurrence at once
- **Match Case** — toggle case-sensitive matching
- **Whole Words** — match whole words only
- **Regex** — use regular expressions for both search and replace
- **All documents** — when checked, **Replace All** operates across all open tabs, not just the active one

All replacements are undo-friendly — use `Ctrl+Z` to undo any replacement. The window stays open so you can make multiple replacements without reopening it. Close with the ✕ button or press `Esc`. The dialog follows the current theme (dark/light) and updates live when you toggle themes.

### Word Wrap

Toggle word wrap via **Format → Word Wrap** or the toolbar button. The current state is indicated by a check mark in the menu item.

### Show Whitespace

Click the **·** (middle dot) toolbar button to toggle visibility of spaces, tabs, and end-of-line markers in the editor. The button turns blue when active. This applies to all open tabs.

### Autocomplete

When editing XML, PathFinder suggests inline closing tags. Type `</` and the matching closing tag name is immediately inserted as selected (highlighted) text — for example `</root>`. Press **Tab** or **Enter** to accept and move the caret past the `>`. Press **Escape** or start typing any other character to dismiss the suggestion without inserting anything.

### Convert XML ↔ JSON ↔ YAML

Two toolbar buttons let you convert the active document between any two formats:

**⇄ Convert (To JSON / To XML)** button:

- **XML → JSON** — serializes the XML document to a formatted JSON object (button shows **To JSON**)
- **JSON → XML** — deserializes the JSON document into an XML document (button shows **To XML**)
- **YAML → JSON** — converts the YAML document to a formatted JSON object (button shows **To JSON**)
- **JSON → YAML** — converts the JSON document to a YAML document

**⇄ Convert (To YAML / To XML)** button:

- **XML → YAML** — converts XML to YAML via a JSON intermediate step (button shows **To YAML**)
- **JSON → YAML** — converts the JSON document to a YAML document (button shows **To YAML**)
- **YAML → XML** — converts YAML to XML via a JSON intermediate step (button shows **To XML**)

The source tab is left unchanged in all cases. The conversion result opens as a new untitled tab with syntax highlighting already applied. Both buttons are disabled for EDI and untitled tabs.

### Generate Sample XML from XSD

Click the **⚙ Sample XML** toolbar button to generate a skeleton XML document from the active XSD schema. The button is only enabled when the active tab is an `.xsd` file.

PathFinder walks the schema and substitutes representative sample values for each element and attribute:

| XSD type | Sample value |
|---|---|
| `xs:string` | `String` |
| `xs:unsignedByte` | `255` |
| `xs:unsignedInt`, `xs:unsignedShort`, `xs:unsignedLong` | `0` |
| `xs:int`, `xs:integer`, `xs:long`, etc. | `0` |
| `xs:boolean` | `false` |
| `xs:dateTime` | `2001-12-17T09:30:47Z` |
| `xs:date` | `2001-12-17` |
| `xs:anyURI` | `http://example.com` |

- The root element is namespace-prefixed (`n1:`) when the schema has a `targetNamespace`.
- Child elements respect `elementFormDefault`: unqualified by default.
- An `xsi:schemaLocation` attribute is added automatically, pointing back to the XSD filename.
- For `maxOccurs="unbounded"` elements, exactly one sample instance is generated.
- The generated XML opens in a new untitled tab with syntax highlighting applied.

### Generate Sample JSON from Schema

Click the **⚙ Sample JSON** toolbar button to generate a sample JSON document from the active JSON Schema or OpenAPI specification. The button is only enabled when schema content is detected in the active tab (JSON or YAML).

PathFinder walks the schema and generates representative sample values:

| JSON Schema type | Sample value |
|---|---|
| `string` | `"String"` |
| `string` (format: `date-time`) | `"2001-12-17T09:30:47Z"` |
| `string` (format: `date`) | `"2001-12-17"` |
| `string` (format: `email`) | `"user@example.com"` |
| `string` (format: `uuid`) | `"550e8400-e29b-41d4-a716-446655440000"` |
| `string` (format: `uri`) | `"http://example.com"` |
| `integer` | `0` |
| `number` | `0.0` |
| `boolean` | `false` |
| `array` | One sample item |
| `enum` | First listed value |

- **`$ref` resolution** — references to `$defs`, `definitions`, and `components.schemas` are followed
- **`allOf`** — properties from all sub-schemas are merged
- **`oneOf` / `anyOf`** — the first option is used
- **Recursive schemas** — detected and stopped to prevent infinite loops
- The generated JSON opens in a new untitled tab with syntax highlighting applied.

### Validate XML Against XSD

Click the **✓ Validate XML** or **✓ Validate vs XSD** toolbar button to validate an XML document against an XSD schema.

- When the active tab is an **XSD** file, the button reads **Validate XML**. Click it to select the XML document to validate (from an open tab or browse from disk).
- When the active tab is an **XML** file (including `.xsl`, `.xslt`), the button reads **Validate vs XSD**. Click it to select the XSD schema to validate against (from an open tab or browse from disk).

A context menu appears with:
- All open tabs of the matching type (XML or XSD, as required)
- **Browse for XSD/XML file…** — opens a file picker to select a file from disk

If validation **passes**, a success message appears in the status bar.

If validation **fails**, errors appear in the **Messages** panel with clickable line numbers for quick navigation to the problem location in the XML document.

### Copy for Excel

Click the **📋** toolbar button to copy the entire document as syntax-highlighted HTML to the clipboard. Paste it into Excel to get a color-coded table preserving indentation. This button is enabled only when the active tab contains XML, JSON, or YAML. Light mode colors are always used so content is readable on white Excel cells.

### Sort DCSA

Click the **⇅ Sort DCSA** toolbar button to sort the JSON document's properties to match the field order defined in the corresponding DCSA OpenAPI schema. This button is enabled only when the active tab contains JSON.

When clicked, PathFinder:

1. Fetches the latest OpenAPI specifications from the configured SwaggerHub API URLs
2. Identifies which DCSA schema best matches the JSON document (e.g. Booking, ShippingInstructionsNotification)
3. Recursively sorts all object properties to match the schema's field order
4. Properties not defined in the schema are appended in their original order

The matched schema name, API, and version are shown in the status bar (bottom left) after a successful sort. The sort is performed in-place and supports **undo** (`Ctrl+Z`).

> **Important:** After sorting, always verify that the correct schema was identified by checking the schema name shown in the status bar at the bottom left (e.g. "Sorted as TransportDocumentNotification (DCSA_EBL v3.0.2)"). If the wrong schema was matched, undo the sort with `Ctrl+Z`.

**Nested document support:** If the JSON document is wrapped in a custom envelope (e.g. a notification payload inside a `JsonContent` property), PathFinder will search up to 3 levels deep to find and identify the DCSA message, then sort only the matched portion while preserving the wrapper structure.

If no matching schema is found or an error occurs, the error message is shown in the **Messages** panel.

### Right-click Context Menu

Right-click anywhere in the editor to access:

- **Copy XPath / JSONPath / YAMLPath** — copies the path at the current cursor line to the clipboard
- **Copy Value** — copies the element or property value at the cursor
- Standard Cut / Copy / Paste / Select All

---

## XPath / JSONPath / YAMLPath Querying

The **XPath** tab in the right panel lets you run expressions against the active document. For XML files the label shows **XPath Expression**; for JSON it shows **JSONPath Expression**; for YAML it shows **YAMLPath Expression**.

Type your expression in the text area, then press `Ctrl+Enter` or click **▶ Execute**. All matching nodes are listed below with their path, value preview, and line number. Click any result to jump directly to that line in the editor.

Use the **Up/Down arrow keys** in the query box to cycle through previously executed queries. History is kept for the current session.

### Query Autocomplete

As you type in the query box, a **popup** appears below the text box showing paths from the All Paths list that match your input (prefix or substring match, top 20 results). Use:

- **Up / Down arrows** — navigate through the suggestions
- **Enter** or **Tab** — accept the selected suggestion and insert it into the query box
- **Escape** — dismiss the popup

When the popup is closed, Up/Down arrows cycle through query history as usual.

### XPath tips

- Standard XPath 1.0 expressions are supported (e.g. `//item[@id='1']`)
- Namespace-prefixed documents are handled automatically — use the prefix declared in the document (e.g. `ns0:root/ns0:item`)
- Sibling index `[n]` is omitted from paths when the element name is unique among its siblings

### JSONPath tips

- Standard JSONPath supported via Newtonsoft.Json (e.g. `$.store.book[*].author`)
- Wildcard `$.*` returns all top-level properties
- Deep-scan operator `$..key` finds a key at any nesting level

### YAMLPath tips

- Uses dot-notation starting with `$` (e.g. `$.store.name`, `$.books[0].title`)
- Array elements are accessed with `[n]` index notation (e.g. `$.items[2]`)
- Wildcard `$.*` returns all top-level mapping children
- Mapping nodes preview as `"{…}"`, sequence nodes as `"[…]"`

---

## All Paths Panel

Switch to the **All Paths** tab to see every element, attribute, and value in the document listed as a path — like a full outline of the document structure.

The panel **auto-refreshes** whenever:

- You switch to the All Paths tab
- You switch to a different editor tab
- The document content changes (with a 600 ms debounce)

### Filtering

Type in the **Filter paths…** box to instantly narrow the list by path or value (case-insensitive substring match). The header shows `matched / total` counts. Click **✕** to clear the filter.

### Navigating and copying

Click any row to highlight and scroll to its line in the editor. Use the **⎘** button on each row to copy the path to the clipboard. Right-click a row for **Copy Path** or **Copy Value** options.

---
## Messages Panel

The **Messages** tab on the right side displays validation errors and format failures. When errors are detected, the Messages tab is automatically activated so you can see them immediately.

Each error is shown as a separate row. When a **line number** is available, clicking the row highlights and scrolls to that line in the editor.

Messages are populated automatically:

- When **syntax validation** detects errors (runs live as you type with a 600 ms debounce) — for XML, JSON, YAML, and EDIFACT
- When **Auto Indent** (Ctrl+Shift+F) fails due to a syntax error
- When **EDIFACT formatting** produces validation errors

The header shows the count of messages: `Messages  (N)`. Messages are cleared when the document becomes valid.

### Copying Messages

You can copy one or more message rows to the clipboard:

- **Single row:** click to select, then press `Ctrl+C`
- **Multiple rows:** hold `Ctrl` or `Shift` and click to select multiple rows, then press `Ctrl+C`
- **Right-click:** right-click any selected row and choose **Copy** from the context menu

All selected rows are copied as plain text (one line per message) separated by newlines. Navigation to the corresponding line in the editor only fires when exactly one row is selected.

---
## EDIFACT Support

PathFinder can open and format **.edi** (EDIFACT) files. Auto Indent splits segments onto separate lines at each unescaped `'` terminator. The `?` release character is honoured — `?'` is treated as a literal quote, not a segment break.

EDIFACT syntax highlighting colors segment tags, element separators (`+`), component separators (`:`), segment terminators (`'`), and release sequences (`?`) in both dark and light themes.

### Syntax Validation

PathFinder validates EDIFACT structure in real time and when formatting. If an error is found, the tab title shows a **⚠** warning icon and errors are displayed in the **Messages** panel on the right side — including the segment number and what went wrong. Click any message with a line number to navigate to that line. Checks include:

- **Missing segment terminator** — every segment must end with an unescaped `'`
- **Line without terminator** — every non-empty line must end with `'`; the error reports the line number
- **Invalid segment tag** — tags must be exactly 2–3 uppercase letters (e.g. `UNH`, `BGM`)
- **Empty segments** — a bare `'` with no tag is rejected
- **Unexpected content** after the last segment terminator
- **Invalid character after tag** — only `+`, `:`, or `'` may follow the tag
- **UNH/UNT pairing** — every `UNH` must have a matching `UNT`; nested `UNH` without closing `UNT` is rejected
- **UNT segment count** — the count declared in `UNT` must match the actual number of segments from `UNH` to `UNT` (inclusive)
- **UNT reference number** — the reference number in `UNT` must match the one in the corresponding `UNH`

### Definition-Aware Validation

When the bundled EDIFACT directory definitions are available, PathFinder also validates each message against the official message structure from the UN/EDIFACT directories (D96A, D97A, D98A, D99A, D99B, D01B, D10B).

The message type, version, and release are read from the `UNH` segment (e.g. `UNH+1+IFTMCS:D:96A:UN'` → directory **D96A**, message **IFTMCS**). Errors are reported in the **Messages** panel just like syntax errors.

Definition-aware checks include:

- **UNA format** — if a `UNA` service string advice is present, it must be exactly 9 characters (`UNA` tag + 5 delimiter characters + 1 segment terminator)
- **Unknown directory** — reported when the directory extracted from `UNH` is not in the bundled definitions
- **Unknown message type** — reported when the message type is not defined in that directory
- **Mandatory segment missing** — e.g. `BGM` is mandatory in IFTMCS; if it is absent or appears after optional segments have consumed its slot, an error is raised
- **Segment in wrong position** — segments that appear out of order relative to the message definition trigger "Unexpected segment" errors
- **Segment exceeds maximum occurrences** — e.g. a second `BGM` when the definition allows only one
- **Segment group validation** — groups (e.g. SG1, SG11) are tracked with a stack-based state machine; a segment that belongs to a group starts a new group repetition automatically; skipped mandatory groups are reported
- **Field-level validation** (per segment):
  - Mandatory fields that are missing or empty
  - Values that exceed the maximum character length for that field
  - Data type mismatches (`n` = digits only, `a` = letters only, `an` = alphanumeric)
  - Code list values — when a field references a coded element, the value is checked against the allowed code list; if the value is not in the list, a **📋 Show valid codes** link appears below the error — click it to open a popup listing all allowed codes with their descriptions and a search bar for quick filtering (searches both code and description)

All errors include a `Line N:` prefix so you can click the message and jump directly to the offending line.

### Segment Tooltips

Hover over any **segment tag** in an EDIFACT document to see a tooltip with the segment’s name and a table of its field definitions. The tooltip shows:

- **Segment tag and name** (e.g. `BGM — Beginning of message`)
- **Fields** — each field’s ID, name, mandatory/conditional status, data type (`n`/`a`/`an`), and maximum length

This requires the bundled EDIFACT definitions to be present. The directory is determined from the `UNH` segment in the document.

> **Note:** Definition-aware validation requires that the EDIFACT definitions resource has been generated. If the bundled definitions are not present, only syntax validation runs. To generate the definitions, run `python Build/ScrapeEdifactDefinitions.py` (requires Python 3 with `pip install requests beautifulsoup4`).

---

## File Encoding

PathFinder detects file encoding from the BOM automatically when opening a file. The current encoding is shown in the **status bar** (bottom right).

Click the encoding label to change it. Supported encodings:

- `UTF-8` — no BOM (default)
- `UTF-8 BOM` — with `EF BB BF` prefix
- `UTF-16 LE BOM` — with `FF FE` prefix
- `UTF-16 BE BOM` — with `FE FF` prefix

When saving an XML file, the `<?xml … encoding="…"?>` declaration is updated automatically to match the selected encoding. Both double-quoted and single-quoted attributes are supported; the original quote style is preserved.

---

## Zoom

### Editor Zoom

Hold `Ctrl` and scroll the mouse wheel over the editor to zoom in or out (range 6–48 px, default 13 px). Each tab has its own zoom level. You can also use the **−** / **+** buttons in the bottom-right of each tab. Click the **%** label to reset to 100%.

### Right-Panel Zoom

Hold `Ctrl` and scroll over the right panel (XPath results or All Paths) to zoom both lists (range 6–36 px, default 11 px). Use the **−** / **+** buttons below the panel, or click the **%** label to reset.

---

## Dark / Light Theme

Click the **🌙 / ☀️** button in the toolbar to toggle between **dark** and **light** mode. The theme is applied instantly to all open tabs, the editor, the grid view, and all syntax highlighting colors. Your chosen theme is **remembered across sessions** — on next launch, PathFinder (including the splash screen) will use the last active theme. The theme preference is also included when exporting/importing settings.

---

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+N` | New File / Tab |
| `Ctrl+O` | Open File… |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save All |
| `Ctrl+F` | Find in Editor |
| `Ctrl+H` | Find & Replace |
| `Ctrl+G` | Go to Line |
| `F3` | Find Next |
| `Shift+F3` | Find Previous |
| `Ctrl+Shift+F` | Auto Indent (pretty-print) |
| `Ctrl+Shift+M` | Minify (XML / JSON / EDI) |
| `Ctrl+Enter` | Execute XPath / JSONPath / YAMLPath expression |
| `Up/Down` | Cycle through query history (when focused in query box) |
| `Ctrl+T` | Close current tab |
| `Esc` | Close search panel |
| `F1` | Open this Help window |
| `Ctrl+Scroll` | Zoom editor or right panel |
| `Alt+F4` | Exit PathFinder |

---

## Status Bar

The status bar at the bottom of the window shows (left to right):

- **Status message** — feedback after operations (e.g. "Saved", "X result(s) found")
- **Line / Column** — shows `Ln {line}, Col {col}` for the current cursor position
- **File path** — full path of the active file
- **Encoding picker** — click to change the file's encoding
- **File type** — XML / JSON / YAML / XSD / XSL / XSLT / EDI

---

## Window Layout & Session Persistence

PathFinder remembers your window position, size, maximised state, right-panel width, the active theme (dark/light), the list of open files (including which tabs are pinned), and your recent files list across sessions. Settings are saved to `%LOCALAPPDATA%\PathFinder\settings.json` on close and restored on the next launch.

You can resize the left editor / right tool panel split by dragging the vertical divider.

### Session Restore

On startup, PathFinder reopens the files you had open last time (skipping any that no longer exist on disk). Pinned tabs are restored with their pin state. If no saved files are found, a blank tab is opened.

### Splash Screen

A splash screen is shown briefly while PathFinder loads — it displays the app icon, title, and a loading indicator. The splash screen matches your saved theme preference (dark or light). It closes automatically once the main window is ready.

### Export / Import Settings

Use **Settings → Export Settings…** to save your current layout, theme, open files, and DCSA schema URLs to a `.pathfinder.json` file. Use **Settings → Import Settings…** to restore a previously exported configuration. Importing will close all current tabs, apply the saved theme, and reopen the saved file list with pin states.

### Customize Syntax Colors

Use **Settings → Customize Colors…** to open the color customization dialog. You can change the syntax highlighting colors for **XML**, **JSON**, **YAML**, and **EDIFACT** independently for both **dark** and **light** themes.

YAML has 6 customizable color categories: **Key**, **Value**, **Comment**, **Anchor**, **Tag**, and **DocMarker** (document markers like `---` and `...`).

Each color entry shows a colored swatch and a hex code (e.g. `#4EC9B0`). You can:
- **Click the colored swatch** to open the system color picker dialog
- **Type a hex color** directly into the text box (format: `#RRGGBB`)

Use the **Dark Theme / Light Theme** radio buttons at the top to switch which theme's colors you are editing. Click **Reset to Defaults** to restore the built-in colors for the currently selected theme. Click **OK** to apply and save, or **Cancel** to discard changes.

Custom colors are automatically included when using **Settings → Export Settings…** and **Settings → Import Settings…**, so your color preferences travel with the rest of your configuration.

Custom colors are saved to `%LOCALAPPDATA%\PathFinder\colors.json` and persist across sessions.

### DCSA Schema URLs

Use **Settings → DCSA Schema URLs…** to manage the list of SwaggerHub API URLs used by the **Sort DCSA** feature. The dialog lets you:

- **Add** new SwaggerHub API URLs
- **Update** an existing URL by selecting it and editing
- **Delete** URLs from the list
- **Reset to Defaults** to restore the built-in URLs (DCSA_EBL and DCSA_BKG)

DCSA schema URLs are persisted across sessions and are included when exporting/importing settings.

---

## Windows Explorer Integration

Use **Help → Register 'Open with PathFinder' context menu** to add an **"Open with PathFinder"** entry to the Windows right-click context menu.

After registering, you can right-click any file in Windows Explorer and select **Open with PathFinder** to open it directly in the editor.

- No administrator privileges are required (the entry is registered per-user).
- The entry points to the current PathFinder executable, so re-register if you move the application.
- PathFinder is a single-instance application: if it is already running, the file will be opened in the existing window instead of launching a new copy.
- To remove the entry, delete the `HKEY_CURRENT_USER\Software\Classes\*\shell\PathFinder` key from the Windows Registry.
