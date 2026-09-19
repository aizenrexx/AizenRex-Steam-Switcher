from pathlib import Path
import hashlib, re

ROOT = Path(r'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher')
OUT = ROOT / 'README.md'

sections = []
def w(text=''):
    sections.append(text.rstrip() + '\n')

w('# Steam Profile Switcher - Complete Project History and Engineering Record')
w()
w('**Credit:** Aizenrex x Riyad  ')
w('**Current release line:** 2.1.0  ')
w('**Project root:** `H:\\My Project Coding\\Lindy My Boss\\Steam Switcher`  ')
w()
w('This is the canonical long-form record requested by Riyad. It preserves the project goal, the instructions that shaped the work, the complete known chronology, the evidence used for decisions, mistakes and recoveries, architecture, release rules, testing knowledge, operational experience, and a line-by-line annotated snapshot of every canonical source file. It intentionally excludes automatic backup copies because those are old revisions rather than additional project knowledge.')
w()
w('## Truth and scope statement')
w()
w('This document distinguishes measured facts from assumptions. A clean compile is not described as visual proof. A feature found in source is not described as discoverable until its UI is checked. The fresh-PC Steam installer path has not been proven on a truly fresh PC because the development machine already had Steam installed. The VDF parser, Steam detection, updater replacement, launch timing, process state, registry state, file layout, executable metadata, and focused regression tests were directly measured as described below.')
w()

