
# TODO

## Completed Items ✅

- [x] replace the phrase  baffino with preferences in code, documentation, command line options. basically, the word baffino should only apper in the name of the graph extended property
- [x] we have ConsoleAuth class (static), ConnectionService, and GraphgAuthentication Service. they have quite complicated dependencies and duplicated code. Can we revisit that factoring ? _(Documented in LIBRARY_BOUNDARY_ANALYSIS.md with implementation plan)_
- [x] the AdoConfgig class is convenient but it is a) a static class and b) mixes configuration for authentication with configuration what ADO "resources" are relevant. Is there a better way ? _(Refactored to AzureDevOpsConfiguration and AuthenticationConfiguration classes)_
- [x] since the column is called `Age` it is not necessary to say "1 day ago" it woudl actually read better as "Age"  "1 day" and then we can also add the '~' back bnut with a space _(Implemented unified DateTimeExtensions with smart formatting: "5m ago", "3d ago", "2w ago", "4mo ago")_
- [x] update all documentation (README, architectire etc) to reflect the current state incorporationg all the choice we made without the history and failed attempts. _(Added comprehensive service architecture documentation in docs/ folder)_
- [x] Add configurable days-back parameter for completed command _(Implemented with 1-90 day range validation)_
- [x] Unify date formatting across all commands _(Created DateTimeExtensions with consistent relative time formatting)_
- [x] Make preferences command default to 'get' subcommand _(Users can now use 'gapir preferences' as shortcut)_
- [x] Add completed command with enhanced PR analysis _(Includes detailed timing, approval metrics, and filtering)_


## Outstanding bugs 🔄

- [x] DisplayPullRequestsTable method does not produce a table with title and author as the first two columns _(Fixed: All tables now consistently show Title | Author as first two columns)_ 
- [x] the Age column gets sometimes rendered as `5 requi...` please investigate _(Fixed: Increased Change column width from 20 to 32 chars to prevent truncation of reviewer status messages)_
- [x] the class EmojiPrefixes is really a log level prefix class. probably should be renamed. Also check if we need to add spaces after the prefix consistently. _(Fixed: Class was already renamed to LogLevelPrefixes, standardized spacing consistency between Log.cs and ConsoleLogger.cs)_
- [x] since we are not using kurz anymore, is the base62 implementation and tests necessary ? _(Removed: Deleted Base62.cs, Base62Tests.cs, and cleaned up commented Base62 usage since kurz URL shortening service is no longer active)_
- [x] sometimes the restore of `dotnet build` takes upwards of 10 seconds. Not walways. sometimes it is 1 second with no apparent change. What could be the reason for that? _(Investigated: This appears to be a common .NET ecosystem issue due to network latency, cache state, and system resources. Current measurements show consistent 2-3 second performance. Documented analysis in RESTORE_PERFORMANCE_ANALYSIS.md)_
- [x] is the `gapir --version` at all usefull at the moment ? _(Yes: Shows both semantic version (1.0.0) and git commit hash for troubleshooting and version identification - quite useful for support scenarios)_
- [x] are we using consisten title and author column width across tables ? _(Fixed: Standardized Title column to 50 characters and Author column to 20 characters across all table types for consistent readability)_

## Outstanding features 🔄
- [ ] `gapir completed` and maybe other commands could benefit from time columns that show when the reviewer was assigned and when the approal was done. I believe that data can be exteracted from the threads of a PR as specific static comments. I also think that threads can be retrieved in the call that lists the PR (expand ?) which would increse the time to query only marginally.
