<div align="center">
    <h1>CodeWalkerProjects</h1>
    <p>GTA V mod installation tooling, built on the CodeWalker core.</p>
</div>

This repository is a fork of [**CodeWalker** by dexyfex](https://github.com/dexyfex/CodeWalker) — the RPF archive explorer and world editor for Grand Theft Auto V. All of CodeWalker's original functionality is present and behaves as before; see the [upstream repository](https://github.com/dexyfex/CodeWalker) for documentation on the world view, explorer, project editor and file viewers.

What this fork adds:

1. **[Two standalone applications](#part-1--the-applications)** — the *OIV Package Installer*, which installs mods for end users, and the *OIVS Packer*, which builds multi-component mod packages for authors.
2. **[Fixes to the CodeWalker core](#part-2--changes-to-the-codewalker-core)** — a set of round-trip fidelity bugs in the metadata (`.ymt` / `.ytyp` / `.ymap`) read-write pipeline that were silently corrupting game files.
3. **[Support for a new format](#part-3--a-new-format-shader-libraries-sga_awc)** — GTA V Enhanced's compiled shader libraries, which CodeWalker could not open at all.

---

# Part 1 — The Applications

## OIV Package Installer

A lightweight alternative to the OpenIV Package Installer, built directly on the CodeWalker core. It installs mod packages into GTA V without requiring OpenIV to be present.

**Supported package types**

| Type | Description |
|---|---|
| `.oiv` | Standard OpenIV package (Format 2.2), fully supported |
| `.oivs` | *OIVS* — this project's multi-component wizard format (see below) |
| `.rpf` | An RPF used as a package container |
| `dlc.rpf`, or a folder containing one | Installed as an add-on DLC and registered in `dlclist.xml` automatically |

Both **GTA V Legacy** and **GTA V Enhanced** are supported. The installer detects the edition, loads the matching encryption keys from the game executable, and validates the package's `<gameversion>` tag against the installed game, so a Legacy mod cannot be installed onto Enhanced or the reverse.

### How installation works

1. **Read the package.** The `assembly.xml` manifest is parsed into a list of operations. Large packages are unpacked to a working folder chosen for available space, falling back off a full system drive.
2. **Copy targets into `mods\`.** Any game archive an operation touches is copied to the `mods` folder first. Original game files are never modified — this is what makes uninstalling possible, and why a mods loader is required.
3. **Apply the operations** against the `mods` copy. Archives are converted to OPEN encryption so the mods loader can read them.
4. **Record a backup session.** Every file replaced, edited, added or deleted is logged, with the original bytes stored, so the install can be reverted later from **Manage Mods**.

### Supported operations

| Operation | What it does |
|---|---|
| `<archive>` | Opens an RPF for editing; nests arbitrarily deep for archives inside archives |
| `<add>` | Adds or replaces a file, inside an archive or on the file system |
| `<delete>` | Removes a file from an archive or from the game folder |
| `<text>` | Line-level edits to text files — insert, replace and delete, with wildcard, exact, starts-with, ends-with and contains matching |
| `<xml>` | XPath edits (add / replace / remove) to XML files, including binary metadata decompiled to XML |
| `<pso>` | XPath edits to binary metadata files (`.ymt`, `.ytyp`, `.ymap`, `.ymf`, `.pso`) |

### Manage Mods

Every install appears as a single entry that can be reverted. Two uninstall modes are offered: **revert to backups**, which undoes this install while preserving anything installed after it, and **reset to vanilla**, which restores the untouched game files. A separate **DLC Add-ons** tab enables and disables add-on packs without hand-editing `dlclist.xml`.

Uninstall prefers *smart revert* for text and XML files: it removes only the specific lines or nodes that were added, so other mods' edits to the same file survive. Binary files are restored from their stored original bytes.

### Reliability

Several classes of failure are handled explicitly.

- **Packages larger than 2 GB.** Files bound for the file system are streamed rather than buffered, so a package shipping a multi-gigabyte `dlc.rpf` installs correctly and with flat memory use. Free space is checked before writing, producing a readable message instead of a bare "not enough space on the disk".
- **Case-insensitive XPath.** GTA's data names are joaat-hashed and therefore case-insensitive, but XPath is not, and the same name is spelled differently across game builds (`VEH_MID` versus `veh_mid`). Package XPaths are matched exactly first, then case-insensitively.
- **Binary rebuild verification.** After a `<pso>` or `<xml>` edit to a binary file, the rebuilt file is decompiled again and compared against the intended result. If anything was lost the write is refused and the original left untouched — a visible failure rather than a silently corrupted game file.
- **Culture independence.** Game data is culture-neutral: floats always use `.`. Both applications pin the invariant culture at startup so a comma-decimal locale cannot corrupt values written into game files. See [the locale bug](#3-locale-dependent-number-formatting) below for why this matters.
- **Nested archives revert too.** An `<archive>` block can target an `.rpf` inside another `.rpf`, and edits there are backed up like any other. On uninstall the nested archive is reached by walking into the parent rather than being looked up as a path on disk, so those edits are genuinely undone instead of quietly left in place.
- **Faithful resource backups.** A resource entry stored inside an archive is not required to begin with the `RSC7` magic that the same file carries once exported, so the backup rebuilds that header from the entry's own flags and keeps the stored payload byte for byte. Restores therefore return the exact original bytes.

### Requirements

A mods loader must be installed for the `mods` folder to work:

- **GTA V Legacy** — `OpenIV.asi`
- **GTA V Enhanced** — `OpenRPF.asi`

### Command line

The installer also runs headless for scripted installs, via `--install`, `--uninstall`, `--list-options`, `--select` and related flags. Run it with `--help`, or see **Docs → User Guide** inside the application.

---

## OIVS Packer, and the `.oivs` format

### What `.oivs` is

An `.oiv` installs exactly one thing. A `.oivs` ("Super OIV") package bundles a whole collection into a single file:

- an optional **base mod** that is always installed,
- any number of **optional modules** the user ticks on or off,
- **single-choice groups** — pick one streetlight colour out of three, for example.

Opening one in the installer presents a selection wizard: modules as checkboxes, groups as radio buttons, each with its own description and image previews, including click-to-toggle before/after comparisons. One click installs the chosen combination, a backup is recorded, and the whole package appears as a single entry in Manage Mods.

### Package layout

```
package.oivs                (a zip archive)
├── super.xml               manifest: metadata, modules, groups, install operations
├── icon.png
├── media/                  bundled preview images, downscaled at build time
└── content/
    └── <module-id>/        content namespaced per module
        ├── oiv/            files extracted from an author's .oiv
        └── <subfolder>/    loose folders copied to the game root
```

Each module and option carries standard OIV install operations — the same `<content>` grammar as `assembly.xml` — and/or loose folders. Namespacing content per module lets two modules ship files with identical names without colliding. Previews are bundled rather than linked, so packages work offline.

### The Packer

A standalone authoring tool for building `.oivs` packages without touching XML.

- Tree view of the package, with a property editor for metadata, modules, groups and options.
- **Drag and drop**: an `.oiv` or a folder becomes a module, images become previews for the selected item, an `.oivsproj` opens that project.
- Media editor with thumbnails and reordering, supporting both single showcase images and before/after pairs.
- Live validation, with a status bar reporting exactly what is missing.
- **Preview** exports to a temporary package and opens it in the real installer wizard, so you see precisely what users will see.
- Projects save as `.oivsproj` (JSON) so work can be resumed; **Export** produces the final `.oivs`.

A package does not need a required base — leave every module optional to build a pick-any compilation.

---

# Part 2 — Changes to the CodeWalker Core

CodeWalker can decompile any game metadata file to XML and recompile it back to binary. That round trip underpins every `<pso>` and `<xml>` mod operation — and it was losing data.

Measured across **all 3,812 metadata files in a stock Enhanced `update.rpf`**, by decompiling each file, recompiling it with no edits at all, and comparing the result:

| | Before | After |
|---|---:|---:|
| Round-trips losslessly | 1,125 (29.5%) | **3,809 (99.9%)** |
| Loses data | 2,685 | 2 |
| Produces unparseable XML | 2 | 1 |

By format afterwards: **RSC/Meta 3,082 of 3,082 (100%)**, PSO 690 of 692, RBF 37 of 38.

Seven distinct problems were responsible — five in the round trip itself, plus a checksum the game validates and a rebuild that can't be made byte-faithful.

### 1. The recompiler ignored each file's own schema

**The problem.** CodeWalker's *decompiler* is data-driven: it reads the structure definitions embedded in the file it is reading (`cont.GetStructureInfo`). Its *recompiler* was not — it rebuilt from hardcoded tables (`MetaTypes` / `PsoTypes`) generated years ago. Any field those tables did not know about was silently dropped on the way back to binary. Since game builds add fields over time, and the tables omit padding entries that real schemas declare, this affected the majority of files. `vehicledamage.ymt`, for instance, came back with every tuning value erased.

**The fix.** Both builders can now adopt the schema of the file being edited.

- `MetaBuilder.UseSchemaFrom(Meta)` and `PsoBuilder.UseSchemaFrom(PsoFile)` build lookups from the file's own embedded definitions — `Meta.StructureInfos` / `EnumInfos`, and `PsoFile.SchemaSection.Entries` keyed by `IndexInfo.NameHash`.
- `MetaBuilder.GetStructureInfo` / `GetEnumInfo`, and the `PsoBuilder` equivalents, prefer those definitions and fall back to the built-in tables when there is no source file.
- `XmlMeta.Traverse`, `XmlPso.Traverse` and the enum resolution paths now go through the builder rather than the static tables.
- New overloads thread the source through: `XmlMeta.GetMeta(doc, Meta source)`, `XmlMeta.GetRSCData(doc, source)`, `XmlMeta.GetPSOData(doc, PsoFile source)`, `XmlPso.GetPso(doc, source)` and `XmlMeta.GetData(doc, format, path, object source)`.

The existing parameterless overloads are unchanged, so current callers keep working. This also holds up against future game updates: the schema the game itself uses to read a file is now the schema used to rebuild it.

*Files: `MetaBuilder.cs`, `PsoBuilder.cs`, `XmlMeta.cs`, `XmlPso.cs`*

### 2. String values were written to XML unescaped

`MetaXml.WriteStringItemArray` wrote game strings straight into XML with no escaping — the original source even carried a `//TODO: escape this??!` on that line. `dictionary.ymt`, the profanity filter, contains `<`, `>` and `"` as literal entries, which produced XML that could not be parsed back at all. Both it and `WriteHashItemArray` now escape their values.

*File: `MetaXml.cs`*

### 3. Locale-dependent number formatting

On any machine whose locale uses a comma decimal separator — most of Europe and Latin America — floats were written into metadata XML as `4,2` instead of `4.2`, and read back as **42**. A silent tenfold change to real values inside `.ymap` and `.ymt` files, and invisible to anyone developing on an English-locale machine.

`FloatUtil` was already invariant throughout, so this originated in stray culture-sensitive formatting elsewhere in the pipeline. Rather than patch a single call site, both applications now pin `CultureInfo.InvariantCulture` at startup: game data files are culture-neutral by definition, so a tool that reads and writes them should never follow the user's regional settings.

*Files: `CodeWalker.OIVInstaller/Cli/Program.cs`, `CodeWalker.OivsPacker/Program.cs` — applied at application level; the underlying core call site was not isolated.*

### 4. Non-ASCII strings destroyed on rebuild

`PsoBuilder.AddString` encoded with `Encoding.ASCII`, while the reader maps bytes one-to-one onto characters. Every character above 127 became `?`, so accented, Cyrillic and Korean strings were wiped out whenever a PSO file was rebuilt. Writing with ISO-8859-1 (`Encoding.GetEncoding(28591)` — `Encoding.Latin1` is unavailable in this project's target framework) makes the writer the exact inverse of the reader.

*File: `PsoBuilder.cs`*

### 5. Enum values resolved against the built-in tables

The same schema-drift problem as bug 1, in two enum paths that the first fix missed — and the most damaging of the set, because it hit `carcols.ymt` and `cameras.ymt`, two of the most frequently edited files in vehicle and camera modding.

Enum members are stored as values but written to XML as names, so rebuilding has to map each name back. Both mapping paths consulted `PsoTypes.GetEnumInfo` rather than the definitions in the file being edited:

- **Enum arrays.** When the built-in table didn't contain the enum, the name lookup returned nothing and the code fell into an empty `else` branch, leaving every element as `0`. In `carcols.ymt` this silently emptied `turnOffBones` — the list of bones hidden when a mod part is fitted, so stock parts would have shown through new bodywork.
- **Single enum values.** Worse, where the built-in table held the *same* enum with different member keys, a name resolved to the wrong value: `mod_col_3` became `extra_10`, quietly reassigning a collision bone rather than failing.

Both now go through `pb.GetEnumInfo(...)`, so the file's own enum definitions win. `GetEnumInt` takes an optional `PsoBuilder` for this, and the array path gained a null guard.

Together these took `carcols.ymt` from 591 differing lines to **0** and `cameras.ymt` from 261 to **0**. Both are now fully editable: changing a real camera value and rebuilding alters exactly the edited line and nothing else in a 148,780-line file.

*File: `XmlPso.cs`*

### 6. Checksum-protected metadata could not be edited at all

Some metadata files carry a `CHKS` chunk holding a checksum of their own contents, and the game validates it for a handful of them — `cameras.ymt` being the one modders run into. Any edit leaves that checksum stale, and the game then crashes on startup rather than reporting a problem. A single changed byte was enough, which made these files appear permanently unmoddable: even an otherwise byte-identical copy would fail.

`PsoChecksum.Update()` recomputes the chunk after a write: Jenkins one-at-a-time seeded with the chunk's salt, over the file with its size and checksum fields zeroed, hashing the bytes as **signed** values (anything above `0x7F` subtracts rather than adds — treating them as unsigned silently produces the wrong result). It's a no-op on files without a `CHKS` chunk. Verified by reproducing the checksum that ships in a stock `cameras.ymt`.

*File: `PsoChecksum.cs`*

### 7. Editing preserves the original file rather than rebuilding it

Even with the fixes above, a rebuilt PSO isn't byte-identical to Rockstar's: structures carry padding and sentinel defaults that no schema entry describes, so they never reach the XML and can't be reconstructed.

Metadata edits therefore avoid rebuilding entirely. CodeWalker rebuilds the file twice — once from the unedited XML, once from yours — and diffs the two to find exactly which bytes your edit touches, which is reliable because the rebuild now reproduces the source's block layout exactly. Those bytes are applied to the untouched original and the checksum recomputed. A one-value edit to a 828 KB `cameras.ymt` changes five bytes: the value, and the checksum.

Structural changes (adding or removing array items) still take the full rebuild path.

*File: `MetaForm.cs`*

---

# Part 3 — A new format: shader libraries (`sga_*.awc`)

Parts 1 and 2 add applications and repair existing behaviour. This part is neither: it is support for a game format CodeWalker could not open at all.

GTA V Enhanced keeps its compiled shaders in `.awc` files — the same extension as audio wave containers, distinguished only by an `SGD2` magic. CodeWalker read the extension, tried to parse them as audio, and failed. They now open in the shader viewer instead, and the file list labels them rather than showing the audio icon.

## What it does

The format reads and writes in full: browse effects, techniques and passes, inspect registers and constant buffers, export or replace individual `.cso` blobs, and export the whole library to XML and back.

Round-tripping every shader library in a stock Enhanced install — 6 archives, 37,992 shaders — reproduces each one **byte for byte**, through both the binary path and a full XML export and re-import. That is the property that makes writing safe: whatever the format holds that isn't modelled is carried rather than approximated. Writing is gated behind Edit Mode, as everywhere else.

*Files: `AwcShaderFile.cs`, `FxcForm.cs`, `MetaXml.cs`, `ExploreForm.cs`*

## Root signatures are preserved on import

Every one of those 37,992 shaders embeds a root signature in an `RTS0` chunk, and the game builds its pipeline state objects from it. HLSL recompiled with dxc emits no `RTS0`, so dropping a freshly compiled blob into a slot leaves the pipeline without the signature the game expects: PSO creation fails, and the shader either silently never runs or takes the game down. The archive is perfectly valid either way, so there is nothing to find by inspecting it.

Importing a `.cso` therefore compares its root signature against the shader being replaced:

| | |
|---|---|
| Replacement carries the original's signature | imported as-is |
| Replacement carries a different one | flagged, your call |
| Replacement carries none | signature transplanted from the original and the container re-signed |
| Transplant rejected, or dxc unavailable | **import refused, shader left untouched** |

A shader from a toolchain that already transplants the signature passes straight through and no external tool runs. Otherwise dxc does it — an SM6 container is hash-signed, so re-signing one is not something managed code can do. `dxc.exe`, `dxcompiler.dll` and `dxil.dll` are pulled in from the official `Microsoft.Direct3D.DXC` package and deployed beside the application's other native dependencies.

The refusal is the point. Silently writing a shader that fails pipeline creation is far worse than a dialog saying no.

---

# Known issues

**Part 2 — metadata.** Both of these are *fail-safe*: the rebuild verification refuses the write and reports it, rather than producing a corrupt file.

- **Two PSO files render differently after a rebuild** — `animpostfx.ymt` and `junctions.pso`. Both are *stable fixed points*: they change once and then hold, so the data survives editing and repeated edits don't degrade it. The verification guard is stricter than it needs to be here and refuses them; accepting any file that passes a fixed-point check would unblock both.
- **`shopping_cart_validation.ymt` decompiles to invalid XML.** Its element name is `CriminalCareerDefs::ShoppingCartItemCategoryLimits`, and `::` is illegal in an XML name. Fixing this requires a name-mangling scheme that round-trips in both directions, which is a decision about the XML format mod authors work with.

**Part 3 — shader libraries.**

- **The XML re-import path does not check root signatures.** Replacing a single `.cso` through the shader viewer is guarded; exporting a library to XML, editing, and importing it back reloads every `.cso` from disk without that check. That is the higher-volume route, so it is the one more likely to be used for a real mod. Until it is covered, replace shaders individually or make sure the blobs already carry the original signatures.

---

# Credits

CodeWalker is the work of **dexyfex** and its contributors. This fork adds the applications and core fixes described above; everything else belongs to the original project.