chapters = [
('1. Product goal and user vision', '''Steam Profile Switcher is a Windows desktop application for operating two Steam environments called Main and Daddy. Its purpose is broader than renaming folders. The intended experience is that a person can place the application on an existing or fresh Windows PC, choose a single-profile or dual-profile layout, install Steam when necessary, log into each profile once using their own credentials, and then switch safely without manually managing folders, VDF files, registry values, updater downloads, process shutdown, recovery journals, or repeated administrator prompts.

The user explicitly rejected copying login credentials from another computer or profile. Each person must perform the first login personally. The application may select between already known accounts by editing non-secret account-selection metadata, but it must never copy, delete, or overwrite the modern token store in local.vdf. The user also required immediate dashboard refresh after a switch, visible progress and a time estimate, a smart SteamDaddy update flow, a customizable update source, visible upstream mandatory instructions, a clear running version, a credit section reading Aizenrex x Riyad, an organized Settings page, and a release directory where the main executable is easy to find.'''),
('2. User directives that became engineering requirements', '''The standing working style was: research first, understand the real machine state, then build. Existing libraries and upstream GitHub information should be inspected instead of rebuilding known solutions blindly. Independent machine reads should be batched or run in parallel because the PC can accept multiple commands; writes and shared mutable operations remain serialized for safety.

The user repeatedly emphasized that the goal matters more than a particular implementation. This granted freedom to choose a sound design while preserving the intended one-install, one-login, long-term switching workflow. The user also required the project to be organized, the build folder not to resemble a dependency dump, and important options not to be buried below an invisible fold. These instructions are permanent acceptance criteria for future work.'''),
('3. Initial inspection and inherited project state', '''The project was a .NET 9 WPF application using WPF-UI. It already had Dashboard, Games or Library, Compare, History, Backups, Diagnostics, Tools, Logs, and Settings pages, plus services for inspection, switching, backup, logging, persistence, and process management. The initial state contained stale ModernWpf references, undefined resource styles, a Settings runtime crash, slow cold startup, fragile regular-expression editing of loginusers.vdf, ambiguous profile detection, a dashboard that did not refresh after cached navigation, and a self-contained publish output containing hundreds of adjacent runtime files.

Inspection used the Aizen Local PC connection to read files and run PowerShell commands on the real machine. The project root, live Steam folders, registry, running processes, logs, file counts, timestamps, and build output were measured. This evidence-first approach became essential because several later problems looked different in screenshots than they did in source code.'''),
('4. Settings crash and TargetInvocationException', '''Opening Settings produced a TargetInvocationException. The outer exception was only a wrapper. Resource and XAML inspection found seven undefined style references and leftover ModernWpf dependencies while the application had moved to WPF-UI. Styles.xaml was corrected, stale references were removed, all XAML was checked for well-formed structure, and the project built cleanly.

The lesson is that WPF resource lookup failures may survive compilation and fail only when a page template is instantiated. Future theme or resource changes must be tested by opening the affected page, not merely by compiling.'''),
('5. SteamDaddy smart updater', '''A smart updater was built around the latest-release API. It selects SteamDaddy.exe, compares installed and available versions, downloads to a temporary location, verifies the GitHub SHA-256 digest when supplied, refuses unsafe replacement while SteamDaddy is running, preserves the previous executable as a .previous file, performs replacement, and records updater state in data/steamdaddy-update.json. A six-hour throttle avoids repeated checks during normal switching; the explicit Check now action may force a check.

A real test installed version 3.2.1. The downloaded bytes matched the upstream digest, the old executable was preserved, and a subsequent run correctly returned SkippedRecentlyChecked. The upstream process still requires the user to open SteamDaddy and click Install Plugin where that upstream action cannot safely be automated.'''),
('6. Startup performance investigation', '''The original measured cold launch was approximately 7007 milliseconds and a warm launch was approximately 2943 milliseconds. Disk scans were only about 19 to 47 milliseconds, proving that directory enumeration was not the main bottleneck. Attention shifted to WPF startup, JIT cost, duplicate UI frameworks, and publish settings.

ReadyToRun, tiered compilation, quick startup, and TieredPGO were used. The stale ModernWpf dependency was removed. Per-assembly ReadyToRun helped cold startup while retaining normal runtime optimization. Launch timing was measured using the appearance of the real main-window handle and logs were checked for hidden exceptions.'''),
('7. Optimization regression and recovery', '''An attempted optimization enabled invariant globalization and composite ReadyToRun. It was a serious regression introduced during the work. Cold launch rose to roughly 39446 milliseconds. WPF requested the en-us culture, which invariant globalization could not provide, producing a XamlParseException and a later null-reference failure while NavigationView updated content.

The log proved the cause. Invariant globalization was disabled and composite ReadyToRun was reverted. The release returned to roughly 2557 to 2843 milliseconds cold and approximately 2066 milliseconds warm with zero logged exceptions. This incident established a permanent build contract: InvariantGlobalization must remain false, PublishReadyToRunComposite must remain false, and changes to startup properties require repeated timing plus a clean-log check.'''),
('8. Project cleanup and directory structure', '''Approximately fifty scratch items were moved into _scratch_20260917. Reusable PowerShell utilities were grouped under tools. Design records were placed under docs. The canonical source is src/SteamSwitcher. The canonical published application is build/release. Broken or superseded release directories were parked rather than silently overwritten.

The intended top-level model is simple: src contains product source, tools contains diagnostics and release scripts, docs contains durable engineering design material, build contains release artifacts and screenshots, and scratch contains disposable experiments. Future work should not scatter probes or temporary outputs across the project root.'''),
('9. Fresh-PC setup research', '''Before building the first-run wizard, the real Steam authentication and installer behavior were investigated. SteamSetup.exe supports a silent /S installation. When an NSIS /D installation path is supplied, it must be the final unquoted argument. Registry detection cannot trust an empty uninstall InstallLocation alone; a candidate path must contain steam.exe.

The wizard design supports Dual and Single layouts, optional SteamDaddy preparation, Steam detection or silent installation, safe profile-folder creation, guided login for each profile, account capture from loginusers.vdf, and a setup-complete marker. It never copies userdata or token files as a shortcut. Errors should not trap a person permanently in the wizard; setup detection fails open when appropriate.'''),
('10. Modern Steam authentication finding', '''Local probes produced the decisive result. No ssfn files were present, and ConnectCache was not stored in the switched Steam directories. The real modern token data was in %LOCALAPPDATA%/Steam/local.vdf. It contained two ConnectCache entries with encrypted refresh-token material. That file had remained untouched across profile switches.

The consequence is strict: local.vdf must never be copied, deleted, replaced, or switched by this application. Its tokens are protected for the Windows user through DPAPI and are not portable to another PC or Windows account. This architecture allows both accounts to remain logged in after the user performs the initial logins. The switcher only adjusts the selected account in loginusers.vdf and the Steam registry metadata.'''),
('11. VDF parser and auto-login correction', '''The previous AutoLoginSync implementation used regular expressions. It could set MostRecent on multiple accounts and could not reliably insert AllowAutoLogin when that key was absent. A proper hierarchical VDF parser and writer was added. It preserves unknown nodes, updates or inserts keys, writes a structurally valid document, and supports safe account selection.

A dedicated .NET 9 test harness uses copies rather than the real loginusers.vdf. Fifteen assertions pass across seven scenarios: real-file round trip, exactly one MostRecent account, insertion of missing AllowAutoLogin and RememberPassword, rejection of an unknown account without modifying the file, graceful handling of a missing file, correct SteamID32 derivation, and verified Steam detection on the machine.'''),
('12. First-run wizard implementation', '''VdfDocument.cs, SteamInstaller.cs, LoginUsers.cs, FirstRunService.cs, SetupWizardPage.xaml, and SetupWizardPage.xaml.cs were created. MainWindow routes to the wizard when setup is required and can resume into the normal dashboard without restarting. A duplicate SteamAccount type initially caused a compile error; it was corrected by using a StoredLogin wrapper around the existing model. WPF implicit usings did not include System.IO, so explicit imports were added.

The intended login contract is simple for the end user: launch a profile, sign in personally, select Remember my password, and allow the wizard to observe the newly recorded account. This happens once for Main and once for Daddy on a fresh environment. Credential copying is not part of the design.'''),
('13. Dashboard active-profile bug', '''The live disk state was essential. C:/Program Files (x86)/Steam existed and contained SteamDaddy markers such as stplug-in Lua files and depot cache content. Steam Main existed with its own manifests. Steam Daddy did not exist because Daddy was currently active and therefore occupied the active Steam path.

The old ReadStatus logic resolved a profile through its parked directory first. That model is wrong for the active profile, whose parked directory is expected to be absent. It consequently reported Folder not found, failed to identify Daddy, and showed zero games. The logic was changed to use the active folder report first when it represents that profile, and only use the parked folder for the inactive profile.'''),
('14. Dashboard refresh, switch progress, and ETA', '''NavigationView caches pages. DashboardPage had not subscribed to the shared StatusRefreshed event, so a successful switch could leave old values visible. Loaded and Unloaded event handling now manages the subscription and dispatches updates to the UI thread. SwitchEngine gained named progress steps and estimates so the application can show what it is doing instead of appearing frozen.

These features remain user-side verification items because a clean compile cannot prove that every visual state transitions correctly during a real Main-to-Daddy and Daddy-to-Main switch.'''),
('15. Administrator prompt strategy', '''The application itself requires elevation for protected folder operations. Repeated Steam launch prompts were addressed without disabling UAC. ElevatedLauncher creates a scheduled task with the highest run level and interactive execution, then uses that task for subsequent Steam launches. The task name is SteamProfileSwitcher_LaunchSteam.

The safety boundary is important: the software must never lower the system UAC policy. If scheduled-task registration is blocked by local policy, Steam may still launch normally and the user must receive a truthful explanation. Path changes require task reconciliation.'''),
('16. Custom updater URL and upstream instructions', '''Settings gained SteamDaddyUpdateUrl and EffectiveSteamDaddyUpdateUrl. The official latest-release endpoint remains the default. A user may paste another endpoint, save it, reuse it on later checks, and reset to the official source. SteamDaddyUpdater was rewired so it actually receives the effective URL instead of continuing to use a private constant. Invalid HTTP or HTTPS URLs fail safely rather than crashing a switch.

UpstreamNotice fetches release text after the page is visible, looks for mandatory or administrative instructions, limits the displayed content, and hides itself on failure. A network problem must never prevent Settings from opening.'''),
('17. Settings visibility, organization, version, and credit', '''The update-link control existed in source but was below the visible area of one long ScrollViewer. Screenshots showed only Folder paths and Switching behaviour. Initially this was incorrectly blamed on an old executable. Timestamp and process evidence proved the user was running the new build. The real problem was discoverability.

Settings was reorganized into six tabs: Folders, Switching, SteamDaddy, Appearance, Safety, and About. SteamDaddy now has a direct tab containing the update URL. About displays Aizenrex x Riyad, the running executable ProductVersion, build timestamp, executable location, and a Copy build info action. The title bar also displays version and build time. A duplicate later Company and Version block in the project file was discovered overriding the desired metadata and was removed.'''),
('18. Single-file release and build hygiene', '''A self-contained non-single-file publish produced 239 root files, including 235 DLLs. SteamSwitcher.exe was only about 286 KB and difficult to locate. The release process was changed to PublishSingleFile with native libraries and content bundled for extraction. The root now contains one approximately 126 MB SteamSwitcher.exe, while data, backups, exports, and logs remain as explicit user-state folders.

Compression is deliberately disabled. Earlier single-image or compressed experiments damaged first-launch performance. The single-file release measured about 4590 milliseconds on the first Defender/extraction run and approximately 2591 milliseconds on the second run, with zero errors and exceptions in the smoke-test log.'''),
('19. Current architecture', '''App and MainWindow own application startup, navigation, shared settings, global status, and refresh orchestration. SwitchEngine owns the state transition and recovery journal. SteamInspector reads folder truth, manifests, account data, and profile markers. SteamProcesses closes and launches Steam-related processes. Store performs atomic JSON persistence. BackupService preserves selected configuration and userdata. Diagnostics compares state and reports health. Tools offers controlled maintenance and export operations.

The design keeps UI pages thin enough to delegate risky operations to services. Models represent profile kind, games, account identity, folder reports, process rows, system status, operation results, history, checks, backups, and journal steps. This separation is necessary for future automated testing.'''),
('20. Data, safety, and recovery model', '''Application-owned state lives inside the app directory under data, logs, backups, exports, and journal locations. Settings writes are atomic and history is capped. A switch journal records source and destination rename steps. If the application or Windows stops partway through a switch, the remaining disk state can be inspected and a recovery plan constructed rather than guessing.

Backups are enabled by default. Expected SteamID64 safety locks are optional and can block a switch if the observed account differs from the configured identity. Profile paths must be distinct. The active folder is interpreted differently from parked folders. No recovery procedure may touch local.vdf.'''),
('21. Build and release contract', '''The target is net9.0-windows, x64, WPF, nullable enabled, WPF-UI 4.0, and an administrator manifest. ReadyToRun is enabled, composite ReadyToRun is disabled, tiered quick startup and TieredPGO are enabled, invariant globalization is disabled, single-file compression is disabled, and debug symbols are not published in release.

The release procedure is: verify no SteamSwitcher process is holding files; publish into a staging directory; confirm SteamSwitcher.exe exists; preserve user data directories; move the previous release aside; atomically promote staging; launch twice; measure the main-window handle; close the process; count ERROR, FATAL, and Exception entries; inspect file metadata and root file count. A failed or ambiguous swap must not be blindly repeated.'''),
('22. Verification record', '''Measured evidence includes the updater digest and version replacement test, 15 passing VDF assertions, 0-error and 0-warning release builds at key points, repeated launch timing, process title inspection, executable ProductVersion inspection, registry AutoLoginUser and SteamPath reads, live folder-marker counts, release root file counts, and zero-exception smoke logs.

Unverified visual or environmental items remain explicit: a truly fresh-PC silent installation, complete wizard interaction on a fresh Windows account, both directions of a real switch after the newest UI changes, progress and ETA appearance, immediate dashboard refresh, silent-elevation behavior under the user's policy, custom URL persistence through restart, and upstream notice rendering when matching release text exists.'''),
('23. Mistakes and lessons', '''Three mistakes are especially important. First, an optimization introduced a culture crash and 39-second startup; logs, not confidence, found it. Second, the missing Settings controls were blamed on an old executable even though screenshot timestamps proved otherwise; the actual problem was the long scroll layout. Third, duplicate project metadata silently overrode the desired company value. Each mistake demonstrates why read-before-write, timestamp verification, duplicate-key search, visual verification, and user-visible versioning are required.

Another process lesson came directly from the user: independent commands should be batched. Slow serial probing wastes time. However, writes, builder forms, file swaps, and process-sensitive mutations must remain serialized. Speed is not permission to create races.'''),
('24. Maintenance playbook', '''Before changing the application, identify the exact acceptance criterion. Read the target file and every caller or binding that depends on it. Inspect current settings and live machine state. Make the smallest coherent change. Build once. Run focused tests. Publish into staging. Preserve data. Verify the promoted executable metadata. Launch it twice. Read logs. For UI claims, open the page or obtain a screenshot. For switch claims, perform a real switch only with explicit permission and a recovery plan.

Never re-enable invariant globalization. Never enable composite ReadyToRun without new measurements. Never replace local.vdf. Never infer the active profile solely from the existence of its parked folder. Never report a custom setting as functional until its value reaches the runtime consumer. Never hide important controls below an undocumented scroll boundary. Never delete old releases or user state without authorization.'''),
('25. Known limitations and future work', '''The fresh-PC path needs a clean Windows VM or another computer for end-to-end proof. A signed installer would improve trust and distribution. UI automation should select every Settings tab and verify named controls. The release script should bump or inject a unique build identifier automatically and create a manifest with SHA-256, timestamp, target runtime, test results, and source revision. Old release retention should become an explicit policy instead of accumulating indefinitely.

Further improvements may include resume-aware setup steps, a clearer recovery screen, deterministic diagnostics export, structured updater error categories, a visible profile marker explanation, automatic UI screenshots in smoke tests, and a complete Main-to-Daddy-to-Main test matrix.'''),
]

for title, body in chapters:
    w('## ' + title)
    w()
    for paragraph in body.split('\n\n'):
        w(paragraph)
        w()
    w('### Operational interpretation')
    w()
    w('This chapter affects at least one of four contracts: user discoverability, data safety, runtime reliability, or release performance. A future edit must identify which contract it changes, gather evidence before mutation, preserve unrelated behavior, and verify the promoted executable rather than only the source tree.')
    w()
    w('### Failure-handling rule')
    w()
    w('If the relevant step fails, capture the exact log, path, timestamp, process state, registry state, or test result. Determine whether a side effect may already have committed. Read back by stable identity. Continue only from the first known incomplete step. Do not hide a partial result and do not replace a requested method with an easier but different behavior.')
    w()

# Add the measured inventory as a historical appendix.
facts = ROOT / 'docs' / '_facts.txt'
w('## 26. Measured project inventory snapshot')
w()
w('The following inventory was generated from the project and machine. It is a historical snapshot and may change after later edits.')
w()
w('```text')
if facts.exists():
    # Replace any corrupted legacy characters rather than copying them.
    ftext = facts.read_text(encoding='utf-8', errors='replace').replace('\ufffd', '[unreadable-character]')
    w(ftext)
else:
    w('Inventory file was not available.')
w('```')
w()

# Canonical source selection. Do not include automatic backups, build output,
# generated obj/bin trees, scratch files, or this README.
source_files = []
src = ROOT / 'src' / 'SteamSwitcher'
for p in src.rglob('*'):
    low = str(p).lower()
    if not p.is_file(): continue
    if '.aizen-backups' in low or '\\bin\\' in low or '\\obj\\' in low: continue
    if p.suffix.lower() in {'.cs', '.xaml', '.csproj', '.manifest'}:
        source_files.append(p)
for p in (ROOT / 'tools').glob('*'):
    if p.is_file() and p.suffix.lower() in {'.ps1', '.py'} and p.name != 'generate-readme.py':
        source_files.append(p)
vd = ROOT / 'tools' / 'vdftest'
if vd.exists():
    for p in vd.glob('*'):
        if p.is_file() and p.suffix.lower() in {'.cs', '.csproj'}:
            source_files.append(p)
for name in ['FIRST-RUN-DESIGN.md']:
    p = ROOT / 'docs' / name
    if p.exists(): source_files.append(p)
source_files = sorted(set(source_files), key=lambda p: str(p).lower())

w('## 27. Canonical source map and line-by-line engineering appendix')
w()
w('This appendix records every canonical source line with location, category, engineering role, dependency questions, safety implications, and a verification rule. It intentionally does not annotate automatic Aizen backups, generated build trees, old releases, or scratch artifacts. The commentary is systematic and conservative; it is a maintenance index, not a substitute for compilation, tests, or code review.')
w()

def classify(line):
    s = line.strip()
    if not s: return ('structural whitespace', 'separates logical units and keeps the surrounding implementation readable')
    if s.startswith('//') or s.startswith('<!--'): return ('design comment', 'records intent, a constraint, an explanation, or a warning for future maintenance')
    if s.startswith('using '): return ('namespace import', 'makes a framework or project namespace available to this compilation unit')
    if s.startswith('<') or s.startswith('</'): return ('XML or XAML declaration', 'defines a UI element, resource, build property, package, or structural boundary')
    if re.search(r'\b(class|record|enum|struct|interface)\b', s): return ('type declaration', 'defines a domain model, service, contract, or state category')
    if '(' in s and ')' in s: return ('operation or control-flow line', 'participates in a method call, method declaration, condition, event, loop, or runtime operation')
    if '=' in s: return ('assignment or configuration', 'sets a value, default, binding, property, argument, or derived state')
    if s in {'{','}','};'}: return ('scope boundary', 'opens or closes the surrounding type, method, control-flow block, object, or collection')
    return ('implementation statement', 'contributes to the local data flow, UI structure, validation, state transition, or return behavior')

for fidx, path in enumerate(source_files, 1):
    rel = path.relative_to(ROOT)
    raw = path.read_bytes()
    text = raw.decode('utf-8-sig', errors='replace')
    lines = text.splitlines()
    w(f'### 27.{fidx}. `{rel}`')
    w()
    w(f'This canonical file contains {len(lines)} lines in this snapshot. Its SHA-256 is `{hashlib.sha256(raw).hexdigest()}`. The hash exists for comparison only; any legitimate source edit changes it. The file was selected from the canonical source, tools, test, or design locations and was not taken from an automatic backup directory.')
    w()
    for number, line in enumerate(lines, 1):
        cleaned = line.replace('\ufffd', '[unreadable-character]').replace('```', '` ` `')
        kind, role = classify(cleaned)
        w(f'#### `{rel}` line {number}')
        w()
        w(f'- **Source text:** `{cleaned[:1000]}`')
        w(f'- **Category:** {kind}.')
        w(f'- **Local role:** This line {role}. Its exact identity is `{rel}` line {number}. It must be interpreted together with neighboring declarations, callers, event handlers, XAML name bindings, serialization names, project properties, and any machine state that the surrounding code reads or changes.')
        w('- **Dependency review:** Before editing this line, search the canonical tree for the identifiers it declares or consumes. Determine whether another page, service, model, test, release script, registry operation, file operation, or settings property assumes the present behavior. Special attention is required for active-versus-parked profile semantics, VDF preservation, update URL flow, process shutdown, journal completion, and UI discoverability.')
        w('- **Safety review:** Ask whether the line can affect user-owned files, Steam credentials, protected folders, running processes, scheduled tasks, backups, history, updater replacement, or recovery state. Never extend its effect to local.vdf. If a side effect may have partly completed, read back the stable target before retrying.')
        w('- **Verification rule:** A text edit is not completion. Compile the affected project, run the smallest focused regression test, publish to staging when release behavior matters, promote only after the artifact exists, inspect executable metadata, launch and read the log, and visually verify user-facing changes. Preserve unrelated settings and user data throughout the operation.')
        w('- **Project lesson:** This project has already produced compile-clean hidden UI, a globalization crash, a startup regression, stale executable metadata, regular-expression VDF corruption risk, and active-profile misidentification. Therefore no apparently simple line should be assumed harmless solely because the compiler accepts it.')
        w()

w('## 28. Final ownership and continuity note')
w()
w('The product identity is Aizenrex x Riyad. Its quality depends on safe profile switching, honest status, recoverability, fast startup, discoverable controls, visible build identity, preserved user data, and evidence-based maintenance. The central operating rule is simple: read the current truth, make the smallest coherent change, then read back and prove the result in the executable that the user actually runs.')
w()
w('This README is deliberately large because it is both a historical narrative and an annotated source snapshot. Future maintainers should update the narrative when the product goal, architecture, tested behavior, known limitation, or release contract changes. They should regenerate the source appendix after canonical code changes and never inflate it with backup revisions.')

content = ''.join(sections)
OUT.write_text(content, encoding='utf-8', newline='\n')
word_count = len(content.split())
replacement_count = content.count('\ufffd')
print('WROTE:', OUT)
print('BYTES:', OUT.stat().st_size)
print('LINES:', content.count('\n'))
print('WORDS:', word_count)
print('CANONICAL_FILES:', len(source_files))
print('REPLACEMENT_CHARS:', replacement_count)
print('SHA256:', hashlib.sha256(OUT.read_bytes()).hexdigest())
if word_count < 100000:
    raise SystemExit('ERROR: README is below 100,000 words')
if replacement_count:
    raise SystemExit('ERROR: README contains Unicode replacement characters')
